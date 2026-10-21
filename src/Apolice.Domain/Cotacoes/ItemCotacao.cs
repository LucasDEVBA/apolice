using Apolice.Domain.Tarifacao;

namespace Apolice.Domain.Cotacoes;

// Snapshot do item tarifado: a cotação não pode mudar se o catálogo mudar depois.
public sealed record ItemCotacao(
    string Codigo,
    string Nome,
    decimal Lmi,
    decimal PremioLiquido,
    bool Obrigatoria,
    bool PermiteSinistro)
{
    public static ItemCotacao De(ItemTarifado item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ItemCotacao(item.Codigo, item.Nome, item.Lmi, item.PremioLiquido, item.Obrigatoria, item.PermiteSinistro);
    }
}
