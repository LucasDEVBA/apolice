# ADR 0007: Tarifação por Strategy, tempo injetado e dinheiro em `decimal` com `AwayFromZero`

- **Status:** aceita
- **Data:** 2026-10

## Contexto

O cálculo do prêmio é o coração do produto e o primeiro lugar que alguém da área de seguros vai conferir. Três produtos têm regras diferentes (fatores de Auto por idade do condutor, região, uso e bônus; Residencial por tipo de imóvel e uso habitual; Vida por faixa etária e fumante), mas compartilham as mesmas validações: cobertura obrigatória presente, código existente, sem duplicata, LMI dentro dos limites, LMI fixo ignorando o valor enviado.

Duas fontes clássicas de bug em sistema financeiro também estão presentes: **arredondamento** (somas que não fecham, centavo a mais ou a menos) e **tempo** (cotação que expira, idade calculada no dia errado, teste que passa hoje e falha amanhã).

## Decisão

**Strategy com Template Method.**
- `IEstrategiaTarifacao` tem uma implementação por produto: `TarifacaoAuto`, `TarifacaoResidencial` e `TarifacaoVida`.
- `TarifacaoBase<TDados>` concentra o algoritmo e as validações comuns. Cada produto só preenche os ganchos: `ValidarDados`, `DefinirFatores`, `LmiDefinidoPeloRisco` (por exemplo, o LMI do CASCO é o valor do veículo) e `ValidarLmis` (por exemplo, IPA não passa do LMI de MORTE; INCENDIO não passa do valor do imóvel).
- `Tarifador` recebe todas as estratégias por `IEnumerable<IEstrategiaTarifacao>` e as indexa pelo código do produto. Duas estratégias para o mesmo código fazem o `ToDictionary` lançar: erro de composição aparece na subida.
- As estratégias não têm estado e são registradas como singleton com `TryAddEnumerable`. Um produto novo é uma classe nova mais uma linha de registro, sem tocar no `Tarifador`.
- O resultado é determinístico: itens na ordem do catálogo, e só os fatores diferentes de 1 que de fato alteraram algum item contratado.

**Tempo sempre injetado.**
- O domínio nunca lê o relógio. Recebe `DateTimeOffset agora`, `DateOnly hoje` ou `DateOnly dataReferencia` por parâmetro. Até os ids `Guid.CreateVersion7(agora)` e o `EventoId` dos eventos usam o instante recebido.
- A aplicação usa `TimeProvider`; os testes usam `FakeTimeProvider` com data fixa.
- Datas de negócio são `DateOnly` no fuso de Brasília, calculadas por `Relogio.HojeEm(instante)` com **offset fixo de −03:00**: o Brasil não tem horário de verão desde 2019, e o offset fixo dispensa a base de fusos do sistema operacional (tzdata) da Lambda. O dashboard agrupa por mês no SQL com o mesmo offset.
- O endosso retarifa com `dataReferencia` igual à data de emissão: a idade do segurado e a do veículo continuam as da contratação.

**Dinheiro em `decimal`.**
- Valores monetários são `decimal`, e `Dinheiro.Arredondar` é o ponto único de arredondamento: `Math.Round(valor, 2, MidpointRounding.AwayFromZero)`. Cada prêmio de item e o IOF são arredondados; a soma dos itens já arredondados é o prêmio líquido.
- A parcela base usa truncamento (`ArredondarParaBaixo`) e a primeira parcela absorve a diferença, então a soma das parcelas fecha exatamente no prêmio total.
- LMI e valor do veículo precisam estar em centavos exatos (`Dinheiro.EstaEmCentavos`): o valor tarifado é o mesmo que o `numeric(14,2)` do banco guarda. Taxas e alíquotas são `numeric(12,6)`.
- A formatação "R$ 1.234,56" usa um `NumberFormatInfo` montado à mão, para não depender das bibliotecas de globalização (ICU) no runtime da Lambda.
- `double` só aparece na borda da telemetria (`premio_emitido`), que é o tipo que os exportadores OTLP aceitam.

## Alternativas consideradas

- **`switch` por produto dentro de um serviço.** Funciona com três produtos, mas mistura as regras e cresce a cada produto novo.
- **Motor de regras ou tabela de fatores no banco.** Mais flexível para o negócio, mas é complexidade sem demanda neste escopo. O admin já consegue alterar a taxa de cada cobertura.
- **`double`.** Erro de representação binária em valores como 0,1; inaceitável para dinheiro.
- **`MidpointRounding.ToEven` (padrão do .NET).** O "arredondamento bancário" leva 0,125 a 0,12, o que costuma surpreender quem confere à mão; a SPEC fixa `AwayFromZero`.
- **`DateTime.Now` e `TimeZoneInfo` ("America/Sao_Paulo").** Testes dependentes do dia da execução e dependência de tzdata no runtime.

## Consequências

- Os testes de domínio conferem valores **exatos**, calculados à mão no comentário de cada caso. Exemplo de `TarifacaoAutoTests`: o produto dos fatores 1,40 × 1,25 × 1,35 × 1,10 × 0,85 = 2,2089375 aplicado a um CASCO de R$ 2.560,00 dá R$ 5.654,88.
- Mudar uma regra de arredondamento é mudar um método, e os testes de borda de LMI, cronograma e cancelamento mostram o efeito.
- Se o Brasil voltar a ter horário de verão, `Relogio` é o único ponto a trocar.
- A tarifação é pura e síncrona: não acessa banco nem relógio, e a simulação ao vivo da tela de cotação chama a mesma regra que a gravação.
