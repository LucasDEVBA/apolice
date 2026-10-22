# ADR 0006: Lambda com Function URL atrás do rewrite da Vercel (mesma origem)

- **Status:** aceita
- **Data:** 2026-10

## Contexto

A demo precisa ficar no ar o tempo todo com custo perto de zero e tráfego esporádico. Um container ou uma VM ligados cobram por hora mesmo sem ninguém usando. Além disso, o desenho de autenticação ([ADR 0005](0005-jwt-refresh-rotativo-cookie-anti-bola.md)) depende de o cookie de refresh ser *first-party* com `SameSite=Strict`, e isso só funciona se o navegador enxergar o frontend e a API na mesma origem.

## Decisão

- **API em AWS Lambda** (runtime gerenciado `dotnet10`, arm64, 1024 MB, timeout de 30 s). O mesmo `Program.cs` do ASP.NET Core roda local e na Lambda: `AddAWSLambdaHosting(LambdaEventSource.HttpApi)` não faz nada fora da Lambda e, dentro dela, traduz o evento HTTP para o pipeline normal do ASP.NET Core.
- **Function URL** com `authorization_type = NONE` como porta de entrada, sem API Gateway e sem bloco de CORS. A autenticação é da aplicação (JWT).
- **Frontend na Vercel**, estático. O `vercel.json` reescreve `/api/*`, `/scalar/*` e `/openapi/*` para a Function URL. Para o navegador tudo vem do domínio da Vercel: o cookie de refresh é do mesmo site, `SameSite=Strict` funciona e não há chamada cross-origin para autorizar.
- **Worker em duas Lambdas** com o mesmo pacote: uma consome o SQS (lotes de 10, falha parcial, concorrência máxima 2 no poller) e outra é o relay do outbox, disparado por uma regra do EventBridge a cada 5 minutos.
- **Configuração e segredos** no SSM Parameter Store (SecureString), carregados como fonte de configuração do .NET a partir do prefixo `Ssm:Prefixo` (`/apolice/prod`). Variáveis de ambiente das Lambdas só carregam o que não é segredo (endpoint do DSQL, URL da fila, prefixo do SSM).
- Localmente, o Vite faz o mesmo papel da Vercel: proxy de `/api`, `/scalar`, `/openapi` e `/health` para a API em `127.0.0.1:8080`.

## Alternativas consideradas

- **API Gateway HTTP API.** Daria throttling por rota e autorizador JWT na borda, mas cobra por requisição e não está entre as franquias gratuitas que não expiram; a Function URL não tem cobrança além da própria invocação da Lambda.
- **ECS Fargate ou App Runner.** Sem cold start, mas com custo por hora parado.
- **Frontend e API em domínios diferentes com CORS.** Exigiria cookie `SameSite=None` num domínio de terceiro, que os navegadores bloqueiam cada vez mais, e uma política de CORS a manter.
- **Servir a SPA pela própria Lambda.** Cada asset estático viraria uma invocação cobrada; a Vercel entrega pelo CDN.

## Consequências

- **Cold start.** A primeira requisição de um container novo paga a inicialização do .NET e do pipeline. SnapStart está desligado porque, em .NET, ele cobra cache e restore.
- **A Function URL é pública.** Quem descobrir a URL pode chamá-la direto, sem passar pela Vercel, e o rate limiting da aplicação só age depois da invocação, que já foi cobrada. O freio é `api_reserved_concurrency` no Terraform (0 desliga a API), mas conta nova (limite de 10 execuções simultâneas) não consegue reservar nada acima de 0. O orçamento mensal só avisa. Detalhes e estimativa do pior caso no [README da infraestrutura](../../infra/terraform/README.md#limitações-conhecidas).
- **Estado por container.** Rate limiter, L1 do cache e pool de conexões vivem em memória e valem por container; dois containers não compartilham contagem nem cache ([ADR 0008](0008-feature-flags-hybridcache-rate-limiting.md)).
- **O IP que a API enxerga** é o de quem chama a Function URL; atrás da Vercel, é a infraestrutura da Vercel, e o IP do usuário chega em `X-Forwarded-For`. Como os IPs da Vercel não são fixos, a API aceita esse header de qualquer remetente, com `ForwardLimit = 1` (`src/Apolice.Api/Extensions/SegurancaExtensions.cs`). O risco, aceito e documentado no código: quem chama a Function URL direto pode forjar o header e trocar de partição no limite por IP. O limite por usuário autenticado não depende disso.
- **O destino do rewrite está no `vercel.json`**, que é público no repositório: a URL da API não é segredo, e a defesa contra abuso não pode depender de escondê-la.
- O pacote da Lambda é enviado direto (até 50 MB compactado); acima disso seria preciso publicar via S3.
