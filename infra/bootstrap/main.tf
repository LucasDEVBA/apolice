provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      project     = var.project
      environment = "shared"
      managed-by  = "terraform"
    }
  }
}

data "aws_caller_identity" "current" {}

locals {
  # O id da conta no nome garante unicidade global sem sufixo aleatório e torna o nome previsível
  # para o backend.hcl e para a policy da role de deploy.
  state_bucket_name = "${var.project}-tfstate-${data.aws_caller_identity.current.account_id}"

  abort_multipart_days = 7
  tls_minimo           = "1.2"
}

resource "aws_s3_bucket" "tfstate" {
  bucket = local.state_bucket_name

  # Perder o state significa perder o mapeamento de toda a infraestrutura.
  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_s3_bucket_ownership_controls" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  rule {
    object_ownership = "BucketOwnerEnforced"
  }
}

resource "aws_s3_bucket_public_access_block" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# Versionamento permite recuperar um state corrompido por um apply interrompido.
resource "aws_s3_bucket_versioning" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  versioning_configuration {
    status = "Enabled"
  }
}

# SSE-S3 em vez de KMS com chave própria: mesma proteção em repouso para este caso, sem o custo
# fixo mensal de uma CMK (relevante num orçamento de US$ 5).
resource "aws_s3_bucket_server_side_encryption_configuration" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  # Cada apply gera uma versão nova do state; sem expiração o bucket cresce para sempre.
  rule {
    id     = "expirar-versoes-antigas"
    status = "Enabled"

    filter {}

    noncurrent_version_expiration {
      noncurrent_days = var.noncurrent_version_days
    }

    abort_incomplete_multipart_upload {
      days_after_initiation = local.abort_multipart_days
    }
  }

  depends_on = [aws_s3_bucket_versioning.tfstate]
}

data "aws_iam_policy_document" "tfstate" {
  statement {
    sid     = "NegarTransporteSemTls"
    effect  = "Deny"
    actions = ["s3:*"]
    resources = [
      aws_s3_bucket.tfstate.arn,
      "${aws_s3_bucket.tfstate.arn}/*",
    ]

    principals {
      type        = "*"
      identifiers = ["*"]
    }

    condition {
      test     = "Bool"
      variable = "aws:SecureTransport"
      values   = ["false"]
    }
  }

  statement {
    sid     = "NegarTlsAntigo"
    effect  = "Deny"
    actions = ["s3:*"]
    resources = [
      aws_s3_bucket.tfstate.arn,
      "${aws_s3_bucket.tfstate.arn}/*",
    ]

    principals {
      type        = "*"
      identifiers = ["*"]
    }

    condition {
      test     = "NumericLessThan"
      variable = "s3:TlsVersion"
      values   = [local.tls_minimo]
    }
  }
}

resource "aws_s3_bucket_policy" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id
  policy = data.aws_iam_policy_document.tfstate.json

  # A policy só pode entrar depois do bloqueio de acesso público para não haver janela aberta.
  depends_on = [aws_s3_bucket_public_access_block.tfstate]
}
