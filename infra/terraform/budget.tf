# Orçamento da conta inteira (não filtrado por tag): tags de custo só valem depois de ativadas
# no Billing e não pegariam gastos fora do Terraform, que são justamente os inesperados.
resource "aws_budgets_budget" "mensal" {
  name         = "${var.project}-mensal"
  budget_type  = "COST"
  limit_amount = tostring(var.monthly_budget_usd)
  limit_unit   = "USD"
  time_unit    = "MONTHLY"

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 80
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = [var.alert_email]
  }

  # Previsão avisa antes de estourar, enquanto ainda dá para reagir no mesmo mês.
  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 100
    threshold_type             = "PERCENTAGE"
    notification_type          = "FORECASTED"
    subscriber_email_addresses = [var.alert_email]
  }
}
