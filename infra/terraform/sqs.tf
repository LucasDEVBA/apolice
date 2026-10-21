# DLQ com retenção maior que a fila principal: a SQS preserva o timestamp original da mensagem,
# então uma retenção igual ou menor faria a mensagem expirar logo ao chegar na DLQ.
resource "aws_sqs_queue" "eventos_dlq" {
  name                      = local.dlq_eventos_nome
  sqs_managed_sse_enabled   = true
  message_retention_seconds = local.dlq_retencao_segundos
}

resource "aws_sqs_queue" "eventos" {
  name                       = local.fila_eventos_nome
  sqs_managed_sse_enabled    = true
  visibility_timeout_seconds = local.fila_visibility_timeout_segundos
  message_retention_seconds  = local.fila_retencao_segundos

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.eventos_dlq.arn
    maxReceiveCount     = local.fila_max_recebimentos
  })
}

# Só a fila de eventos pode usar esta DLQ; recurso separado evita o ciclo fila <-> DLQ.
resource "aws_sqs_queue_redrive_allow_policy" "eventos_dlq" {
  queue_url = aws_sqs_queue.eventos_dlq.id

  redrive_allow_policy = jsonencode({
    redrivePermission = "byQueue"
    sourceQueueArns   = [aws_sqs_queue.eventos.arn]
  })
}
