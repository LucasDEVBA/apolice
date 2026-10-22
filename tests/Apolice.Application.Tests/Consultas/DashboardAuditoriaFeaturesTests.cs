using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Application.Features.Auditoria;
using Apolice.Application.Features.Auditoria.Listar;
using Apolice.Application.Features.Dashboard;
using Apolice.Application.Features.Dashboard.Obter;
using Apolice.Application.Features.Sistema.ObterFeatures;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Comum;
using NSubstitute;

namespace Apolice.Application.Tests.Consultas;

public sealed class DashboardAuditoriaFeaturesTests : TesteBase
{
    private static readonly MetricasDashboard Metricas = new(3, 1_000m, [new("2026-03", 1_000m)], 2, 0.5m, 1, 0.1m, []);

    [Fact]
    public async Task Deve_consultar_so_a_carteira_quando_corretor_abre_dashboard()
    {
        Amb.Dashboard.ObterAsync(Arg.Any<FiltroDashboard>(), Arg.Any<CancellationToken>()).Returns(Metricas);

        var dashboard = (await Amb.Servico<ObterDashboardHandler>().HandleAsync(new ObterDashboardQuery(), Ct)).DeveTerSucesso();

        dashboard.Escopo.ShouldBe(EscopoDashboard.Carteira);
        dashboard.ApolicesAtivas.ShouldBe(3);
        await Amb.Dashboard.Received(1).ObterAsync(new FiltroDashboard(Amb.Ana.Id, Amb.Hoje, Amb.Agora), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_consultar_a_carteira_inteira_quando_analista_abre_dashboard()
    {
        Amb.Como(Amb.Carla);
        Amb.Dashboard.ObterAsync(Arg.Any<FiltroDashboard>(), Arg.Any<CancellationToken>()).Returns(Metricas);

        var dashboard = (await Amb.Servico<ObterDashboardHandler>().HandleAsync(new ObterDashboardQuery(), Ct)).DeveTerSucesso();

        dashboard.Escopo.ShouldBe(EscopoDashboard.Global);
        await Amb.Dashboard.Received(1).ObterAsync(Arg.Is<FiltroDashboard>(f => f.CorretorId == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_exigir_autenticacao_quando_anonimo_abre_dashboard()
    {
        Amb.UsuarioAtual.Anonimo();

        var resultado = await Amb.Servico<ObterDashboardHandler>().HandleAsync(new ObterDashboardQuery(), Ct);

        resultado.DeveFalharCom("Auth.NaoAutenticado", ErrorType.NaoAutorizado);
        await Amb.Dashboard.DidNotReceiveWithAnyArgs().ObterAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_listar_auditoria_paginada_quando_admin_consulta()
    {
        Amb.Como(Amb.Diego);
        var id = Guid.NewGuid();
        var cursor = Cursor.Codificar(Amb.Agora, id);
        var esperada = new Pagina<AuditoriaDto>([new AuditoriaDto(Guid.NewGuid(), "Diego Martins", AcoesAuditoria.Login, EntidadesAuditoria.Usuario, null, null, "corr-1", Amb.Agora)], null);
        Amb.ConsultasAuditoria.ListarAsync(Arg.Any<PosicaoCursor?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(esperada);

        var pagina = (await Amb.Servico<ListarAuditoriaHandler>().HandleAsync(new ListarAuditoriaQuery(cursor, 50), Ct)).DeveTerSucesso();

        pagina.ShouldBeSameAs(esperada);
        await Amb.ConsultasAuditoria.Received(1).ListarAsync(new PosicaoCursor(Amb.Agora, id), 50, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_proibir_auditoria_e_validar_cursor_quando_nao_admin_ou_cursor_ruim()
    {
        Amb.Como(Amb.Carla);
        var analista = await Amb.Servico<ListarAuditoriaHandler>().HandleAsync(new ListarAuditoriaQuery(), Ct);
        Amb.Como(Amb.Diego);
        var cursorRuim = await Amb.Servico<ListarAuditoriaHandler>().HandleAsync(new ListarAuditoriaQuery("x"), Ct);

        analista.DeveFalharCom("Comum.Proibido", ErrorType.Proibido);
        cursorRuim.DeveFalharCom("Comum.CursorInvalido", ErrorType.Validacao);
    }

    [Fact]
    public async Task Deve_refletir_as_flags_quando_consulta_features()
    {
        Amb.UsuarioAtual.Anonimo();
        Amb.Flags.Desligar(FeatureFlags.DocumentoPdf);

        var features = (await Amb.Servico<ObterFeaturesHandler>().HandleAsync(new ObterFeaturesQuery(), Ct)).DeveTerSucesso();

        features.ShouldBe(new FeaturesDto(LoginDemo: true, TriagemAutomaticaSinistros: true, DocumentoPdf: false, EndossoOnline: true));
    }
}
