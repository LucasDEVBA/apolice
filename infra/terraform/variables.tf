variable "aws_region" {
  description = "Região AWS de toda a infraestrutura."
  type        = string
  default     = "us-east-1"

  validation {
    condition     = can(regex("^[a-z]{2}(-[a-z]+)+-[0-9]$", var.aws_region))
    error_message = "aws_region deve ser um código de região AWS válido (ex.: us-east-1)."
  }
}

variable "project" {
  description = "Prefixo de nomes (funções, roles, filas, parâmetros SSM)."
  type        = string
  default     = "apolice"

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]{1,20}$", var.project))
    error_message = "project deve ter 2 a 21 caracteres minúsculos, dígitos ou hífen, começando por letra."
  }
}

variable "environment" {
  description = "Nome do ambiente; compõe o prefixo SSM (/<project>/<environment>) e as tags."
  type        = string
  default     = "prod"

  validation {
    condition     = can(regex("^[a-z][a-z0-9]{1,15}$", var.environment))
    error_message = "environment deve ter 2 a 16 caracteres minúsculos ou dígitos, começando por letra."
  }
}

variable "api_zip_path" {
  description = "Caminho do pacote .zip da API (saída de `dotnet publish` de Apolice.Api), relativo a infra/terraform."
  type        = string
  default     = "../../artifacts/api.zip"

  validation {
    condition     = endswith(var.api_zip_path, ".zip")
    error_message = "api_zip_path deve apontar para um arquivo .zip."
  }
}

variable "worker_zip_path" {
  description = "Caminho do pacote .zip do worker (Apolice.Worker), compartilhado pelo consumidor SQS e pelo relay do outbox."
  type        = string
  default     = "../../artifacts/worker.zip"

  validation {
    condition     = endswith(var.worker_zip_path, ".zip")
    error_message = "worker_zip_path deve apontar para um arquivo .zip."
  }
}

variable "github_repository" {
  description = "Repositório (owner/nome) autorizado a assumir a role de deploy via OIDC."
  type        = string
  default     = "LucasDEVBA/apolice"

  validation {
    condition     = can(regex("^[A-Za-z0-9-]+/[A-Za-z0-9._-]+$", var.github_repository))
    error_message = "github_repository deve estar no formato owner/repositorio."
  }
}

variable "create_github_oidc_provider" {
  description = "Cria o OIDC provider do GitHub. Use false se a conta já tiver um (só pode existir um por URL)."
  type        = bool
  default     = true
}

variable "api_reserved_concurrency" {
  description = "Teto de execuções simultâneas da API (reserved concurrency). null = sem reserva; 0 = API desligada (freio de emergência). Valores >= 1 exigem cota de conta que deixe 100 livres."
  type        = number
  default     = null

  validation {
    condition     = var.api_reserved_concurrency == null ? true : var.api_reserved_concurrency >= 0 && floor(var.api_reserved_concurrency) == var.api_reserved_concurrency
    error_message = "api_reserved_concurrency deve ser null ou um inteiro >= 0."
  }
}

variable "alert_email" {
  description = "E-mail que recebe os alertas do orçamento mensal e da DLQ (a assinatura SNS precisa ser confirmada pelo link enviado pela AWS)."
  type        = string
  default     = "lucasdevba@gmail.com"

  validation {
    condition     = can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", var.alert_email))
    error_message = "alert_email deve ser um e-mail válido."
  }
}

variable "monthly_budget_usd" {
  description = "Limite do orçamento mensal da conta, em dólares."
  type        = number
  default     = 5

  validation {
    condition     = var.monthly_budget_usd > 0
    error_message = "monthly_budget_usd deve ser maior que zero."
  }
}
