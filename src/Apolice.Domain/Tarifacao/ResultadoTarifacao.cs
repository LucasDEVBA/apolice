using Apolice.Domain.Comum;

namespace Apolice.Domain.Tarifacao;

public sealed record ResultadoTarifacao
{
    public const int ParcelasMinimas = 1;
    private const int ParcelasMaximas = 10;
    private const decimal ParcelaMinima = 60.00m;

    private ResultadoTarifacao(
        IReadOnlyList<ItemTarifado> itens,
        IReadOnlyList<FatorAplicado> fatores,
        decimal premioLiquido,
        decimal aliquotaIof,
        decimal iof)
    {
        Itens = itens;
        Fatores = fatores;
        PremioLiquido = premioLiquido;
        AliquotaIof = aliquotaIof;
        Iof = iof;
        PremioTotal = premioLiquido + iof;
        MaxParcelas = CalcularMaxParcelas(PremioTotal);
    }

    public IReadOnlyList<ItemTarifado> Itens { get; }

    public IReadOnlyList<FatorAplicado> Fatores { get; }

    public decimal PremioLiquido { get; }

    public decimal AliquotaIof { get; }

    public decimal Iof { get; }

    public decimal PremioTotal { get; }

    public int MaxParcelas { get; }

    public static ResultadoTarifacao Criar(
        IReadOnlyList<ItemTarifado> itens,
        IReadOnlyList<FatorAplicado> fatores,
        decimal aliquotaIof)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(fatores);

        // Cópias somente leitura: quem montou as listas não consegue mais quebrar PremioLiquido = Σ itens.
        IReadOnlyList<ItemTarifado> copiaItens = [.. itens];
        IReadOnlyList<FatorAplicado> copiaFatores = [.. fatores];
        var premioLiquido = copiaItens.Sum(i => i.PremioLiquido);
        var iof = Dinheiro.Arredondar(premioLiquido * aliquotaIof);
        return new ResultadoTarifacao(copiaItens, copiaFatores, premioLiquido, aliquotaIof, iof);
    }

    // Parcela mínima de R$ 60 limita o parcelamento de prêmios baixos; acima disso o teto é 10x.
    public static int CalcularMaxParcelas(decimal premioTotal) =>
        (int)Math.Clamp(Math.Floor(premioTotal / ParcelaMinima), ParcelasMinimas, ParcelasMaximas);

    // Igualdade de VO pelo conteúdo das listas (o record compararia referências). Os totais derivam
    // dos itens e da alíquota, então não precisam entrar na comparação.
    public bool Equals(ResultadoTarifacao? other) =>
        other is not null
        && AliquotaIof == other.AliquotaIof
        && Itens.SequenceEqual(other.Itens)
        && Fatores.SequenceEqual(other.Fatores);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(AliquotaIof);
        foreach (var item in Itens)
        {
            hash.Add(item);
        }

        foreach (var fator in Fatores)
        {
            hash.Add(fator);
        }

        return hash.ToHashCode();
    }
}
