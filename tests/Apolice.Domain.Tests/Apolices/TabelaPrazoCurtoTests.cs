using Apolice.Domain.Apolices;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Apolices;

// Regra: vale a primeira linha com Dias ≥ decorridos. Cada linha é testada na borda e no dia seguinte à anterior.
public sealed class TabelaPrazoCurtoTests
{
    public static TheoryData<int, string> Bordas => new()
    {
        { 0, "0.13" }, { 15, "0.13" }, { 16, "0.20" }, { 30, "0.20" }, { 31, "0.27" }, { 45, "0.27" },
        { 46, "0.30" }, { 60, "0.30" }, { 61, "0.37" }, { 75, "0.37" }, { 76, "0.40" }, { 90, "0.40" },
        { 91, "0.46" }, { 105, "0.46" }, { 106, "0.50" }, { 120, "0.50" }, { 121, "0.56" }, { 135, "0.56" },
        { 136, "0.60" }, { 150, "0.60" }, { 151, "0.66" }, { 165, "0.66" }, { 166, "0.70" }, { 180, "0.70" },
        { 181, "0.73" }, { 195, "0.73" }, { 196, "0.75" }, { 210, "0.75" }, { 211, "0.78" }, { 225, "0.78" },
        { 226, "0.80" }, { 240, "0.80" }, { 241, "0.83" }, { 255, "0.83" }, { 256, "0.85" }, { 270, "0.85" },
        { 271, "0.88" }, { 285, "0.88" }, { 286, "0.90" }, { 300, "0.90" }, { 301, "0.93" }, { 315, "0.93" },
        { 316, "0.95" }, { 330, "0.95" }, { 331, "0.98" }, { 345, "0.98" }, { 346, "1.00" }, { 365, "1.00" },
        { 366, "1.00" }, // ano bissexto: além da última linha também retém 100%
    };

    [Theory]
    [MemberData(nameof(Bordas))]
    public void Deve_reter_o_percentual_da_linha_quando_dias_decorridos_na_borda(int diasDecorridos, string percentual)
    {
        TabelaPrazoCurto.PercentualRetido(diasDecorridos).ShouldBe(Valor.De(percentual));
    }

    [Fact]
    public void Deve_ter_24_linhas_crescentes_quando_carregada()
    {
        var linhas = TabelaPrazoCurto.Linhas;

        linhas.Count.ShouldBe(24);
        linhas.Select(l => l.Dias).ShouldBeInOrder(SortDirection.Ascending);
        linhas[^1].ShouldBe(new LinhaPrazoCurto(365, 1.00m));
    }
}
