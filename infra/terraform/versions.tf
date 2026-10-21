terraform {
  # 1.10 trouxe o lock nativo do backend S3 (use_lockfile, sem DynamoDB) e 1.11 os atributos
  # write-only (value_wo) que mantêm os segredos fora do state.
  required_version = ">= 1.11"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.69"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.9"
    }
  }

  # Configuração parcial: bucket, key e região dependem da conta e entram via
  # `terraform init -backend-config=backend.hcl` (ver backend.hcl.example).
  backend "s3" {}
}
