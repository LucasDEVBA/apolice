using Apolice.Domain.Cotacoes;
using Apolice.Domain.Tarifacao;

namespace Apolice.Application.Features.Cotacoes;

public sealed record TarifacaoDto(
    IReadOnlyList<ItemTarifadoDto> Itens,
    IReadOnlyList<FatorDto> Fatores,
    decimal PremioLiquido,
    decimal AliquotaIof,
    decimal Iof,
    decimal PremioTotal,
    int MaxParcelas)
{
    public static TarifacaoDto De(ResultadoTarifacao resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        return new TarifacaoDto(
            [.. resultado.Itens.Select(ItemTarifadoDto.De)],
            [.. resultado.Fatores.Select(FatorDto.De)],
            resultado.PremioLiquido,
            resultado.AliquotaIof,
            resultado.Iof,
            resultado.PremioTotal,
            resultado.MaxParcelas);
    }
}

public sealed record ItemTarifadoDto(string Codigo, string Nome, decimal Lmi, decimal PremioLiquido, bool Obrigatoria, bool PermiteSinistro)
{
    public static ItemTarifadoDto De(ItemTarifado item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ItemTarifadoDto(item.Codigo, item.Nome, item.Lmi, item.PremioLiquido, item.Obrigatoria, item.PermiteSinistro);
    }

    public static ItemTarifadoDto De(ItemCotacao item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ItemTarifadoDto(item.Codigo, item.Nome, item.Lmi, item.PremioLiquido, item.Obrigatoria, item.PermiteSinistro);
    }
}

public sealed record FatorDto(string Descricao, decimal Fator)
{
    public static FatorDto De(FatorAplicado fator)
    {
        ArgumentNullException.ThrowIfNull(fator);
        return new FatorDto(fator.Descricao, fator.Fator);
    }
}
