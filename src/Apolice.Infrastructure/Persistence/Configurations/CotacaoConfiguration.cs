using Apolice.Domain.Cotacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;
using Apolice.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apolice.Infrastructure.Persistence.Configurations;

internal sealed class CotacaoConfiguration : IEntityTypeConfiguration<Cotacao>
{
    internal const string ChaveCotacao = "CotacaoId";

    public void Configure(EntityTypeBuilder<Cotacao> builder)
    {
        builder.ToTable("cotacoes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ProdutoCodigo).HasMaxLength(Tamanhos.CodigoProduto).IsRequired();
        builder.ComplexProperty(c => c.Segurado, ComplexosComuns.Segurado);
        builder.Property(c => c.DadosRisco).IsRequired();
        builder.Property(c => c.AliquotaIof).HasPrecision(Tamanhos.PrecisaoTaxa, Tamanhos.EscalaTaxa);
        builder.Property(c => c.Status).IsRequired();

        builder.HasOne<Usuario>().WithMany().HasForeignKey(c => c.CorretorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Produto>().WithMany().HasForeignKey(c => c.ProdutoCodigo).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(c => c.Itens).WithOne().HasForeignKey(ChaveCotacao).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Fatores).WithOne().HasForeignKey(ChaveCotacao).OnDelete(DeleteBehavior.Cascade);

        // Keyset da listagem: (criada_em, id) DESC, com e sem filtro de corretor.
        builder.HasIndex(c => new { c.CriadaEm, c.Id });
        builder.HasIndex(c => new { c.CorretorId, c.CriadaEm });
    }
}

internal sealed class ItemCotacaoConfiguration : IEntityTypeConfiguration<ItemCotacao>
{
    public void Configure(EntityTypeBuilder<ItemCotacao> builder)
    {
        builder.ToTable("cotacao_itens");
        builder.Property<Guid>(CotacaoConfiguration.ChaveCotacao);
        builder.HasKey(CotacaoConfiguration.ChaveCotacao, nameof(ItemCotacao.Codigo));
        builder.Property<int>(ApoliceDbContext.PropriedadeOrdem);
        builder.Property(i => i.Codigo).HasMaxLength(Tamanhos.CodigoCobertura);
        builder.Property(i => i.Nome).HasMaxLength(Tamanhos.Nome).IsRequired();
    }
}

// Fator não tem chave natural (a descrição é texto livre do tarifador): id próprio, gerado aqui, sem sequence.
internal sealed class FatorAplicadoConfiguration : IEntityTypeConfiguration<FatorAplicado>
{
    public void Configure(EntityTypeBuilder<FatorAplicado> builder)
    {
        builder.ToTable("cotacao_fatores");
        builder.Property<Guid>(ChavesSombra.Id).HasValueGenerator<GeradorIdV7>();
        builder.HasKey(ChavesSombra.Id);
        builder.Property<Guid>(CotacaoConfiguration.ChaveCotacao);
        builder.Property<int>(ApoliceDbContext.PropriedadeOrdem);
        builder.Property(f => f.Descricao).HasMaxLength(Tamanhos.DescricaoFator).IsRequired();
        builder.Property(f => f.Fator).HasPrecision(Tamanhos.PrecisaoTaxa, Tamanhos.EscalaTaxa);
    }
}
