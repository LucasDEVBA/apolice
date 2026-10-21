variable "aws_region" {
  description = "Região AWS do bucket de state (a mesma da infraestrutura principal)."
  type        = string
  default     = "us-east-1"

  validation {
    condition     = can(regex("^[a-z]{2}(-[a-z]+)+-[0-9]$", var.aws_region))
    error_message = "aws_region deve ser um código de região AWS válido (ex.: us-east-1)."
  }
}

variable "project" {
  description = "Prefixo dos recursos; precisa bater com o `project` da infraestrutura principal."
  type        = string
  default     = "apolice"

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]{1,20}$", var.project))
    error_message = "project deve ter 2 a 21 caracteres minúsculos, dígitos ou hífen, começando por letra."
  }
}

variable "noncurrent_version_days" {
  description = "Dias que uma versão antiga do state é mantida antes de expirar."
  type        = number
  default     = 90

  validation {
    condition     = var.noncurrent_version_days >= 1
    error_message = "noncurrent_version_days deve ser >= 1."
  }
}
