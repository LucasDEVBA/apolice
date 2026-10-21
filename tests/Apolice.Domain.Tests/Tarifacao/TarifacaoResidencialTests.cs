using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Tarifacao;

public sealed class TarifacaoResidencialTests
{
    private static readonly Tarifador Tarifador = Tarifador.Padrao();

    [Fact]
    public void Deve_agravar_so_o_roubo_quando_casa_habitual_com_todas_as_coberturas()
    {
        var dados = new DadosRiscoResidencial(600_000m, TipoImovel.Casa, "40000-000", Habitual: true);
        CoberturaSolicitada[] coberturas =
        [
            new("INCENDIO", 500_000m), new("DANOS_ELETRICOS", 20_000m), new("ROUBO", 30_000m),
            new("RC_FAMILIAR", 50_000m), new("VENDAVAL", 40_000m),
        ];

        var resultado = Calcular(dados, coberturas).DeveTerSucesso();

        resultado.Fatores.ShouldBe([new FatorAplicado("Casa (roubo)", 1.30m)]);
        resultado.Itens.Select(i => (i.Codigo, i.PremioLiquido)).ShouldBe(
        [
            ("INCENDIO", 225.00m), // 500.000 × 0,00045
            ("DANOS_ELETRICOS", 240.00m), // 20.000 × 0,0120
            ("ROUBO", 702.00m), // 30.000 × 0,0180 = 540 × 1,30
            ("RC_FAMILIAR", 175.00m), // 50.000 × 0,0035
            ("VENDAVAL", 240.00m), // 40.000 × 0,0060
        ]);
        resultado.PremioLiquido.ShouldBe(1_582.00m);
        resultado.Iof.ShouldBe(116.75m); // 1.582 × 0,0738 = 116,7516
        resultado.PremioTotal.ShouldBe(1_698.75m);
        resultado.MaxParcelas.ShouldBe(10); // floor(1.698,75 / 60) = 28 → teto 10
    }

    [Fact]
    public void Deve_combinar_apartamento_e_veraneio_em_todos_os_itens_quando_nao_habitual()
    {
        var dados = new DadosRiscoResidencial(300_000m, TipoImovel.Apartamento, "88000-000", Habitual: false);

        // INCENDIO = valor do imóvel (limite aceito).
        var resultado = Calcular(dados, [new("INCENDIO", 300_000m), new("ROUBO", 10_000m)]).DeveTerSucesso();

        // Π = 0,80 × 1,30 = 1,04
        resultado.Fatores.ShouldBe([new FatorAplicado("Apartamento", 0.80m), new FatorAplicado("Imóvel não habitual (veraneio)", 1.30m)]);
        resultado.Itens.Select(i => i.PremioLiquido).ShouldBe([140.40m, 187.20m]); // 135 × 1,04 · 180 × 1,04
        resultado.PremioLiquido.ShouldBe(327.60m);
        resultado.Iof.ShouldBe(24.18m); // 327,60 × 0,0738 = 24,17688
        resultado.PremioTotal.ShouldBe(351.78m);
        resultado.MaxParcelas.ShouldBe(5); // floor(351,78 / 60) = 5
    }

    [Fact]
    public void Deve_omitir_fator_da_casa_quando_roubo_nao_contratado()
    {
        var dados = new DadosRiscoResidencial(200_000m, TipoImovel.Casa, "40000-000", Habitual: true);

        var resultado = Calcular(dados, [new("INCENDIO", 150_000m)]).DeveTerSucesso();

        resultado.Fatores.ShouldBeEmpty(); // o 1,30 da casa só vale para o ROUBO, ausente aqui
        resultado.PremioLiquido.ShouldBe(67.50m); // 150.000 × 0,00045
        resultado.Iof.ShouldBe(4.98m); // 67,50 × 0,0738 = 4,9815
        resultado.PremioTotal.ShouldBe(72.48m);
        resultado.MaxParcelas.ShouldBe(1); // floor(72,48 / 60) = 1
    }

    [Fact]
    public void Deve_calcular_max_parcelas_sobre_o_premio_total_quando_o_iof_muda_a_faixa()
    {
        var dados = new DadosRiscoResidencial(300_000m, TipoImovel.Casa, "40000-000", Habitual: true);

        var resultado = Calcular(dados, [new("INCENDIO", 250_000m)]).DeveTerSucesso();

        // 250.000 × 0,00045 = 112,50; IOF 8,3025 → 8,30; total 120,80 → floor(120,80 / 60) = 2 (sobre o líquido daria 1).
        (resultado.PremioLiquido, resultado.Iof, resultado.PremioTotal).ShouldBe((112.50m, 8.30m, 120.80m));
        resultado.MaxParcelas.ShouldBe(2);
    }

    [Theory]
    [InlineData("50000", "10000", "4.50")] // valor mínimo do imóvel; 10.000 × 0,00045
    [InlineData("5000000", "5000000", "2250.00")] // valor máximo, com INCENDIO no LMI máximo
    public void Deve_aceitar_quando_valor_do_imovel_nas_bordas(string valorImovel, string lmiIncendio, string premioIncendio)
    {
        var dados = new DadosRiscoResidencial(Valor.De(valorImovel), TipoImovel.Casa, "40000-000", Habitual: true);

        Calcular(dados, [new("INCENDIO", Valor.De(lmiIncendio))]).DeveTerSucesso().PremioLiquido.ShouldBe(Valor.De(premioIncendio));
    }

    [Fact]
    public void Deve_rejeitar_quando_valor_do_imovel_tem_mais_de_2_casas()
    {
        var dados = new DadosRiscoResidencial(200_000.001m, TipoImovel.Casa, "40000-000", Habitual: true);

        Calcular(dados, [new("INCENDIO", 10_000m)]).DeveFalharCom("Tarifacao.ValorMonetarioInvalido", ErrorType.Validacao);
    }

    [Fact]
    public void Deve_rejeitar_quando_lmi_de_incendio_supera_o_valor_do_imovel()
    {
        var dados = new DadosRiscoResidencial(100_000m, TipoImovel.Casa, "40000-000", Habitual: true);

        Calcular(dados, [new("INCENDIO", 100_000.01m)]).DeveFalharCom("Tarifacao.LmiAcimaDoValorImovel", ErrorType.Validacao);
    }

    [Theory]
    [InlineData("10000", true)] // LMI mínimo do incêndio
    [InlineData("9999.99", false)]
    public void Deve_validar_quando_lmi_do_incendio_na_borda_minima(string lmi, bool valido)
    {
        var dados = new DadosRiscoResidencial(100_000m, TipoImovel.Casa, "40000-000", Habitual: true);

        Calcular(dados, [new("INCENDIO", Valor.De(lmi))]).IsSuccess.ShouldBe(valido);
    }

    [Theory]
    [InlineData("49999.99")]
    [InlineData("5000000.01")]
    public void Deve_rejeitar_quando_valor_do_imovel_fora_da_faixa(string valorImovel)
    {
        var dados = new DadosRiscoResidencial(Valor.De(valorImovel), TipoImovel.Casa, "40000-000", Habitual: true);

        Calcular(dados, [new("INCENDIO", 10_000m)]).DeveFalharCom("Tarifacao.ValorImovelForaDoLimite");
    }

    [Fact]
    public void Deve_rejeitar_quando_cep_ou_tipo_de_imovel_invalidos()
    {
        Calcular(new DadosRiscoResidencial(100_000m, TipoImovel.Casa, "4000", true), [new("INCENDIO", 10_000m)])
            .DeveFalharCom("Tarifacao.CepInvalido");
        Calcular(new DadosRiscoResidencial(100_000m, (TipoImovel)99, "40000-000", true), [new("INCENDIO", 10_000m)])
            .DeveFalharCom("Tarifacao.TipoImovelInvalido");
    }

    private static Result<ResultadoTarifacao> Calcular(DadosRiscoResidencial dados, IReadOnlyList<CoberturaSolicitada> coberturas) =>
        Tarifador.Calcular(CatalogoPadrao.Residencial(), Cenarios.SeguradoPadrao(), dados, coberturas, Cenarios.Hoje);
}
