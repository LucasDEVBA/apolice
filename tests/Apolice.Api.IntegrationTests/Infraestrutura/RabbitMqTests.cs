using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Observability;
using Apolice.Infrastructure.Correlacao;
using Apolice.Infrastructure.Messaging;
using Apolice.Infrastructure.Messaging.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace Apolice.Api.IntegrationTests.Infraestrutura;

public sealed class RabbitMqFixture : IAsyncLifetime
{
    public RabbitMqContainer Container { get; } = new RabbitMqBuilder("rabbitmq:4-alpine").Build();

    public async ValueTask InitializeAsync() => await Container.StartAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync() => await Container.DisposeAsync();
}

// Publicação (confirms, mensagem persistente, cabeçalhos) e consumo (escopo por mensagem, correlação, trace, ack/nack
// e DLQ) contra um RabbitMQ real; o dispatcher da aplicação é trocado por um que registra o que recebeu.
public sealed class RabbitMqTests(RabbitMqFixture rabbit) : IClassFixture<RabbitMqFixture>, IAsyncLifetime
{
    private const string SemBanco = "Host=localhost;Port=1;Database=nenhum;Username=ninguem";
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(60);
    private readonly Recebidas _recebidas = new();
    private readonly ActivityListener _ouvinte = new()
    {
        ShouldListenTo = fonte => fonte.Name == ApoliceTelemetry.Nome,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    };

    private AmbienteInfra _ambiente = null!;
    private CancellationTokenSource _parar = null!;
    private Task _consumo = Task.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        ActivitySource.AddActivityListener(_ouvinte);
        var endpoint = new Uri(rabbit.Container.GetConnectionString());
        _ambiente = await AmbienteInfra.CriarAsync(
            SemBanco,
            new Dictionary<string, string?>
            {
                ["Messaging:Transport"] = "RabbitMq",
                ["Messaging:RabbitMq:Host"] = endpoint.Host,
                ["Messaging:RabbitMq:Port"] = endpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Messaging:RabbitMq:Username"] = "rabbitmq",
                ["Messaging:RabbitMq:Password"] = "rabbitmq",
            },
            servicos => servicos.AddScoped<IIntegrationEventDispatcher>(sp => new DispatcherRegistrador(_recebidas, sp.GetRequiredService<ContextoCorrelacao>())),
            migrar: false);

        await using var canal = await CanalAsync();
        await TopologiaRabbitMq.DeclararAsync(canal, Ct);
        await canal.QueuePurgeAsync(TopologiaRabbitMq.Fila, Ct);
        await canal.QueuePurgeAsync(TopologiaRabbitMq.FilaDlq, Ct);
        _parar = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        _consumo = Task.Run(() => _ambiente.Provedor.GetRequiredService<RabbitMqConsumidor>().ConsumirAsync(_parar.Token), Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _parar.CancelAsync();
        await _consumo;
        _parar.Dispose();
        await _ambiente.DisposeAsync();
        _ouvinte.Dispose();
    }

    [Fact]
    public async Task Deve_entregar_ao_dispatcher_com_id_tipo_payload_correlacao_e_trace_quando_publicar()
    {
        using var origem = new Activity("requisicao.http").SetIdFormat(ActivityIdFormat.W3C).Start();
        var mensagem = new MensagemIntegracao(Guid.CreateVersion7(), "ApoliceEmitida", "{\"numero\":\"AUTO-2026-000001\"}", DateTimeOffset.UtcNow, origem.Id, "corr-rabbit");
        origem.Stop();

        await _ambiente.Provedor.GetRequiredService<IMessageBus>().PublishAsync(mensagem, Ct);

        var recebida = await _recebidas.PrimeiraAsync(Prazo);
        recebida.MensagemId.ShouldBe(mensagem.Id);
        recebida.Tipo.ShouldBe(mensagem.Tipo);
        recebida.Payload.ShouldBe(mensagem.Payload);
        recebida.CorrelationId.ShouldBe("corr-rabbit");
        recebida.TraceId.ShouldBe(origem.TraceId.ToString());
        await EsperarAsync(async () => await ContarAsync(TopologiaRabbitMq.Fila) == 0);
    }

    [Fact]
    public async Task Deve_mandar_direto_para_a_dlq_quando_a_mensagem_nao_tem_id()
    {
        await using var canal = await CanalAsync();
        await canal.BasicPublishAsync(TopologiaRabbitMq.Exchange, "Qualquer", Encoding.UTF8.GetBytes("{}"), Ct);

        await EsperarAsync(async () => await ContarAsync(TopologiaRabbitMq.FilaDlq) == 1);
        _recebidas.Total.ShouldBe(0);
    }

    [Fact]
    public async Task Deve_reentregar_e_depois_mandar_para_a_dlq_quando_o_processamento_falha_sempre()
    {
        _recebidas.Falhar = true;
        var mensagem = new MensagemIntegracao(Guid.CreateVersion7(), "SinistroAberto", "{}", DateTimeOffset.UtcNow, null, null);

        await _ambiente.Provedor.GetRequiredService<IMessageBus>().PublishAsync(mensagem, Ct);

        await EsperarAsync(async () => await ContarAsync(TopologiaRabbitMq.FilaDlq) == 1);
        _recebidas.Total.ShouldBe(TopologiaRabbitMq.LimiteEntregas);
        (await ContarAsync(TopologiaRabbitMq.Fila)).ShouldBe(0u);
    }

    [Fact]
    public async Task Deve_reportar_o_broker_saudavel_na_prontidao()
    {
        var relatorio = await _ambiente.Provedor.GetRequiredService<HealthCheckService>().CheckHealthAsync(r => r.Name == "rabbitmq", Ct);

        relatorio.Status.ShouldBe(HealthStatus.Healthy);
    }

    private async Task<IChannel> CanalAsync()
    {
        var conexao = await _ambiente.Provedor.GetRequiredService<ConexaoRabbitMq>().ObterAsync(Ct);
        return await conexao.CreateChannelAsync(cancellationToken: Ct);
    }

    private async Task<uint> ContarAsync(string fila)
    {
        await using var canal = await CanalAsync();
        return await canal.MessageCountAsync(fila, Ct);
    }

    private static async Task EsperarAsync(Func<Task<bool>> condicao)
    {
        var limite = Stopwatch.StartNew();
        while (!await condicao())
        {
            limite.Elapsed.ShouldBeLessThan(Prazo, "condição não atingida a tempo");
            await Task.Delay(TimeSpan.FromMilliseconds(200), Ct);
        }
    }

    private sealed record Recebida(Guid MensagemId, string Tipo, string Payload, string? CorrelationId, string? TraceId);

    private sealed class Recebidas
    {
        private readonly ConcurrentQueue<Recebida> _itens = new();
        private readonly TaskCompletionSource<Recebida> _primeira = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Falhar { get; set; }

        public int Total => _itens.Count;

        public void Adicionar(Recebida recebida)
        {
            _itens.Enqueue(recebida);
            _primeira.TrySetResult(recebida);
        }

        public Task<Recebida> PrimeiraAsync(TimeSpan prazo) => _primeira.Task.WaitAsync(prazo);
    }

    private sealed class DispatcherRegistrador(Recebidas recebidas, ContextoCorrelacao correlacao) : IIntegrationEventDispatcher
    {
        public Task DispatchAsync(Guid mensagemId, string tipo, string payload, CancellationToken cancellationToken)
        {
            recebidas.Adicionar(new Recebida(mensagemId, tipo, payload, correlacao.CorrelationId, Activity.Current?.TraceId.ToString()));
            return recebidas.Falhar ? throw new InvalidOperationException("consumidor quebrado") : Task.CompletedTask;
        }
    }
}
