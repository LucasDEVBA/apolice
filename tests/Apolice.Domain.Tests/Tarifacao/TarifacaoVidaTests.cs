using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Tarifacao;

// Referência 10/03/2026. Base da MORTE com LMI 100.000: 100.000 × 0,80 / 1.000 = 80,00.
public sealed class TarifacaoVidaTests
{
    private static readonly Tarifador Tarifador = Tarifador.Padrao();
    private static readonly DadosRiscoVida NaoFumante = new(Fumante: false);

    [Fact]
    public void Deve_agravar_so_a_morte_quando_faixa_31_a_40_com_ipa_e_funeral()
    {
        // Nascido em 11/03/1990: em 10/03/2026 tem 35 anos (aniversário no dia seguinte).
        var segurado = Cenarios.SeguradoPadrao(new DateOnly(1990, 3, 11));

        var resultado = Calcular(segurado, NaoFumante, [new("MORTE", 200_000m), new("IPA", 100_000m), new("FUNERAL", null)]).DeveTerSucesso();

        resultado.Fatores.ShouldBe([new FatorAplicado("Faixa etária: 31 a 40 anos (morte)", 1.50m)]);
        resultado.Itens.ShouldBe(
        [
            new ItemTarifado("MORTE", "Morte natural ou acidental", 200_000m, 240.00m, true, true), // 200.000 × 0,80/1.000 = 160 × 1,50
            new ItemTarifado("IPA", "Invalidez permanente por acidente", 100_000m, 35.00m, false, true), // 100.000 × 0,35/1.000, sem fator
            new ItemTarifado("FUNERAL", "Assistência funeral", 7_000m, 90.00m, false, true),
        ]);
        resultado.PremioLiquido.ShouldBe(365.00m);
        resultado.AliquotaIof.ShouldBe(0.0038m);
        resultado.Iof.ShouldBe(1.39m); // 365 × 0,0038 = 1,387
        resultado.PremioTotal.ShouldBe(366.39m);
        resultado.MaxParcelas.ShouldBe(6); // floor(366,39 / 60) = 6
    }

    [Fact]
    public void Deve_multiplicar_faixa_e_fumante_quando_fumante_de_55_anos()
    {
        // Nascido em 01/06/1970: 55 anos em 10/03/2026 → faixa 51–60.
        var segurado = Cenarios.SeguradoPadrao(new DateOnly(1970, 6, 1));

        var resultado = Calcular(segurado, new DadosRiscoVida(Fumante: true), [new("MORTE", 100_000m)]).DeveTerSucesso();

        resultado.Fatores.ShouldBe([new FatorAplicado("Faixa etária: 51 a 60 anos (morte)", 6.25m), new FatorAplicado("Fumante (morte)", 1.50m)]);
        resultado.PremioLiquido.ShouldBe(750.00m); // 80 × 6,25 × 1,50
        resultado.Iof.ShouldBe(2.85m); // 750 × 0,0038
        resultado.PremioTotal.ShouldBe(752.85m);
        resultado.MaxParcelas.ShouldBe(10); // floor(752,85 / 60) = 12 → teto 10
    }

    [Fact]
    public void Deve_agravar_so_a_morte_quando_fumante_com_ipa()
    {
        var segurado = Cenarios.SeguradoPadrao(Cenarios.Hoje.AddYears(-25));

        var resultado = Calcular(segurado, new DadosRiscoVida(Fumante: true), [new("MORTE", 100_000m), new("IPA", 100_000m)]).DeveTerSucesso();

        // MORTE 80 × 1,50 = 120,00; IPA 35,00 sem fator; IOF 155 × 0,0038 = 0,589 → 0,59
        resultado.Fatores.ShouldBe([new FatorAplicado("Fumante (morte)", 1.50m)]);
        resultado.Itens.Select(i => (i.Codigo, i.PremioLiquido)).ShouldBe([("MORTE", 120.00m), ("IPA", 35.00m)]);
        (resultado.PremioLiquido, resultado.Iof, resultado.PremioTotal).ShouldBe((155.00m, 0.59m, 155.59m));
    }

    [Fact]
    public void Deve_arredondar_meio_centavo_do_iof_para_cima_quando_tarifa_vida()
    {
        // 55 anos (×6,25): 255.000 × 0,80/1.000 = 204 × 6,25 = 1.275,00; IOF 1.275 × 0,0038 = 4,845 → 4,85 (AwayFromZero).
        var resultado = Calcular(Cenarios.SeguradoPadrao(new DateOnly(1970, 6, 1)), NaoFumante, [new("MORTE", 255_000m)]).DeveTerSucesso();

        (resultado.PremioLiquido, resultado.Iof, resultado.PremioTotal).ShouldBe((1_275.00m, 4.85m, 1_279.85m));
        resultado.MaxParcelas.ShouldBe(10);
    }

    [Fact]
    public void Deve_tarifar_sem_teto_para_a_ipa_quando_produto_vida_nao_tem_morte()
    {
        // Produto.Criar aceita VIDA só com IPA; o fator de faixa (alvo MORTE) não tem item e não aparece.
        var ipa = CoberturaProduto.ComTaxa("IPA", "Invalidez", "Invalidez por acidente.", true, TipoCalculo.TaxaPorMil, 0.35m, 10_000m, 2_000_000m).Value;
        var produto = Produto.Criar("VIDA", "Vida só IPA", "Produto de teste.", 0.0038m, [ipa]).Value;
        var segurado = Cenarios.SeguradoPadrao(new DateOnly(1990, 3, 11));

        var resultado = Tarifador.Calcular(produto, segurado, NaoFumante, [new("IPA", 100_000m)], Cenarios.Hoje).DeveTerSucesso();

        resultado.Itens.ShouldHaveSingleItem().PremioLiquido.ShouldBe(35.00m);
        resultado.Fatores.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(18, "80.00")] // 18–30: ×1,00
    [InlineData(30, "80.00")]
    [InlineData(31, "120.00")] // ×1,50
    [InlineData(40, "120.00")]
    [InlineData(41, "240.00")] // ×3,00
    [InlineData(50, "240.00")]
    [InlineData(51, "500.00")] // ×6,25
    [InlineData(60, "500.00")]
    [InlineData(61, "1000.00")] // ×12,50
    [InlineData(70, "1000.00")]
    public void Deve_aplicar_fator_da_faixa_etaria_quando_idade_na_borda(int idade, string premioMorte)
    {
        // Nascimento = referência − idade anos: completa a idade exatamente na data de referência.
        var segurado = Cenarios.SeguradoPadrao(Cenarios.Hoje.AddYears(-idade));

        Calcular(segurado, NaoFumante, [new("MORTE", 100_000m)]).DeveTerSucesso().PremioLiquido.ShouldBe(Valor.De(premioMorte));
    }

    [Fact]
    public void Deve_ficar_na_faixa_anterior_quando_aniversario_e_no_dia_seguinte()
    {
        // Nascido em 11/03/1995: ainda 30 anos em 10/03/2026 → sem agravo.
        Calcular(Cenarios.SeguradoPadrao(new DateOnly(1995, 3, 11)), NaoFumante, [new("MORTE", 100_000m)])
            .DeveTerSucesso().PremioLiquido.ShouldBe(80.00m);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(71)]
    public void Deve_rejeitar_quando_idade_fora_de_18_a_70(int idade)
    {
        Calcular(Cenarios.SeguradoPadrao(Cenarios.Hoje.AddYears(-idade)), NaoFumante, [new("MORTE", 100_000m)])
            .DeveFalharCom("Tarifacao.IdadeSeguradoForaDoLimite", ErrorType.Validacao);
    }

    [Fact]
    public void Deve_rejeitar_quando_ipa_supera_a_morte()
    {
        var segurado = Cenarios.SeguradoPadrao(Cenarios.Hoje.AddYears(-25));

        Calcular(segurado, NaoFumante, [new("MORTE", 50_000m), new("IPA", 50_000m)]).IsSuccess.ShouldBeTrue();
        Calcular(segurado, NaoFumante, [new("MORTE", 50_000m), new("IPA", 50_000.01m)])
            .DeveFalharCom("Tarifacao.LmiIpaAcimaDaMorte", ErrorType.Validacao);
    }

    [Fact]
    public void Deve_calcular_nas_bordas_do_lmi_quando_morte_maxima_e_ipa_minima()
    {
        var segurado = Cenarios.SeguradoPadrao(Cenarios.Hoje.AddYears(-25));

        var resultado = Calcular(segurado, NaoFumante, [new("MORTE", 2_000_000m), new("IPA", 10_000m)]).DeveTerSucesso();

        // 2.000.000 × 0,80/1.000 = 1.600,00 · 10.000 × 0,35/1.000 = 3,50
        resultado.Itens.Select(i => i.PremioLiquido).ShouldBe([1_600.00m, 3.50m]);
    }

    [Fact]
    public void Deve_rejeitar_quando_morte_abaixo_do_minimo()
    {
        Calcular(Cenarios.SeguradoPadrao(Cenarios.Hoje.AddYears(-25)), NaoFumante, [new("MORTE", 19_999.99m)])
            .DeveFalharCom("Tarifacao.LmiForaDoLimite");
    }

    private static Result<ResultadoTarifacao> Calcular(Segurado segurado, DadosRiscoVida dados, IReadOnlyList<CoberturaSolicitada> coberturas) =>
        Tarifador.Calcular(CatalogoPadrao.Vida(), segurado, dados, coberturas, Cenarios.Hoje);
}
