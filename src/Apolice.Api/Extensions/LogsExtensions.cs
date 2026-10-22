using System.Globalization;
using Apolice.Api.Http;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Settings.Configuration;

namespace Apolice.Api.Extensions;

internal static class LogsExtensions
{
    public const string NomeAplicacao = "Apolice.Api";
    private const string TemplateDesenvolvimento =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {{CorrelationId={CorrelationId}}}{NewLine}{Exception}";

    private const string MensagemRequisicao = "HTTP {RequestMethod} {RequestPath} respondeu {StatusCode} em {Elapsed:0.0} ms";

    public static WebApplicationBuilder AddLogsApolice(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var ambiente = builder.Environment;
        builder.Services.AddSerilog((servicos, logger) =>
        {
            // Assembly do sink informado: sem ele o leitor varre o DependencyContext atrás de extensões (custo de cold start).
            logger.ReadFrom.Configuration(builder.Configuration, new ConfigurationReaderOptions(typeof(ConsoleLoggerConfigurationExtensions).Assembly))
                .ReadFrom.Services(servicos)
                .Enrich.FromLogContext()
                .Enrich.WithSpan()
                .Enrich.WithProperty("Application", NomeAplicacao)
                .Enrich.WithProperty("Environment", ambiente.EnvironmentName);

            if (ambiente.IsDevelopment())
            {
                logger.WriteTo.Console(outputTemplate: TemplateDesenvolvimento, formatProvider: CultureInfo.InvariantCulture);
            }
            else
            {
                logger.WriteTo.Console(new RenderedCompactJsonFormatter());
            }
        });
        return builder;
    }

    // Só método, caminho (sem query string: a busca pode carregar nome ou CPF), status e duração; corpo e headers nunca.
    public static IApplicationBuilder UseLogRequisicoes(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(opcoes =>
        {
            opcoes.MessageTemplate = MensagemRequisicao;
            opcoes.GetLevel = NivelDaRequisicao;
        });

    private static LogEventLevel NivelDaRequisicao(HttpContext http, double elapsed, Exception? excecao)
    {
        if (excecao is not null || http.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogEventLevel.Error;
        }

        // Sondas de saúde batem a cada poucos segundos: em Information afogariam o log útil.
        return http.Request.Path.StartsWithSegments(RotasApi.Saude) ? LogEventLevel.Verbose : LogEventLevel.Information;
    }
}
