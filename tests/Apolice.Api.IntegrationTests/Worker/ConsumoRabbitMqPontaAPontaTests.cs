using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Apolice.Api.IntegrationTests.Infraestrutura;
using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Api.IntegrationTests.Worker.Suporte;
using Apolice.Application.Abstractions.Observability;
using Apolice.Domain.Notificacoes;
using Apolice.Infrastructure.Messaging;
using Apolice.Worker.Composicao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Apolice.Api.IntegrationTests.Worker;

// O host local do Worker inteiro (OutboxRelayService + RabbitMqConsumerService), como no docker compose: o evento sai
// do outbox pelo relay, passa por um RabbitMQ real e o consumidor gera a notificação. RabbitMQ próprio desta classe
// (IClassFixture), para não disputar a fila com os testes do adaptador.
public sealed class ConsumoRabbitMqPontaAPontaTests(PostgresFixture postgres, RabbitMqFixture rabbit) : IClassFixture<RabbitMqFixture>, IAsyncLifetime
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(60);
    private readonly ConcurrentQueue<Activity> _consumos = new();
    private ActivityListener _ouvinte = null!;
    private IHost _host = null!;
    private bool _parado;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _ouvinte = new ActivityListener
        {
            ShouldListenTo = fonte => fonte.Name == ApoliceTelemetry.Nome,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = atividade =>
            {
                if (atividade.DisplayName.StartsWith(ProcessadorMensagemIntegracao.AtividadeProcessar, StringComparison.Ordinal))
                {
                    _consumos.Enqueue(atividade);
                }
            },
        };
        ActivitySource.AddActivityListener(_ouvinte);

        var endpoint = new Uri(rabbit.Container.GetConnectionString());
        _host = HostWorker.Criar(
            await postgres.CriarBancoAsync("worker_rabbit"),
            new Dictionary<string, string?>
            {
                ["Messaging:Transport"] = "RabbitMq",
                ["Messaging:RabbitMq:Host"] = endpoint.Host,
                ["Messaging:RabbitMq:Port"] = endpoint.Port.ToString(CultureInfo.InvariantCulture),
                ["Messaging:RabbitMq:Username"] = "rabbitmq",
                ["Messaging:RabbitMq:Password"] = "rabbitmq",
                ["Outbox:IntervaloSegundos"] = "1",
            },
            hostLocal: true);

        // Banco pronto antes de o relay começar a ler o outbox (no compose, o "migrate" e o "seed" rodam antes).
        await HostWorker.PrepararBancoAsync(_host.Services, Ct);
        await _host.StartAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_parado)
        {
            await _host.StopAsync(CancellationToken.None);
        }

        await HostWorker.DescartarAsync(_host);
        _ouvinte.Dispose();
    }

    [Fact]
    public async Task Deve_levar_o_evento_do_outbox_ate_a_notificacao_pelo_rabbitmq_quando_o_host_local_esta_rodando()
    {
        using var origem = new Activity("teste.requisicao").SetIdFormat(ActivityIdFormat.W3C).Start();
        var emissao = await HostWorker.EmitirAsync(_host.Services, Ct, "corr-worker-e2e");
        origem.Stop();

        var notificacao = await EsperarAsync(() => HostWorker.ConsultarAsync(_host.Services, db => db.Notificacoes.AsNoTracking().SingleOrDefaultAsync(n => n.ReferenciaId == emissao.ApoliceId, Ct)));

        notificacao.Tipo.ShouldBe(TipoNotificacao.BoasVindas);
        var mensagem = await HostWorker.ConsultarAsync(_host.Services, db => db.Outbox.AsNoTracking().SingleAsync(m => m.Id == emissao.EventoId, Ct));
        mensagem.ProcessadoEm.ShouldNotBeNull();
        mensagem.CorrelationId.ShouldBe("corr-worker-e2e");
        (await HostWorker.ConsultarAsync(_host.Services, db => db.MensagensProcessadas.CountAsync(m => m.MensagemId == emissao.EventoId, Ct))).ShouldBe(1);

        // O span de consumo é filho do span que gravou o evento: o trace da requisição continua no Worker. O ouvinte é
        // global (pega spans de testes paralelos), daí o filtro pelo trace desta emissão.
        var consumo = _consumos.Where(a => a.TraceId == origem.TraceId).ShouldHaveSingleItem();
        consumo.ParentSpanId.ShouldBe(origem.SpanId);
        consumo.Kind.ShouldBe(ActivityKind.Consumer);
    }

    [Fact]
    public async Task Deve_encerrar_relay_e_consumidor_dentro_do_prazo_quando_o_host_para()
    {
        var relogio = Stopwatch.StartNew();

        await _host.StopAsync(Ct);
        _parado = true;

        relogio.Elapsed.ShouldBeLessThan(ComposicaoWorker.PrazoEncerramento);
    }

    private static async Task<T> EsperarAsync<T>(Func<Task<T?>> consulta)
        where T : class
    {
        var limite = Stopwatch.StartNew();
        while (true)
        {
            if (await consulta() is { } resultado)
            {
                return resultado;
            }

            limite.Elapsed.ShouldBeLessThan(Prazo, "o consumidor não gerou a notificação a tempo");
            await Task.Delay(TimeSpan.FromMilliseconds(200), Ct);
        }
    }
}
