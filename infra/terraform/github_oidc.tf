# Só pode existir um OIDC provider por URL na conta; se outro projeto já criou, reaproveita.
# Sem thumbprint_list: a AWS valida o emissor do GitHub pela própria biblioteca de CAs.
resource "aws_iam_openid_connect_provider" "github" {
  count = var.create_github_oidc_provider ? 1 : 0

  url            = "https://${local.github_oidc_host}"
  client_id_list = [local.github_oidc_audience]
}

data "aws_iam_openid_connect_provider" "github" {
  count = var.create_github_oidc_provider ? 0 : 1

  url = "https://${local.github_oidc_host}"
}

# ARNs montados a partir dos nomes, nunca lidos de recursos gerenciados: um data source que
# referencia recurso com mudança pendente (até uma tag) só é lido no apply, a policy vira
# "known after apply" e a pipeline tentaria reescrever a própria role, o que ela não pode.
locals {
  arn_iam                  = "arn:${local.partition}:iam::${local.account_id}"
  github_oidc_provider_arn = "${local.arn_iam}:oidc-provider/${local.github_oidc_host}"
  lambda_boundary_arn      = "${local.arn_iam}:policy/${local.lambda_boundary_nome}"

  arn_lambda       = "arn:${local.partition}:lambda:${var.aws_region}:${local.account_id}"
  arn_logs         = "arn:${local.partition}:logs:${var.aws_region}:${local.account_id}"
  arn_events       = "arn:${local.partition}:events:${var.aws_region}:${local.account_id}"
  arn_sqs          = "arn:${local.partition}:sqs:${var.aws_region}:${local.account_id}"
  arn_cloudwatch   = "arn:${local.partition}:cloudwatch:${var.aws_region}:${local.account_id}"
  arn_state_bucket = "arn:${local.partition}:s3:::${local.state_bucket_name}"

  topico_alertas_arn = "arn:${local.partition}:sns:${var.aws_region}:${local.account_id}:${local.topico_alertas_nome}"
}

# Trust só para jobs do environment "prod". Um job que declara environment recebe
# sub=...:environment:<nome> venha da branch ou do evento que for, então quem impede branch
# qualquer ou PR do próprio repositório é a regra do environment no GitHub ("Deployment branches
# and tags: Selected -> main"), obrigatória (README). Sem o valor ref:refs/heads/main de propósito:
# ele deixaria qualquer workflow da main sem environment (pull_request_target, workflow_run de
# outro workflow) assumir a role por fora das regras de proteção. Forks não recebem token OIDC.
data "aws_iam_policy_document" "github_assume" {
  statement {
    sid     = "GitHubActionsOidc"
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [local.github_oidc_provider_arn]
    }

    condition {
      test     = "StringEquals"
      variable = "${local.github_oidc_host}:aud"
      values   = [local.github_oidc_audience]
    }

    condition {
      test     = "StringEquals"
      variable = "${local.github_oidc_host}:sub"
      values   = ["repo:${var.github_repository}:environment:${var.environment}"]
    }
  }
}

resource "aws_iam_role" "github_deploy" {
  name               = "${var.project}-github-deploy"
  description        = "Assumida pelo GitHub Actions (OIDC) para aplicar o Terraform e rodar migrations."
  assume_role_policy = data.aws_iam_policy_document.github_assume.json

  # O ARN do provider é montado, então a ordem (e, com provider existente, a falha rápida se ele
  # não existir) vem daqui.
  depends_on = [
    aws_iam_openid_connect_provider.github,
    data.aws_iam_openid_connect_provider.github,
  ]
}

# Política de deploy: o suficiente para aplicar ESTE Terraform e rodar migrate/seed no DSQL.
# Ela propositalmente NÃO consegue alterar a si mesma, o boundary, o OIDC provider nem o ciclo de
# vida e o trust das roles das funções: mudanças nesses recursos exigem apply local com credencial
# de admin (evita escalada de privilégio e acesso persistente de fora da conta).
# Onde há "*" é porque a ação não suporta restrição por recurso (listagens/descrições).
data "aws_iam_policy_document" "github_deploy" {
  # --- State remoto (bucket do bootstrap, só o prefixo do projeto) ---
  statement {
    sid       = "StateListarBucket"
    actions   = ["s3:ListBucket"]
    resources = [local.arn_state_bucket]
  }

  statement {
    sid       = "StateObjetosELock"
    actions   = ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"]
    resources = ["${local.arn_state_bucket}/${var.project}/*"]
  }

  # --- Lambda ---
  statement {
    sid = "LambdaFuncoesDoProjeto"
    actions = [
      "lambda:CreateFunction",
      "lambda:DeleteFunction",
      "lambda:GetFunction",
      "lambda:GetFunctionConfiguration",
      "lambda:GetFunctionCodeSigningConfig",
      "lambda:GetFunctionConcurrency",
      "lambda:PutFunctionConcurrency",
      "lambda:DeleteFunctionConcurrency",
      "lambda:ListVersionsByFunction",
      "lambda:UpdateFunctionCode",
      "lambda:UpdateFunctionConfiguration",
      "lambda:GetPolicy",
      "lambda:AddPermission",
      "lambda:RemovePermission",
      "lambda:CreateFunctionUrlConfig",
      "lambda:GetFunctionUrlConfig",
      "lambda:UpdateFunctionUrlConfig",
      "lambda:DeleteFunctionUrlConfig",
      "lambda:TagResource",
      "lambda:UntagResource",
      "lambda:ListTags",
    ]
    resources = ["${local.arn_lambda}:function:${var.project}-*"]
  }

  # CreateEventSourceMapping não aceita ARN de recurso ("*"); a condição lambda:FunctionArn
  # restringe às funções do projeto.
  statement {
    sid = "LambdaMapeamentoDaFila"
    actions = [
      "lambda:CreateEventSourceMapping",
      "lambda:UpdateEventSourceMapping",
      "lambda:DeleteEventSourceMapping",
    ]
    resources = ["*"]

    condition {
      test     = "ArnLike"
      variable = "lambda:FunctionArn"
      values   = ["${local.arn_lambda}:function:${var.project}-*"]
    }
  }

  # O UUID do mapeamento só existe depois da criação, então o ARN não pode ser fixado.
  statement {
    sid = "LambdaMapeamentoLeituraETags"
    actions = [
      "lambda:GetEventSourceMapping",
      "lambda:TagResource",
      "lambda:UntagResource",
      "lambda:ListTags",
    ]
    resources = ["${local.arn_lambda}:event-source-mapping:*"]
  }

  # --- IAM: leitura de tudo que é do projeto (inclusive esta role, para o refresh do plan) ---
  statement {
    sid = "IamLeituraDoProjeto"
    actions = [
      "iam:GetRole",
      "iam:GetRolePolicy",
      "iam:ListRolePolicies",
      "iam:ListAttachedRolePolicies",
      "iam:ListRoleTags",
      "iam:ListInstanceProfilesForRole",
      "iam:GetPolicy",
      "iam:GetPolicyVersion",
      "iam:ListPolicyVersions",
      "iam:ListPolicyTags",
    ]
    resources = [
      "${local.arn_iam}:role/${var.project}-*",
      "${local.arn_iam}:policy/${var.project}-*",
    ]
  }

  # Escrita só nas policies inline das roles das funções e só se elas carregarem o boundary: a
  # própria role de deploy não tem boundary, logo não pode ganhar permissões novas pela pipeline.
  # Sem CreateRole/DeleteRole/UpdateAssumeRolePolicy: o trust (lambda.amazonaws.com) é fixo, e
  # com elas um workflow comprometido recriaria a role confiando numa conta externa, que leria os
  # segredos do SSM mesmo depois de o OIDC ser revogado (o boundary limita o que a role faz, não
  # quem a assume). Sem Attach/DetachRolePolicy: o código só usa policies inline.
  statement {
    sid = "IamRolesDasFuncoesComBoundary"
    actions = [
      "iam:PutRolePolicy",
      "iam:DeleteRolePolicy",
      "iam:PutRolePermissionsBoundary",
      "iam:UpdateRole",
      "iam:UpdateRoleDescription",
    ]
    resources = local.roles_lambda_arns

    condition {
      test     = "StringEquals"
      variable = "iam:PermissionsBoundary"
      values   = [local.lambda_boundary_arn]
    }
  }

  statement {
    sid       = "IamTagsDoProjeto"
    actions   = ["iam:TagRole", "iam:UntagRole", "iam:TagPolicy", "iam:UntagPolicy"]
    resources = ["${local.arn_iam}:role/${var.project}-*", "${local.arn_iam}:policy/${var.project}-*"]
  }

  statement {
    sid       = "IamPassarRolesSoParaLambda"
    actions   = ["iam:PassRole"]
    resources = local.roles_lambda_arns

    condition {
      test     = "StringEquals"
      variable = "iam:PassedToService"
      values   = ["lambda.amazonaws.com"]
    }
  }

  statement {
    sid = "IamOidcProviderLeituraETags"
    actions = [
      "iam:GetOpenIDConnectProvider",
      "iam:ListOpenIDConnectProviderTags",
      "iam:TagOpenIDConnectProvider",
      "iam:UntagOpenIDConnectProvider",
    ]
    resources = ["${local.arn_iam}:oidc-provider/${local.github_oidc_host}"]
  }

  # O data source procura o provider existente por URL, o que exige listar (sem ARN de recurso).
  dynamic "statement" {
    for_each = var.create_github_oidc_provider ? [] : [1]

    content {
      sid       = "IamOidcProviderListar"
      actions   = ["iam:ListOpenIDConnectProviders"]
      resources = ["*"]
    }
  }

  # --- SQS ---
  statement {
    sid = "SqsFilasDoProjeto"
    actions = [
      "sqs:CreateQueue",
      "sqs:DeleteQueue",
      "sqs:GetQueueAttributes",
      "sqs:SetQueueAttributes",
      "sqs:GetQueueUrl",
      "sqs:TagQueue",
      "sqs:UntagQueue",
      "sqs:ListQueueTags",
    ]
    resources = [
      "${local.arn_sqs}:${local.fila_eventos_nome}",
      "${local.arn_sqs}:${local.dlq_eventos_nome}",
    ]
  }

  # --- SSM: parâmetros das funções e o caminho só do deploy (senha do seed) ---
  statement {
    sid = "SsmParametrosDoAmbiente"
    actions = [
      "ssm:PutParameter",
      "ssm:GetParameter",
      "ssm:GetParameters",
      "ssm:GetParametersByPath",
      "ssm:DeleteParameter",
      "ssm:AddTagsToResource",
      "ssm:RemoveTagsFromResource",
      "ssm:ListTagsForResource",
    ]
    resources = [
      local.ssm_parametros_arn,
      "${local.ssm_parametros_arn}/*",
      "${local.ssm_parametros_deploy_arn}/*",
    ]
  }

  statement {
    sid       = "SsmDescreverParametros"
    actions   = ["ssm:DescribeParameters"]
    resources = ["*"]
  }

  # --- KMS: só a chave gerenciada aws/ssm e só por intermédio do SSM ---
  statement {
    sid       = "KmsChaveSsmViaSsm"
    actions   = ["kms:Encrypt", "kms:Decrypt"]
    resources = [data.aws_kms_alias.ssm.target_key_arn]

    condition {
      test     = "StringEquals"
      variable = "kms:ViaService"
      values   = ["ssm.${var.aws_region}.amazonaws.com"]
    }
  }

  statement {
    sid       = "KmsDescreverChaveSsm"
    actions   = ["kms:DescribeKey"]
    resources = [data.aws_kms_alias.ssm.target_key_arn]
  }

  statement {
    sid       = "KmsListarAliases"
    actions   = ["kms:ListAliases"]
    resources = ["*"]
  }

  # --- DSQL: sem Create/DeleteCluster (o banco nunca é recriado nem apagado pela pipeline);
  # DbConnectAdmin é para o verbo migrate (DDL + CREATE ROLE/AWS IAM GRANT do apolice_app).
  # cluster/* (conta + região) porque o identificador é gerado pela AWS e referenciar o recurso
  # cairia no problema de "known after apply" descrito no topo do arquivo.
  statement {
    sid = "DsqlClusterDoProjeto"
    actions = [
      "dsql:GetCluster",
      "dsql:GetVpcEndpointServiceName",
      "dsql:UpdateCluster",
      "dsql:TagResource",
      "dsql:UntagResource",
      "dsql:ListTagsForResource",
      "dsql:DbConnectAdmin",
    ]
    resources = ["arn:${local.partition}:dsql:${var.aws_region}:${local.account_id}:cluster/*"]
  }

  # --- CloudWatch Logs ---
  statement {
    sid = "LogsGruposDasFuncoes"
    actions = [
      "logs:CreateLogGroup",
      "logs:DeleteLogGroup",
      "logs:PutRetentionPolicy",
      "logs:DeleteRetentionPolicy",
      "logs:TagResource",
      "logs:UntagResource",
      "logs:ListTagsForResource",
      "logs:TagLogGroup",
      "logs:UntagLogGroup",
      "logs:ListTagsLogGroup",
    ]
    resources = [
      "${local.arn_logs}:log-group:/aws/lambda/${var.project}-*",
      "${local.arn_logs}:log-group:/aws/lambda/${var.project}-*:*",
    ]
  }

  statement {
    sid       = "LogsDescreverGrupos"
    actions   = ["logs:DescribeLogGroups"]
    resources = ["*"]
  }

  # --- EventBridge (regra agendada do relay) ---
  statement {
    sid = "EventsRegrasDoProjeto"
    actions = [
      "events:PutRule",
      "events:DeleteRule",
      "events:DescribeRule",
      "events:EnableRule",
      "events:DisableRule",
      "events:PutTargets",
      "events:RemoveTargets",
      "events:ListTargetsByRule",
      "events:TagResource",
      "events:UntagResource",
      "events:ListTagsForResource",
    ]
    resources = ["${local.arn_events}:rule/${var.project}-*"]
  }

  # --- Alarmes (CloudWatch) e tópico de alertas (SNS) ---
  # AlarmActions preso ao tópico do projeto: a pipeline não consegue criar alarme que dispare
  # outra coisa (Lambda, ações de EC2/SSM) nem que avise um tópico de fora.
  statement {
    sid       = "CloudWatchCriarAlarmesSoComOTopico"
    actions   = ["cloudwatch:PutMetricAlarm"]
    resources = ["${local.arn_cloudwatch}:alarm:${var.project}-*"]

    condition {
      test     = "ForAllValues:StringEquals"
      variable = "cloudwatch:AlarmActions"
      values   = [local.topico_alertas_arn]
    }
  }

  statement {
    sid = "CloudWatchAlarmesDoProjeto"
    actions = [
      "cloudwatch:DescribeAlarms",
      "cloudwatch:DeleteAlarms",
      "cloudwatch:TagResource",
      "cloudwatch:UntagResource",
      "cloudwatch:ListTagsForResource",
    ]
    resources = ["${local.arn_cloudwatch}:alarm:${var.project}-*"]
  }

  # As ações de assinatura são avaliadas no ARN da assinatura (<tópico>:<uuid>).
  statement {
    sid = "SnsTopicoDeAlertas"
    actions = [
      "sns:CreateTopic",
      "sns:DeleteTopic",
      "sns:GetTopicAttributes",
      "sns:SetTopicAttributes",
      "sns:ListSubscriptionsByTopic",
      "sns:GetSubscriptionAttributes",
      "sns:SetSubscriptionAttributes",
      "sns:Unsubscribe",
      "sns:TagResource",
      "sns:UntagResource",
      "sns:ListTagsForResource",
    ]
    resources = [local.topico_alertas_arn, "${local.topico_alertas_arn}:*"]
  }

  # Só assinatura por e-mail: impede plugar um endpoint HTTP/SQS/Lambda de terceiros no tópico.
  statement {
    sid       = "SnsAssinarSoPorEmail"
    actions   = ["sns:Subscribe"]
    resources = [local.topico_alertas_arn]

    condition {
      test     = "StringEquals"
      variable = "sns:Protocol"
      values   = ["email"]
    }
  }

  # --- Budgets (recurso global: o ARN não tem região) ---
  statement {
    sid = "BudgetsDoProjeto"
    actions = [
      "budgets:ViewBudget",
      "budgets:ModifyBudget",
      "budgets:TagResource",
      "budgets:UntagResource",
      "budgets:ListTagsForResource",
    ]
    resources = ["arn:${local.partition}:budgets::${local.account_id}:budget/${var.project}-*"]
  }
}

resource "aws_iam_role_policy" "github_deploy" {
  name   = "${var.project}-github-deploy-permissoes"
  role   = aws_iam_role.github_deploy.id
  policy = data.aws_iam_policy_document.github_deploy.json
}
