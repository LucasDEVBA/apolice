using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Tarifacao;

// Referência fixa em 10/03/2026 (ano 2026). Caso base: CASCO 100.000 (3.200,00) + RCF_DM 100.000 (600,00) = 3.800,00.
public sealed class TarifacaoAutoTests
{
    private static readonly Tarifador Tarifador = Tarifador.Padrao();

    [Fact]
    public void Deve_calcular_premio_exato_quando_auto_sem_agravos()
    {
        var resultado = Calcular(Cenarios.DadosAutoNeutro()).DeveTerSucesso();

        // CASCO 100.000 × 0,0320 = 3.200,00 · RCF_DM 100.000 × 0,0060 = 600,00
        resultado.Itens.ShouldBe(
        [
            new ItemTarifado("CASCO", "Colisão, incêndio e roubo", 100_000m, 3_200.00m, true, true),
            new ItemTarifado("RCF_DM", "RC danos materiais a terceiros", 100_000m, 600.00m, true, true),
        ]);
        resultado.Fatores.ShouldBeEmpty(); // todos os fatores = 1 não aparecem
        resultado.PremioLiquido.ShouldBe(3_800.00m);
        resultado.AliquotaIof.ShouldBe(0.0738m);
        resultado.Iof.ShouldBe(280.44m); // 3.800 × 0,0738 = 280,44
        resultado.PremioTotal.ShouldBe(4_080.44m);
        resultado.MaxParcelas.ShouldBe(10); // floor(4.080,44 / 60) = 68 → teto 10
    }

    [Fact]
    public void Deve_aplicar_todos_os_fatores_quando_condutor_jovem_sp_uso_comercial_veiculo_antigo_e_bonus_5()
    {
        var dados = Cenarios.DadosAutoNeutro(80_000m, anoFabricacao: 2014, cep: "01310-100", idadeCondutor: 25, usoComercial: true, classeBonus: 5);
        CoberturaSolicitada[] coberturas =
        [
            new("CASCO", null), new("RCF_DM", 100_000m), new("RCF_DC", 50_000m),
            new("VIDROS", null), new("CARRO_RESERVA", null), new("ASSISTENCIA_24H", null),
        ];

        var resultado = Calcular(dados, coberturas).DeveTerSucesso();

        // Π fatores = 1,40 × 1,25 × 1,35 × 1,10 × 0,85 = 2,2089375
        resultado.Fatores.ShouldBe(
        [
            new FatorAplicado("Idade do condutor: 18 a 25 anos", 1.40m),
            new FatorAplicado("Região de pernoite: Grande São Paulo", 1.25m),
            new FatorAplicado("Uso comercial", 1.35m),
            new FatorAplicado("Veículo com mais de 10 anos", 1.10m), // 2026 − 2014 = 12 > 10
            new FatorAplicado("Bônus classe 5", 0.85m), // 1 − 0,03 × 5
        ]);
        resultado.Itens.ShouldBe(
        [
            new ItemTarifado("CASCO", "Colisão, incêndio e roubo", 80_000m, 5_654.88m, true, true), // 2.560 × 2,2089375 = 5.654,88
            new ItemTarifado("RCF_DM", "RC danos materiais a terceiros", 100_000m, 1_325.36m, true, true), // 600 × 2,2089375 = 1.325,3625
            new ItemTarifado("RCF_DC", "RC danos corporais a terceiros", 50_000m, 441.79m, false, true), // 200 × 2,2089375 = 441,7875
            new ItemTarifado("VIDROS", "Vidros, faróis e retrovisores", 3_000m, 220.00m, false, true), // fixo, sem fatores
            new ItemTarifado("CARRO_RESERVA", "Carro reserva (15 dias)", 0m, 180.00m, false, false),
            new ItemTarifado("ASSISTENCIA_24H", "Assistência 24h", 0m, 120.00m, false, false),
        ]);
        resultado.PremioLiquido.ShouldBe(7_942.03m); // 5.654,88 + 1.325,36 + 441,79 + 220 + 180 + 120
        resultado.Iof.ShouldBe(586.12m); // 7.942,03 × 0,0738 = 586,121814
        resultado.PremioTotal.ShouldBe(8_528.15m);
    }

    [Theory]
    [InlineData(18, "5320.00")] // 3.800 × 1,40
    [InlineData(25, "5320.00")] // borda superior da faixa 18–25
    [InlineData(26, "4180.00")] // 3.800 × 1,10
    [InlineData(35, "4180.00")]
    [InlineData(36, "3800.00")] // 36–60 neutro
    [InlineData(60, "3800.00")]
    [InlineData(61, "4370.00")] // 3.800 × 1,15
    [InlineData(90, "4370.00")]
    public void Deve_aplicar_fator_da_faixa_quando_idade_do_condutor_na_borda(int idade, string premioLiquido)
    {
        Calcular(Cenarios.DadosAutoNeutro(idadeCondutor: idade)).DeveTerSucesso().PremioLiquido.ShouldBe(Valor.De(premioLiquido));
    }

    [Theory]
    [InlineData("01310-100", "4750.00")] // '0' Grande SP: 3.800 × 1,25
    [InlineData("20040-002", "4940.00")] // '2' RJ/ES: 3.800 × 1,30
    [InlineData("40020000", "4180.00")] // '4' BA/SE: 3.800 × 1,10 (sem hífen também vale)
    [InlineData("30140-071", "3800.00")] // demais: neutro
    [InlineData("10000-000", "3800.00")]
    public void Deve_aplicar_fator_regional_pelo_primeiro_digito_quando_cep_valido(string cep, string premioLiquido)
    {
        Calcular(Cenarios.DadosAutoNeutro(cep: cep)).DeveTerSucesso().PremioLiquido.ShouldBe(Valor.De(premioLiquido));
    }

    [Fact]
    public void Deve_omitir_fator_quando_bonus_zero()
    {
        var resultado = Calcular(Cenarios.DadosAutoNeutro(classeBonus: 0)).DeveTerSucesso();

        resultado.PremioLiquido.ShouldBe(3_800.00m); // fator 1 − 0,03 × 0 = 1
        resultado.Fatores.ShouldBeEmpty();
    }

    [Fact]
    public void Deve_descontar_30_por_cento_quando_bonus_maximo_10()
    {
        var resultado = Calcular(Cenarios.DadosAutoNeutro(classeBonus: 10)).DeveTerSucesso();

        // 1 − 0,03 × 10 = 0,70 → CASCO 2.240,00 + RCF_DM 420,00
        resultado.Fatores.ShouldBe([new FatorAplicado("Bônus classe 10", 0.70m)]);
        resultado.PremioLiquido.ShouldBe(2_660.00m);
    }

    [Fact]
    public void Deve_agravar_35_por_cento_quando_uso_comercial()
    {
        // 3.800 × 1,35 = 5.130,00
        Calcular(Cenarios.DadosAutoNeutro(usoComercial: true)).DeveTerSucesso().PremioLiquido.ShouldBe(5_130.00m);
    }

    [Theory]
    [InlineData(2016, "3800.00")] // 10 anos: ainda não agrava (precisa ser > 10)
    [InlineData(2015, "4180.00")] // 11 anos: 3.800 × 1,10
    [InlineData(2006, "4180.00")] // 20 anos: limite inferior aceito
    [InlineData(2027, "3800.00")] // ano + 1: limite superior aceito
    public void Deve_agravar_quando_veiculo_tem_mais_de_10_anos(int anoFabricacao, string premioLiquido)
    {
        Calcular(Cenarios.DadosAutoNeutro(anoFabricacao: anoFabricacao)).DeveTerSucesso().PremioLiquido.ShouldBe(Valor.De(premioLiquido));
    }

    [Theory]
    [InlineData(2005)]
    [InlineData(2028)]
    public void Deve_rejeitar_quando_ano_fabricacao_fora_da_janela_de_20_anos(int anoFabricacao)
    {
        Calcular(Cenarios.DadosAutoNeutro(anoFabricacao: anoFabricacao)).DeveFalharCom("Tarifacao.AnoFabricacaoInvalido", ErrorType.Validacao);
    }

    [Theory]
    [InlineData(2009, null)] // 2030 − 21: fora da janela
    [InlineData(2010, "4180.00")] // 2030 − 20: aceito, com agravo (20 > 10)
    [InlineData(2019, "4180.00")] // 11 anos em 2030
    [InlineData(2020, "3800.00")] // 10 anos em 2030: sem agravo
    [InlineData(2031, "3800.00")] // 2030 + 1
    [InlineData(2032, null)]
    public void Deve_mover_a_janela_e_o_agravo_quando_data_de_referencia_em_outro_ano(int anoFabricacao, string? premioLiquido)
    {
        var resultado = Calcular(Cenarios.DadosAutoNeutro(anoFabricacao: anoFabricacao), dataReferencia: new DateOnly(2030, 6, 15));

        if (premioLiquido is null)
        {
            resultado.DeveFalharCom("Tarifacao.AnoFabricacaoInvalido");
            return;
        }

        resultado.DeveTerSucesso().PremioLiquido.ShouldBe(Valor.De(premioLiquido));
    }

    [Fact]
    public void Deve_aplicar_todos_os_fatores_quando_referencia_em_2030_e_veiculo_de_2018()
    {
        // Mesmo perfil do caso de 2026/2014: 2030 − 2018 = 12 > 10, então o Π continua 2,2089375.
        var dados = Cenarios.DadosAutoNeutro(80_000m, anoFabricacao: 2018, cep: "01310-100", idadeCondutor: 25, usoComercial: true, classeBonus: 5);

        var resultado = Calcular(dados, [new("CASCO", null), new("RCF_DM", 100_000m)], new DateOnly(2030, 6, 15)).DeveTerSucesso();

        resultado.Fatores.Select(f => f.Fator).ShouldBe([1.40m, 1.25m, 1.35m, 1.10m, 0.85m]);
        resultado.Itens.Select(i => i.PremioLiquido).ShouldBe([5_654.88m, 1_325.36m]);
        resultado.PremioLiquido.ShouldBe(6_980.24m);
        resultado.Iof.ShouldBe(515.14m); // 6.980,24 × 0,0738 = 515,141712
        resultado.PremioTotal.ShouldBe(7_495.38m);
    }

    [Fact]
    public void Deve_rejeitar_quando_segurado_nasce_depois_da_data_de_referencia()
    {
        var naoNascido = Cenarios.SeguradoPadrao(Cenarios.Hoje.AddDays(1));
        var nascidoHoje = Cenarios.SeguradoPadrao(Cenarios.Hoje);

        Tarifador.Calcular(CatalogoPadrao.Auto(), naoNascido, Cenarios.DadosAutoNeutro(), Cenarios.CoberturasAutoBasicas(), Cenarios.Hoje)
            .DeveFalharCom("Segurado.DataNascimentoInvalida", ErrorType.Validacao);
        Tarifador.Calcular(CatalogoPadrao.Auto(), nascidoHoje, Cenarios.DadosAutoNeutro(), Cenarios.CoberturasAutoBasicas(), Cenarios.Hoje)
            .DeveTerSucesso();
    }

    [Fact]
    public void Deve_rejeitar_quando_valor_do_veiculo_ou_lmi_tem_mais_de_2_casas()
    {
        Calcular(Cenarios.DadosAutoNeutro(valorVeiculo: 100_000.123m)).DeveFalharCom("Tarifacao.ValorMonetarioInvalido", ErrorType.Validacao);
        Calcular(Cenarios.DadosAutoNeutro(), [new("CASCO", null), new("RCF_DM", 100_000.005m)]).DeveFalharCom("Tarifacao.ValorMonetarioInvalido");
    }

    [Fact]
    public void Deve_aceitar_quando_valor_tem_zeros_a_direita_alem_dos_centavos()
    {
        // 100.000,120 é o mesmo valor que 100.000,12: a escala do decimal não importa, só os centavos exatos.
        var resultado = Calcular(Cenarios.DadosAutoNeutro(valorVeiculo: 100_000.120m), [new("CASCO", null), new("RCF_DM", 100_000.10m)]).DeveTerSucesso();

        resultado.Itens.Select(i => i.Lmi).ShouldBe([100_000.12m, 100_000.10m]);
    }

    [Fact]
    public void Deve_usar_valor_do_veiculo_como_lmi_do_casco_quando_lmi_enviado_e_diferente()
    {
        var resultado = Calcular(Cenarios.DadosAutoNeutro(), [new("CASCO", 1m), new("RCF_DM", 100_000m)]).DeveTerSucesso();

        resultado.Itens[0].Lmi.ShouldBe(100_000m);
    }

    [Fact]
    public void Deve_ignorar_lmi_enviado_quando_cobertura_tem_lmi_fixo()
    {
        var resultado = Calcular(Cenarios.DadosAutoNeutro(), [.. Cenarios.CoberturasAutoBasicas(), new("VIDROS", 50_000m)]).DeveTerSucesso();

        resultado.Itens.Single(i => i.Codigo == "VIDROS").Lmi.ShouldBe(3_000m);
    }

    [Theory]
    [InlineData("50000", "300.00")] // LMI mínimo: 50.000 × 0,0060
    [InlineData("500000", "3000.00")] // LMI máximo: 500.000 × 0,0060
    public void Deve_aceitar_quando_lmi_nas_bordas_da_faixa(string lmi, string premioRcfDm)
    {
        var resultado = Calcular(Cenarios.DadosAutoNeutro(), [new("CASCO", null), new("RCF_DM", Valor.De(lmi))]).DeveTerSucesso();

        resultado.Itens[1].PremioLiquido.ShouldBe(Valor.De(premioRcfDm));
    }

    [Theory]
    [InlineData("49999.99")]
    [InlineData("500000.01")]
    public void Deve_rejeitar_quando_lmi_fora_da_faixa(string lmi)
    {
        Calcular(Cenarios.DadosAutoNeutro(), [new("CASCO", null), new("RCF_DM", Valor.De(lmi))])
            .DeveFalharCom("Tarifacao.LmiForaDoLimite", ErrorType.Validacao);
    }

    [Fact]
    public void Deve_exigir_lmi_quando_cobertura_por_taxa_sem_lmi_derivado()
    {
        Calcular(Cenarios.DadosAutoNeutro(), [new("CASCO", null), new("RCF_DM", null)]).DeveFalharCom("Tarifacao.LmiObrigatorio");
    }

    [Theory]
    [InlineData("10000", "320.00")] // 10.000 × 0,0320
    [InlineData("500000", "16000.00")] // 500.000 × 0,0320
    public void Deve_aceitar_quando_valor_do_veiculo_nas_bordas(string valorVeiculo, string premioCasco)
    {
        Calcular(Cenarios.DadosAutoNeutro(valorVeiculo: Valor.De(valorVeiculo))).DeveTerSucesso().Itens[0].PremioLiquido.ShouldBe(Valor.De(premioCasco));
    }

    [Theory]
    [InlineData("9999.99")]
    [InlineData("500000.01")]
    public void Deve_rejeitar_quando_valor_do_veiculo_fora_da_faixa(string valorVeiculo)
    {
        Calcular(Cenarios.DadosAutoNeutro(valorVeiculo: Valor.De(valorVeiculo))).DeveFalharCom("Tarifacao.ValorVeiculoForaDoLimite");
    }

    [Theory]
    [InlineData("3014007")]
    [InlineData("301400711")]
    [InlineData("30140-07a")]
    [InlineData("30140_071")]
    [InlineData("")]
    public void Deve_rejeitar_quando_cep_sem_8_digitos(string cep)
    {
        Calcular(Cenarios.DadosAutoNeutro(cep: cep)).DeveFalharCom("Tarifacao.CepInvalido");
    }

    [Fact]
    public void Deve_rejeitar_quando_cep_ausente()
    {
        Calcular(Cenarios.DadosAutoNeutro(cep: null!)).DeveFalharCom("Tarifacao.CepInvalido");
    }

    [Theory]
    [InlineData(17)]
    [InlineData(91)]
    public void Deve_rejeitar_quando_idade_do_condutor_fora_de_18_a_90(int idade)
    {
        Calcular(Cenarios.DadosAutoNeutro(idadeCondutor: idade)).DeveFalharCom("Tarifacao.IdadeCondutorForaDoLimite");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Deve_rejeitar_quando_classe_bonus_fora_de_0_a_10(int classe)
    {
        Calcular(Cenarios.DadosAutoNeutro(classeBonus: classe)).DeveFalharCom("Tarifacao.ClasseBonusInvalida");
    }

    private static Result<ResultadoTarifacao> Calcular(
        DadosRiscoAuto dados,
        IReadOnlyList<CoberturaSolicitada>? coberturas = null,
        DateOnly? dataReferencia = null) =>
        Tarifador.Calcular(CatalogoPadrao.Auto(), Cenarios.SeguradoPadrao(), dados, coberturas ?? Cenarios.CoberturasAutoBasicas(), dataReferencia ?? Cenarios.Hoje);
}
