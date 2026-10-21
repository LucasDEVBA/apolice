resource "aws_lambda_function" "api" {
  function_name = local.funcoes.api
  description   = "API HTTP do Apolice (ASP.NET Core Minimal APIs) exposta por Function URL."
  role          = aws_iam_role.lambda["api"].arn

  runtime       = local.lambda_runtime
  architectures = local.lambda_arquiteturas
  # Assembly executável: AddAWSLambdaHosting faz o bootstrap dentro do próprio Program.cs.
  handler     = "Apolice.Api"
  memory_size = local.api_memoria_mb
  timeout     = local.api_timeout_segundos

  # A Function URL é pública e o rate limiting da aplicação só age depois da invocação já cobrada:
  # este teto é o que limita o custo de um flood. Sem reserva por padrão porque conta nova
  # (limite 10) não consegue reservar nada acima de 0 (README).
  reserved_concurrent_executions = var.api_reserved_concurrency

  filename         = var.api_zip_path
  source_code_hash = filebase64sha256(var.api_zip_path)

  environment {
    variables = local.ambiente_api
  }

  logging_config {
    log_format = "Text"
    log_group  = aws_cloudwatch_log_group.lambda["api"].name
  }

  # A role precisa da policy antes do primeiro cold start (SSM/DSQL na inicialização), e o grupo
  # de logs precisa existir com retenção antes que a Lambda o crie implicitamente.
  depends_on = [
    aws_cloudwatch_log_group.lambda,
    aws_iam_role_policy.lambda,
  ]
}

# Desde 2025 uma Function URL pública exige DUAS declarações na resource policy:
# lambda:InvokeFunctionUrl (auth NONE) e lambda:InvokeFunction condicionada a
# lambda:InvokedViaFunctionUrl = true. Os statement_id são os mesmos que o provider usaria ao
# criar a URL: declarando-os aqui (antes da URL) elas ficam sob gestão do Terraform (drift,
# destroy) em vez de nascerem como efeito colateral que sobrevive ao destroy.
resource "aws_lambda_permission" "api_url_publica" {
  statement_id           = "FunctionURLAllowPublicAccess"
  action                 = "lambda:InvokeFunctionUrl"
  function_name          = aws_lambda_function.api.function_name
  principal              = "*"
  function_url_auth_type = "NONE"
}

resource "aws_lambda_permission" "api_url_invocacao" {
  statement_id             = "FunctionURLAllowInvokeAction"
  action                   = "lambda:InvokeFunction"
  function_name            = aws_lambda_function.api.function_name
  principal                = "*"
  invoked_via_function_url = true
}

# Sem bloco cors: o browser fala com a Vercel (rewrite same-origin), então não há chamada
# cross-origin para autorizar (SPEC §12).
resource "aws_lambda_function_url" "api" {
  function_name      = aws_lambda_function.api.function_name
  authorization_type = "NONE"

  depends_on = [
    aws_lambda_permission.api_url_publica,
    aws_lambda_permission.api_url_invocacao,
  ]
}
