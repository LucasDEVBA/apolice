using Apolice.Application.Common;
using Apolice.Application.Features.Cotacoes;
using Apolice.Application.Features.Cotacoes.Simular;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;

namespace Apolice.Application.Tests.Cotacoes;

public sealed class SimularCotacaoHandlerTests : TesteBase
{
    private SimularCotacaoHandler Handler => Amb.Servico<SimularCotacaoHandler>();

    [Fact]
    public async Task Deve_devolver_a_tarifacao_do_dominio_sem_gravar_quando_simula()
    {
        var savesAntes = Amb.Banco.TotalSaves;

        var resultado = await Handler.HandleAsync(new SimularCotacaoCommand(Dados.CotacaoAuto()), Ct);

        var tarifacao = resultado.DeveTerSucesso();
        tarifacao.PremioLiquido.ShouldBe(Dados.PremioLiquidoAuto);
        tarifacao.Iof.ShouldBe(Dados.IofAuto);
        tarifacao.PremioTotal.ShouldBe(Dados.PremioTotalAuto);
        tarifacao.AliquotaIof.ShouldBe(0.0738m);
        tarifacao.MaxParcelas.ShouldBe(10);
        tarifacao.Itens.ShouldBe(
        [
            new ItemTarifadoDto(CodigosCobertura.Casco, "Colisão, incêndio e roubo", 100_000m, 3_200m, true, true),
            new ItemTarifadoDto(CodigosCobertura.RcfDanosMateriais, "RC danos materiais a terceiros", 100_000m, 600m, true, true),
        ]);
        tarifacao.Fatores.ShouldBeEmpty();
        Amb.Banco.TotalSaves.ShouldBe(savesAntes);
        Amb.Banco.Cotacoes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Deve_simular_igual_quando_codigos_vem_em_minusculas()
    {
        var request = Dados.CotacaoAuto() with { ProdutoCodigo = "auto", Coberturas = [new("casco", null), new("rcf_dm", 100_000m)] };

        var tarifacao = (await Handler.HandleAsync(new SimularCotacaoCommand(request), Ct)).DeveTerSucesso();

        tarifacao.PremioTotal.ShouldBe(Dados.PremioTotalAuto);
        tarifacao.Itens.Select(i => i.Codigo).ShouldBe([CodigosCobertura.Casco, CodigosCobertura.RcfDanosMateriais]);
    }

    [Fact]
    public async Task Deve_listar_fatores_aplicados_quando_risco_tem_agravo()
    {
        var request = Dados.CotacaoAuto() with { DadosRisco = new DadosRiscoAuto(100_000m, 2024, "01310-100", 22, false, 0) };

        var tarifacao = (await Handler.HandleAsync(new SimularCotacaoCommand(request), Ct)).DeveTerSucesso();

        tarifacao.Fatores.ShouldBe([new FatorDto("Idade do condutor: 18 a 25 anos", 1.40m), new FatorDto("Região de pernoite: Grande São Paulo", 1.25m)]);
    }

    [Theory]
    [MemberData(nameof(PedidosInvalidos))]
    public async Task Deve_retornar_erro_previsivel_quando_pedido_invalido(CotacaoRequest request, string codigo, ErrorType tipo)
    {
        var resultado = await Handler.HandleAsync(new SimularCotacaoCommand(request), Ct);

        resultado.DeveFalharCom(codigo, tipo);
    }

    public static TheoryData<CotacaoRequest, string, ErrorType> PedidosInvalidos() => new()
    {
        { Dados.CotacaoAuto() with { Segurado = null! }, "Comum.DadosObrigatorios", ErrorType.Validacao },
        { Dados.CotacaoAuto() with { DadosRisco = null! }, "Comum.DadosObrigatorios", ErrorType.Validacao },
        { Dados.CotacaoAuto() with { Coberturas = null! }, "Comum.DadosObrigatorios", ErrorType.Validacao },
        { Dados.CotacaoAuto() with { Coberturas = [null!] }, "Comum.DadosObrigatorios", ErrorType.Validacao },
        { Dados.CotacaoAuto() with { ProdutoCodigo = " " }, "Comum.DadosObrigatorios", ErrorType.Validacao },
        { Dados.CotacaoAuto() with { ProdutoCodigo = "MOTO" }, "Comum.NaoEncontrado", ErrorType.NaoEncontrado },
        { Dados.CotacaoAuto() with { Segurado = Dados.Segurado(cpf: "111.111.111-11") }, "Cpf.Invalido", ErrorType.Validacao },
        { Dados.CotacaoAuto() with { DadosRisco = new DadosRiscoVida(false) }, "Tarifacao.ProdutoIncompativel", ErrorType.Validacao },
        { Dados.CotacaoAuto() with { Coberturas = [new(CodigosCobertura.Casco, null)] }, "Tarifacao.CoberturaObrigatoriaAusente", ErrorType.Validacao },
        { Dados.CotacaoAuto(new CoberturaRequest(CodigosCobertura.RcfDanosCorporais, 1_000m)), "Tarifacao.LmiForaDoLimite", ErrorType.Validacao },
        { Dados.CotacaoAuto(new CoberturaRequest("vidros", null), new CoberturaRequest("VIDROS", null)), "Tarifacao.CoberturaDuplicada", ErrorType.Validacao },
    };

    [Fact]
    public async Task Deve_tratar_corpo_ausente_e_codigo_nulo_como_entrada_do_cliente_quando_simula()
    {
        var semCorpo = await Handler.HandleAsync(new SimularCotacaoCommand(null!), Ct);
        var codigoNulo = await Handler.HandleAsync(new SimularCotacaoCommand(Dados.CotacaoAuto(new CoberturaRequest(null!, null))), Ct);

        semCorpo.DeveFalharCom("Comum.DadosObrigatorios", ErrorType.Validacao);
        codigoNulo.DeveFalharCom("Tarifacao.CoberturaInexistente", ErrorType.Validacao);
    }

    [Fact]
    public async Task Deve_proibir_simulacao_quando_usuario_e_analista_ou_anonimo()
    {
        Amb.Como(Amb.Carla);
        var analista = await Handler.HandleAsync(new SimularCotacaoCommand(Dados.CotacaoAuto()), Ct);
        Amb.UsuarioAtual.Anonimo();
        var anonimo = await Handler.HandleAsync(new SimularCotacaoCommand(Dados.CotacaoAuto()), Ct);

        analista.DeveFalharCom(ComumErros.Proibido.Codigo, ErrorType.Proibido);
        anonimo.DeveFalharCom("Auth.NaoAutenticado", ErrorType.NaoAutorizado);
    }
}
