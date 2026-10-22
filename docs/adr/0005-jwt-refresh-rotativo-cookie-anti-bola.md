# ADR 0005: JWT curto, refresh rotativo em cookie HttpOnly e anti-BOLA com 404

- **Status:** aceita
- **Data:** 2026-10

## Contexto

A SPA em React precisa autenticar contra uma API stateless em Lambda. Os riscos que importam aqui são os do OWASP API Top 10 e do Top 10 web:

- **roubo de token por XSS:** qualquer token em `localStorage` é legível por script injetado;
- **CSRF:** credencial que o navegador envia sozinho (cookie) pode ser usada por outro site;
- **BOLA/IDOR:** um corretor trocar o id na URL e ler a apólice de outro corretor;
- **adivinhação de senha e enumeração de usuários.**

## Decisão

**Dois tokens com papéis diferentes.**
- **Access token:** JWT HS256 de 15 minutos com `sub`, `name`, `email`, `role` e `jti`, emissor `apolice-api` e audiência `apolice-web`. A chave (`Jwt:SigningKey`, 32 bytes ou mais) vem do SSM em produção; em Development, se ausente, é gerada por processo. O frontend guarda o access token **só em memória** e o envia no header `Authorization`.
- **Refresh token:** 32 bytes aleatórios em base64url, válido por 7 dias, enviado num cookie `refresh_token` com `HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth`. O banco guarda só o **SHA-256** dele (`RefreshToken.TokenHash`): um vazamento do banco não entrega sessões.

**Rotação com família.** Cada login abre uma família. Cada `/auth/refresh` revoga o token apresentado, aponta o substituto (`SubstituidoPorId`) e emite um novo na mesma família. Se um token **já rotacionado** volta, é sinal de roubo: a família inteira é revogada, o evento vai para a auditoria e a resposta é `Auth.RefreshReutilizado`. Token revogado sem substituto (logout, usuário desativado) só responde `Auth.RefreshInvalido`, sem alarme, porque uma aba antiga reenviando o cookie não é ataque. Logout revoga a família toda.

**Defesa extra contra CSRF.** `refresh` e `logout` exigem o header `X-Requested-With: apolice-web`. O `SameSite=Strict` já impede o envio do cookie a partir de outro site; o header custom é a segunda camada, porque um formulário HTML não define headers e um `fetch` de outra origem com header custom exige um preflight de CORS que a API não autoriza.

**Login.**
- E-mail inexistente, conta inativa e senha errada respondem o mesmo `Auth.CredenciaisInvalidas`. Nos dois primeiros casos o PBKDF2 roda contra um hash fictício (`IPasswordHasher.SimularVerificacao`), para o tempo de resposta não denunciar a conta.
- A 5ª falha seguida bloqueia a conta por 15 minutos e responde `Auth.UsuarioBloqueado`. O bloqueio não derruba sessões abertas, senão um atacante deslogaria a vítima só errando a senha dela.
- Tentativas em paralelo podem conflitar na versão do usuário; o handler recarrega e refaz a gravação (até 10 vezes), para toda falha contar no bloqueio.
- O login por persona demo ignora o bloqueio de propósito: os e-mails das personas são públicos, e respeitar o bloqueio deixaria qualquer um tirar a demo do ar.
- Senhas com `PasswordHasher` do ASP.NET Core Identity (PBKDF2, formato V3).

**Validação do token na API** (`src/Apolice.Api/Extensions/SegurancaExtensions.cs`): emissor, audiência e chave vêm das mesmas options que o emissor usa; só HS256 é aceito (`ValidAlgorithms`), então um token com `alg: none` ou outro algoritmo é recusado; expiração obrigatória, com tolerância de relógio de 30 s. Sem o header anti-CSRF, `refresh` e `logout` respondem 403 (`Auth.CabecalhoCsrfAusente`).

**Autorização: papel e recurso.**
- Política de fallback exige usuário autenticado: rota nova sem metadado de autorização já nasce protegida, e as anônimas declaram `AllowAnonymous`.
- Políticas por papel na borda: `Corretor` (Corretor ou Admin), `Analista` (Analista ou Admin) e `Admin`.
- Por recurso, nos handlers (`Application/Common/AcessoUsuario.cs`): o corretor só alcança o que tem `CorretorId` igual ao seu id; Analista e Admin enxergam tudo. **Recurso de outro corretor responde `NaoEncontrado` (404)**, igual a um id que não existe. O Analista vê a apólice mas não a opera (403); o corretor dono não decide o próprio sinistro (403).
- A autenticação é conferida antes de carregar o recurso, para um anônimo não distinguir "existe" (401) de "não existe" (404).

## Alternativas consideradas

- **Tudo em `localStorage`.** Simples, mas um XSS leva a sessão inteira.
- **Access token em cookie.** Todo endpoint passaria a ser alvo de CSRF e precisaria de token anti-CSRF.
- **Sessão no servidor com cookie opaco.** Também seria seguro, mas exigiria consultar a sessão no banco a cada requisição; o JWT curto valida sem I/O e é o que a vaga pede.
- **RS256 (chave assimétrica).** Útil quando outros serviços validam o token sem poder emiti-lo. Aqui a mesma API emite e valida.
- **Responder 403 para recurso de outro corretor.** Revela que o id existe.

## Consequências

- Um access token roubado vale até expirar (no máximo 15 minutos); não há lista de revogação para ele.
- Ao recarregar a página, o access token some da memória e o frontend chama `/auth/refresh` com o cookie (single-flight, também em resposta a 401).
- `SameSite=Strict` e cookie first-party só funcionam porque o frontend e a API estão na mesma origem graças ao rewrite da Vercel ([ADR 0006](0006-serverless-lambda-function-url-rewrite-vercel.md)).
- A regra de carteira vive num lugar só (`AcessoUsuario`). Os testes de aplicação (`EscopoPorCorretorTests`) cobrem os casos de 404 por carteira, e os testes HTTP (`AutorizacaoApiTests`, `SessaoApiTests`) cobrem token adulterado ou com outro algoritmo, 403 por papel, 404 de outra carteira, atributos do cookie, rotação, reuso e o header anti-CSRF.
