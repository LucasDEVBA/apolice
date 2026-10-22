using Apolice.Api.Http;
using Apolice.Api.Seguranca;
using Apolice.Application.Common;
using Apolice.Application.Features.Cotacoes;
using Apolice.Application.Features.Cotacoes.Aceitar;
using Apolice.Application.Features.Cotacoes.Criar;
using Apolice.Application.Features.Cotacoes.Listar;
using Apolice.Application.Features.Cotacoes.Obter;
using Apolice.Application.Features.Cotacoes.Recusar;
using Apolice.Application.Features.Cotacoes.Simular;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apolice.Api.Endpoints;

internal static class CotacoesEndpoints
{
    private const string Colecao = "cotacoes";

    public static IEndpointRouteBuilder MapCotacoesEndpoints(this IEndpointRouteBuilder api)
    {
        var cotacoes = api.MapGroup($"/{Colecao}").WithTags(TagsApi.Cotacoes).ProducesProblem(StatusCodes.Status401Unauthorized);

        cotacoes.MapPost("/simular", SimularAsync)
            .WithName("SimularCotacao")
            .WithSummary("Simular uma cotação")
            .WithDescription("Calcula prêmio, IOF, fatores e parcelas sem gravar nada (simulação ao vivo do assistente).")
            .RequireAuthorization(Politicas.Corretor)
            .Validar<CotacaoRequest>()
            .ComProblemas(StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity);

        cotacoes.MapPost("", CriarAsync)
            .WithName("CriarCotacao")
            .WithSummary("Criar uma cotação")
            .WithDescription("Tarifa com o catálogo vigente e grava a cotação (válida por 30 dias) na carteira do corretor.")
            .RequireAuthorization(Politicas.Corretor)
            .Validar<CotacaoRequest>()
            .ComProblemas(StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity);

        cotacoes.MapGet("", ListarAsync)
            .WithName("ListarCotacoes")
            .WithSummary("Listar cotações")
            .WithDescription("Paginação por cursor. Corretor vê só a própria carteira; Analista e Admin veem todas.")
            .Validar<ListarCotacoesQuery>();

        cotacoes.MapGet("/{id:guid}", ObterAsync)
            .WithName("ObterCotacao")
            .WithSummary("Detalhe de uma cotação")
            .ComProblemas(StatusCodes.Status404NotFound);

        cotacoes.MapPost("/{id:guid}/aceitar", AceitarAsync)
            .WithName("AceitarCotacao")
            .WithSummary("Aceitar uma cotação")
            .WithDescription("Só a cotação calculada e dentro da validade; a aceita pode ser emitida.")
            .RequireAuthorization(Politicas.Corretor)
            .ComProblemas(StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);

        cotacoes.MapPost("/{id:guid}/recusar", RecusarAsync)
            .WithName("RecusarCotacao")
            .WithSummary("Recusar uma cotação")
            .RequireAuthorization(Politicas.Corretor)
            .ComProblemas(StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);

        return api;
    }

    private static async Task<Results<Ok<TarifacaoDto>, ProblemHttpResult>> SimularAsync(
        CotacaoRequest request, SimularCotacaoHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new SimularCotacaoCommand(request), cancellationToken));

    private static async Task<Results<Created<CotacaoDto>, ProblemHttpResult>> CriarAsync(
        CotacaoRequest request, CriarCotacaoHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Criado(await handler.HandleAsync(new CriarCotacaoCommand(request), cancellationToken), c => RotasApi.Recurso(Colecao, c.Id));

    private static async Task<Results<Ok<Pagina<CotacaoResumoDto>>, ProblemHttpResult>> ListarAsync(
        [AsParameters] ListarCotacoesQuery query, ListarCotacoesHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(query, cancellationToken));

    private static async Task<Results<Ok<CotacaoDto>, ProblemHttpResult>> ObterAsync(
        Guid id, ObterCotacaoHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new ObterCotacaoQuery(id), cancellationToken));

    private static async Task<Results<Ok<CotacaoDto>, ProblemHttpResult>> AceitarAsync(
        Guid id, AceitarCotacaoHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new AceitarCotacaoCommand(id), cancellationToken));

    private static async Task<Results<Ok<CotacaoDto>, ProblemHttpResult>> RecusarAsync(
        Guid id, RecusarCotacaoHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new RecusarCotacaoCommand(id), cancellationToken));
}
