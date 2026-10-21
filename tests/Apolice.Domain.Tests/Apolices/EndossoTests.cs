using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Apolices;

// Apólice AUTO emitida em 10/03/2026 (vigência 10/03/2026 → 10/03/2027 = 365 dias) com
// CASCO 100.000 (3.200,00) + RCF_DM 100.000 (600,00) + ASSISTENCIA_24H (120,00) = 3.920,00 líquido.
// Endosso em 10/09/2026: faltam 181 dias (30+31+30+31+31+28).
public sealed class EndossoTests
{
    private static readonly DateTimeOffset DiaDoEndosso = Cenarios.Em(2026, 9, 10, hora: 14);
    private static readonly DateOnly DataDoEndosso = new(2026, 9, 10);
    private static readonly Tarifador Tarifador = Tarifador.Padrao();

    [Fact]
    public void Deve_cobrar_pro_rata_e_atualizar_o_item_quando_aumenta_lmi()
    {
        var apolice = ApoliceComAssistencia();

        var endosso = apolice.Endossar(new(TipoEndosso.AlteracaoLmi, "RCF_DM", 200_000m), CatalogoPadrao.Auto(), Tarifador, Cenarios.AnalistaId, DiaDoEndosso)
            .DeveTerSucesso();

        // Novo anual 200.000 × 0,0060 = 1.200; ajuste = (1.200 − 600) × 181 / 365 = 297,5342… → 297,53
        endosso.Numero.ShouldBe(1);
        endosso.ApoliceId.ShouldBe(apolice.Id);
        endosso.Tipo.ShouldBe(TipoEndosso.AlteracaoLmi);
        (endosso.CoberturaCodigo, endosso.CoberturaNome).ShouldBe(("RCF_DM", "RC danos materiais a terceiros"));
        (endosso.LmiAnterior, endosso.LmiNovo).ShouldBe(((decimal?)100_000m, (decimal?)200_000m));
        endosso.DataEfeito.ShouldBe(DataDoEndosso);
        (endosso.PremioAnualAnterior, endosso.PremioAnualNovo).ShouldBe((600.00m, 1_200.00m));
        endosso.AjusteLiquido.ShouldBe(297.53m);
        endosso.IofAjuste.ShouldBe(21.96m); // 297,53 × 0,0738 = 21,957714
        endosso.AjusteTotal.ShouldBe(319.49m);
        endosso.CriadoEm.ShouldBe(DiaDoEndosso);
        endosso.CriadoPorId.ShouldBe(Cenarios.AnalistaId);
        endosso.CoberturaPermiteSinistro.ShouldBeTrue();

        var item = apolice.ObterCobertura("RCF_DM")!;
        (item.Lmi, item.PremioLiquido).ShouldBe((200_000m, 1_200.00m));
        apolice.PremioLiquido.ShouldBe(3_920.00m); // o prêmio da emissão não muda
        apolice.AjustesEndosso.ShouldBe(297.53m);
        apolice.PremioLiquidoVigente.ShouldBe(4_217.53m); // 3.920 + 297,53
    }

    [Fact]
    public void Deve_levantar_apolice_endossada_quando_endossa()
    {
        var apolice = ApoliceComAssistencia();
        apolice.ClearDomainEvents();

        apolice.Endossar(new(TipoEndosso.AlteracaoLmi, "RCF_DM", 200_000m), CatalogoPadrao.Auto(), Tarifador, Cenarios.AnalistaId, DiaDoEndosso);

        var evento = apolice.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ApoliceEndossada>();
        (evento.ApoliceId, evento.Numero, evento.EndossoNumero).ShouldBe((apolice.Id, Cenarios.NumeroApolice, 1));
        (evento.Tipo, evento.CoberturaCodigo, evento.AjusteTotal).ShouldBe((TipoEndosso.AlteracaoLmi, "RCF_DM", 319.49m));
        (evento.SeguradoNome, evento.SeguradoEmail, evento.OcorridoEm).ShouldBe(("Maria Silva", "maria.silva@exemplo.com", DiaDoEndosso));
    }

    [Fact]
    public void Deve_cobrar_pro_rata_do_premio_fixo_quando_inclui_cobertura()
    {
        var apolice = ApoliceComAssistencia();

        var endosso = apolice.Endossar(new(TipoEndosso.InclusaoCobertura, "VIDROS"), CatalogoPadrao.Auto(), Tarifador, Cenarios.AnalistaId, DiaDoEndosso)
            .DeveTerSucesso();

        // (220 − 0) × 181 / 365 = 109,0958… → 109,10; IOF 109,10 × 0,0738 = 8,05158 → 8,05
        (endosso.LmiAnterior, endosso.LmiNovo).ShouldBe(((decimal?)null, (decimal?)3_000m));
        (endosso.PremioAnualAnterior, endosso.PremioAnualNovo).ShouldBe((0m, 220.00m));
        (endosso.AjusteLiquido, endosso.IofAjuste, endosso.AjusteTotal).ShouldBe((109.10m, 8.05m, 117.15m));
        endosso.CoberturaPermiteSinistro.ShouldBeTrue();
        var vidros = apolice.ObterCobertura("VIDROS")!;
        (vidros.Nome, vidros.Lmi, vidros.PremioLiquido, vidros.Obrigatoria, vidros.PermiteSinistro)
            .ShouldBe(("Vidros, faróis e retrovisores", 3_000m, 220.00m, false, true));
    }

    [Fact]
    public void Deve_devolver_pro_rata_e_remover_o_item_quando_exclui_cobertura()
    {
        var apolice = ApoliceComAssistencia();

        var endosso = apolice.Endossar(new(TipoEndosso.ExclusaoCobertura, "ASSISTENCIA_24H"), CatalogoPadrao.Auto(), Tarifador, Cenarios.AnalistaId, DiaDoEndosso)
            .DeveTerSucesso();

        // (0 − 120) × 181 / 365 = −59,5068… → −59,51; IOF −59,51 × 0,0738 = −4,391838 → −4,39
        (endosso.LmiAnterior, endosso.LmiNovo).ShouldBe(((decimal?)0m, (decimal?)null));
        endosso.CoberturaNome.ShouldBe("Assistência 24h");
        endosso.CoberturaPermiteSinistro.ShouldBeFalse(); // guardado para reconstituir o item antes do endosso
        (endosso.AjusteLiquido, endosso.IofAjuste, endosso.AjusteTotal).ShouldBe((-59.51m, -4.39m, -63.90m));
        apolice.ObterCobertura("ASSISTENCIA_24H").ShouldBeNull();
        apolice.Coberturas.Count.ShouldBe(2);
    }

    [Fact]
    public void Deve_numerar_em_sequencia_e_somar_ajustes_quando_varios_endossos()
    {
        var apolice = ApoliceComAssistencia();

        apolice.Endossar(new(TipoEndosso.AlteracaoLmi, "RCF_DM", 200_000m), CatalogoPadrao.Auto(), Tarifador, Cenarios.AnalistaId, DiaDoEndosso);
        apolice.Endossar(new(TipoEndosso.ExclusaoCobertura, "ASSISTENCIA_24H"), CatalogoPadrao.Auto(), Tarifador, Cenarios.AnalistaId, DiaDoEndosso);

        apolice.Endossos.Select(e => e.Numero).ShouldBe([1, 2]);
        apolice.AjustesEndosso.ShouldBe(238.02m); // 297,53 − 59,51
        apolice.PremioLiquidoVigente.ShouldBe(4_158.02m); // 3.920 + 238,02
    }

    [Fact]
    public void Deve_usar_a_taxa_atual_do_produto_quando_taxa_mudou_apos_a_emissao()
    {
        var apolice = ApoliceComAssistencia();
        var produto = CatalogoPadrao.Auto();
        produto.AtualizarTaxa("RCF_DM", 0.0070m, Cenarios.Agora);

        var simulacao = apolice.SimularEndosso(new(TipoEndosso.AlteracaoLmi, "RCF_DM", 200_000m), produto, Tarifador, DataDoEndosso).DeveTerSucesso();

        // Novo anual 200.000 × 0,0070 = 1.400; (1.400 − 600) × 181 / 365 = 396,7123… → 396,71; IOF 29,277198 → 29,28
        (simulacao.PremioAnualAnterior, simulacao.PremioAnualNovo).ShouldBe((600.00m, 1_400.00m));
        (simulacao.AjusteLiquido, simulacao.IofAjuste, simulacao.AjusteTotal).ShouldBe((396.71m, 29.28m, 425.99m));
    }

    [Fact]
    public void Deve_tarifar_pela_data_de_emissao_quando_segurado_mudou_de_faixa_depois()
    {
        // Nascido em 11/03/1985: 40 anos na emissão (10/03/2026, faixa ×1,50) e 41 no endosso (faixa ×3,00).
        var apolice = ApoliceVida(new DateOnly(1985, 3, 11), [new("MORTE", 200_000m)]);

        var endosso = apolice.Endossar(new(TipoEndosso.AlteracaoLmi, "MORTE", 300_000m), CatalogoPadrao.Vida(), Tarifador, Cenarios.AnalistaId, DiaDoEndosso)
            .DeveTerSucesso();

        // SPEC 4.4: dataReferencia = emissão → 300.000 × 0,80/1.000 × 1,50 = 360,00 (pela data do endosso seria 720,00).
        // (360 − 240) × 181 / 365 = 59,5068… → 59,51; IOF 59,51 × 0,0038 = 0,226138 → 0,23
        (endosso.PremioAnualAnterior, endosso.PremioAnualNovo).ShouldBe((240.00m, 360.00m));
        (endosso.AjusteLiquido, endosso.IofAjuste, endosso.AjusteTotal).ShouldBe((59.51m, 0.23m, 59.74m));
    }

    [Fact]
    public void Deve_aceitar_veiculo_da_janela_da_emissao_quando_endosso_ja_e_no_ano_seguinte()
    {
        // Veículo 2006 emitido em 20/12/2026 (janela 2006–2027, agravo de mais de 10 anos ×1,10). Em 2027 a
        // janela seria 2007–2028: tarifar pela data do endosso recusaria o veículo da própria apólice.
        var apolice = ApoliceAutoAntiga(Cenarios.Em(2026, 12, 20));

        var endosso = apolice.Endossar(new(TipoEndosso.AlteracaoLmi, "RCF_DM", 200_000m), CatalogoPadrao.Auto(), Tarifador, Cenarios.AnalistaId, Cenarios.Em(2027, 1, 10))
            .DeveTerSucesso();

        // RCF_DM 660,00 → 1.320,00; restam 344 de 365 dias: 660 × 344 / 365 = 622,0273… → 622,03; IOF 45,905814 → 45,91
        (endosso.PremioAnualAnterior, endosso.PremioAnualNovo).ShouldBe((660.00m, 1_320.00m));
        (endosso.DataEfeito, endosso.AjusteLiquido, endosso.IofAjuste, endosso.AjusteTotal)
            .ShouldBe((new DateOnly(2027, 1, 10), 622.03m, 45.91m, 667.94m));
    }

    [Fact]
    public void Deve_usar_366_dias_no_pro_rata_quando_vigencia_atravessa_29_de_fevereiro()
    {
        // Vigência 10/03/2027 → 10/03/2028 (366 dias); endosso em 10/09/2027 com 182 dias restantes.
        var apolice = Cenarios.ApoliceAuto(Cenarios.Em(2027, 3, 1), new DateOnly(2027, 3, 10));

        var endosso = apolice.Endossar(new(TipoEndosso.AlteracaoLmi, "RCF_DM", 200_000m), CatalogoPadrao.Auto(), Tarifador, Cenarios.AnalistaId, Cenarios.Em(2027, 9, 10))
            .DeveTerSucesso();
        var simulacao = Cenarios.ApoliceAuto(Cenarios.Em(2027, 3, 1), new DateOnly(2027, 3, 10))
            .SimularEndosso(new(TipoEndosso.AlteracaoLmi, "RCF_DM", 200_000m), CatalogoPadrao.Auto(), Tarifador, new DateOnly(2027, 9, 10))
            .DeveTerSucesso();

        // 600 × 182 / 366 = 298,3606… → 298,36; IOF 298,36 × 0,0738 = 22,018968 → 22,02
        (simulacao.DiasRestantes, simulacao.DiasVigencia).ShouldBe((182, 366));
        (endosso.AjusteLiquido, endosso.IofAjuste, endosso.AjusteTotal).ShouldBe((298.36m, 22.02m, 320.38m));
    }

    [Fact]
    public void Deve_usar_o_dia_de_brasilia_quando_endossa_as_23h30_do_penultimo_dia()
    {
        // 02:30Z de 10/03/2027 = 23:30 de 09/03 em Brasília: efeito 09/03, 1 dia restante (em UTC seria o fim → recusado).
        var apolice = ApoliceComAssistencia();

        var endosso = apolice.Endossar(new(TipoEndosso.AlteracaoLmi, "RCF_DM", 200_000m), CatalogoPadrao.Auto(), Tarifador, Cenarios.AnalistaId, new DateTimeOffset(2027, 3, 10, 2, 30, 0, TimeSpan.Zero))
            .DeveTerSucesso();

        // 600 × 1 / 365 = 1,6438… → 1,64; IOF 1,64 × 0,0738 = 0,121032 → 0,12
        endosso.DataEfeito.ShouldBe(new DateOnly(2027, 3, 9));
        (endosso.AjusteLiquido, endosso.IofAjuste, endosso.AjusteTotal).ShouldBe((1.64m, 0.12m, 1.76m));
    }

    [Fact]
    public void Deve_manter_a_apolice_intacta_quando_apenas_simula()
    {
        var apolice = ApoliceComAssistencia();
        apolice.ClearDomainEvents();

        var simulacao = apolice.SimularEndosso(new(TipoEndosso.AlteracaoLmi, "RCF_DM", 200_000m), CatalogoPadrao.Auto(), Tarifador, DataDoEndosso)
            .DeveTerSucesso();

        (simulacao.DataEfeito, simulacao.DiasRestantes, simulacao.DiasVigencia).ShouldBe((DataDoEndosso, 181, 365));
        simulacao.AjusteTotal.ShouldBe(319.49m);
        apolice.Endossos.ShouldBeEmpty();
        apolice.DomainEvents.ShouldBeEmpty();
        apolice.ObterCobertura("RCF_DM")!.Lmi.ShouldBe(100_000m);
    }

    [Fact]
    public void Deve_ter_efeito_no_inicio_e_cobrar_o_ano_inteiro_quando_apolice_programada()
    {
        // Emitida em 10/03 com início em 20/03; endosso no próprio dia 10/03 → efeito 20/03, 365 dias restantes.
        var apolice = Cenarios.ApoliceAuto(Cenarios.Agora, new DateOnly(2026, 3, 20));

        var simulacao = apolice.SimularEndosso(new(TipoEndosso.AlteracaoLmi, "RCF_DM", 200_000m), CatalogoPadrao.Auto(), Tarifador, Cenarios.Hoje)
            .DeveTerSucesso();

        // (1.200 − 600) × 365 / 365 = 600,00; IOF 600 × 0,0738 = 44,28
        simulacao.DataEfeito.ShouldBe(new DateOnly(2026, 3, 20));
        (simulacao.AjusteLiquido, simulacao.IofAjuste, simulacao.AjusteTotal).ShouldBe((600.00m, 44.28m, 644.28m));
    }

    [Fact]
    public void Deve_negar_quando_efeito_seria_o_ultimo_dia_de_vigencia()
    {
        ApoliceComAssistencia().SimularEndosso(new(TipoEndosso.AlteracaoLmi, "RCF_DM", 200_000m), CatalogoPadrao.Auto(), Tarifador, new DateOnly(2027, 3, 10))
            .DeveFalharCom("Apolice.EndossoForaDaVigencia", ErrorType.RegraDeNegocio);
    }

    [Fact]
    public void Deve_negar_quando_apolice_vencida_ou_cancelada()
    {
        var apolice = ApoliceComAssistencia();
        var solicitacao = new SolicitacaoEndosso(TipoEndosso.AlteracaoLmi, "RCF_DM", 200_000m);

        apolice.SimularEndosso(solicitacao, CatalogoPadrao.Auto(), Tarifador, new DateOnly(2027, 3, 11))
            .DeveFalharCom("Apolice.StatusInvalido", ErrorType.RegraDeNegocio);

        apolice.Cancelar(MotivoCancelamento.PedidoDoSegurado, Cenarios.Em(2026, 6, 1));
        apolice.Endossar(solicitacao, CatalogoPadrao.Auto(), Tarifador, Cenarios.AnalistaId, DiaDoEndosso)
            .DeveFalharCom("Apolice.StatusInvalido");
    }

    [Theory]
    [InlineData(TipoEndosso.ExclusaoCobertura, "CASCO", "Apolice.CoberturaObrigatoria")]
    [InlineData(TipoEndosso.InclusaoCobertura, "RCF_DM", "Apolice.CoberturaJaContratada")]
    [InlineData(TipoEndosso.AlteracaoLmi, "RCF_DC", "Apolice.CoberturaNaoContratada")]
    [InlineData(TipoEndosso.ExclusaoCobertura, "VIDROS", "Apolice.CoberturaNaoContratada")]
    [InlineData((TipoEndosso)99, "RCF_DM", "Apolice.TipoEndossoInvalido")]
    public void Deve_negar_quando_regra_de_endosso_violada(TipoEndosso tipo, string cobertura, string erro)
    {
        ApoliceComAssistencia().SimularEndosso(new(tipo, cobertura, 200_000m), CatalogoPadrao.Auto(), Tarifador, DataDoEndosso)
            .DeveFalharCom(erro);
    }

    [Theory]
    [InlineData("CASCO", "150000")] // LMI do casco vem do valor do veículo
    [InlineData("RCF_DM", "100000")] // mesmo LMI
    public void Deve_negar_quando_lmi_nao_muda(string cobertura, string lmi)
    {
        ApoliceComAssistencia().SimularEndosso(new(TipoEndosso.AlteracaoLmi, cobertura, Valor.De(lmi)), CatalogoPadrao.Auto(), Tarifador, DataDoEndosso)
            .DeveFalharCom("Apolice.LmiInalterado", ErrorType.Validacao);
    }

    [Fact]
    public void Deve_propagar_erro_da_tarifacao_quando_conjunto_alterado_e_invalido()
    {
        var apolice = ApoliceComAssistencia();

        apolice.SimularEndosso(new(TipoEndosso.AlteracaoLmi, "RCF_DM", 600_000m), CatalogoPadrao.Auto(), Tarifador, DataDoEndosso)
            .DeveFalharCom("Tarifacao.LmiForaDoLimite");
        apolice.SimularEndosso(new(TipoEndosso.InclusaoCobertura, "XYZ"), CatalogoPadrao.Auto(), Tarifador, DataDoEndosso)
            .DeveFalharCom("Tarifacao.CoberturaInexistente");
    }

    [Fact]
    public void Deve_revalidar_ipa_contra_morte_quando_reduz_morte_na_apolice_de_vida()
    {
        var apolice = ApoliceVida();

        // IPA 100.000 continua no conjunto: MORTE 50.000 deixaria IPA > MORTE.
        apolice.SimularEndosso(new(TipoEndosso.AlteracaoLmi, "MORTE", 50_000m), CatalogoPadrao.Vida(), Tarifador, DataDoEndosso)
            .DeveFalharCom("Tarifacao.LmiIpaAcimaDaMorte");
    }

    [Fact]
    public void Deve_revalidar_ipa_contra_morte_quando_inclui_ipa_por_endosso()
    {
        var apolice = ApoliceVida(new DateOnly(1990, 3, 11), [new("MORTE", 50_000m)]);

        apolice.SimularEndosso(new(TipoEndosso.InclusaoCobertura, "IPA", 50_000.01m), CatalogoPadrao.Vida(), Tarifador, DataDoEndosso)
            .DeveFalharCom("Tarifacao.LmiIpaAcimaDaMorte");
        apolice.SimularEndosso(new(TipoEndosso.InclusaoCobertura, "IPA", 50_000m), CatalogoPadrao.Vida(), Tarifador, DataDoEndosso)
            .DeveTerSucesso();
    }

    [Fact]
    public void Deve_revalidar_incendio_contra_valor_do_imovel_quando_aumenta_lmi_por_endosso()
    {
        var produto = CatalogoPadrao.Residencial();
        var dados = new DadosRiscoResidencial(200_000m, TipoImovel.Casa, "40000-000", Habitual: true);
        var tarifacao = Tarifador.Calcular(produto, Cenarios.SeguradoPadrao(), dados, [new("INCENDIO", 150_000m)], Cenarios.Hoje).Value;
        var cotacao = Cotacao.Criar(Cenarios.CorretorId, produto, Cenarios.SeguradoPadrao(), dados, tarifacao, Cenarios.Agora);
        cotacao.Aceitar(Cenarios.Agora);
        var apolice = ApoliceSeguro.Emitir(cotacao, "RESID-2026-000001", Cenarios.Hoje, 1, Cenarios.Agora).Value;

        apolice.SimularEndosso(new(TipoEndosso.AlteracaoLmi, "INCENDIO", 200_000.01m), produto, Tarifador, DataDoEndosso)
            .DeveFalharCom("Tarifacao.LmiAcimaDoValorImovel");
        apolice.SimularEndosso(new(TipoEndosso.AlteracaoLmi, "INCENDIO", 200_000m), produto, Tarifador, DataDoEndosso)
            .DeveTerSucesso();
    }

    [Theory]
    [InlineData(TipoEndosso.InclusaoCobertura, "VIDROS")] // o tarifador "esqueceu" o item incluído
    [InlineData(TipoEndosso.ExclusaoCobertura, "ASSISTENCIA_24H")] // o tarifador devolveu o item excluído
    public void Deve_lancar_quando_tarifador_devolve_conjunto_diferente_do_pedido(TipoEndosso tipo, string cobertura)
    {
        var apolice = ApoliceComAssistencia();
        var original = Tarifador.Calcular(CatalogoPadrao.Auto(), Cenarios.SeguradoPadrao(), Cenarios.DadosAutoNeutro(), Cenarios.CoberturasAutoComAssistencia(), Cenarios.Hoje).Value;

        Should.Throw<InvalidOperationException>(() =>
            apolice.SimularEndosso(new(tipo, cobertura), CatalogoPadrao.Auto(), new TarifadorFixo(original), DataDoEndosso));
    }

    [Fact]
    public void Deve_lancar_quando_produto_de_outra_linha_ou_usuario_vazio()
    {
        var apolice = ApoliceComAssistencia();
        var solicitacao = new SolicitacaoEndosso(TipoEndosso.AlteracaoLmi, "RCF_DM", 200_000m);

        Should.Throw<ArgumentException>(() => apolice.SimularEndosso(solicitacao, CatalogoPadrao.Vida(), Tarifador, DataDoEndosso));
        Should.Throw<ArgumentException>(() => apolice.Endossar(solicitacao, CatalogoPadrao.Auto(), Tarifador, Guid.Empty, DiaDoEndosso));
        Should.Throw<ArgumentNullException>(() => apolice.SimularEndosso(null!, CatalogoPadrao.Auto(), Tarifador, DataDoEndosso));
    }

    private static ApoliceSeguro ApoliceComAssistencia() =>
        Cenarios.ApoliceAuto(Cenarios.Agora, coberturas: Cenarios.CoberturasAutoComAssistencia());

    private static ApoliceSeguro ApoliceVida() =>
        ApoliceVida(new DateOnly(1990, 3, 11), [new("MORTE", 200_000m), new("IPA", 100_000m)]);

    private static ApoliceSeguro ApoliceVida(DateOnly nascimento, IReadOnlyList<CoberturaSolicitada> coberturas)
    {
        var produto = CatalogoPadrao.Vida();
        var segurado = Cenarios.SeguradoPadrao(nascimento);
        var dados = new DadosRiscoVida(false);
        var tarifacao = Tarifador.Calcular(produto, segurado, dados, coberturas, Cenarios.Hoje).Value;
        var cotacao = Cotacao.Criar(Cenarios.CorretorId, produto, segurado, dados, tarifacao, Cenarios.Agora);
        cotacao.Aceitar(Cenarios.Agora);
        return ApoliceSeguro.Emitir(cotacao, "VIDA-2026-000001", Cenarios.Hoje, 1, Cenarios.Agora).Value;
    }

    private static ApoliceSeguro ApoliceAutoAntiga(DateTimeOffset emitidaEm)
    {
        var produto = CatalogoPadrao.Auto();
        var dados = Cenarios.DadosAutoNeutro(anoFabricacao: 2006);
        var hoje = Relogio.HojeEm(emitidaEm);
        var tarifacao = Tarifador.Calcular(produto, Cenarios.SeguradoPadrao(), dados, Cenarios.CoberturasAutoBasicas(), hoje).Value;
        var cotacao = Cotacao.Criar(Cenarios.CorretorId, produto, Cenarios.SeguradoPadrao(), dados, tarifacao, emitidaEm);
        cotacao.Aceitar(emitidaEm);
        return ApoliceSeguro.Emitir(cotacao, Cenarios.NumeroApolice, hoje, 1, emitidaEm).Value;
    }

    private sealed class TarifadorFixo(ResultadoTarifacao resultado) : ITarifador
    {
        public Result<ResultadoTarifacao> Calcular(
            Produto produto,
            Segurado segurado,
            DadosRisco dadosRisco,
            IReadOnlyList<CoberturaSolicitada> coberturas,
            DateOnly dataReferencia) => resultado;
    }
}
