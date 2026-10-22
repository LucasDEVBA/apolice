using Apolice.Domain.Apolices;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;
using Microsoft.EntityFrameworkCore;

namespace Apolice.Infrastructure.Persistence.Repositories;

// Forma única de carregar cada agregado completo (repositório com tracking e leitura do detalhe sem tracking): as
// coleções filhas voltam na ordem de inserção (coluna "ordem"), a mesma da lista do domínio quando foi gravado.
internal static class Carregamentos
{
    public static IQueryable<Produto> ComCoberturas(this IQueryable<Produto> produtos) =>
        produtos.Include(p => p.Coberturas.OrderBy(c => EF.Property<int>(c, ApoliceDbContext.PropriedadeOrdem)));

    public static IQueryable<Cotacao> Completas(this IQueryable<Cotacao> cotacoes) =>
        cotacoes
            .Include(c => c.Itens.OrderBy(i => EF.Property<int>(i, ApoliceDbContext.PropriedadeOrdem)))
            .Include(c => c.Fatores.OrderBy(f => EF.Property<int>(f, ApoliceDbContext.PropriedadeOrdem)))
            .AsSingleQuery();

    public static IQueryable<ApoliceSeguro> Completas(this IQueryable<ApoliceSeguro> apolices) =>
        apolices
            .Include(a => a.Coberturas.OrderBy(c => EF.Property<int>(c, ApoliceDbContext.PropriedadeOrdem)))
            .Include(a => a.Endossos.OrderBy(e => e.Numero))
            .AsSingleQuery();

    public static IQueryable<Sinistro> Completos(this IQueryable<Sinistro> sinistros) =>
        sinistros.Include(s => s.Historico.OrderBy(h => EF.Property<int>(h, ApoliceDbContext.PropriedadeOrdem)));
}
