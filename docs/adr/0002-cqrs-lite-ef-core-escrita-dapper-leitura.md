# ADR 0002: CQRS-lite, EF Core na escrita e Dapper na leitura

- **Status:** aceita
- **Data:** 2026-10

## Contexto

Escrita e leitura têm formas diferentes neste sistema:

- **Escrita** mexe num agregado inteiro (apólice com coberturas e endossos, sinistro com histórico), precisa de concorrência otimista e transforma eventos de domínio em linhas de outbox na mesma transação.
- **Leitura** é listagem filtrada e paginada (cotações, apólices, sinistros, auditoria), busca por texto e CPF, e um dashboard com agregações por mês e por produto. Junta tabelas, devolve DTOs achatados e não precisa de change tracking.

Usar o mesmo modelo para os dois obriga a escolher entre um ORM fazendo consultas analíticas ou SQL à mão para manter invariantes de agregado.

## Decisão

Separar os lados dentro do mesmo banco, sem barramento nem réplica de leitura:

- **Escrita: EF Core 10** com tracking. Os repositórios (`Persistence/Repositories`) carregam o agregado completo por um único caminho (`Carregamentos.Completas()`), com `Include` filtrado e ordenado e `AsSingleQuery`. O caso de uso altera o agregado pelos métodos do domínio e chama `SaveChangesAsync`.
- **Leitura: Dapper** (`Persistence/Queries`), atrás das portas `ICotacaoQueries`, `IApoliceQueries`, `ISinistroQueries`, `IDashboardQueries` e `IAuditoriaQueries`:
  - SQL sempre parametrizado; o `WHERE` é montado só com fragmentos fixos do código (`ConsultaSql`), e a busca escapa os curingas do `ILIKE`;
  - paginação **keyset** por `(instante, id) DESC` com cursor opaco em base64url, pedindo `limite + 1` linhas para saber se há próxima página;
  - índices compostos que casam com a ordenação (por exemplo `(emitida_em, id)` e `(corretor_id, emitida_em)` em `apolices`).
- **Um único `NpgsqlDataSource`** por processo serve EF, Dapper e o numerador: um pool só, e no DSQL um único provedor de token IAM.

**Exceção consciente:** o detalhe da apólice (`ApoliceQueries.ObterAsync`) materializa o agregado com EF Core **sem tracking** e o mapeia para DTO. Cronograma de parcelas e status efetivo saem das regras do domínio, em vez de uma cópia delas em SQL. O complemento (sinistros, notificações, nomes) vem por Dapper.

## Alternativas consideradas

- **Só EF Core.** Funciona, mas as listagens com status efetivo calculado, a busca por CPF com ou sem máscara e as agregações condicionais do dashboard (`SUM(CASE ...)` por produto e por mês) ficariam em LINQ de SQL difícil de prever ou em `FromSql` de qualquer jeito.
- **Só Dapper.** Perde o change tracking, a concorrência por token e os interceptors que gravam o outbox na mesma transação. Cada agregado precisaria de SQL de persistência à mão.
- **CQRS completo** (modelo de leitura projetado por eventos). Consistência eventual na tela e muito mais código, sem volume que justifique.
- **Paginação por `OFFSET`.** Custo cresce com a página e itens pulam ou repetem quando entram registros novos.

## Consequências

- O status efetivo da apólice e da cotação existe em dois lugares: no domínio e num `CASE` SQL na listagem. Os testes de integração `Deve_listar_apolices_com_status_efetivo_do_dominio_quando_filtrar_e_paginar` e `Deve_listar_cotacoes_como_o_dominio_quando_filtrar_e_paginar` comparam as duas versões.
- O keyset usa `OR` explícito (`a < x OR (a = x AND id < y)`) em vez de row-value `(a, id) < (x, y)`: os dois funcionam no Aurora DSQL (SPEC §14), mas o comentário em `Sql.cs` registra que o DSQL não otimiza a comparação de row-value como o PostgreSQL.
- A leitura não passa pelo agregado, então a carteira do corretor vira filtro no SQL. Nos detalhes (cotação, apólice, PDF), o handler ainda confere o registro que voltou (`EscopoCorretor.Alcanca`) antes de devolvê-lo: um filtro esquecido na consulta responde 404 em vez de vazar dado.
- Mudança de schema exige atualizar o modelo do EF e o SQL do Dapper; os testes de integração rodam os dois contra o PostgreSQL migrado.
