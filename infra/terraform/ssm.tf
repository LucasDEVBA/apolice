# Segredos gerados aqui para nunca passarem por mãos humanas, pipeline ou repositório. Efêmeros
# + value_wo (write-only): o valor vai direto para o SSM e nunca é gravado no state nem no plan,
# então ler o bucket de state (ReadOnlyAccess já lê) não entrega a chave do JWT.
ephemeral "random_password" "jwt_signing_key" {
  length = 64
  # Só caracteres seguros em URL/env/JSON: evita escapes quebrando a leitura da chave HS256.
  special          = true
  override_special = "-_"
}

ephemeral "random_password" "seed_senha_usuarios" {
  length           = 24
  special          = true
  override_special = "-_.@#%+="
  min_lower        = 1
  min_upper        = 1
  min_numeric      = 1
  min_special      = 1
}

locals {
  # O valor efêmero muda a cada execução, mas só é enviado quando a versão muda: incrementar
  # rotaciona o segredo. Pegadinha do provider: alterar outro atributo do parâmetro (descrição,
  # tier) sem incrementar a versão reenvia valor vazio e o PutParameter falha.
  # Rotacionar a senha do seed não troca a dos usuários já criados (o seed é idempotente).
  versao_jwt_signing_key     = 1
  versao_seed_senha_usuarios = 1
}

# Sem key_id: a chave gerenciada aws/ssm não tem custo mensal e a política das lambdas só a
# libera via SSM (kms:ViaService).
resource "aws_ssm_parameter" "jwt_signing_key" {
  name             = "${local.ssm_prefixo}/Jwt/SigningKey"
  description      = "Chave HS256 dos access tokens (Jwt:SigningKey)."
  type             = "SecureString"
  value_wo         = ephemeral.random_password.jwt_signing_key.result
  value_wo_version = local.versao_jwt_signing_key
}

# Fora do Ssm:Prefixo das funções (ver locals.tf): só o verbo seed, no runner, lê este valor.
resource "aws_ssm_parameter" "seed_senha_usuarios" {
  name             = "${local.ssm_prefixo_deploy}/Seed/SenhaUsuarios"
  description      = "Senha dos usuários de demonstração criados pelo seed (Seed:SenhaUsuarios)."
  type             = "SecureString"
  value_wo         = ephemeral.random_password.seed_senha_usuarios.result
  value_wo_version = local.versao_seed_senha_usuarios
}
