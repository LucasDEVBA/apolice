# Criados antes das funções: se a Lambda criasse o grupo sozinha, ele nasceria sem retenção
# (logs eternos e cobrados) e a role precisaria de logs:CreateLogGroup.
resource "aws_cloudwatch_log_group" "lambda" {
  for_each = local.funcoes

  name              = "/aws/lambda/${each.value}"
  retention_in_days = local.log_retencao_dias
}
