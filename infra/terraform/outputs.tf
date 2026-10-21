output "api_function_url" {
  description = "URL pública da API (destino dos rewrites /api/*, /scalar/*, /openapi/* no vercel.json)."
  value       = aws_lambda_function_url.api.function_url
}

output "api_function_name" {
  description = "Nome da Lambda da API (para update-function-code no CD)."
  value       = aws_lambda_function.api.function_name
}

output "worker_function_name" {
  description = "Nome da Lambda consumidora da fila de eventos."
  value       = aws_lambda_function.worker.function_name
}

output "relay_function_name" {
  description = "Nome da Lambda agendada do relay do outbox."
  value       = aws_lambda_function.relay.function_name
}

output "dsql_endpoint" {
  description = "Endpoint do cluster DSQL (Database:DsqlEndpoint)."
  value       = local.dsql_endpoint
}

output "dsql_cluster_arn" {
  description = "ARN do cluster DSQL."
  value       = aws_dsql_cluster.this.arn
}

output "dsql_identifier" {
  description = "Identificador do cluster DSQL."
  value       = aws_dsql_cluster.this.identifier
}

output "queue_url" {
  description = "URL da fila de eventos (Messaging:Sqs:QueueUrl)."
  value       = aws_sqs_queue.eventos.url
}

output "dlq_url" {
  description = "URL da DLQ da fila de eventos."
  value       = aws_sqs_queue.eventos_dlq.url
}

output "seed_senha_parametro" {
  description = "Nome do parâmetro SSM com a senha dos usuários demo (lido pelo passo de seed do deploy.yml)."
  value       = aws_ssm_parameter.seed_senha_usuarios.name
}

output "alertas_topic_arn" {
  description = "Tópico SNS dos alarmes (a assinatura por e-mail precisa ser confirmada)."
  value       = aws_sns_topic.alertas.arn
}

output "deploy_role_arn" {
  description = "Role assumida pelo GitHub Actions via OIDC (secret/variable AWS_ROLE_ARN do workflow)."
  value       = aws_iam_role.github_deploy.arn
}

output "api_role_arn" {
  description = "Role da API; entra em Database:AppRoleArns do migrator (AWS IAM GRANT apolice_app)."
  value       = aws_iam_role.lambda["api"].arn
}

output "worker_role_arn" {
  description = "Role do consumidor; entra em Database:AppRoleArns do migrator."
  value       = aws_iam_role.lambda["worker"].arn
}

output "relay_role_arn" {
  description = "Role do relay; entra em Database:AppRoleArns do migrator."
  value       = aws_iam_role.lambda["relay"].arn
}
