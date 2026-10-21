using Apolice.Domain.Apolices;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Apolices;

// Prêmio total da apólice AUTO base: 4.080,44.
public sealed class CronogramaParcelasTests
{
    [Fact]
    public void Deve_ter_parcela_unica_igual_ao_total_quando_1_parcela()
    {
        Cenarios.ApoliceAuto(Cenarios.Agora, parcelas: 1).CronogramaParcelas()
            .ShouldBe([new ParcelaCronograma(1, new DateOnly(2026, 3, 10), 4_080.44m)]);
    }

    [Fact]
    public void Deve_jogar_a_sobra_na_primeira_quando_divisao_nao_e_exata()
    {
        // 4.080,44 / 3 = 1.360,1466… → base 1.360,14; 1ª = 4.080,44 − 2 × 1.360,14 = 1.360,16
        Cenarios.ApoliceAuto(Cenarios.Agora, parcelas: 3).CronogramaParcelas().ShouldBe(
        [
            new ParcelaCronograma(1, new DateOnly(2026, 3, 10), 1_360.16m),
            new ParcelaCronograma(2, new DateOnly(2026, 4, 10), 1_360.14m),
            new ParcelaCronograma(3, new DateOnly(2026, 5, 10), 1_360.14m),
        ]);
    }

    [Fact]
    public void Deve_fechar_no_total_quando_numero_maximo_de_parcelas()
    {
        var cronograma = Cenarios.ApoliceAuto(Cenarios.Agora, parcelas: 10).CronogramaParcelas();

        // 4.080,44 / 10 = 408,044 → base 408,04; 1ª = 4.080,44 − 9 × 408,04 = 408,08
        cronograma.Count.ShouldBe(10);
        cronograma[0].ShouldBe(new ParcelaCronograma(1, new DateOnly(2026, 3, 10), 408.08m));
        cronograma.Skip(1).ShouldAllBe(p => p.Valor == 408.04m);
        cronograma[9].Vencimento.ShouldBe(new DateOnly(2026, 12, 10)); // início + 9 meses
        cronograma.Sum(p => p.Valor).ShouldBe(4_080.44m);
    }

    [Fact]
    public void Deve_ajustar_para_o_ultimo_dia_do_mes_quando_inicio_no_dia_31()
    {
        // Emitida em 20/01/2026 com início em 31/01: fevereiro não tem dia 31.
        var apolice = Cenarios.ApoliceAuto(Cenarios.Em(2026, 1, 20), new DateOnly(2026, 1, 31), parcelas: 3);

        apolice.CronogramaParcelas().Select(p => p.Vencimento).ShouldBe(
            [new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31)]);
    }
}
