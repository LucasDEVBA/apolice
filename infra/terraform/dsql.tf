# Aurora DSQL single-region: serverless de verdade (sem instância ociosa cobrando) e autenticação
# só por IAM, então não existe senha de banco para guardar ou rotacionar.
resource "aws_dsql_cluster" "this" {
  # Um destroy acidental apagaria todos os dados; desligar exige uma mudança explícita no código.
  deletion_protection_enabled = true

  tags = {
    Name = "${var.project}-${var.environment}"
  }
}
