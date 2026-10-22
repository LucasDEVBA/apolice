using Apolice.Domain.Comum;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apolice.Infrastructure.Persistence.Configurations;

// VOs do domínio como tipos complexos (colunas na própria tabela, prefixo do nome da propriedade:
// segurado_nome, segurado_cpf, vigencia_inicio...). Cpf e Email usam os conversores das convenções do contexto.
internal static class ComplexosComuns
{
    public static void Segurado(ComplexPropertyBuilder<Segurado> segurado)
    {
        segurado.IsRequired();
        segurado.Property(s => s.Nome).HasMaxLength(Tamanhos.Nome).IsRequired();
        segurado.Property(s => s.Cpf).IsRequired();
        segurado.Property(s => s.Email).IsRequired();
        segurado.Property(s => s.DataNascimento).IsRequired();
    }

    public static void Vigencia(ComplexPropertyBuilder<Vigencia> vigencia)
    {
        vigencia.IsRequired();
        vigencia.Property(v => v.Inicio).IsRequired();
        vigencia.Property(v => v.Fim).IsRequired();
        vigencia.Ignore(v => v.DiasTotais);
    }
}
