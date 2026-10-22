using System.Globalization;
using Apolice.Application;
using Apolice.Infrastructure;
using Apolice.Infrastructure.Configuracao;
using Apolice.Infrastructure.Observabilidade;
using Apolice.Worker.Hospedagem;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Settings.Configuration;

namespace Apolice.Worker.Composicao;

// Composição única dos três modos do Worker (verbos, host local e Lambda): o que muda entre eles é só o que é
// hospedado (hosted services) e quem controla o ciclo de vida, nunca o grafo de serviços.
public static class ComposicaoWorker
{
    public const string NomeServicoPadrao = "apolice-worker";
    public const string ChavePrefixoSsm = "Ssm:Prefixo";
    public const string ChaveNomeServico = "OTEL_SERVICE_NAME";
    public const string ChaveEndpointOtlp = "OTEL_EXPORTER_OTLP_ENDPOINT";

    // Abaixo do stop_grace_period do docker compose: o host termina o ciclo do relay e devolve as mensagens em voo
    // antes de o container receber SIGKILL.
    public static readonly TimeSpan PrazoEncerramento = TimeSpan.FromSeconds(20);

    private const string TemplateConsoleDesenvolvimento =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    public static HostApplicationBuilder CriarBuilder(HostApplicationBuilderSettings configuracoes)
    {
        ArgumentNullException.ThrowIfNull(configuracoes);

        var builder = Host.CreateApplicationBuilder(configuracoes);
        AdicionarSsm(builder.Configuration);
        Configurar(builder);
        return builder;
    }

    // Host local: relay do outbox sempre; consumidor só com RabbitMQ (no SQS quem consome é a Lambda, e o InMemory
    // entrega direto ao dispatcher no próprio relay).
    public static void AdicionarHostLocal(HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHostedService<OutboxRelayService>();
        if (Transporte(builder.Configuration) == TransporteMensageria.RabbitMq)
        {
            builder.Services.AddHostedService<RabbitMqConsumerService>();
        }
    }

    private static void Configurar(HostApplicationBuilder builder)
    {
        var servicos = builder.Services;
        var nomeServico = NomeServico(builder.Configuration);

        builder.Logging.ClearProviders();
        servicos.AddSerilog((_, log) => ConfigurarLog(log, builder.Configuration, builder.Environment, nomeServico), preserveStaticLogger: true);
        ConfigurarTelemetria(servicos, builder.Configuration, nomeServico);

        servicos.AddApplication();
        servicos.AddInfrastructure(builder.Configuration, builder.Environment);
        servicos.TryAddSingleton<DrenagemOutbox>();
        servicos.TryAddSingleton<PurgaIdempotencia>();
        servicos.Configure<HostOptions>(o => o.ShutdownTimeout = PrazoEncerramento);
    }

    // Parâmetros do SSM entram por último: em produção os segredos (SecureString) prevalecem sobre env vars.
    // Falhar aqui é intencional: subir sem a configuração de produção mascararia o problema até a primeira mensagem.
    private static void AdicionarSsm(IConfigurationManager configuracao)
    {
        if (configuracao[ChavePrefixoSsm] is not { Length: > 0 } prefixo)
        {
            return;
        }

        if (!prefixo.StartsWith('/'))
        {
            throw new InvalidOperationException($"{ChavePrefixoSsm} deve ser um caminho do Parameter Store iniciado por '/'.");
        }

        configuracao.AddSystemsManager(prefixo, optional: false);
    }

    private static void ConfigurarLog(LoggerConfiguration log, IConfiguration configuracao, IHostEnvironment ambiente, string nomeServico)
    {
        // Padrões no código (SQL do EF/Npgsql só em Warning) e a seção "Serilog" da configuração por cima. Assembly do
        // sink informado: o leitor não varre o DependencyContext atrás de extensões (custo de cold start).
        log.MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Npgsql", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .ReadFrom.Configuration(configuracao, new ConfigurationReaderOptions(typeof(ConsoleLoggerConfigurationExtensions).Assembly))
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .Enrich.WithProperty("Application", nomeServico)
            .Enrich.WithProperty("Environment", ambiente.EnvironmentName);

        if (ambiente.IsDevelopment())
        {
            log.WriteTo.Console(outputTemplate: TemplateConsoleDesenvolvimento, formatProvider: CultureInfo.InvariantCulture);
            return;
        }

        log.WriteTo.Console(new RenderedCompactJsonFormatter());
    }

    // Exportador OTLP só com endpoint configurado (SPEC 9): sem ele os providers existem, mas nada sai do processo.
    private static void ConfigurarTelemetria(IServiceCollection servicos, IConfiguration configuracao, string nomeServico)
    {
        var exportar = !string.IsNullOrWhiteSpace(configuracao[ChaveEndpointOtlp]);
        servicos.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(nomeServico))
            .WithTracing(t =>
            {
                t.AddInstrumentacaoInfraestrutura().AddHttpClientInstrumentation();
                if (exportar)
                {
                    t.AddOtlpExporter();
                }
            })
            .WithMetrics(m =>
            {
                m.AddInstrumentacaoInfraestrutura().AddRuntimeInstrumentation().AddHttpClientInstrumentation();
                if (exportar)
                {
                    m.AddOtlpExporter();
                }
            });
    }

    private static string NomeServico(IConfiguration configuracao) =>
        configuracao[ChaveNomeServico] is { Length: > 0 } nome ? nome : NomeServicoPadrao;

    private static TransporteMensageria Transporte(IConfiguration configuracao) =>
        configuracao.GetSection(MessagingOptions.Secao).GetValue<TransporteMensageria?>(nameof(MessagingOptions.Transport)) ?? TransporteMensageria.RabbitMq;
}
