terraform {
  required_version = ">= 1.10"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.69"
    }
  }

  # State local de propósito: este módulo cria o bucket que guarda o state remoto do resto da
  # infraestrutura (problema do ovo e da galinha). Ele quase nunca muda; se o state local se
  # perder, basta `terraform import aws_s3_bucket.tfstate apolice-tfstate-<conta>`.
}
