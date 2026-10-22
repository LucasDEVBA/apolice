using Apolice.Api.Http;
using Apolice.Api.OpenApi;
using Scalar.AspNetCore;

namespace Apolice.Api.Extensions;

internal static class DocumentacaoExtensions
{
    public static IServiceCollection AddDocumentacaoApi(this IServiceCollection services) =>
        services.AddOpenApi(RotasApi.NomeDocumentoV1, opcoes =>
        {
            opcoes.AddDocumentTransformer<InformacoesDocumentoTransformer>();
            opcoes.AddOperationTransformer<OperacoesTransformer>();
            opcoes.AddSchemaTransformer<DadosRiscoSchemaTransformer>();
            opcoes.AddSchemaTransformer<EsquemasTransformer>();
            opcoes.AddDocumentTransformer<ContratoDocumentoTransformer>();
        });

    // Publicados também em produção (SPEC 7: é portfólio). O Scalar fica sem fontes externas, telemetria, agente de IA
    // e MCP: tudo é servido pela própria API, o que permite a CSP de mesma origem do SecurityHeadersMiddleware.
    public static WebApplication MapDocumentacao(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapOpenApi(RotasApi.DocumentoOpenApi).AllowAnonymous();
        app.MapScalarApiReference(opcoes => opcoes
                .WithTitle("Apólice API")
                .WithOpenApiRoutePattern(RotasApi.DocumentoOpenApi)
                .DisableDefaultFonts()
                .DisableTelemetry()
                .DisableAgent()
                .DisableMcp())
            .AllowAnonymous();
        return app;
    }
}
