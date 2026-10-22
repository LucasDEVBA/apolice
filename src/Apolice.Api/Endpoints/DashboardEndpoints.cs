using Apolice.Api.Http;
using Apolice.Application.Features.Dashboard;
using Apolice.Application.Features.Dashboard.Obter;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apolice.Api.Endpoints;

internal static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/dashboard", ObterAsync)
            .WithTags(TagsApi.Dashboard)
            .WithName("ObterDashboard")
            .WithSummary("Indicadores da carteira")
            .WithDescription("Escopo Carteira para o corretor (só a dele) e Global para Analista e Admin.")
            .ComProblemas(StatusCodes.Status401Unauthorized);
        return api;
    }

    private static async Task<Results<Ok<DashboardDto>, ProblemHttpResult>> ObterAsync(
        ObterDashboardHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new ObterDashboardQuery(), cancellationToken));
}
