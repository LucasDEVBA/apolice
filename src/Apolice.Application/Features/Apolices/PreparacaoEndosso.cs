using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;

namespace Apolice.Application.Features.Apolices;

public sealed record EndossoPreparado(ApoliceOperavel Carregada, Produto Produto, SolicitacaoEndosso Solicitacao);

// Simulação e endosso efetivo passam pelos mesmos portões (flag, carteira, produto atual) antes do domínio.
public sealed class PreparacaoEndosso(IFeatureFlags flags, AcessoApolice acesso, IProdutoRepository produtos)
{
    public async Task<Result<EndossoPreparado>> PrepararAsync(
        Guid apoliceId,
        TipoEndosso tipo,
        string? coberturaCodigo,
        decimal? lmi,
        CancellationToken cancellationToken)
    {
        var habilitado = await flags.ExigirAsync(FeatureFlags.EndossoOnline, cancellationToken);
        if (habilitado.IsFailure)
        {
            return habilitado.Error;
        }

        var carregada = await acesso.CarregarParaOperacaoAsync(apoliceId, cancellationToken);
        if (carregada.IsFailure)
        {
            return carregada.Error;
        }

        // SPEC 4.4: retarifar com o produto ATUAL. Lido do cadastro, e não do cache, porque o L1 de outra instância
        // pode ficar com a taxa antiga até expirar; a simulação usa a mesma fonte para mostrar o que o endosso cobrará.
        var produto = await produtos.ObterAsync(carregada.Value.Apolice.ProdutoCodigo, cancellationToken);
        if (produto is null)
        {
            return ComumErros.NaoEncontrado(Entidades.Produto);
        }

        var codigo = CodigoCatalogo.Normalizar(coberturaCodigo) ?? string.Empty;
        return new EndossoPreparado(carregada.Value, produto, new SolicitacaoEndosso(tipo, codigo, lmi));
    }
}
