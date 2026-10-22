using Apolice.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apolice.Infrastructure.Persistence.Configurations;

internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Nome).HasMaxLength(Tamanhos.Nome).IsRequired();
        builder.Property(u => u.Email).IsRequired();
        builder.Property(u => u.SenhaHash).HasMaxLength(Tamanhos.SenhaHash).IsRequired();
        builder.Property(u => u.Papel).IsRequired();

        // E-mail já normalizado pelo VO: igualdade exata no índice único basta (sem lower()).
        builder.HasIndex(u => u.Email).IsUnique();
    }
}
