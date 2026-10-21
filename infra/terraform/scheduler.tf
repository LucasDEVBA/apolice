# Regra agendada do EventBridge em vez do EventBridge Scheduler: para um cron fixo de 5 min as
# duas são gratuitas, mas a regra autoriza a invocação por resource policy presa ao ARN da regra
# (source_arn) e dispensa uma role IAM extra com iam:PassRole para a pipeline. Perder uma
# execução é inofensivo: a próxima, 5 min depois, despacha o mesmo outbox pendente.
resource "aws_cloudwatch_event_rule" "relay" {
  name                = local.funcoes.relay
  description         = "Dispara o relay do outbox a cada 5 minutos."
  schedule_expression = local.relay_agenda
}

resource "aws_cloudwatch_event_target" "relay" {
  rule      = aws_cloudwatch_event_rule.relay.name
  target_id = local.funcoes.relay
  arn       = aws_lambda_function.relay.arn
}

resource "aws_lambda_permission" "relay_agenda" {
  statement_id  = "PermitirRegraAgendadaEventBridge"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.relay.function_name
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.relay.arn
}
