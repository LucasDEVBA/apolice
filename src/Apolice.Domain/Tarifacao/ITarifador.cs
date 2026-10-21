using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;

namespace Apolice.Domain.Tarifacao;

public interface ITarifador
{
    Result<ResultadoTarifacao> Calcular(
        Produto produto,
        Segurado segurado,
        DadosRisco dadosRisco,
        IReadOnlyList<CoberturaSolicitada> coberturas,
        DateOnly dataReferencia);
}
