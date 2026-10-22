# ADR 0003: Aurora DSQL em produção, PostgreSQL local e no CI

- **Status:** aceita
- **Data:** 2026-10

## Contexto

O projeto precisa de um banco relacional em produção que:

1. custe perto de zero parado (é um portfólio, com tráfego esporádico);
2. não exija VPC: Lambda em VPC precisa de NAT ou de VPC endpoints para alcançar SQS e SSM, e os dois cobram por hora;
3. não tenha senha de banco para guardar e rotacionar;
4. fale o protocolo do PostgreSQL, para o EF Core e o Dapper funcionarem e para desenvolver e testar contra um PostgreSQL comum.

O Aurora DSQL atende aos quatro: é serverless (os primeiros 100 mil DPUs e 1 GB de cada mês são gratuitos), é acessado pela internet com TLS, autentica por token IAM e é compatível com o protocolo do PostgreSQL. Em troca, não é um PostgreSQL completo: concorrência otimista com conflito detectado no `COMMIT`, uma DDL por transação, índices criados de forma assíncrona e ausência de `jsonb`, sequence/identity, trigger e índice parcial.

## Decisão

**Dois provedores, um modelo.** `Database:Provider` escolhe `Postgres` (connection string, usado no `docker compose`, no CI e nos Testcontainers) ou `Dsql`. O modelo do EF e o SQL do Dapper ficam dentro do subconjunto que o DSQL aceita:

- coleções de value objects viram tabelas filhas; `DadosRisco` é uma coluna `text` com JSON polimórfico (sem `jsonb`);
- ids `Guid.CreateVersion7()` gerados no domínio, nunca pelo banco (sem sequence);
- números de apólice e de sinistro vêm de uma tabela `numeradores` com `INSERT ... ON CONFLICT DO UPDATE ... RETURNING`, refeito em caso de conflito;
- nada de trigger: versão e ordem dos filhos são preenchidas por interceptors do EF.

**Conexão com o DSQL** (`Persistence/Provedores/FonteDadosNpgsql.cs`): um `NpgsqlDataSourceBuilder` com `SslMode=VerifyFull` e `UsePeriodicPasswordProvider`, que gera o token IAM pelo `DSQLAuthTokenGenerator` do `AWSSDK.DSQL` (validade de 15 min, renovado a cada 10 min em segundo plano). O registro em DI é síncrono, sem sync-over-async. A conexão física vive no máximo 55 min (o DSQL encerra em 1 h) e usa `NoResetOnClose`, porque o DSQL não aceita o `DISCARD ALL` que o pool do Npgsql envia. A estratégia de retry do EF inclui os SQLSTATE de conflito `40001`, `OC000` e `OC001`.

**Migrator próprio para o DSQL.** As migrations são geradas pelo EF com o provedor do PostgreSQL. No PostgreSQL, o verbo `migrate` do Worker chama `Database.MigrateAsync`. No DSQL, chama `DsqlMigrationRunner`, que:

- gera o SQL de cada migration com o `IMigrationsSqlGenerator` do próprio EF;
- passa cada comando pelo `TransformadorSqlDsql`: `CREATE [UNIQUE] INDEX` vira `... INDEX ASYNC IF NOT EXISTS`, tabelas e colunas ganham `IF NOT EXISTS`, e DDL incompatível (sequence, identity, trigger, extension, `jsonb`, índice parcial) é recusado antes de tocar o banco;
- executa um comando por conexão, em autocommit, com retry de OCC, tratando "já existe" como sucesso: a migration é reexecutável depois de uma falha no meio;
- espera o job de cada índice com `sys.wait_for_job` (best-effort) e grava o histórico na tabela do próprio EF;
- cria o papel `apolice_app` com `LOGIN`, vincula-o às roles IAM das três Lambdas (`AWS IAM GRANT`) e concede `USAGE` no schema e `SELECT, INSERT, UPDATE, DELETE` tabela por tabela. As Lambdas conectam com esse papel; só o deploy usa o `admin`.

O `Database.MigrateAsync` não serve no DSQL porque usa `LOCK TABLE` e várias DDLs por transação. O único adapter oficial do EF para DSQL publicado no NuGet, `Amazon.AuroraDsql.EntityFrameworkCore` 1.0.0, é compilado contra EF Core 9 (depende de `Microsoft.EntityFrameworkCore >= 9.0.7` e `Npgsql.EntityFrameworkCore.PostgreSQL >= 9.0.1`), enquanto este projeto usa EF Core 10.0.13 e Npgsql EF 10.0.3.

**Compatibilidade verificada no cluster real** em 22/10/2026 (SPEC §14): PK composta, FK aplicada, índices `ASYNC`, o upsert do numerador, `ILIKE`, keyset com `OR` e com row-value, `date_trunc` com `AT TIME ZONE`, `UPDATE ... WHERE versao = x`, criação de papel e `AWS IAM GRANT`/`REVOKE`.

## Alternativas consideradas

- **Aurora PostgreSQL Serverless v2.** PostgreSQL completo, mas roda em VPC, e a Lambda em VPC precisaria de endpoints ou NAT, que cobram por hora, para chegar ao SQS e ao SSM.
- **RDS PostgreSQL.** A franquia gratuita não é permanente, e o banco também fica em VPC.
- **Banco PostgreSQL de terceiros (Neon, Supabase).** Funciona, mas tira o banco do IAM da AWS e põe um segredo de conexão no lugar.
- **DynamoDB.** Custo zero real, mas sem SQL relacional: perderia EF Core, Dapper e as consultas do dashboard.
- **Usar o adapter oficial do DSQL.** Obrigaria a voltar o projeto inteiro para EF Core 9.

## Consequências

- O CI e os testes de integração rodam contra PostgreSQL, não contra o DSQL. A diferença é contida por três barreiras: o transformador recusa DDL incompatível já no deploy, a lista da SPEC §14 foi conferida no cluster, e o deploy roda migrations, seed e um smoke test (`/health/ready`) contra o DSQL de verdade a cada publicação.
- Toda escrita precisa tolerar conflito de OCC no commit: retry no EF, no numerador e no migrator; o que persiste além das tentativas vira `Concorrencia.Conflito` (409).
- Índice criado com `ASYNC` só é usado quando o job termina. O migrator espera, mas uma consulta nova não deve depender de um índice criado no mesmo deploy.
- Números de apólice e sinistro podem ter lacunas (uma emissão recusada depois de numerar), o que a SPEC aceita.
- Sem senha de banco em lugar nenhum: o acesso é decidido no IAM. As Lambdas só têm `dsql:DbConnect`, que autentica o papel `apolice_app`; só a role de deploy tem `dsql:DbConnectAdmin`, usado pelo `migrate` e pelo `seed`.
