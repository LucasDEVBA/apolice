using Apolice.Application.Abstractions.Services;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Infrastructure.Persistence;
using Apolice.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Apolice.Infrastructure.Services;

// HybridCache (L1 em memória + L2 Redis opcional) com um RETRATO imutável e serializável do catálogo; cada chamada
// reconstrói agregados novos pelas fábricas do domínio, como exige a porta (instâncias exclusivas do chamador).
// A carga usa um escopo próprio: com a proteção contra estouro do HybridCache, uma única carga atende vários chamadores
// ao mesmo tempo, e ela não pode depender do DbContext de quem chegou primeiro (que pode terminar antes).
internal sealed class CatalogoProdutosCache(HybridCache cache, IServiceScopeFactory escopos) : ICatalogoProdutos
{
    internal const string Chave = "produtos:v1";
    internal const string Tag = "produtos";
    private static readonly TimeSpan Expiracao = TimeSpan.FromMinutes(10);
    private static readonly string[] Tags = [Tag];
    private static readonly HybridCacheEntryOptions Opcoes = new() { Expiration = Expiracao, LocalCacheExpiration = Expiracao };

    public async Task<IReadOnlyList<Produto>> ListarAsync(CancellationToken cancellationToken)
    {
        var retrato = await RetratoAsync(cancellationToken);
        return [.. retrato.Select(Reconstituir)];
    }

    public async Task<Produto?> ObterAsync(string codigo, CancellationToken cancellationToken)
    {
        var retrato = await RetratoAsync(cancellationToken);
        return retrato.FirstOrDefault(p => p.Codigo == codigo) is { } produto ? Reconstituir(produto) : null;
    }

    // Tag e chave: a tag cobre outras chaves que surgirem (ex.: v2 durante um deploy); a chave limpa o L2 de imediato.
    public async Task InvalidarAsync(CancellationToken cancellationToken)
    {
        await cache.RemoveByTagAsync(Tag, cancellationToken);
        await cache.RemoveAsync(Chave, cancellationToken);
    }

    private async Task<IReadOnlyList<ProdutoCacheado>> RetratoAsync(CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(Chave, escopos, static async (fabrica, ct) => await CarregarAsync(fabrica, ct), Opcoes, Tags, cancellationToken);

    private static async Task<ProdutoCacheado[]> CarregarAsync(IServiceScopeFactory escopos, CancellationToken cancellationToken)
    {
        await using var escopo = escopos.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<ApoliceDbContext>();
        var produtos = await db.Produtos.AsNoTracking().ComCoberturas().OrderBy(p => p.Id).ToListAsync(cancellationToken);
        return [.. produtos.Select(ProdutoCacheado.De)];
    }

    // Reconstrução pelas fábricas: um retrato corrompido (no L2 compartilhado) falha alto em vez de virar catálogo errado.
    private static Produto Reconstituir(ProdutoCacheado retrato)
    {
        var coberturas = retrato.Coberturas.Select(ReconstituirCobertura).ToList();
        var produto = Produto.Criar(retrato.Codigo, retrato.Nome, retrato.Descricao, retrato.AliquotaIof, coberturas);
        return produto.IsSuccess ? produto.Value : throw new DadoPersistidoInvalidoException($"Produto {retrato.Codigo} em cache inválido: {produto.Error.Codigo}.");
    }

    private static CoberturaProduto ReconstituirCobertura(CoberturaCacheada c)
    {
        var cobertura = c.TipoCalculo == TipoCalculo.PremioFixo
            ? CoberturaProduto.ComPremioFixo(c.Codigo, c.Nome, c.Descricao, c.Obrigatoria, c.PremioFixo ?? 0m, c.LmiFixo ?? 0m, c.PermiteSinistro)
            : CoberturaProduto.ComTaxa(c.Codigo, c.Nome, c.Descricao, c.Obrigatoria, c.TipoCalculo, c.Taxa, c.LmiMinimo, c.LmiMaximo, c.PermiteSinistro);
        return cobertura.IsSuccess ? cobertura.Value : throw new DadoPersistidoInvalidoException($"Cobertura {c.Codigo} em cache inválida: {cobertura.Error.Codigo}.");
    }
}

// Retrato serializável (System.Text.Json no L2): só dados, nenhuma regra.
internal sealed record ProdutoCacheado(string Codigo, string Nome, string Descricao, decimal AliquotaIof, IReadOnlyList<CoberturaCacheada> Coberturas)
{
    public static ProdutoCacheado De(Produto produto) =>
        new(produto.Codigo, produto.Nome, produto.Descricao, produto.AliquotaIof, [.. produto.Coberturas.Select(CoberturaCacheada.De)]);
}

internal sealed record CoberturaCacheada(
    string Codigo,
    string Nome,
    string Descricao,
    bool Obrigatoria,
    TipoCalculo TipoCalculo,
    decimal Taxa,
    decimal? PremioFixo,
    decimal LmiMinimo,
    decimal LmiMaximo,
    decimal? LmiFixo,
    bool PermiteSinistro)
{
    public static CoberturaCacheada De(CoberturaProduto c) =>
        new(c.Codigo, c.Nome, c.Descricao, c.Obrigatoria, c.TipoCalculo, c.Taxa, c.PremioFixo, c.LmiMinimo, c.LmiMaximo, c.LmiFixo, c.PermiteSinistro);
}
