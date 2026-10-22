using Apolice.Domain.Produtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apolice.Infrastructure.Persistence.Configurations;

internal sealed class ProdutoConfiguration : IEntityTypeConfiguration<Produto>
{
    internal const string ChaveProduto = "ProdutoCodigo";

    public void Configure(EntityTypeBuilder<Produto> builder)
    {
        builder.ToTable("produtos");
        builder.HasKey(p => p.Id);

        // O código é a identidade do agregado (Produto.Codigo => Id).
        builder.Property(p => p.Id).HasColumnName("codigo").HasMaxLength(Tamanhos.CodigoProduto);
        builder.Property(p => p.Nome).HasMaxLength(Tamanhos.Nome).IsRequired();
        builder.Property(p => p.Descricao).HasMaxLength(Tamanhos.DescricaoProduto).IsRequired();
        builder.Property(p => p.AliquotaIof).HasPrecision(Tamanhos.PrecisaoTaxa, Tamanhos.EscalaTaxa);

        builder.HasMany(p => p.Coberturas).WithOne().HasForeignKey(ChaveProduto).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CoberturaProdutoConfiguration : IEntityTypeConfiguration<CoberturaProduto>
{
    public void Configure(EntityTypeBuilder<CoberturaProduto> builder)
    {
        builder.ToTable("coberturas_produto");
        builder.Property<string>(ProdutoConfiguration.ChaveProduto).HasMaxLength(Tamanhos.CodigoProduto);
        builder.HasKey(ProdutoConfiguration.ChaveProduto, nameof(CoberturaProduto.Codigo));
        builder.Property<int>(ApoliceDbContext.PropriedadeOrdem);
        builder.Property(c => c.Codigo).HasMaxLength(Tamanhos.CodigoCobertura);
        builder.Property(c => c.Nome).HasMaxLength(Tamanhos.Nome).IsRequired();
        builder.Property(c => c.Descricao).HasMaxLength(Tamanhos.DescricaoCobertura).IsRequired();
        builder.Property(c => c.TipoCalculo).IsRequired();
        builder.Property(c => c.Taxa).HasPrecision(Tamanhos.PrecisaoTaxa, Tamanhos.EscalaTaxa);
        builder.Ignore(c => c.CalculaPorTaxa);
    }
}
