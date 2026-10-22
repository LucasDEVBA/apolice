resource "aws_lambda_function" "worker" {
  function_name = local.funcoes.worker
  description   = "Consumidor SQS dos eventos de integracao (handlers idempotentes)."
  role          = aws_iam_role.lambda["worker"].arn

  runtime       = local.lambda_runtime
  architectures = local.lambda_arquiteturas
  handler       = "Apolice.Worker::Apolice.Worker.Lambda.SqsFunction::HandleAsync"
  memory_size   = local.worker_memoria_mb
  timeout       = local.worker_timeout_segundos

  filename         = var.worker_zip_path
  source_code_hash = filebase64sha256(var.worker_zip_path)

  environment {
    variables = local.ambiente_worker
  }

  logging_config {
    log_format = "Text"
    log_group  = aws_cloudwatch_log_group.lambda["worker"].name
  }

  depends_on = [
    aws_cloudwatch_log_group.lambda,
    aws_iam_role_policy.lambda,
  ]
}

resource "aws_lambda_event_source_mapping" "worker_eventos" {
  event_source_arn = aws_sqs_queue.eventos.arn
  function_name    = aws_lambda_function.worker.arn
  batch_size       = local.worker_batch_size

  # Falha parcial: só as mensagens que falharam voltam para a fila (e, após 5, para a DLQ),
  # em vez de reprocessar o lote inteiro.
  function_response_types = ["ReportBatchItemFailures"]

  # Teto de concorrência no poller protege o DSQL e o limite de concorrência de contas novas
  # (10) sem usar reserved concurrency, que exige folga mínima de 100 na conta.
  scaling_config {
    maximum_concurrency = local.worker_concorrencia_maxima
  }

  # A criação valida se a role da função já pode ler a fila; sem isto o primeiro apply falha.
  depends_on = [aws_iam_role_policy.lambda]
}

resource "aws_lambda_function" "relay" {
  function_name = local.funcoes.relay
  description   = "Relay agendado do outbox: republica mensagens que o despacho imediato da API nao entregou."
  role          = aws_iam_role.lambda["relay"].arn

  runtime       = local.lambda_runtime
  architectures = local.lambda_arquiteturas
  handler       = "Apolice.Worker::Apolice.Worker.Lambda.OutboxRelayFunction::HandleAsync"
  memory_size   = local.relay_memoria_mb
  timeout       = local.relay_timeout_segundos

  filename         = var.worker_zip_path
  source_code_hash = filebase64sha256(var.worker_zip_path)

  environment {
    variables = local.ambiente_relay
  }

  logging_config {
    log_format = "Text"
    log_group  = aws_cloudwatch_log_group.lambda["relay"].name
  }

  depends_on = [
    aws_cloudwatch_log_group.lambda,
    aws_iam_role_policy.lambda,
  ]
}
