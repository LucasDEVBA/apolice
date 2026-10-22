data "aws_caller_identity" "current" {}

data "aws_partition" "current" {}

# DescribeKey pelo alias materializa a chave gerenciada aws/ssm mesmo numa conta que nunca usou
# SecureString; assim as políticas apontam para o ARN real da chave em vez de key/*.
data "aws_kms_alias" "ssm" {
  name = "alias/aws/ssm"
}

locals {
  account_id = data.aws_caller_identity.current.account_id
  partition  = data.aws_partition.current.partition

  # Ssm:Prefixo da aplicação: "/apolice/prod/Jwt/SigningKey" vira a chave Jwt:SigningKey.
  ssm_prefixo        = "/${var.project}/${var.environment}"
  ssm_parametros_arn = "arn:${local.partition}:ssm:${var.aws_region}:${local.account_id}:parameter${local.ssm_prefixo}"

  # Fora do Ssm:Prefixo: as funções carregam tudo que está sob ele e não usam a senha do seed,
  # que só o verbo seed (no runner, com a role de deploy) precisa. "-deploy" não colide com o
  # prefixo de outro ambiente porque var.environment não aceita hífen.
  ssm_prefixo_deploy        = "${local.ssm_prefixo}-deploy"
  ssm_parametros_deploy_arn = "arn:${local.partition}:ssm:${var.aws_region}:${local.account_id}:parameter${local.ssm_prefixo_deploy}"

  funcoes = {
    api    = "${var.project}-api"
    worker = "${var.project}-worker"
    relay  = "${var.project}-outbox-relay"
  }

  fila_eventos_nome    = "${var.project}-eventos"
  dlq_eventos_nome     = "${var.project}-eventos-dlq"
  lambda_boundary_nome = "${var.project}-lambda-boundary"
  topico_alertas_nome  = "${var.project}-alertas"

  roles_lambda      = { for chave, nome in local.funcoes : chave => "${nome}-role" }
  roles_lambda_arns = [for nome in values(local.roles_lambda) : "arn:${local.partition}:iam::${local.account_id}:role/${nome}"]

  # O recurso aws_dsql_cluster não expõe o endpoint; o formato é documentado e estável.
  dsql_endpoint    = "${aws_dsql_cluster.this.identifier}.dsql.${var.aws_region}.on.aws"
  dsql_usuario_app = "apolice_app"

  lambda_runtime      = "dotnet10"
  lambda_arquiteturas = ["arm64"]
  log_retencao_dias   = 14

  api_memoria_mb          = 1024
  api_timeout_segundos    = 30
  worker_memoria_mb       = 512
  worker_timeout_segundos = 60
  relay_memoria_mb        = 512
  relay_timeout_segundos  = 60

  # A AWS recomenda visibility timeout >= 6x o timeout da função consumidora: o poller da Lambda
  # pode reprocessar um lote com throttling e a mensagem não pode reaparecer na fila no meio disso.
  fila_visibility_timeout_segundos = 6 * local.worker_timeout_segundos
  fila_retencao_segundos           = 4 * 24 * 60 * 60
  dlq_retencao_segundos            = 14 * 24 * 60 * 60
  fila_max_recebimentos            = 5
  worker_batch_size                = 10
  worker_concorrencia_maxima       = 2

  relay_agenda = "rate(5 minutes)"

  # Qualquer mensagem na DLQ já é falha definitiva (5 tentativas esgotadas), então o limite é
  # zero; a DLQ guarda 14 dias, o aviso não precisa ser de minuto.
  alarme_dlq_periodo_segundos = 5 * 60
  alarme_dlq_limite_mensagens = 0

  github_oidc_host     = "token.actions.githubusercontent.com"
  github_oidc_audience = "sts.amazonaws.com"

  # Mesma convenção do infra/bootstrap; a role de deploy precisa do nome para ler/gravar o state.
  state_bucket_name = "${var.project}-tfstate-${local.account_id}"

  ambiente_comum = {
    Database__Provider       = "Dsql"
    Database__DsqlEndpoint   = local.dsql_endpoint
    Database__DsqlUser       = local.dsql_usuario_app
    Messaging__Transport     = "Sqs"
    Messaging__Sqs__QueueUrl = aws_sqs_queue.eventos.url
  }

  # A API publica o outbox logo após o commit (latência baixa); o relay agendado cobre as falhas.
  # Sem Cors__Origens (SPEC §12/§13): o browser só fala com a Vercel, que reescreve para a
  # Function URL na mesma origem; liberar uma origem aqui seria superfície sem uso.
  # Ssm__Prefixo só na API: o único parâmetro sob ele é a chave de assinatura do JWT, que o consumidor e o relay não
  # usam. Com ela, uma dessas funções comprometida conseguiria forjar access tokens da API.
  ambiente_api = merge(local.ambiente_comum, {
    Ssm__Prefixo             = local.ssm_prefixo
    ASPNETCORE_ENVIRONMENT   = "Production"
    Outbox__DispatchImediato = "true"
    OTEL_SERVICE_NAME        = local.funcoes.api
  })

  # Consumidor e relay são o mesmo binário (Apolice.Worker); só o handler muda.
  ambiente_worker = merge(local.ambiente_comum, {
    DOTNET_ENVIRONMENT       = "Production"
    Outbox__DispatchImediato = "false"
    OTEL_SERVICE_NAME        = local.funcoes.worker
  })

  # Nome de serviço próprio: logs e traces do relay não se confundem com os do consumidor.
  ambiente_relay = merge(local.ambiente_worker, {
    OTEL_SERVICE_NAME = local.funcoes.relay
  })
}
