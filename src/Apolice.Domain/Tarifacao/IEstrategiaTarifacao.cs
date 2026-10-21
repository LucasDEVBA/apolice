using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;

namespace Apolice.Domain.Tarifacao;

public interface IEstrategiaTarifacao
{
    string ProdutoCodigo { get; }

    Result<ResultadoTarifacao> Calcular(
        Produto produto,
        Segurado segurado,
        DadosRisco dadosRisco,
        IReadOnlyList<CoberturaSolicitada> coberturas,
        DateOnly dataReferencia);
}
