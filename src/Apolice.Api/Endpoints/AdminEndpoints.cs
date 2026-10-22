using Apolice.Api.Http;
using Apolice.Api.Seguranca;
using Apolice.Application.Common;
using Apolice.Application.Features.Auditoria;
using Apolice.Application.Features.Auditoria.Listar;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apolice.Api.Endpoints;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder api)
    {
        var admin = api.MapGroup("/admin").WithTags(TagsApi.Administracao).RequireAuthorization(Politicas.Admin);

        admin.MapGet("/auditoria", ListarAuditoriaAsync)
            .WithName("ListarAuditoria")
            .WithSummary("Trilha de auditoria")
            .WithDescription("Ações sensíveis (login, emissão, endosso, cancelamento, decisões de sinistro, tarifas), mais recentes primeiro.")
            .Validar<ListarAuditoriaQuery>()
            .ComProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden);
        return api;
    }

    private static async Task<Results<Ok<Pagina<AuditoriaDto>>, ProblemHttpResult>> ListarAuditoriaAsync(
        [AsParameters] ListarAuditoriaQuery query, ListarAuditoriaHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(query, cancellationToken));
}
