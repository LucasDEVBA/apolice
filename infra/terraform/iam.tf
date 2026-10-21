locals {
  # Só quem publica recebe SendMessage; só o consumidor lê, apaga e estende visibilidade.
  acoes_sqs = {
    api = ["sqs:SendMessage"]
    worker = [
      "sqs:ReceiveMessage",
      "sqs:DeleteMessage",
      "sqs:GetQueueAttributes",
      "sqs:ChangeMessageVisibility",
    ]
    relay = ["sqs:SendMessage"]
  }
}

data "aws_iam_policy_document" "lambda_assume" {
  statement {
    sid     = "ServicoLambda"
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

# Permissions boundary das roles das funções. A role de deploy do GitHub só consegue alterar as
# policies inline de roles que carreguem este boundary, então um workflow comprometido não escala
# para além disto, mesmo escrevendo uma policy inline "*:*". Alterar o boundary, criar/apagar as
# roles ou mudar o trust delas exige apply local de admin.
data "aws_iam_policy_document" "lambda_boundary" {
  statement {
    sid       = "LogsDasFuncoes"
    actions   = ["logs:CreateLogStream", "logs:PutLogEvents"]
    resources = ["arn:${local.partition}:logs:${var.aws_region}:${local.account_id}:log-group:/aws/lambda/${var.project}-*:*"]
  }

  statement {
    sid     = "ParametrosDoProjeto"
    actions = ["ssm:GetParameter", "ssm:GetParameters", "ssm:GetParametersByPath"]
    resources = [
      "arn:${local.partition}:ssm:${var.aws_region}:${local.account_id}:parameter/${var.project}",
      "arn:${local.partition}:ssm:${var.aws_region}:${local.account_id}:parameter/${var.project}/*",
    ]
  }

  statement {
    sid       = "DecifrarViaSsm"
    actions   = ["kms:Decrypt"]
    resources = [data.aws_kms_alias.ssm.target_key_arn]

    condition {
      test     = "StringEquals"
      variable = "kms:ViaService"
      values   = ["ssm.${var.aws_region}.amazonaws.com"]
    }
  }

  # Wildcard de cluster no boundary para não precisar de apply de admin se o cluster mudar;
  # o teto continua sendo DbConnect (papel de aplicação), nunca DbConnectAdmin.
  statement {
    sid       = "ConectarDsqlComoAplicacao"
    actions   = ["dsql:DbConnect"]
    resources = ["arn:${local.partition}:dsql:${var.aws_region}:${local.account_id}:cluster/*"]
  }

  statement {
    sid       = "FilasDoProjeto"
    actions   = distinct(flatten(values(local.acoes_sqs)))
    resources = ["arn:${local.partition}:sqs:${var.aws_region}:${local.account_id}:${var.project}-*"]
  }
}

resource "aws_iam_policy" "lambda_boundary" {
  name        = local.lambda_boundary_nome
  description = "Teto de permissões das roles das funções Lambda do ${var.project}."
  policy      = data.aws_iam_policy_document.lambda_boundary.json
}

# Uma role por função: o comprometimento de uma não dá à outra permissões que ela não tem
# (ex.: a API não consegue consumir/apagar mensagens da fila).
resource "aws_iam_role" "lambda" {
  for_each = local.funcoes

  name                 = local.roles_lambda[each.key]
  description          = "Role de execução da Lambda ${each.value}."
  assume_role_policy   = data.aws_iam_policy_document.lambda_assume.json
  permissions_boundary = aws_iam_policy.lambda_boundary.arn
}

data "aws_iam_policy_document" "lambda" {
  for_each = local.funcoes

  # Sem logs:CreateLogGroup: o grupo já existe (logs.tf) e só este grupo é gravável.
  statement {
    sid       = "LogsDoProprioGrupo"
    actions   = ["logs:CreateLogStream", "logs:PutLogEvents"]
    resources = ["${aws_cloudwatch_log_group.lambda[each.key].arn}:*"]
  }

  # GetParametersByPath é autorizado no ARN do próprio caminho; Get(s) nos parâmetros abaixo dele.
  statement {
    sid       = "ParametrosDoAmbiente"
    actions   = ["ssm:GetParametersByPath", "ssm:GetParameter", "ssm:GetParameters"]
    resources = [local.ssm_parametros_arn, "${local.ssm_parametros_arn}/*"]
  }

  statement {
    sid       = "DecifrarSomenteViaSsm"
    actions   = ["kms:Decrypt"]
    resources = [data.aws_kms_alias.ssm.target_key_arn]

    condition {
      test     = "StringEquals"
      variable = "kms:ViaService"
      values   = ["ssm.${var.aws_region}.amazonaws.com"]
    }
  }

  # DbConnect (e não DbConnectAdmin): o token só autentica papéis mapeados por AWS IAM GRANT,
  # ou seja, apolice_app com os GRANTs mínimos dados pelo migrator.
  statement {
    sid       = "ConectarDsqlComoAplicacao"
    actions   = ["dsql:DbConnect"]
    resources = [aws_dsql_cluster.this.arn]
  }

  statement {
    sid       = "FilaDeEventos"
    actions   = local.acoes_sqs[each.key]
    resources = [aws_sqs_queue.eventos.arn]
  }
}

resource "aws_iam_role_policy" "lambda" {
  for_each = local.funcoes

  name   = "${each.value}-permissoes"
  role   = aws_iam_role.lambda[each.key].id
  policy = data.aws_iam_policy_document.lambda[each.key].json
}
