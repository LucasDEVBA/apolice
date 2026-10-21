using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;

namespace Apolice.Domain.Tarifacao;

public sealed class Tarifador : ITarifador
{
    private readonly Dictionary<string, IEstrategiaTarifacao> _estrategias;

    public Tarifador(IEnumerable<IEstrategiaTarifacao> estrategias)
    {
        ArgumentNullException.ThrowIfNull(estrategias);

        // ToDictionary lança com código repetido: duas estratégias para o mesmo produto é erro de composição.
        _estrategias = estrategias.ToDictionary(e => e.ProdutoCodigo, StringComparer.Ordinal);
    }

    public static Tarifador Padrao() =>
        new([new TarifacaoAuto(), new TarifacaoResidencial(), new TarifacaoVida()]);

    public Result<ResultadoTarifacao> Calcular(
        Produto produto,
        Segurado segurado,
        DadosRisco dadosRisco,
        IReadOnlyList<CoberturaSolicitada> coberturas,
        DateOnly dataReferencia)
    {
        ArgumentNullException.ThrowIfNull(produto);

        return _estrategias.TryGetValue(produto.Codigo, out var estrategia)
            ? estrategia.Calcular(produto, segurado, dadosRisco, coberturas, dataReferencia)
            : TarifacaoErros.ProdutoNaoSuportado(produto.Codigo);
    }
}
