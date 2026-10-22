using Apolice.Domain.Apolices;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apolice.Infrastructure.Persistence.Configurations;

internal sealed class ApoliceConfiguration : IEntityTypeConfiguration<ApoliceSeguro>
{
    internal const string ChaveApolice = "ApoliceId";

    public void Configure(EntityTypeBuilder<ApoliceSeguro> builder)
    {
        builder.ToTable("apolices");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Numero).HasMaxLength(Tamanhos.NumeroDocumento).IsRequired();
        builder.Property(a => a.ProdutoCodigo).HasMaxLength(Tamanhos.CodigoProduto).IsRequired();
        builder.ComplexProperty(a => a.Segurado, ComplexosComuns.Segurado);
        builder.ComplexProperty(a => a.Vigencia, ComplexosComuns.Vigencia);
        builder.Property(a => a.DadosRisco).IsRequired();
        builder.Property(a => a.AliquotaIof).HasPrecision(Tamanhos.PrecisaoTaxa, Tamanhos.EscalaTaxa);
        builder.Property(a => a.Status).IsRequired();

        // Derivados das regras do domínio: nunca persistidos (o status efetivo depende de "hoje").
        builder.Ignore(a => a.AjustesEndosso);
        builder.Ignore(a => a.PremioLiquidoVigente);
        builder.Ignore(a => a.DataEmissao);
        builder.Ignore(a => a.DataCancelamento);

        builder.HasIndex(a => a.Numero).IsUnique();
        builder.HasIndex(a => a.CotacaoId).IsUnique();
        builder.HasIndex(a => new { a.EmitidaEm, a.Id });
        builder.HasIndex(a => new { a.CorretorId, a.EmitidaEm });

        builder.HasOne<Cotacao>().WithMany().HasForeignKey(a => a.CotacaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(a => a.CorretorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Produto>().WithMany().HasForeignKey(a => a.ProdutoCodigo).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(a => a.Coberturas).WithOne().HasForeignKey(ChaveApolice).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(a => a.Endossos).WithOne().HasForeignKey(e => e.ApoliceId).OnDelete(DeleteBehavior.Cascade);
    }
}

// Entidade filha com chave (apolice_id, codigo): o endosso altera LMI e prêmio no lugar (UPDATE, não delete + insert).
internal sealed class CoberturaApoliceConfiguration : IEntityTypeConfiguration<CoberturaApolice>
{
    public void Configure(EntityTypeBuilder<CoberturaApolice> builder)
    {
        builder.ToTable("apolice_coberturas");
        builder.Property<Guid>(ApoliceConfiguration.ChaveApolice);
        builder.HasKey(ApoliceConfiguration.ChaveApolice, nameof(CoberturaApolice.Codigo));
        builder.Property<int>(ApoliceDbContext.PropriedadeOrdem);
        builder.Property(c => c.Codigo).HasMaxLength(Tamanhos.CodigoCobertura);
        builder.Property(c => c.Nome).HasMaxLength(Tamanhos.Nome).IsRequired();
    }
}

internal sealed class EndossoConfiguration : IEntityTypeConfiguration<Endosso>
{
    public void Configure(EntityTypeBuilder<Endosso> builder)
    {
        builder.ToTable("endossos");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Tipo).IsRequired();
        builder.Property(e => e.CoberturaCodigo).HasMaxLength(Tamanhos.CodigoCobertura).IsRequired();
        builder.Property(e => e.CoberturaNome).HasMaxLength(Tamanhos.Nome).IsRequired();

        // Numeração 1..n por apólice: a unicidade é a última barreira se dois endossos escaparem do controle de versão.
        builder.HasIndex(e => new { e.ApoliceId, e.Numero }).IsUnique();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(e => e.CriadoPorId).OnDelete(DeleteBehavior.Restrict);
    }
}
