# ADR 0008: Feature flags, HybridCache e rate limiting

- **Status:** aceita
- **Data:** 2026-10

## Contexto

Três necessidades pequenas, cada uma com uma solução nativa do ecossistema .NET:

- **Ligar e desligar funcionalidades sem deploy de código:** o login por persona da demo, a triagem automática de sinistros, o PDF da apólice e o endosso online.
- **Não reler o catálogo de produtos a cada simulação:** a tela de cotação recalcula o prêmio a cada campo, e o catálogo (produtos, coberturas, taxas) quase nunca muda.
- **Proteger os endpoints de autenticação e a API como um todo** contra força bruta e abuso.

## Decisão

**Feature flags com `Microsoft.FeatureManagement`.**
- Quatro flags: `LoginDemo`, `TriagemAutomaticaSinistros`, `DocumentoPdf` e `EndossoOnline`. A aplicação depende só da porta `IFeatureFlags`; o adapter da infraestrutura usa o `IVariantFeatureManager`.
- O FeatureManagement trata flag ausente como desligada, mas a SPEC quer as quatro ligadas por padrão. Os padrões entram como a camada **mais baixa** da configuração (`ComPadroesDeFeatures`), e qualquer valor explícito (appsettings, variável de ambiente, parâmetro do SSM sob o prefixo da aplicação) prevalece.
- Flag desligada responde `Comum.FuncionalidadeDesabilitada` como **404**, o mesmo comportamento do `FeatureGate` do pacote: funcionalidade desligada se comporta como inexistente. A triagem desligada não é erro: o consumidor de triagem só registra em log, e a notificação do sinistro, que é outro consumidor, sai normalmente.
- `GET /api/v1/features` (anônimo) expõe o estado das quatro para o frontend esconder o que não está disponível.

**Catálogo em `HybridCache`** (`Services/CatalogoProdutosCache.cs`).
- Chave `produtos:v1`, tag `produtos`, expiração de 10 minutos. L1 em memória sempre; L2 em Redis opcional, ligado por `Redis:ConnectionString` (Valkey no perfil `cache` do compose).
- O valor em cache é um **retrato imutável e serializável** (records só com dados). Cada leitura reconstrói agregados `Produto` novos pelas fábricas do domínio, então nenhum chamador recebe instância compartilhada, e um retrato corrompido no L2 falha alto em vez de virar catálogo errado.
- A carga abre um escopo de DI próprio. O `HybridCache` protege contra estouro (*stampede*): várias requisições simultâneas compartilham uma única carga, e ela não pode depender do `DbContext` de quem chegou primeiro, que pode terminar antes.
- A alteração de taxa pelo admin invalida por tag e por chave **depois** do commit. Se a invalidação falhar, a resposta continua sendo sucesso (a taxa já foi gravada) e o cache expira sozinho.
- **Só a simulação lê o cache.** Criar a cotação, que grava o prêmio, lê o cadastro pelo repositório (`TarifacaoCotacao.TarifarParaGravarAsync`), porque o L1 de outra instância pode estar com a taxa antiga.

**Rate limiting do ASP.NET Core** (`src/Apolice.Api/Http/LimitesRequisicao.cs`, SPEC §7).
- Política `auth`: janela fixa de 10 requisições por minuto por IP em `login`, `demo` e `refresh`.
- Limitador global: token bucket de 120 fichas, repostas a 2 por segundo (120 por minuto sem abrir uma janela de 240 na virada do minuto). A partição é `usuario:{sub}` para quem tem JWT válido e `ip:{endereço}` para anônimos.
- Para a partição por usuário ser confiável, a **autenticação roda antes do limitador** no pipeline, ao contrário da ordem escrita na SPEC: só o JWT já validado diz quem é o usuário, e ler o `sub` sem validar deixaria um atacante trocar de partição a cada token forjado. O ajuste está comentado em `src/Apolice.Api/Extensions/PipelineExtensions.cs`.
- Estouro responde 429 em ProblemDetails, com o header `Retry-After` e a extensão `retryAfter`. Os health checks ficam fora do limitador.
- Complementa o bloqueio de conta por senha errada ([ADR 0005](0005-jwt-refresh-rotativo-cookie-anti-bola.md)): o bloqueio protege uma conta, o limitador protege o endpoint.

## Alternativas consideradas

- **AWS AppConfig ou um serviço de flags (LaunchDarkly, Unleash).** Segmentação por usuário e mudança instantânea, mas mais um serviço e mais custo para quatro booleanos.
- **`IMemoryCache` ou `IDistributedCache` direto.** O `HybridCache` já junta os dois níveis, protege contra estouro e tem invalidação por tag.
- **Sem cache.** Cada recálculo da simulação iria ao banco buscar produto e coberturas. Funcionaria, mas é o caso de uso mais quente da tela.
- **Rate limiting na borda (API Gateway, WAF).** Contaria entre instâncias e barraria antes da invocação, mas custa por mês e a escolha da Function URL ([ADR 0006](0006-serverless-lambda-function-url-rewrite-vercel.md)) o deixa de fora.

## Consequências

- **Tudo isso é por instância em produção.** Não há Redis na AWS, então cada container Lambda tem o próprio L1: depois de uma mudança de taxa, outro container pode simular com a taxa anterior por até 10 minutos (a cotação gravada não, pela regra acima). O limitador também conta por container: com N containers quentes, o limite efetivo é até N vezes maior.
- O limite por IP depende do `X-Forwarded-For`, que a API aceita de qualquer remetente porque os IPs da Vercel não são fixos; quem chama a Function URL direto pode forjá-lo ([ADR 0006](0006-serverless-lambda-function-url-rewrite-vercel.md)).
- Mudar uma flag em produção é gravar um parâmetro no SSM sob `/apolice/prod`. A configuração do SSM é carregada na inicialização (obrigatória: sem ela a API não sobe), então o valor novo vale para os containers que iniciarem depois.
- Os testes de integração cobrem as quatro flags ligadas por padrão, uma flag desligada pela configuração, as instâncias exclusivas do catálogo e a taxa nova visível só depois da invalidação.
