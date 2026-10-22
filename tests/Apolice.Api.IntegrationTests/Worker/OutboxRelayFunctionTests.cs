using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Api.IntegrationTests.Worker.Suporte;
using Apolice.Domain.Notificacoes;
using Apolice.Worker.Lambda;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Apolice.Api.IntegrationTests.Worker;

// Relay agendado contra o PostgreSQL real. Transporte InMemory: a publicação entrega direto aos consumidores, então a
// notificação no banco prova o caminho outbox → publicação → consumidor. Lote 1 obriga a drenagem a dar mais de uma volta.
public sealed class OutboxRelayFunctionTests(PostgresFixture postgres) : IAsyncLifetime
{
    private IHost _host = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _host = HostWorker.Criar(await postgres.CriarBancoAsync("worker_relay"), new Dictionary<string, string?> { ["Outbox:Lote"] = "1" });
        await HostWorker.PrepararBancoAsync(_host.Services, Ct);
        await _host.StartAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync(CancellationToken.None);
        await HostWorker.DescartarAsync(_host);
    }

    [Fact]
    public async Task Deve_drenar_o_backlog_em_varios_lotes_e_gerar_as_notificacoes_quando_o_relay_agendado_roda()
    {
        var primeira = await HostWorker.EmitirAsync(_host.Services, Ct);
        var segunda = await HostWorker.EmitirAsync(_host.Services, Ct);
        var funcao = new OutboxRelayFunction(_host.Services);

        var resultado = await funcao.HandleAsync(Stream.Null, ContextoLambdaTeste.Folgado());

        resultado.ShouldBe(new ResultadoRelay(2, PrazoEsgotado: false));
        (await HostWorker.ConsultarAsync(_host.Services, db => db.Outbox.CountAsync(m => m.ProcessadoEm == null, Ct))).ShouldBe(0);
        var notificacoes = await HostWorker.ConsultarAsync(_host.Services, db => db.Notificacoes.AsNoTracking().Where(n => n.Tipo == TipoNotificacao.BoasVindas).Select(n => n.ReferenciaId).ToListAsync(Ct));
        notificacoes.ShouldBe([primeira.ApoliceId, segunda.ApoliceId], ignoreOrder: true);
        (await funcao.HandleAsync(Stream.Null, ContextoLambdaTeste.Folgado())).Publicadas.ShouldBe(0);
    }

    [Fact]
    public async Task Deve_encerrar_sem_erro_e_sinalizar_quando_o_prazo_da_invocacao_esta_no_fim()
    {
        var resultado = await new OutboxRelayFunction(_host.Services).HandleAsync(Stream.Null, new ContextoLambdaTeste(TimeSpan.FromSeconds(1)));

        resultado.PrazoEsgotado.ShouldBeTrue();
    }
}
