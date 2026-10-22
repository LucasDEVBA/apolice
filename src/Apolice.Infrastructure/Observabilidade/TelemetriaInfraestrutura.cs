using Apolice.Application.Abstractions.Observability;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Apolice.Infrastructure.Observabilidade;

// O que a infraestrutura emite, num lugar só para a API e o Worker ligarem no OpenTelemetry: os spans de publicação e
// consumo de mensagens (ActivitySource "Apolice"), os do Npgsql (EF, Dapper, numerador) e as métricas de negócio.
public static class TelemetriaInfraestrutura
{
    public static TracerProviderBuilder AddInstrumentacaoInfraestrutura(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(ApoliceTelemetry.Nome).AddNpgsql();
    }

    public static MeterProviderBuilder AddInstrumentacaoInfraestrutura(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddMeter(ApoliceTelemetry.Nome).AddNpgsqlInstrumentation();
    }
}
