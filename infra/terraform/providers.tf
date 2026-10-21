provider "aws" {
  region = var.aws_region

  # Tags padrão permitem filtrar custo e inventário do projeto sem repetir em cada recurso.
  default_tags {
    tags = {
      project     = var.project
      environment = var.environment
      managed-by  = "terraform"
    }
  }
}
