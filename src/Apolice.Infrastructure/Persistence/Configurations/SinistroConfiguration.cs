using Apolice.Domain.Apolices;
using Apolice.Domain.Sinistros;
using Apolice.Domain.Usuarios;
using Apolice.Infrastructure.Persistence.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apolice.Infrastructure.Persistence.Configurations;

internal sealed class SinistroConfiguration : IEntityTypeConfiguration<Sinistro>
{
    internal const string ChaveSinistro = "SinistroId";
    private const int TamanhoAlertas = 500;

    public void Configure(EntityTypeBuilder<Sinistro> builder)
    {
        builder.ToTable("sinistros");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Numero).HasMaxLength(Tamanhos.NumeroDocumento).IsRequired();
        builder.Property(s => s.ApoliceNumero).HasMaxLength(Tamanhos.NumeroDocumento).IsRequired();
        builder.Property(s => s.ProdutoCodigo).HasMaxLength(Tamanhos.CodigoProduto).IsRequired();
        builder.Property(s => s.SeguradoNome).HasMaxLength(Tamanhos.Nome).IsRequired();
        builder.Property(s => s.SeguradoEmail).IsRequired();
        builder.Property(s => s.CoberturaCodigo).HasMaxLength(Tamanhos.CodigoCobertura).IsRequired();
        builder.Property(s => s.CoberturaNome).HasMaxLength(Tamanhos.Nome).IsRequired();
        builder.Property(s => s.Descricao).HasMaxLength(Tamanhos.TextoLivre).IsRequired();
        builder.Property(s => s.MotivoNegativa).HasMaxLength(Tamanhos.TextoLivre);
        builder.Property(s => s.Status).IsRequired();

        // Triagem opcional como tipo complexo (triagem_score, triagem_alertas, triagem_triado_em): nula até o consumidor
        // do SinistroAberto rodar. Alertas em text com JSON — o DSQL não tem colunas de array.
        builder.ComplexProperty(s => s.Triagem, triagem =>
        {
            triagem.Property(t => t.Score);
            triagem.Property(t => t.TriadoEm);
            triagem.Property(t => t.Alertas)
                .HasConversion(new ListaTextoConverter(), new ListaTextoComparer())
                .HasMaxLength(TamanhoAlertas);
        });

        builder.HasIndex(s => s.Numero).IsUnique();
        builder.HasIndex(s => new { s.AbertoEm, s.Id });
        builder.HasIndex(s => new { s.CorretorId, s.AbertoEm });
        builder.HasIndex(s => s.ApoliceId);

        builder.HasOne<ApoliceSeguro>().WithMany().HasForeignKey(s => s.ApoliceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(s => s.CorretorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(s => s.Historico).WithOne().HasForeignKey(ChaveSinistro).OnDelete(DeleteBehavior.Cascade);
    }
}

// Histórico não tem chave natural (um status pode, em tese, repetir em versões futuras do fluxo): id próprio + ordem.
internal sealed class HistoricoSinistroConfiguration : IEntityTypeConfiguration<HistoricoSinistro>
{
    public void Configure(EntityTypeBuilder<HistoricoSinistro> builder)
    {
        builder.ToTable("sinistro_historico");
        builder.Property<Guid>(ChavesSombra.Id).HasValueGenerator<GeradorIdV7>();
        builder.HasKey(ChavesSombra.Id);
        builder.Property<Guid>(SinistroConfiguration.ChaveSinistro);
        builder.Property<int>(ApoliceDbContext.PropriedadeOrdem);
        builder.Property(h => h.Status).IsRequired();
        builder.Property(h => h.Observacao).HasMaxLength(Tamanhos.TextoLivre);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(h => h.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}
