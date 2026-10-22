using Apolice.Api.Http;
using Apolice.Infrastructure.Observabilidade;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Apolice.Api.Extensions;

internal static class ObservabilidadeExtensions
{
    private const string ChaveEndpointOtlp = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string ChaveNomeServico = "OTEL_SERVICE_NAME";
    private const string NomeServicoPadrao = "apolice-api";

    // Traces: ASP.NET Core, HttpClient, Npgsql e o ActivitySource "Apolice" (publicação/consumo); métricas: ASP.NET Core,
    // runtime e o Meter "Apolice". Sem endpoint OTLP configurado nada é exportado (a coleta fica em memória, barata).
    public static IServiceCollection AddObservabilidadeApi(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var telemetria = services.AddOpenTelemetry()
            .ConfigureResource(recurso => recurso.AddService(configuration[ChaveNomeServico] ?? NomeServicoPadrao))
            .WithTracing(traces => traces
                .AddAspNetCoreInstrumentation(opcoes => opcoes.Filter = http => !http.Request.Path.StartsWithSegments(RotasApi.Saude))
                .AddHttpClientInstrumentation()
                .AddInstrumentacaoInfraestrutura())
            .WithMetrics(metricas => metricas
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddInstrumentacaoInfraestrutura());

        if (!string.IsNullOrWhiteSpace(configuration[ChaveEndpointOtlp]))
        {
            telemetria.UseOtlpExporter();
        }

        return services;
    }
}
