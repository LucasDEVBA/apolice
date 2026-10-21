# Sem kms_master_key_id: o CloudWatch não consegue publicar em tópico cifrado com a chave
# gerenciada aws/sns, e uma CMK própria custaria US$ 1/mês fixo (20% do orçamento). A
# notificação só carrega nome do alarme e contagem, nada sensível.
resource "aws_sns_topic" "alertas" {
  name = local.topico_alertas_nome
}

# A AWS manda um e-mail de confirmação; até o clique a assinatura fica pendente e nada chega.
resource "aws_sns_topic_subscription" "alertas_email" {
  topic_arn = aws_sns_topic.alertas.arn
  protocol  = "email"
  endpoint  = var.alert_email
}

# Sem este alarme uma mensagem que esgota as tentativas some em silêncio quando a DLQ expira
# (14 dias), e com ela a notificação ou a triagem. Alarme standard e e-mail via SNS cabem no
# free tier: custo parado zero.
resource "aws_cloudwatch_metric_alarm" "dlq_eventos" {
  alarm_name        = "${local.dlq_eventos_nome}-com-mensagens"
  alarm_description = "Há mensagens na DLQ ${local.dlq_eventos_nome}: eventos que falharam ${local.fila_max_recebimentos} vezes. Investigar e reprocessar (redrive) antes de expirarem."

  namespace   = "AWS/SQS"
  metric_name = "ApproximateNumberOfMessagesVisible"
  dimensions = {
    QueueName = aws_sqs_queue.eventos_dlq.name
  }
  statistic           = "Maximum"
  period              = local.alarme_dlq_periodo_segundos
  evaluation_periods  = 1
  comparison_operator = "GreaterThanThreshold"
  threshold           = local.alarme_dlq_limite_mensagens

  # A SQS para de publicar métricas de fila vazia e ociosa; ausência de dado é DLQ vazia.
  treat_missing_data = "notBreaching"

  # O aviso de volta ao OK confirma que o redrive esvaziou a DLQ.
  alarm_actions = [aws_sns_topic.alertas.arn]
  ok_actions    = [aws_sns_topic.alertas.arn]
}
