# Infraestrutura AWS (Terraform)

Lambda (.NET 10, arm64) com Function URL para a API, Lambda consumidora de SQS (+ DLQ com alarme
por e-mail), Lambda agendada do relay do outbox, Aurora DSQL com IAM auth, segredos em SSM
SecureString, orçamento mensal e role de deploy do GitHub Actions via OIDC. Ver `docs/SPEC.md`
seções 12 e 13.

```
infra/
  bootstrap/   bucket S3 do state remoto (state local, roda uma vez por conta)
  terraform/   todo o resto (backend S3 com lock nativo via use_lockfile)
```

## Ordem de execução (primeira vez, credencial de admin local)

```bash
# 1. Bucket do state
terraform -chdir=infra/bootstrap init
terraform -chdir=infra/bootstrap apply

# 2. Backend parcial
cp infra/terraform/backend.hcl.example infra/terraform/backend.hcl
#    troque <ACCOUNT_ID> por: terraform -chdir=infra/bootstrap output -raw state_bucket_name

# 3. Pacotes das Lambdas (padrão: artifacts/api.zip e artifacts/worker.zip, como no deploy.yml)
#    dotnet publish -c Release -r linux-arm64 --self-contained false + zip do diretório publicado

# 4. Infraestrutura
terraform -chdir=infra/terraform init -backend-config=backend.hcl
terraform -chdir=infra/terraform apply
```

Depois do apply:

- **Migrations/seed**: rodar `Apolice.Worker migrate` e `seed` com `Database__Provider=Dsql`,
  `Database__DsqlEndpoint=<output dsql_endpoint>`, `Database__DsqlUser=admin`,
  `Database__AppRole=apolice_app`, `Database__AppRoleArns__0..2=<api|worker|relay_role_arn>`,
  `Messaging__Transport=Sqs` e `Messaging__Sqs__QueueUrl=<output queue_url>` (sem a mensageria o
  Worker cai no padrão RabbitMq/localhost; o `seed` também precisa de `Seed__SenhaUsuarios`, lida
  do parâmetro do output `seed_senha_parametro`). Sem o `AWS IAM GRANT` do migrator as Lambdas não
  conectam no banco. O `deploy.yml` já faz isso a cada deploy.
- **GitHub, environment `prod` (obrigatório antes do primeiro deploy pela pipeline)**: em
  *Settings → Environments → prod*, **Deployment branches and tags: Selected branches and tags →
  `main`** (opcional: *Required reviewers*). O trust da role aceita qualquer job que declare
  `environment: prod` (`sub = repo:<repo>:environment:prod`), venha da branch ou do evento que
  for; sem essa regra, um job de outra branch ou de um PR do próprio repositório assumiria a role
  de produção. Variables do environment: `AWS_DEPLOY_ROLE_ARN` = output `deploy_role_arn`,
  `TF_STATE_BUCKET` = output do bootstrap, `AWS_REGION`.
  Reforço opcional: customizar o `sub` do repositório para incluir a ref
  (`PUT /repos/{owner}/{repo}/actions/oidc/customization/sub` com
  `include_claim_keys: [repo, context, ref]`) e trocar o valor aceito em `github_oidc.tf` para
  `repo:<repo>:environment:prod:ref:refs/heads/main`. Os dois andam juntos: um sem o outro
  quebra o deploy.
- **Deploy e rollback**: o `deploy.yml` só publica a ponta atual da `main` com CI verde nesse
  commit (pelo `workflow_run` do CI ou por disparo manual). Refazer o CI, ou o deploy, de um commit
  antigo é recusado: as migrations só avançam, e voltar o código por cima delas é arriscado. Para
  voltar versão, faça revert na `main`.
- **Alertas**: confirme o e-mail "AWS Notification - Subscription Confirmation" enviado para
  `alert_email`; até o clique, o alarme da DLQ dispara sem avisar ninguém.
- **Vercel**: `api_function_url` é o destino dos rewrites `/api/*`, `/scalar/*` e `/openapi/*`.
- **Senha dos usuários demo** (fora do `Ssm:Prefixo`, então as Lambdas não a carregam):
  `aws ssm get-parameter --name "$(terraform -chdir=infra/terraform output -raw seed_senha_parametro)" --with-decryption --query Parameter.Value --output text`

A pipeline aplica com os **defaults** de `variables.tf` (o `terraform.tfvars` é ignorado pelo
git): um valor que precise valer em produção muda no default, senão o próximo deploy o desfaz.

## O que só um admin aplica (a pipeline não consegue)

A role de deploy não altera a si mesma, a policy `apolice-lambda-boundary` nem o OIDC provider,
não cria nem apaga o cluster DSQL e não cria, apaga nem muda o trust das roles das funções (só
reescreve as policies inline delas, sempre sob o boundary). Mudanças em `github_oidc.tf`, no
boundary ou no trust/nome das roles (`iam.tf`), uma função nova ou algo que recrie o cluster
exigem `terraform apply` local com credencial de admin. É o que impede um workflow comprometido de
escalar privilégios ou de deixar uma role confiando numa conta externa (acesso que sobreviveria à
revogação do OIDC).

Onde a role de deploy usa `*` em `Resource` (ações sem suporte a restrição por recurso):
`lambda:CreateEventSourceMapping`/`Update`/`Delete` (limitadas pela condição `lambda:FunctionArn`
às funções `apolice-*`), `ssm:DescribeParameters`, `logs:DescribeLogGroups`, `kms:ListAliases` e,
só com `create_github_oidc_provider = false`, `iam:ListOpenIDConnectProviders`. Curingas parciais:
`event-source-mapping:*` (UUID gerado na criação) e `dsql:...:cluster/*` (identificador gerado
pela AWS; referenciar o recurso tornaria a policy "known after apply" a cada mudança pendente).

## Limitações conhecidas

- Os segredos (chave do JWT e senha do seed) são `ephemeral "random_password"` gravados por
  `value_wo`: não aparecem no state nem no plan, porque o state não protege segredo (qualquer
  principal com `s3:Get*` na conta, como a política `ReadOnlyAccess`, lê o bucket). Rotacionar =
  incrementar `versao_*` em `ssm.tf`; mudar outro atributo desses parâmetros também exige
  incrementar, senão o provider reenvia valor vazio e o apply falha.
- Upload direto do zip na Lambda aceita até 50 MB compactado; acima disso, publicar via S3.
- A Function URL é pública (auth `NONE`, como manda a SPEC): chamadas diretas pulam a Vercel, e o
  rate limiting da API só age depois da invocação, que já foi cobrada. O orçamento só avisa. Sem
  teto, um flood contínuo ocupa toda a concorrência da conta (o worker e o relay ficam sem vaga)
  e custa ≈ US$ 345/mês só de duração com o limite de 10 de conta nova (10 × 1 GB × 30 dias ×
  US$ 0,0000133334/GB-s em arm64), 100× isso com a cota padrão de 1.000. Mitigação:
  `api_reserved_concurrency` (ex.: `5`) assim que a cota deixar 100 livres além da reserva; em
  incidente, `0` desliga a API (reserva zero não tira nada da cota livre da conta). Um header
  secreto injetado pela Vercel ajudaria pouco: o `vercel.json` é público no repositório (exigiria
  middleware com segredo) e a invocação que o rejeitasse seria cobrada do mesmo jeito.
- O orçamento cobre a conta inteira, não só os recursos do projeto.
- Sem SnapStart (cobra cache e restore em .NET); o cold start da API fica por conta do runtime.
