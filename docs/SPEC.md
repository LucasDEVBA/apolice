# Apólice — Especificação técnica (fonte da verdade)

> Documento interno de construção. Todo código deve seguir este contrato. Se algo aqui estiver
> ambíguo, escolha a opção mais simples e idiomática em .NET 10 e registre a decisão no PR/ADR.

## 1. Objetivo

Plataforma de **cotação, emissão e gestão de apólices de seguro** (Auto, Residencial e Vida), com
endosso, cancelamento e sinistro. É um projeto de portfólio para vaga **Fullstack Pleno C#/.NET +
React**: precisa demonstrar, com código real e testado, os requisitos da vaga:

- C# / .NET 10 no backend: DI, middlewares, pipeline HTTP, Minimal APIs, options pattern.
- REST com versionamento (`/api/v1`), OpenAPI 3.1 + Scalar, ProblemDetails (RFC 9457).
- SOLID, Clean Architecture, DDD tático (agregados, value objects, eventos de domínio), Strategy.
- EF Core 10 (escrita) + Dapper (leitura) = CQRS-lite. PostgreSQL local/CI; **Aurora DSQL** em produção.
- Mensageria: Outbox transacional → RabbitMQ (local/CI) / Amazon SQS (produção), consumidores idempotentes.
- Cache (HybridCache), feature flags (Microsoft.FeatureManagement), rate limiting.
- Segurança: JWT curto + refresh token rotativo em cookie HttpOnly, RBAC + autorização por recurso
  (anti-BOLA/IDOR), lockout, headers de segurança, validação, auditoria.
- Observabilidade: Serilog JSON, OpenTelemetry (traces/métricas), correlation id, health checks.
- Testes: unitários, integração (Testcontainers), arquitetura (ArchUnitNET), E2E (Playwright).
- Docker, GitHub Actions (CI/CD com OIDC), Terraform (AWS), React + TypeScript + TanStack Query.

## 2. Convenções gerais (obrigatórias)

- **Idioma**: linguagem ubíqua do domínio em **português sem acento** nos identificadores
  (`Cotacao`, `ApoliceSeguro`, `Sinistro`, `Segurado`, `Cobertura`); padrões técnicos em inglês
  (`Repository`, `Handler`, `Result`, `Options`). Comentários em português, **só o PORQUÊ**.
- O agregado de apólice chama-se **`ApoliceSeguro`** (o nome `Apolice` colide com o namespace raiz).
- C#: file-scoped namespaces, `sealed` por padrão, `record`/`readonly record struct` para VOs e DTOs,
  primary constructors quando simplificar, `async`/`await` com `CancellationToken` em toda I/O,
  nada de `dynamic`, nada de `async void`, nada de `.Result`/`.Wait()`.
- Funções curtas (alvo < 20 linhas, máx. 50), no máximo 3 níveis de indentação, early return.
- Sem números mágicos: constantes nomeadas (`private const decimal ParcelaMinima = 60.00m;`).
- `TreatWarningsAsErrors` está ligado: o build tem de sair **limpo**.
- Dinheiro: `decimal`, arredondamento `MidpointRounding.AwayFromZero` em 2 casas.
- Datas de negócio (vigência, ocorrência) são `DateOnly` no fuso de Brasília. Como o Brasil não tem
  horário de verão desde 2019, use offset fixo `-03:00` (`Comum.Relogio.Hoje(TimeProvider)`), sem
  depender de tzdata do sistema (Lambda).
- Formatação monetária pt-BR montada manualmente (`NumberFormatInfo` próprio) — não dependa de ICU.
- Ids: `Guid.CreateVersion7()` gerado no domínio.
- Tempo sempre injetado: domínio recebe `DateTimeOffset agora`/`DateOnly hoje` por parâmetro;
  aplicação usa `TimeProvider` (testes usam `FakeTimeProvider`).
- Nomes de testes: `Deve_<resultado>_quando_<condicao>` (ex.: `Deve_negar_quando_apolice_cancelada`).

## 3. Estrutura da solução e regras de dependência

```
src/
  Apolice.Domain          → sem dependências (só BCL)
  Apolice.Application     → Domain (+ FluentValidation)
  Apolice.Infrastructure  → Application (EF Core, Npgsql, Dapper, RabbitMQ, SQS, QuestPDF, ...)
  Apolice.Api             → Infrastructure (composição) — Minimal APIs
  Apolice.Worker          → Infrastructure — host local (RabbitMQ) + handlers Lambda (SQS/agendado) + verbos migrate/seed
tests/
  Apolice.Domain.Tests, Apolice.Application.Tests, Apolice.Api.IntegrationTests, Apolice.ArchitectureTests
frontend/   → Vite + React 19 + TS (strict)
infra/terraform/
docs/ (SPEC, ADRs, ESTUDO, openapi/v1.json gerado no build)
```

Regras verificadas por teste de arquitetura:
- Domain não referencia Application, Infrastructure, Api, Worker, EF Core, ASP.NET, FluentValidation.
- Application não referencia Infrastructure, Api, Worker, EF Core, Npgsql, Dapper, ASP.NET Core, AWS, RabbitMQ.
- Infrastructure não referencia Api nem Worker.
- Classes que implementam `ICommandHandler<,>`/`IQueryHandler<,>`/`IIntegrationEventHandler<>` são
  `sealed` e terminam em `Handler`.

Versões de pacotes: **somente** via `Directory.Packages.props` (Central Package Management). Não
adicione pacote sem registrar a versão lá. Não use MediatR, AutoMapper, FluentAssertions (licenças
comerciais) — mapeamento é manual e explícito.

## 4. Domínio (`Apolice.Domain`)

### 4.1 Kernel comum (`Apolice.Domain.Comum`)

- `Entity<TId>` (igualdade por Id) e `AggregateRoot<TId>` (lista de `IDomainEvent`,
  `protected void Raise(IDomainEvent e)`, `IReadOnlyCollection<IDomainEvent> DomainEvents`,
  `void ClearDomainEvents()`, `public int Versao { get; private set; }` — token de concorrência
  incrementado pela infraestrutura ao salvar).
- `IDomainEvent { Guid EventoId { get; } DateTimeOffset OcorridoEm { get; } }` — eventos são `sealed record`.
- `Error(string Codigo, string Mensagem, ErrorType Tipo)` com `ErrorType { Validacao, NaoEncontrado,
  Conflito, Proibido, RegraDeNegocio, NaoAutorizado }`.
- `Result` e `Result<T>`: `IsSuccess`, `IsFailure`, `Error`, `Value` (lança se falha), `Success()`,
  `Failure(Error)`, conversão implícita de `T` e de `Error`. Usado para falhas **previsíveis**;
  exceções só para bugs/infra.
- VOs: `Dinheiro` (`readonly record struct`, operadores + − × por decimal, `Zero`, `Arredondado()`,
  comparação, `ToString()` pt-BR "R$ 1.234,56"), `Cpf` (valida dígitos verificadores, rejeita
  sequências repetidas, armazena só dígitos, `Mascarado` = `***.456.789-**`), `Email` (trim,
  lowercase, validação simples), `Vigencia(DateOnly Inicio, DateOnly Fim)` com `DiasTotais`,
  `Contem(DateOnly)`, `DiasDecorridos(DateOnly)`, `DiasRestantes(DateOnly)`.
- `Relogio.Hoje(TimeProvider)` e `Relogio.HojeEm(DateTimeOffset)` (offset fixo −03:00).
- `Segurado` (VO/record): `Nome` (3–120), `Cpf`, `Email`, `DataNascimento`; `IdadeEm(DateOnly)`.

### 4.2 Produtos e tarifação (`Apolice.Domain.Produtos`, `Apolice.Domain.Tarifacao`)

`Produto` (agregado do catálogo): `Codigo` (`AUTO`|`RESID`|`VIDA`), `Nome`, `Descricao`,
`AliquotaIof` (fração), `IReadOnlyList<CoberturaProduto> Coberturas`.
`CoberturaProduto`: `Codigo`, `Nome`, `Descricao`, `Obrigatoria`, `TipoCalculo`
(`TaxaSobreLmi` = taxa em fração do LMI | `TaxaPorMil` = ‰ do LMI | `PremioFixo`), `Taxa`,
`PremioFixo?`, `LmiMinimo`, `LmiMaximo`, `LmiFixo?`, `PermiteSinistro`.
Método `Produto.AtualizarTaxa(string coberturaCodigo, decimal novaTaxa)` → `Result` (taxa > 0 e ≤ 1;
cobertura de prêmio fixo não aceita taxa) e levanta `TaxaCoberturaAtualizada`.

`DadosRisco` — hierarquia polimórfica (serializada em JSON com discriminador `"produto"`):
- `DadosRiscoAuto(decimal ValorVeiculo, int AnoFabricacao, string CepPernoite, int IdadeCondutor, bool UsoComercial, int ClasseBonus)`
- `DadosRiscoResidencial(decimal ValorImovel, TipoImovel TipoImovel /*Casa|Apartamento*/, string Cep, bool Habitual)`
- `DadosRiscoVida(bool Fumante)` — a idade vem de `Segurado.DataNascimento`.

`CoberturaSolicitada(string Codigo, decimal? Lmi)`.

`ITarifador.Calcular(Produto, Segurado, DadosRisco, IReadOnlyList<CoberturaSolicitada>, DateOnly dataReferencia)`
→ `Result<ResultadoTarifacao>`. Implementação `Tarifador` escolhe a estratégia pelo código do produto
(**Strategy**: `IEstrategiaTarifacao` com `TarifacaoAuto`, `TarifacaoResidencial`, `TarifacaoVida`).

`ResultadoTarifacao`: `Itens` (`ItemTarifado(Codigo, Nome, Lmi, PremioLiquido, Obrigatoria, PermiteSinistro)`),
`Fatores` (`FatorAplicado(string Descricao, decimal Fator)`, só os ≠ 1), `PremioLiquido` (Σ itens),
`Iof` = arred(PremioLiquido × AliquotaIof), `AliquotaIof`, `PremioTotal` = PremioLiquido + Iof,
`MaxParcelas` = clamp(floor(PremioTotal / 60,00), 1, 10).

Validações comuns da tarifação (falha = `Validacao`): produto e dados de risco do mesmo tipo;
toda cobertura obrigatória presente; códigos existentes; sem duplicatas; LMI dentro de
[LmiMinimo, LmiMaximo] quando informado/exigido; coberturas com `LmiFixo` ignoram o LMI enviado.

Prêmio por item: `TaxaSobreLmi` → arred(Lmi × Taxa × Πfatores); `TaxaPorMil` → arred(Lmi × Taxa/1000 × Πfatores);
`PremioFixo` → PremioFixo (sem fatores).

**AUTO** (IOF 7,38% = 0,0738). Dados: ValorVeiculo 10.000–500.000; AnoFabricacao entre
(ano de referência − 20) e (ano + 1); CEP 8 dígitos; IdadeCondutor 18–90; ClasseBonus 0–10.

| Código | Nome | Obrig. | Cálculo | Taxa/Prêmio | LMI |
|---|---|---|---|---|---|
| CASCO | Colisão, incêndio e roubo | sim | TaxaSobreLmi | 0,0320 | = ValorVeiculo (sempre) |
| RCF_DM | RC danos materiais a terceiros | sim | TaxaSobreLmi | 0,0060 | 50.000–500.000 |
| RCF_DC | RC danos corporais a terceiros | não | TaxaSobreLmi | 0,0040 | 50.000–500.000 |
| VIDROS | Vidros, faróis e retrovisores | não | PremioFixo | 220,00 | fixo 3.000 |
| CARRO_RESERVA | Carro reserva (15 dias) | não | PremioFixo | 180,00 | fixo 0, não permite sinistro |
| ASSISTENCIA_24H | Assistência 24h | não | PremioFixo | 120,00 | fixo 0, não permite sinistro |

Fatores (multiplicam só itens com taxa — CASCO, RCF_DM, RCF_DC):
idade do condutor 18–25 → 1,40; 26–35 → 1,10; 36–60 → 1,00; ≥ 61 → 1,15 ·
região pelo 1º dígito do CEP: `0` → 1,25 (Grande SP); `2` → 1,30 (RJ/ES); `4` → 1,10 (BA/SE); demais 1,00 ·
uso comercial → 1,35 · veículo com mais de 10 anos (ano ref − ano fabricação > 10) → 1,10 ·
bônus: fator (1 − 0,03 × ClasseBonus).

**RESID** (IOF 7,38%). Dados: ValorImovel 50.000–5.000.000; CEP 8 dígitos.

| Código | Nome | Obrig. | Cálculo | Taxa | LMI |
|---|---|---|---|---|---|
| INCENDIO | Incêndio, raio e explosão | sim | TaxaSobreLmi | 0,00045 | 10.000–5.000.000 e ≤ ValorImovel |
| DANOS_ELETRICOS | Danos elétricos | não | TaxaSobreLmi | 0,0120 | 5.000–50.000 |
| ROUBO | Roubo e furto qualificado | não | TaxaSobreLmi | 0,0180 | 5.000–100.000 |
| RC_FAMILIAR | Responsabilidade civil familiar | não | TaxaSobreLmi | 0,0035 | 10.000–200.000 |
| VENDAVAL | Vendaval e granizo | não | TaxaSobreLmi | 0,0060 | 5.000–200.000 |

Fatores: Apartamento → 0,80 em todos os itens; Casa → 1,30 **só no ROUBO**; não habitual
(veraneio) → 1,30 em todos os itens.

**VIDA** (IOF 0,38% = 0,0038). Idade do segurado na data de referência: 18–70.

| Código | Nome | Obrig. | Cálculo | Taxa | LMI |
|---|---|---|---|---|---|
| MORTE | Morte natural ou acidental | sim | TaxaPorMil | 0,80 (base) | 20.000–2.000.000 |
| IPA | Invalidez permanente por acidente | não | TaxaPorMil | 0,35 | 10.000–2.000.000 e ≤ LMI de MORTE |
| FUNERAL | Assistência funeral | não | PremioFixo | 90,00 | fixo 7.000 |

Fatores só na MORTE: faixa etária 18–30 ×1,00; 31–40 ×1,50; 41–50 ×3,00; 51–60 ×6,25; 61–70 ×12,50 ·
fumante ×1,50.

### 4.3 Cotação (`Apolice.Domain.Cotacoes`)

`Cotacao` (agregado): `Id`, `CorretorId`, `ProdutoCodigo`, `Segurado`, `DadosRisco`,
`Itens` (`ItemCotacao` = snapshot de `ItemTarifado`), `Fatores`, `PremioLiquido`, `Iof`,
`PremioTotal`, `MaxParcelas`, `Status` (`Calculada`|`Aceita`|`Emitida`|`Recusada`),
`ValidaAte` (= criação + 30 dias), `CriadaEm`, `ApoliceId?`.
- `Cotacao.Criar(Guid corretorId, Produto, Segurado, DadosRisco, ResultadoTarifacao, DateTimeOffset agora)`.
- `Aceitar(agora)` — só `Calculada` e não expirada → `Aceita`.
- `Recusar(agora)` — só `Calculada`/`Aceita` → `Recusada`.
- `MarcarEmitida(Guid apoliceId, agora)` — só `Aceita` e não expirada → `Emitida`.
- `StatusEfetivo(agora)` → `"Expirada"` quando `agora > ValidaAte` e status ∈ {Calculada, Aceita}.
- Erros: `Cotacao.Expirada`, `Cotacao.StatusInvalido` (RegraDeNegocio).

### 4.4 Apólice (`Apolice.Domain.Apolices`)

`ApoliceSeguro` (agregado): `Id`, `Numero` (ex.: `AUTO-2026-000042`), `CotacaoId`, `CorretorId`,
`ProdutoCodigo`, `Segurado`, `DadosRisco`, `Vigencia`, `Coberturas` (`CoberturaApolice`: Codigo,
Nome, Lmi, PremioLiquido **anual**, Obrigatoria, PermiteSinistro), `PremioLiquido` (da emissão),
`Iof`, `PremioTotal`, `AliquotaIof`, `Parcelas`, `Status` (`Ativa`|`Cancelada`), `EmitidaEm`,
`CanceladaEm?`, `MotivoCancelamento?`, `ValorRestituicao?`, `BoasVindasEnviadasEm?`,
`Endossos` (lista de `Endosso`).
- Derivados: `AjustesEndosso` = Σ endossos.AjusteLiquido; `PremioLiquidoVigente` = PremioLiquido + AjustesEndosso.
- `StatusEfetivo(hoje)`: `Cancelada` | `Programada` (hoje < início) | `Vencida` (hoje > fim) | `Ativa`.
- `ApoliceSeguro.Emitir(Cotacao, string numero, DateOnly inicioVigencia, int parcelas, DateTimeOffset agora)`
  → `Result<ApoliceSeguro>`: cotação `Aceita` e não expirada; início ∈ [hoje, hoje + 30];
  fim = início + 1 ano; parcelas ∈ [1, MaxParcelas]; chama `cotacao.MarcarEmitida`; levanta `ApoliceEmitida`.
- `CronogramaParcelas()` → lista `(Numero, Vencimento, Valor)`: vencimentos mensais a partir do início;
  valor base = floor(PremioTotal / N, 2 casas); a 1ª parcela absorve a diferença.
- **Endosso** — `SimularEndosso(...)`/`Endossar(...)`: tipos `AlteracaoLmi`, `InclusaoCobertura`,
  `ExclusaoCobertura`. Data de efeito = max(hoje, início); exige status efetivo `Ativa`/`Programada`
  e efeito < fim. Não exclui obrigatória; não inclui cobertura já presente; altera/exclui só
  existente. O novo prêmio **anual** do item vem de nova tarifação do conjunto alterado
  (`ITarifador` + `Produto` atual + dados de risco originais + `dataReferencia` = data de emissão).
  `AjusteLiquido` = arred((novoAnual − anualAtual) × DiasRestantes(efeito) / DiasTotais);
  `IofAjuste` = arred(AjusteLiquido × AliquotaIof); `AjusteTotal` = soma. Endosso numerado 1..n.
  Atualiza o item (LMI e prêmio anual) ou inclui/remove. Levanta `ApoliceEndossada`.
- **Cancelamento** — `SimularCancelamento(motivo, hoje)`/`Cancelar(motivo, agora)`:
  motivos `PedidoDoSegurado`|`IniciativaSeguradora`|`Inadimplencia`. Só status efetivo
  `Ativa`/`Programada`. Base = `PremioLiquidoVigente`.
  - hoje < início → método `Integral`, restituição = base.
  - `PedidoDoSegurado` → método `PrazoCurto`: dias decorridos = (hoje − início).Days; percentual retido
    = primeira linha da tabela com `Dias ≥ decorridos`; restituição = arred(base × (1 − retido)).
  - demais → método `ProRataDie`: restituição = arred(base × DiasRestantes(hoje) / DiasTotais).
  - Tabela de prazo curto (dias → % retido): 15→13, 30→20, 45→27, 60→30, 75→37, 90→40, 105→46,
    120→50, 135→56, 150→60, 165→66, 180→70, 195→73, 210→75, 225→78, 240→80, 255→83, 270→85,
    285→88, 300→90, 315→93, 330→95, 345→98, 365→100 (366 dias também → 100).
  - IOF não é restituído (simplificação documentada). Levanta `ApoliceCancelada`.
- `MarcarBoasVindasEnviadas(agora)` (idempotente).

### 4.5 Sinistro (`Apolice.Domain.Sinistros`)

`Sinistro` (agregado): `Id`, `Numero` (`SIN-2026-000007`), `ApoliceId`, `ApoliceNumero`,
`CorretorId`, `ProdutoCodigo`, `SeguradoNome`, `CoberturaCodigo`, `CoberturaNome`, `LmiCobertura`,
`DataOcorrencia`, `Descricao` (20–2000), `ValorReclamado` (> 0), `ValorIndenizacao?`, `Status`
(`Aberto` → `EmAnalise` → `Aprovado`|`Negado`; `Aprovado` → `Pago`), `MotivoNegativa?`,
`Triagem?` (`TriagemSinistro(int Score, IReadOnlyList<string> Alertas, DateTimeOffset TriadoEm)`),
`AbertoEm`, `Historico` (`HistoricoSinistro(Status, Observacao?, Guid? UsuarioId, DateTimeOffset OcorridoEm)`).
- `Sinistro.Abrir(ApoliceSeguro, string numero, string coberturaCodigo, DateOnly dataOcorrencia, string descricao, decimal valorReclamado, Guid? usuarioId, DateTimeOffset agora)`:
  ocorrência ≤ hoje e dentro da vigência; se a apólice está cancelada, ocorrência < data do
  cancelamento; cobertura existe na apólice e `PermiteSinistro`. Levanta `SinistroAberto`.
- `IniciarAnalise(usuarioId, agora)`, `Aprovar(valor, observacao?, usuarioId, agora)` (valor > 0 e
  ≤ min(ValorReclamado, LmiCobertura)), `Negar(motivo ≥ 10 chars, usuarioId, agora)`,
  `RegistrarPagamento(usuarioId, agora)`. Cada transição grava histórico e levanta evento
  (`SinistroAprovado`, `SinistroNegado`, `SinistroPago`). Transição inválida → `Sinistro.TransicaoInvalida`.
- `RegistrarTriagem(TriagemSinistro, agora)` — só uma vez (segunda chamada é no-op).
- Serviço puro `AvaliadorTriagem.Avaliar(Sinistro, DateOnly inicioVigencia, DateOnly hoje)`:
  +40 se ocorrência até 30 dias após o início da vigência (`VIGENCIA_RECENTE`);
  +30 se reclamado > 80% do LMI (`VALOR_PROXIMO_LMI`); +20 se reclamado > LMI (`VALOR_ACIMA_LMI`);
  +10 se aberto mais de 30 dias após a ocorrência (`COMUNICACAO_TARDIA`). Score máx. 100.

### 4.6 Usuários e tokens (`Apolice.Domain.Usuarios`)

`Usuario`: `Id`, `Nome`, `Email`, `SenhaHash`, `Papel` (`Corretor`|`Analista`|`Admin`), `Ativo`,
`FalhasLogin`, `BloqueadoAte?`. `RegistrarFalhaLogin(agora)` (5ª falha consecutiva bloqueia 15 min
e zera o contador), `RegistrarLoginSucesso()`, `EstaBloqueado(agora)`.
`RefreshToken`: `Id`, `UsuarioId`, `TokenHash` (SHA-256 hex do token), `FamiliaId`, `ExpiraEm`
(7 dias), `CriadoEm`, `RevogadoEm?`, `SubstituidoPorId?`; `EstaAtivo(agora)`, `Revogar(agora)`,
`Rotacionar(Guid novoId, agora)`.

### 4.7 Notificações (`Apolice.Domain.Notificacoes`)

`Notificacao`: `Id`, `ReferenciaId` (apólice/sinistro), `Tipo` (`BoasVindas`, `Endosso`,
`Cancelamento`, `SinistroAberto`, `SinistroDecidido`), `Destinatario` (e-mail), `Assunto`, `Corpo`,
`CriadaEm`. São e-mails **simulados** (caixa de saída exibida na UI).

### 4.8 Eventos de domínio

`ApoliceEmitida(ApoliceId, Numero, CorretorId, ProdutoCodigo, SeguradoNome, SeguradoEmail, PremioTotal, VigenciaInicio, VigenciaFim)`,
`ApoliceEndossada(ApoliceId, Numero, EndossoNumero, Tipo, CoberturaCodigo, AjusteTotal, SeguradoNome, SeguradoEmail)`,
`ApoliceCancelada(ApoliceId, Numero, Motivo, ValorRestituicao, SeguradoNome, SeguradoEmail)`,
`SinistroAberto(SinistroId, Numero, ApoliceId, CoberturaCodigo, ValorReclamado, SeguradoEmail)`,
`SinistroAprovado(SinistroId, Numero, ValorIndenizacao, SeguradoEmail)`,
`SinistroNegado(SinistroId, Numero, Motivo, SeguradoEmail)`, `SinistroPago(SinistroId, Numero, SeguradoEmail)`,
`TaxaCoberturaAtualizada(ProdutoCodigo, CoberturaCodigo, TaxaAnterior, TaxaNova)`.
Todos `sealed record` com `EventoId` e `OcorridoEm`. Nome da classe = "tipo" da mensagem no outbox.

## 5. Aplicação (`Apolice.Application`)

Sem mediator: handlers são injetados diretamente nos endpoints (explícito, fácil de navegar).

```
Abstractions/
  Messaging/   ICommandHandler<TCommand,TResponse> { Task<Result<TResponse>> HandleAsync(TCommand, CancellationToken) }
               IQueryHandler<TQuery,TResponse>     (mesma forma)
               IIntegrationEventHandler<TEvent> where TEvent : IDomainEvent { Task HandleAsync(TEvent, CancellationToken) }
               IIntegrationEventDispatcher { Task DispatchAsync(Guid mensagemId, string tipo, string payload, CancellationToken) }
  Data/        IUnitOfWork, ICotacaoRepository, IApoliceRepository, ISinistroRepository, IProdutoRepository,
               IUsuarioRepository, IRefreshTokenRepository, INotificacaoRepository, INumerador,
               IMensagensProcessadas (inbox)
  Queries/     ICotacaoQueries, IApoliceQueries, ISinistroQueries, IDashboardQueries, IAuditoriaQueries
  Security/    ICurrentUser { Guid? UsuarioId; string? Nome; Papel? Papel; bool Autenticado; bool PodeVerTudo /*Analista|Admin*/ },
               IPasswordHasher, ITokenService
  Services/    ICatalogoProdutos (leitura cacheada + invalidação), IAuditoria, IFeatureFlags, IGeradorDocumentoApolice
  Observability/ ApoliceTelemetry (ActivitySource "Apolice" + Meter "Apolice": apolices_emitidas, premio_emitido, sinistros_abertos)
Common/        Pagina<T>(IReadOnlyList<T> Itens, string? ProximoCursor), Cursor (encode/decode base64url de (DateTimeOffset, Guid)),
               Mascaramento (CPF), JsonOpcoes (System.Text.Json compartilhado: camelCase, enums string, DateOnly)
Features/<Contexto>/<Caso>/  Command|Query + Handler + Validator (FluentValidation) + DTOs
DependencyInjection.cs → AddApplication(): registra handlers por varredura (ICommandHandler<,>, IQueryHandler<,>, IIntegrationEventHandler<>), validators, Tarifador/estratégias, dispatcher.
```

Regras dos handlers:
- Autorização por recurso: corretor só enxerga/age sobre o que tem `CorretorId == UsuarioId`;
  Analista/Admin enxergam tudo. **Acesso a recurso de outro corretor retorna `NaoEncontrado`** (não
  revela existência). Ações de sinistro (análise/decisão/pagamento) exigem Analista/Admin.
- Mutação: carrega agregado → chama domínio → `IAuditoria.Registrar(...)` → `IUnitOfWork.SaveChangesAsync`.
  Eventos de domínio viram linhas de outbox **na mesma transação** (interceptor da infra).
- Erros de aplicação conhecidos: `Comum.NaoEncontrado(entidade)`, `Auth.CredenciaisInvalidas`,
  `Auth.UsuarioBloqueado`, `Auth.RefreshInvalido`, `Auth.RefreshReutilizado`, `Concorrencia.Conflito`.
- Login: e-mail inexistente e senha errada retornam o **mesmo** erro (`CredenciaisInvalidas`);
  usuário bloqueado → `UsuarioBloqueado` (429/423 não; use `NaoAutorizado` 401 com código próprio).
- Refresh: token desconhecido/expirado → `RefreshInvalido`; token já revogado (reuso) → revoga a
  família inteira e retorna `RefreshReutilizado`.
- Consumidores (`Features/Eventos`): `ApoliceEmitida` → notificação de boas-vindas + `MarcarBoasVindasEnviadas`;
  `ApoliceEndossada`, `ApoliceCancelada`, `SinistroAprovado`/`Negado`/`Pago` → notificação;
  `SinistroAberto` → triagem automática (se flag `TriagemAutomaticaSinistros`) + notificação.
  Idempotência: o `IntegrationEventDispatcher` checa/grava `IMensagensProcessadas` por
  (mensagemId, nome do handler) na mesma transação do efeito.

## 6. Infraestrutura (`Apolice.Infrastructure`)

- `ApoliceDbContext : DbContext, IUnitOfWork` com `UseSnakeCaseNamingConvention()`. Configurações em
  `Persistence/Configurations`. Coleções de VO = tabelas filhas (não use colunas `jsonb`: o DSQL não suporta).
  `DadosRisco` = coluna `text` com JSON polimórfico (value converter + `JsonOpcoes`).
- Concorrência: `Versao` (int) com `IsConcurrencyToken()`; interceptor incrementa em agregados
  modificados. `DbUpdateConcurrencyException` → erro `Concorrencia.Conflito` (409).
- Interceptors: `DomainEventsToOutboxInterceptor` (SavingChanges: eventos → `OutboxMessage`),
  `VersaoInterceptor`, `OutboxDispatchImediatoInterceptor` (SavedChanges: publica as mensagens
  recém-gravadas, best-effort; o relay cobre falhas). Marcação de processado via `ExecuteUpdateAsync`.
- Provedores (`Database:Provider`): `Postgres` (connection string) | `Dsql` (`Amazon.AuroraDsql.Npgsql`
  → `DsqlDataSource.DataSource` → `UseNpgsql(dataSource)`), com estratégia de retry que inclui
  SQLSTATE `40001`, `OC000`, `OC001`. Sem transações explícitas/savepoints no caminho normal.
- Migrations EF geradas com o provedor Postgres. `DsqlMigrationRunner` aplica no DSQL: um comando
  DDL por transação (autocommit), `CREATE [UNIQUE] INDEX` → `... ASYNC`, `CREATE TABLE/INDEX IF NOT EXISTS`
  (reexecutável), grava histórico do EF, depois `CREATE ROLE`/`AWS IAM GRANT`/`GRANT` do papel da
  aplicação (`Database:AppRole`, `Database:AppRoleArn`). Sem partial index, sem trigger, sem sequence.
- `Numerador` (INumerador): `INSERT ... ON CONFLICT (chave) DO UPDATE SET ultimo = numeradores.ultimo + 1 RETURNING ultimo`
  com retry de OCC. Chaves `APOLICE:{PRODUTO}:{ANO}` e `SINISTRO:{ANO}`. Lacunas são aceitas.
- Leitura (Dapper, `Persistence/Queries`): listagens com paginação por cursor (keyset
  `(criado_em, id) DESC`), filtros, dashboard. SQL parametrizado sempre.
- Mensageria (`Messaging:Transport` = `RabbitMq`|`Sqs`|`InMemory`): `IMessageBus.PublishAsync(MensagemIntegracao)`
  (`Id`, `Tipo`, `Payload`, `OcorridoEm`, `Traceparent?`, `CorrelationId?`). RabbitMQ: exchange topic
  `apolice.eventos`, fila quorum `apolice.worker` (`x-delivery-limit` 5, DLX `apolice.eventos.dlx` →
  `apolice.worker.dlq`), publisher confirms, mensagens persistentes. SQS: `SendMessage` com atributos
  `tipo`, `traceparent`, `correlationId`. InMemory: despacha direto para `IIntegrationEventDispatcher` (testes).
- `OutboxDispatcher.DespacharPendentesAsync(int lote)` (ordem por `ocorrido_em`, incrementa
  `tentativas`, grava `ultimo_erro`).
- Segurança: `PasswordHasher<Usuario>` (PBKDF2 do ASP.NET Core Identity), `JwtTokenService`
  (HS256, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` ≥ 32 bytes, access 15 min com claims
  `sub`, `name`, `email`, `role`, `jti`), refresh token = 32 bytes aleatórios base64url, guardado como SHA-256.
- Cache: `CatalogoProdutosCache` com `HybridCache` (chave `produtos:v1`, tag `produtos`, 10 min);
  L2 Redis opcional (`Redis:ConnectionString`). Invalidação por tag ao atualizar taxa.
- `QuestPdfGeradorDocumentoApolice` (licença Community). `FeatureFlags` sobre `IFeatureManager`.
  `Auditoria` grava `AuditoriaRegistro` no mesmo DbContext (salvo com o UoW).
- `DataSeeder` (idempotente): 3 produtos com coberturas/taxas da seção 4.2; usuários demo
  (`ana.corretora@apolice.dev` Corretor "Ana Ribeiro", `bruno.corretor@apolice.dev` Corretor
  "Bruno Lima", `carla.analista@apolice.dev` Analista "Carla Souza", `admin@apolice.dev` Admin
  "Diego Martins"); senha vinda de `Seed:SenhaUsuarios` (obrigatória fora de Development — em dev,
  gerar aleatória e logar só o aviso, nunca a senha). Dados de demonstração criados **pelos métodos
  do domínio** com datas passadas (≈10 apólices em 12 meses, cotações em todos os status, sinistros
  em todos os status, 2 endossos, 1 cancelamento).
- `DependencyInjection.AddInfrastructure(IServiceCollection, IConfiguration)` registra tudo e valida
  options no start (`ValidateOnStart`).

### 6.1 Tabelas (snake_case)

`usuarios`, `refresh_tokens`, `produtos`, `coberturas_produto` (PK produto_codigo+codigo),
`cotacoes`, `cotacao_itens` (PK cotacao_id+codigo), `cotacao_fatores`, `apolices` (numero UNIQUE,
cotacao_id UNIQUE), `apolice_coberturas` (PK apolice_id+codigo), `endossos` (UNIQUE apolice_id+numero),
`sinistros` (numero UNIQUE), `sinistro_historico`, `notificacoes` (índice referencia_id),
`numeradores` (PK chave), `outbox_mensagens` (índice processado_em+ocorrido_em),
`mensagens_processadas` (PK mensagem_id+consumidor), `idempotencia` (PK usuario_id+chave),
`auditoria` (índice ocorrido_em). Dinheiro `numeric(14,2)`, taxas `numeric(12,6)`, timestamps
`timestamp with time zone`, datas `date`. FKs normais (o DSQL suporta desde 10/2026).

## 7. API (`Apolice.Api`)

Minimal APIs agrupadas por contexto (`Endpoints/*Endpoints.cs`, cada um com `MapXxxEndpoints(this IEndpointRouteBuilder)`),
versionamento por segmento (`/api/v{version:apiVersion}` com Asp.Versioning, v1), OpenAPI nativo
(`AddOpenApi("v1")` com transformer de segurança Bearer e info do documento) + Scalar em `/scalar/v1`
(habilitado em produção, é portfólio). JSON: camelCase, enums como string, `DateOnly` "yyyy-MM-dd".
Polimorfismo de `DadosRisco` com `[JsonPolymorphic(TypeDiscriminatorPropertyName = "produto")]`.

**Pipeline (ordem):** ForwardedHeaders → ExceptionHandler (`IExceptionHandler` → ProblemDetails sem
stack trace) + StatusCodePages → HSTS (prod) → `CorrelationIdMiddleware` (header `X-Correlation-Id`,
aceita ≤ 64 chars `[A-Za-z0-9-]`, senão gera; devolve no response; vai para o LogContext e para o
outbox) → `SecurityHeadersMiddleware` (nosniff, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`,
`Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` exceto rotas do Scalar,
`Permissions-Policy`) → Serilog request logging → CORS (origens de `Cors:Origens`) → RateLimiter →
Authentication → Authorization → endpoints (filtros: validação FluentValidation → 400
`ValidationProblemDetails`; idempotência).

**Mapeamento de erro → HTTP:** Validacao 400, NaoAutorizado 401, Proibido 403, NaoEncontrado 404,
Conflito 409, RegraDeNegocio 422. ProblemDetails com `type = "urn:apolice:problema:{codigo}"`,
`title`, `status`, `detail`, extensões `codigo`, `traceId`, `correlationId`.

**Autorização:** políticas `Corretor` (Corretor|Admin), `Analista` (Analista|Admin), `Admin`.
**Rate limiting:** política `auth` (janela fixa 10/min por IP) em login/demo/refresh; limitador
global token bucket 120/min por usuário (ou IP se anônimo); 429 com `Retry-After` em ProblemDetails.
**Idempotência:** header opcional `Idempotency-Key` (≤ 100 chars) em `POST /apolices` e `POST /sinistros`:
mesma chave + mesmo usuário + mesmo corpo → devolve a resposta gravada; corpo diferente → 422. Guarda 24 h.
**Cookies:** refresh em `refresh_token` — `HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth; Max-Age=7d`.
Endpoints `refresh`/`logout` exigem também o header `X-Requested-With: apolice-web` (defesa em profundidade contra CSRF).
**Health:** `/health/live` (sem dependências), `/health/ready` (banco + broker), fora do versionamento.
**Lambda:** `AddAWSLambdaHosting(LambdaEventSource.HttpApi)` (no-op fora da Lambda); em produção,
configuração adicional do SSM Parameter Store via `Ssm:Prefixo` (ex.: `/apolice/prod`).

### 7.1 Endpoints v1 (todos sob `/api/v1`)

| Método e rota | Política | Corpo → Resposta |
|---|---|---|
| POST `/auth/login` | anônimo + rate `auth` | `LoginRequest{email,senha}` → 200 `AuthResponse` + cookie |
| POST `/auth/demo` | anônimo + rate `auth` + flag `LoginDemo` | `LoginDemoRequest{persona: corretora|corretor|analista|admin}` → 200 `AuthResponse` + cookie |
| POST `/auth/refresh` | cookie + header | → 200 `AuthResponse` + novo cookie |
| POST `/auth/logout` | cookie + header | → 204 (revoga e limpa cookie) |
| GET `/auth/me` | autenticado | → 200 `UsuarioDto` |
| GET `/features` | anônimo | → 200 `FeaturesDto{loginDemo,triagemAutomaticaSinistros,documentoPdf,endossoOnline}` |
| GET `/produtos` | autenticado | → 200 `ProdutoDto[]` (cache) |
| GET `/produtos/{codigo}` | autenticado | → 200 `ProdutoDto` |
| PUT `/produtos/{codigo}/coberturas/{coberturaCodigo}/taxa` | Admin | `{taxa}` → 200 `ProdutoDto` |
| POST `/cotacoes/simular` | Corretor | `CotacaoRequest` → 200 `TarifacaoDto` (não persiste) |
| POST `/cotacoes` | Corretor | `CotacaoRequest` → 201 `CotacaoDto` |
| GET `/cotacoes` | autenticado (escopo) | `?status&produto&busca&cursor&limite` → 200 `Pagina<CotacaoResumoDto>` |
| GET `/cotacoes/{id}` | autenticado (escopo) | → 200 `CotacaoDto` |
| POST `/cotacoes/{id}/aceitar` · `/recusar` | Corretor (dono) | → 200 `CotacaoDto` |
| POST `/apolices` | Corretor (dono da cotação) + Idempotency-Key | `{cotacaoId, inicioVigencia, parcelas}` → 201 `ApoliceDto` |
| GET `/apolices` | autenticado (escopo) | `?status&produto&busca&cursor&limite` → 200 `Pagina<ApoliceResumoDto>` |
| GET `/apolices/{id}` | autenticado (escopo) | → 200 `ApoliceDto` (com endossos, sinistros, notificações, cronograma) |
| GET `/apolices/{id}/documento` | autenticado (escopo) + flag `DocumentoPdf` | → 200 `application/pdf` |
| POST `/apolices/{id}/endossos/simular` · `/endossos` | Corretor (dono) + flag `EndossoOnline` | `{tipo, coberturaCodigo, lmi?}` → 200 `SimulacaoEndossoDto` · 201 `ApoliceDto` |
| POST `/apolices/{id}/cancelamento/simular` · `/cancelamento` | Corretor (dono) | `{motivo}` → 200 `SimulacaoCancelamentoDto` · 200 `ApoliceDto` |
| POST `/sinistros` | Corretor (dono da apólice) + Idempotency-Key | `{apoliceId, coberturaCodigo, dataOcorrencia, descricao, valorReclamado}` → 201 `SinistroDto` |
| GET `/sinistros` | autenticado (escopo) | `?status&busca&cursor&limite` → 200 `Pagina<SinistroResumoDto>` |
| GET `/sinistros/{id}` | autenticado (escopo) | → 200 `SinistroDto` |
| POST `/sinistros/{id}/analise` · `/aprovacao` · `/negativa` · `/pagamento` | Analista | `—` · `{valorIndenizacao, observacao?}` · `{motivo}` · `—` → 200 `SinistroDto` |
| GET `/dashboard` | autenticado (escopo Carteira/Global) | → 200 `DashboardDto` |
| GET `/admin/auditoria` | Admin | `?cursor&limite` → 200 `Pagina<AuditoriaDto>` |

`limite`: 1–100 (padrão 20). `CotacaoRequest = {produtoCodigo, segurado{nome,cpf,email,dataNascimento}, dadosRisco{produto,...}, coberturas[{codigo,lmi?}]}`.

DTOs principais (camelCase):
- `AuthResponse {accessToken, expiraEm, usuario: UsuarioDto}`; `UsuarioDto {id, nome, email, papel}`.
- `ProdutoDto {codigo, nome, descricao, aliquotaIof, coberturas: CoberturaProdutoDto[]}`;
  `CoberturaProdutoDto {codigo, nome, descricao, obrigatoria, tipoCalculo, taxa, premioFixo?, lmiMinimo, lmiMaximo, lmiFixo?, permiteSinistro}`.
- `TarifacaoDto {itens[{codigo,nome,lmi,premioLiquido,obrigatoria,permiteSinistro}], fatores[{descricao,fator}], premioLiquido, aliquotaIof, iof, premioTotal, maxParcelas}`.
- `CotacaoDto {id, produtoCodigo, produtoNome, segurado, dadosRisco, itens, fatores, premioLiquido, aliquotaIof, iof, premioTotal, maxParcelas, status, validaAte, criadaEm, corretorId, corretorNome, apoliceId?}`;
  `CotacaoResumoDto {id, produtoCodigo, seguradoNome, premioTotal, status, validaAte, criadaEm, corretorNome}`.
- `ApoliceDto {id, numero, cotacaoId, produtoCodigo, produtoNome, segurado, dadosRisco, vigenciaInicio, vigenciaFim, coberturas[...], premioLiquido, ajustesEndosso, premioLiquidoVigente, aliquotaIof, iof, premioTotal, parcelas, cronogramaParcelas[{numero,vencimento,valor}], status, emitidaEm, canceladaEm?, motivoCancelamento?, valorRestituicao?, boasVindasEnviadasEm?, corretorId, corretorNome, endossos: EndossoDto[], sinistros: SinistroResumoDto[], notificacoes: NotificacaoDto[]}`;
  `ApoliceResumoDto {id, numero, produtoCodigo, seguradoNome, seguradoCpfMascarado, premioTotal, vigenciaInicio, vigenciaFim, status, corretorNome, emitidaEm}`.
- `EndossoDto {id, numero, tipo, coberturaCodigo, coberturaNome, lmiAnterior?, lmiNovo?, dataEfeito, ajusteLiquido, iofAjuste, ajusteTotal, criadoEm, criadoPorNome}`;
  `SimulacaoEndossoDto {dataEfeito, diasRestantes, diasVigencia, premioAnualAnterior, premioAnualNovo, ajusteLiquido, iofAjuste, ajusteTotal}`.
- `SimulacaoCancelamentoDto {dataCancelamento, diasDecorridos, diasVigencia, metodo, percentualRetido?, premioLiquidoBase, valorRestituicao}`.
- `SinistroDto {id, numero, apoliceId, apoliceNumero, produtoCodigo, seguradoNome, coberturaCodigo, coberturaNome, lmiCobertura, dataOcorrencia, descricao, valorReclamado, valorIndenizacao?, status, motivoNegativa?, triagem?{score, alertas[], triadoEm}, abertoEm, corretorNome, historico[{status, observacao?, usuarioNome?, ocorridoEm}]}`;
  `SinistroResumoDto {id, numero, apoliceNumero, seguradoNome, coberturaNome, valorReclamado, status, triagemScore?, abertoEm}`.
- `NotificacaoDto {id, tipo, destinatario, assunto, corpo, criadaEm}`.
- `DashboardDto {escopo, apolicesAtivas, premioEmitidoMes, premioEmitido12Meses[{mes:"2026-10", valor}], cotacoesAbertas, taxaConversao90d, sinistrosEmAberto, sinistralidade12m, porProduto[{produtoCodigo, produtoNome, apolicesAtivas, premioEmitido}]}`.
- `AuditoriaDto {id, usuarioNome?, acao, entidade, entidadeId?, detalhes?, correlationId?, ocorridoEm}`.

Listagens mascaram CPF; o detalhe mostra o CPF completo para quem tem acesso.

## 8. Worker (`Apolice.Worker`)

- `dotnet Apolice.Worker.dll migrate` → aplica migrations (Postgres: `Database.MigrateAsync`; DSQL: `DsqlMigrationRunner`).
- `dotnet Apolice.Worker.dll seed` → `DataSeeder`.
- sem argumentos → host local: `OutboxRelayService` (a cada 2 s) + `RabbitMqConsumerService`
  (prefetch 10, escopo de DI por mensagem, ack/nack sem requeue → delivery-limit/DLQ).
- Lambda (produção): `Apolice.Worker::Apolice.Worker.Lambda.SqsFunction::HandleAsync`
  (`SQSEvent` → `SQSBatchResponse` com falhas parciais) e
  `Apolice.Worker::Apolice.Worker.Lambda.OutboxRelayFunction::HandleAsync` (agendada a cada 5 min).
  O `ServiceProvider` é criado uma vez por container (cold start).
- Propagação: `traceparent` da mensagem vira o pai da `Activity` de consumo; `CorrelationId` vai para o LogContext.

## 9. Observabilidade

Serilog (JSON compacto em produção, console legível em dev) com `CorrelationId`, `TraceId`, `SpanId`,
`Application`, `Environment`; nunca logar senha, token, CPF completo ou e-mail completo.
OpenTelemetry: traces (ASP.NET Core, HttpClient, Npgsql, ActivitySource `Apolice` para publicar/consumir),
métricas (ASP.NET Core, runtime, Meter `Apolice`); exportador OTLP só quando
`OTEL_EXPORTER_OTLP_ENDPOINT` existir (local: Aspire Dashboard no docker compose).

## 10. Testes

- Domain: valores **exatos** da tarifação (casos calculados à mão nos testes), arredondamento,
  CPF, máquinas de estado, endosso pro rata, cancelamento (bordas da tabela de prazo curto, pro rata,
  integral), cronograma de parcelas, triagem, lockout, refresh.
- Application: handlers com fakes/NSubstitute — escopo por corretor (404), fluxos, erros, idempotência
  do dispatcher, login/refresh (reuso revoga família), validators.
- Integração: `WebApplicationFactory<Program>` + Testcontainers PostgreSQL (e RabbitMQ no teste do
  adaptador); migrations reais; fluxo completo cotação → emissão → endosso → sinistro → aprovação;
  401/403/404/400/409/422/429; idempotency-key; refresh rotativo; headers de segurança; health; outbox → consumidor → notificação.
- Arquitetura: regras da seção 3.
- E2E (Playwright): login demo corretora → nova cotação Auto → aceitar → emitir → ver apólice →
  abrir sinistro → login analista → analisar → aprovar.
- Cobertura alvo > 80% em Domain + Application.

## 11. Frontend (`frontend/`)

Vite + React 19 + TypeScript strict, TanStack Query v5, React Router 7, React Hook Form + Zod,
Tailwind CSS 4, componentes acessíveis (Radix), Recharts, `openapi-typescript` + `openapi-fetch`
gerando o cliente tipado a partir de `docs/openapi/v1.json`. Access token só em memória; refresh
automático (single-flight) em 401 via middleware do openapi-fetch; cookie HttpOnly no refresh.
Páginas: login (botões de persona demo + formulário), dashboard, cotações (lista + assistente de nova
cotação com simulação ao vivo), detalhe da cotação, apólices (lista + detalhe com abas e ações:
PDF, endosso, cancelamento com simulação), sinistros (lista + detalhe com linha do tempo e ações do
analista), admin (tarifas, auditoria), "Sobre o projeto" (arquitetura e links). WCAG 2.1 AA, mobile
first (375 px), tema claro/escuro, pt-BR (moeda e datas). Vitest + Testing Library + axe; Playwright.

## 12. Produção (AWS + Vercel)

- **API**: Lambda `dotnet10` (arm64, 1024 MB) com Function URL; **Worker**: Lambda SQS + Lambda
  agendada (relay). **Banco**: Aurora DSQL (IAM auth — sem senha de banco; papel de aplicação com
  privilégio mínimo). **Fila**: SQS + DLQ. **Segredos**: SSM Parameter Store (SecureString).
  **IaC**: Terraform (state em S3 com lockfile). **CI/CD**: GitHub Actions com OIDC (sem chaves longas).
- **Frontend**: Vercel; `vercel.json` reescreve `/api/*`, `/scalar/*`, `/openapi/*` para a Function URL
  (mesma origem para o browser → cookie first-party, sem CORS).

## 13. Chaves de configuração (canônicas)

Em env var use `__` no lugar de `:` (ex.: `Database__Provider`). Nada de segredo em `appsettings*.json`.

| Chave | Valores / padrão | Observação |
|---|---|---|
| `Database:Provider` | `Postgres` (padrão) \| `Dsql` | |
| `ConnectionStrings:Apolice` | — | só Postgres |
| `Database:DsqlEndpoint` | `<id>.dsql.<região>.on.aws` | só DSQL |
| `Database:DsqlUser` | `admin` (migrations) \| `apolice_app` (aplicação) | IAM auth, sem senha |
| `Database:AppRole` | `apolice_app` | papel criado pelo migrator |
| `Database:AppRoleArns` | lista de ARNs IAM | `AWS IAM GRANT` do papel da aplicação |
| `Messaging:Transport` | `RabbitMq` (padrão) \| `Sqs` \| `InMemory` | |
| `Messaging:RabbitMq:Host`/`Port`/`Username`/`Password`/`VirtualHost` | `localhost`/`5672`/—/—/`/` | |
| `Messaging:Sqs:QueueUrl` | — | |
| `Outbox:DispatchImediato` | `true` na API, `false` no worker | |
| `Outbox:IntervaloSegundos` / `Outbox:Lote` | `2` / `50` | relay local |
| `Jwt:Issuer` / `Jwt:Audience` | `apolice-api` / `apolice-web` | |
| `Jwt:SigningKey` | ≥ 32 bytes | obrigatório fora de Development; em Development, se ausente, gerar chave efêmera |
| `Jwt:AccessTokenMinutos` / `Jwt:RefreshTokenDias` | `15` / `7` | |
| `Cors:Origens` | lista | vazio = sem CORS (mesma origem via proxy) |
| `Seed:SenhaUsuarios` | — | obrigatório fora de Development |
| `Seed:DadosDemo` | `true` | |
| `Redis:ConnectionString` | — | opcional (L2 do HybridCache) |
| `Ssm:Prefixo` | ex.: `/apolice/prod` | se presente, carrega parâmetros do SSM |
| `FeatureManagement:LoginDemo`/`TriagemAutomaticaSinistros`/`DocumentoPdf`/`EndossoOnline` | `true` | |
| `OTEL_EXPORTER_OTLP_ENDPOINT` / `OTEL_SERVICE_NAME` | — / `apolice-api` \| `apolice-worker` | exportador só se definido |

Portas locais: API `8080` (container) / `5080` (`dotnet run`), frontend dev `5173` (proxy `/api` → API),
Postgres `5432`, RabbitMQ `5672`/`15672`, Aspire Dashboard `18888` (UI) e `4317` (OTLP).

## 14. Compatibilidade do Aurora DSQL — verificada no cluster real (22/10/2026)

Rodado com `psql` (usuário `admin`, IAM token) no cluster de produção, cada comando em autocommit:
`CREATE SCHEMA`; `CREATE TABLE` com PK composta e `REFERENCES ... ON DELETE CASCADE` inline (FK é
aplicada: violação → erro 23503); `CREATE INDEX ASYNC IF NOT EXISTS` e `CREATE UNIQUE INDEX ASYNC
IF NOT EXISTS` (devolvem `job_id`; acompanhar em `sys.jobs`); `INSERT ... ON CONFLICT (chave) DO
UPDATE SET ultimo = t.ultimo + 1 RETURNING ultimo`; `ILIKE`; keyset com `OR` explícito e com
row-value `(a, b) < (x, y)`; `date_trunc('month', ts AT TIME ZONE 'America/Sao_Paulo')` + `to_char`;
agregação com `FILTER`; `UPDATE ... WHERE versao = x`; `CREATE ROLE ... WITH LOGIN`; `GRANT USAGE ON
SCHEMA`; `GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA`; `AWS IAM GRANT <papel> TO
'<arn da role>'` e `AWS IAM REVOKE`. Tudo funcionou. Índice criado com `ASYNC` só passa a ser usado
quando o job termina — o migrator não precisa esperar, mas não dependa dele no mesmo deploy.
