using Apolice.Api.Http;
using Apolice.Application.Features.Sistema.ObterFeatures;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apolice.Api.Endpoints;

internal static class SistemaEndpoints
{
    public static IEndpointRouteBuilder MapSistemaEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/features", ObterFeaturesAsync)
            .WithTags(TagsApi.Sistema)
            .WithName("ObterFeatures")
            .WithSummary("Feature flags ligadas")
            .WithDescription("Anônimo: a tela de login decide por aqui se mostra os botões de persona de demonstração.")
            .AllowAnonymous();
        return api;
    }

    private static async Task<Results<Ok<FeaturesDto>, ProblemHttpResult>> ObterFeaturesAsync(
        ObterFeaturesHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new ObterFeaturesQuery(), cancellationToken));
}
