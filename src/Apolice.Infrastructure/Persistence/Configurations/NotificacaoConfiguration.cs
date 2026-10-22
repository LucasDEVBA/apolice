using Apolice.Domain.Notificacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apolice.Infrastructure.Persistence.Configurations;

internal sealed class NotificacaoConfiguration : IEntityTypeConfiguration<Notificacao>
{
    public void Configure(EntityTypeBuilder<Notificacao> builder)
    {
        builder.ToTable("notificacoes");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Tipo).IsRequired();
        builder.Property(n => n.Destinatario).IsRequired();
        builder.Property(n => n.Assunto).HasMaxLength(Tamanhos.AssuntoNotificacao).IsRequired();
        builder.Property(n => n.Corpo).HasMaxLength(Tamanhos.CorpoNotificacao).IsRequired();

        // A referência é polimórfica (apólice ou sinistro): sem FK, só o índice da busca do detalhe.
        builder.HasIndex(n => n.ReferenciaId);
    }
}
