using Apolice.Domain.Cotacoes;
using Apolice.Domain.Tarifacao;

namespace Apolice.Domain.Apolices;

// Entidade filha (e não VO) porque o endosso altera LMI/prêmio no lugar: o EF atualiza a linha
// em vez de apagar e reinserir a mesma chave (apolice_id, codigo).
public sealed class CoberturaApolice
{
    private CoberturaApolice()
    {
        Codigo = null!;
        Nome = null!;
    }

    private CoberturaApolice(string codigo, string nome, decimal lmi, decimal premioLiquido, bool obrigatoria, bool permiteSinistro)
    {
        Codigo = codigo;
        Nome = nome;
        Lmi = lmi;
        PremioLiquido = premioLiquido;
        Obrigatoria = obrigatoria;
        PermiteSinistro = permiteSinistro;
    }

    public string Codigo { get; private set; }

    public string Nome { get; private set; }

    public decimal Lmi { get; private set; }

    // Prêmio ANUAL do item: o pro rata do endosso é calculado sobre ele.
    public decimal PremioLiquido { get; private set; }

    public bool Obrigatoria { get; private set; }

    public bool PermiteSinistro { get; private set; }

    internal static CoberturaApolice De(ItemCotacao item) =>
        new(item.Codigo, item.Nome, item.Lmi, item.PremioLiquido, item.Obrigatoria, item.PermiteSinistro);

    internal static CoberturaApolice De(ItemTarifado item) =>
        new(item.Codigo, item.Nome, item.Lmi, item.PremioLiquido, item.Obrigatoria, item.PermiteSinistro);

    internal void Atualizar(decimal lmi, decimal premioLiquidoAnual)
    {
        Lmi = lmi;
        PremioLiquido = premioLiquidoAnual;
    }
}
