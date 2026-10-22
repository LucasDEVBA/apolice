using Apolice.Api.Endpoints;
using Apolice.Api.Http;
using Apolice.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Apolice.Api.Extensions;

internal static class EndpointsExtensions
{
    public static WebApplication MapEndpointsApolice(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var versoes = app.NewApiVersionSet().HasApiVersion(HttpApiExtensions.VersaoV1).ReportApiVersions().Build();
        var v1 = app.MapGroup(RotasApi.GrupoVersionado)
            .WithApiVersionSet(versoes)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        v1.MapAuthEndpoints();
        v1.MapSistemaEndpoints();
        v1.MapProdutosEndpoints();
        v1.MapCotacoesEndpoints();
        v1.MapApolicesEndpoints();
        v1.MapSinistrosEndpoints();
        v1.MapDashboardEndpoints();
        v1.MapAdminEndpoints();

        MapSaude(app);
        app.MapDocumentacao();
        return app;
    }

    // Fora do versionamento (SPEC 7). live: o processo responde (nenhuma dependência); ready: banco + broker.
    private static void MapSaude(WebApplication app)
    {
        app.MapHealthChecks($"{RotasApi.Saude}/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous()
            .DisableRateLimiting();
        app.MapHealthChecks($"{RotasApi.Saude}/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(DependencyInjection.TagProntidao) })
            .AllowAnonymous()
            .DisableRateLimiting();
    }
}
