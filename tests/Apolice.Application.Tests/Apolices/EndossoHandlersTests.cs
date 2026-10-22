using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Features.Apolices.Endossar;
using Apolice.Application.Features.Apolices.SimularEndosso;
using Apolice.Application.Features.Produtos.AtualizarTaxa;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;

namespace Apolice.Application.Tests.Apolices;

public sealed class EndossoHandlersTests : TesteBase
{
    private const int DiasAteEndosso = 73;

    [Fact]
    public async Task Deve_calcular_pro_rata_do_dominio_quando_simula_inclusao()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Avancar(TimeSpan.FromDays(DiasAteEndosso));
        var savesAntes = Amb.Banco.TotalSaves;

        var resultado = await Amb.Servico<SimularEndossoHandler>()
            .HandleAsync(new SimularEndossoCommand(apolice.Id, TipoEndosso.InclusaoCobertura, "vidros", null), Ct);

        // VIDROS 220,00/ano × 292 ÷ 365 = 176,00; IOF 7,38% = 12,99.
        resultado.DeveTerSucesso().ShouldBe(new SimulacaoEndossoDto(new DateOnly(2026, 5, 22), 292, 365, 0m, 220m, 176.00m, 12.99m, 188.99m));
        Amb.Banco.Apolices.Single().Endossos.ShouldBeEmpty();
        Amb.Banco.TotalSaves.ShouldBe(savesAntes);
    }

    [Fact]
    public async Task Deve_gravar_endosso_auditar_e_devolver_apolice_quando_endossa()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Avancar(TimeSpan.FromDays(DiasAteEndosso));

        var resultado = await Amb.Servico<EndossarApoliceHandler>()
            .HandleAsync(new EndossarApoliceCommand(apolice.Id, TipoEndosso.InclusaoCobertura, CodigosCobertura.Vidros, null), Ct);

        var endossada = resultado.DeveTerSucesso();
        var endosso = endossada.Endossos.ShouldHaveSingleItem();
        endosso.Numero.ShouldBe(1);
        endosso.CoberturaNome.ShouldBe("Vidros, faróis e retrovisores");
        endosso.CriadoPorNome.ShouldBe("Ana Ribeiro");
        endosso.AjusteTotal.ShouldBe(188.99m);
        endossada.AjustesEndosso.ShouldBe(176.00m);
        endossada.PremioLiquidoVigente.ShouldBe(3_976.00m);
        endossada.Coberturas.Select(c => c.Codigo).ShouldContain(CodigosCobertura.Vidros);
        Amb.Auditoria.Registros.Last().Acao.ShouldBe(AcoesAuditoria.ApoliceEndossada);
        Amb.Auditoria.Registros.Last().Detalhes!.ShouldContain("\"tipo\":\"InclusaoCobertura\"");
        Amb.Banco.Outbox.OfType<ApoliceEndossada>().ShouldHaveSingleItem().AjusteTotal.ShouldBe(188.99m);
    }

    // SPEC 4.4: o novo prêmio anual sai do produto ATUAL. RCF_DM sobe de 0,60% para 0,80%: 200.000 × 0,008 = 1.600/ano;
    // efeito no início da vigência (365/365) → ajuste líquido 1.600 − 600 = 1.000.
    [Fact]
    public async Task Deve_retarifar_com_a_taxa_atual_do_produto_quando_altera_lmi()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Como(Amb.Diego);
        (await Amb.Servico<AtualizarTaxaCoberturaHandler>().HandleAsync(new AtualizarTaxaCoberturaCommand("AUTO", "RCF_DM", 0.0080m), Ct)).DeveTerSucesso();
        Amb.Como(Amb.Ana);

        var simulacao = (await Amb.Servico<SimularEndossoHandler>()
            .HandleAsync(new SimularEndossoCommand(apolice.Id, TipoEndosso.AlteracaoLmi, CodigosCobertura.RcfDanosMateriais, 200_000m), Ct))
            .DeveTerSucesso();
        var endossada = (await Amb.Servico<EndossarApoliceHandler>()
            .HandleAsync(new EndossarApoliceCommand(apolice.Id, TipoEndosso.AlteracaoLmi, CodigosCobertura.RcfDanosMateriais, 200_000m), Ct))
            .DeveTerSucesso();

        simulacao.PremioAnualAnterior.ShouldBe(600m);
        simulacao.PremioAnualNovo.ShouldBe(1_600m);
        simulacao.AjusteLiquido.ShouldBe(1_000m);
        endossada.Endossos.ShouldHaveSingleItem().AjusteLiquido.ShouldBe(1_000m);
        endossada.Coberturas.Single(c => c.Codigo == CodigosCobertura.RcfDanosMateriais).PremioLiquido.ShouldBe(1_600m);
    }

    // Taxa alterada por outra instância da API: o cache desta ainda tem a antiga, mas o endosso lê o cadastro.
    [Fact]
    public async Task Deve_retarifar_pelo_cadastro_e_nao_pelo_cache_quando_cache_esta_defasado()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Catalogo.Aquecer();
        Amb.Banco.Produtos.Single(p => p.Codigo == CodigosProduto.Auto).AtualizarTaxa(CodigosCobertura.RcfDanosMateriais, 0.0080m, Amb.Agora).IsSuccess.ShouldBeTrue();

        var simulacao = (await Amb.Servico<SimularEndossoHandler>()
            .HandleAsync(new SimularEndossoCommand(apolice.Id, TipoEndosso.AlteracaoLmi, CodigosCobertura.RcfDanosMateriais, 200_000m), Ct))
            .DeveTerSucesso();

        simulacao.PremioAnualNovo.ShouldBe(1_600m);
        (await Amb.Catalogo.ObterAsync(CodigosProduto.Auto, Ct))!.ObterCobertura(CodigosCobertura.RcfDanosMateriais)!.Taxa.ShouldBe(0.0060m);
    }

    [Theory]
    [InlineData(TipoEndosso.ExclusaoCobertura, CodigosCobertura.Casco, null, "Apolice.CoberturaObrigatoria", ErrorType.RegraDeNegocio)]
    [InlineData(TipoEndosso.InclusaoCobertura, CodigosCobertura.Casco, null, "Apolice.CoberturaJaContratada", ErrorType.RegraDeNegocio)]
    [InlineData(TipoEndosso.ExclusaoCobertura, null, null, "Apolice.CoberturaNaoContratada", ErrorType.RegraDeNegocio)]
    [InlineData(TipoEndosso.AlteracaoLmi, CodigosCobertura.RcfDanosMateriais, 100_000.0, "Apolice.LmiInalterado", ErrorType.Validacao)]
    [InlineData(TipoEndosso.InclusaoCobertura, CodigosCobertura.RcfDanosCorporais, null, "Tarifacao.LmiObrigatorio", ErrorType.Validacao)]
    public async Task Deve_propagar_erro_do_dominio_quando_endosso_invalido(TipoEndosso tipo, string? cobertura, double? lmi, string codigo, ErrorType erro)
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);

        var resultado = await Amb.Servico<EndossarApoliceHandler>()
            .HandleAsync(new EndossarApoliceCommand(apolice.Id, tipo, cobertura!, (decimal?)lmi), Ct);

        resultado.DeveFalharCom(codigo, erro);
        Amb.Banco.Apolices.Single().Endossos.ShouldBeEmpty();
    }

    [Fact]
    public async Task Deve_propagar_erro_do_dominio_quando_simulacao_de_endosso_invalida()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);

        var resultado = await Amb.Servico<SimularEndossoHandler>()
            .HandleAsync(new SimularEndossoCommand(apolice.Id, TipoEndosso.ExclusaoCobertura, CodigosCobertura.Casco, null), Ct);

        resultado.DeveFalharCom("Apolice.CoberturaObrigatoria", ErrorType.RegraDeNegocio);
    }

    [Fact]
    public async Task Deve_proibir_analista_quando_simula_ou_endossa()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Como(Amb.Carla);

        var simular = await Amb.Servico<SimularEndossoHandler>().HandleAsync(new SimularEndossoCommand(apolice.Id, TipoEndosso.InclusaoCobertura, CodigosCobertura.Vidros, null), Ct);
        var endossar = await Amb.Servico<EndossarApoliceHandler>().HandleAsync(new EndossarApoliceCommand(apolice.Id, TipoEndosso.InclusaoCobertura, CodigosCobertura.Vidros, null), Ct);

        simular.DeveFalharCom("Comum.Proibido", ErrorType.Proibido);
        endossar.DeveFalharCom("Comum.Proibido", ErrorType.Proibido);
    }

    [Fact]
    public async Task Deve_retornar_desabilitada_quando_flag_endosso_online_desligada()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Flags.Desligar(FeatureFlags.EndossoOnline);

        var simular = await Amb.Servico<SimularEndossoHandler>().HandleAsync(new SimularEndossoCommand(apolice.Id, TipoEndosso.InclusaoCobertura, CodigosCobertura.Vidros, null), Ct);
        var endossar = await Amb.Servico<EndossarApoliceHandler>().HandleAsync(new EndossarApoliceCommand(apolice.Id, TipoEndosso.InclusaoCobertura, CodigosCobertura.Vidros, null), Ct);

        simular.DeveFalharCom("Comum.FuncionalidadeDesabilitada", ErrorType.NaoEncontrado);
        endossar.DeveFalharCom("Comum.FuncionalidadeDesabilitada", ErrorType.NaoEncontrado);
    }

    [Fact]
    public async Task Deve_retornar_nao_encontrado_quando_produto_saiu_do_cadastro()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Banco.Produtos.RemoveAll(p => p.Codigo == CodigosProduto.Auto);

        var resultado = await Amb.Servico<SimularEndossoHandler>()
            .HandleAsync(new SimularEndossoCommand(apolice.Id, TipoEndosso.InclusaoCobertura, CodigosCobertura.Vidros, null), Ct);

        resultado.DeveFalharCom("Comum.NaoEncontrado", ErrorType.NaoEncontrado);
    }

    [Fact]
    public async Task Deve_retornar_status_invalido_quando_endossa_apolice_cancelada()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Banco.Apolices.Single().Cancelar(MotivoCancelamento.PedidoDoSegurado, Amb.Agora);

        var resultado = await Amb.Servico<EndossarApoliceHandler>()
            .HandleAsync(new EndossarApoliceCommand(apolice.Id, TipoEndosso.InclusaoCobertura, CodigosCobertura.Vidros, null), Ct);

        resultado.DeveFalharCom("Apolice.StatusInvalido", ErrorType.RegraDeNegocio);
    }

    [Fact]
    public async Task Deve_propagar_o_conflito_sem_gravar_endosso_nem_evento()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Banco.Outbox.Clear();
        Amb.Banco.FalharProximoSaveComConflito = true;

        await Should.ThrowAsync<ConflitoDeConcorrenciaException>(() => Amb.Servico<EndossarApoliceHandler>()
            .HandleAsync(new EndossarApoliceCommand(apolice.Id, TipoEndosso.InclusaoCobertura, CodigosCobertura.Vidros, null), Ct));

        Amb.Banco.Apolices.Single().Endossos.ShouldBeEmpty();
        Amb.Banco.Outbox.ShouldBeEmpty();
        Amb.Auditoria.Acoes.ShouldNotContain(AcoesAuditoria.ApoliceEndossada);
    }
}
