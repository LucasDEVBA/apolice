using Apolice.Application.Features.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Tarifacao;

namespace Apolice.Application.Features.Cotacoes;

public sealed record CotacaoDto(
    Guid Id,
    string ProdutoCodigo,
    string ProdutoNome,
    SeguradoDto Segurado,
    DadosRisco DadosRisco,
    IReadOnlyList<ItemTarifadoDto> Itens,
    IReadOnlyList<FatorDto> Fatores,
    decimal PremioLiquido,
    decimal AliquotaIof,
    decimal Iof,
    decimal PremioTotal,
    int MaxParcelas,
    StatusEfetivoCotacao Status,
    DateTimeOffset ValidaAte,
    DateTimeOffset CriadaEm,
    Guid CorretorId,
    string CorretorNome,
    Guid? ApoliceId);

public sealed record CotacaoResumoDto(
    Guid Id,
    string ProdutoCodigo,
    string SeguradoNome,
    decimal PremioTotal,
    StatusEfetivoCotacao Status,
    DateTimeOffset ValidaAte,
    DateTimeOffset CriadaEm,
    string CorretorNome);

// Mapeamento explícito do agregado para o DTO (sem AutoMapper). A leitura da infra pode usá-lo no detalhe ao
// materializar o agregado sem tracking, garantindo o status efetivo pela regra do domínio. A listagem não tem
// equivalente aqui de propósito: ela sai do SQL (Dapper), sem materializar agregados.
public static class CotacaoMapeamento
{
    public static CotacaoDto ParaDto(Cotacao cotacao, string produtoNome, string corretorNome, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(cotacao);
        return new CotacaoDto(
            cotacao.Id,
            cotacao.ProdutoCodigo,
            produtoNome,
            SeguradoDto.De(cotacao.Segurado),
            cotacao.DadosRisco,
            [.. cotacao.Itens.Select(ItemTarifadoDto.De)],
            [.. cotacao.Fatores.Select(FatorDto.De)],
            cotacao.PremioLiquido,
            cotacao.AliquotaIof,
            cotacao.Iof,
            cotacao.PremioTotal,
            cotacao.MaxParcelas,
            cotacao.StatusEfetivo(agora),
            cotacao.ValidaAte,
            cotacao.CriadaEm,
            cotacao.CorretorId,
            corretorNome,
            cotacao.ApoliceId);
    }
}
