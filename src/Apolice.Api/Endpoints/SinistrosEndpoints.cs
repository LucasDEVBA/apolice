using Apolice.Api.Http;
using Apolice.Api.Seguranca;
using Apolice.Application.Common;
using Apolice.Application.Features.Sinistros;
using Apolice.Application.Features.Sinistros.Abrir;
using Apolice.Application.Features.Sinistros.Aprovar;
using Apolice.Application.Features.Sinistros.IniciarAnalise;
using Apolice.Application.Features.Sinistros.Listar;
using Apolice.Application.Features.Sinistros.Negar;
using Apolice.Application.Features.Sinistros.Obter;
using Apolice.Application.Features.Sinistros.RegistrarPagamento;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apolice.Api.Endpoints;

internal static class SinistrosEndpoints
{
    private const string Colecao = "sinistros";

    private static readonly int[] ProblemasDeOperacao =
    [
        StatusCodes.Status403Forbidden,
        StatusCodes.Status404NotFound,
        StatusCodes.Status409Conflict,
        StatusCodes.Status422UnprocessableEntity,
    ];

    public static IEndpointRouteBuilder MapSinistrosEndpoints(this IEndpointRouteBuilder api)
    {
        var sinistros = api.MapGroup($"/{Colecao}").WithTags(TagsApi.Sinistros).ProducesProblem(StatusCodes.Status401Unauthorized);

        sinistros.MapPost("", AbrirAsync)
            .WithName("AbrirSinistro")
            .WithSummary("Abrir um sinistro")
            .WithDescription("Ocorrência até hoje, dentro da vigência (e antes do cancelamento), numa cobertura que permite sinistro. " +
                "A triagem automática roda em segundo plano. Aceita Idempotency-Key.")
            .RequireAuthorization(Politicas.Corretor)
            .Validar<AbrirSinistroRequest>()
            .Idempotente<AbrirSinistroRequest>()
            .ComProblemas(ProblemasDeOperacao);

        sinistros.MapGet("", ListarAsync)
            .WithName("ListarSinistros")
            .WithSummary("Listar sinistros")
            .WithDescription("Paginação por cursor. Corretor vê os da própria carteira; Analista e Admin veem todos.")
            .Validar<ListarSinistrosQuery>();

        sinistros.MapGet("/{id:guid}", ObterAsync)
            .WithName("ObterSinistro")
            .WithSummary("Detalhe de um sinistro")
            .WithDescription("Com triagem e linha do tempo das decisões.")
            .ComProblemas(StatusCodes.Status404NotFound);

        MapDecisoes(sinistros);
        return api;
    }

    // Análise, decisão e pagamento: só Analista ou Admin (o corretor dono recebe 403; outro corretor, 404).
    private static void MapDecisoes(RouteGroupBuilder sinistros)
    {
        sinistros.MapPost("/{id:guid}/analise", IniciarAnaliseAsync)
            .WithName("IniciarAnaliseSinistro")
            .WithSummary("Iniciar a análise")
            .RequireAuthorization(Politicas.Analista)
            .ComProblemas(ProblemasDeOperacao);

        sinistros.MapPost("/{id:guid}/aprovacao", AprovarAsync)
            .WithName("AprovarSinistro")
            .WithSummary("Aprovar o sinistro")
            .WithDescription("Indenização maior que zero e até o menor entre o valor reclamado e o LMI da cobertura.")
            .RequireAuthorization(Politicas.Analista)
            .Validar<AprovarSinistroRequest>()
            .ComProblemas(ProblemasDeOperacao);

        sinistros.MapPost("/{id:guid}/negativa", NegarAsync)
            .WithName("NegarSinistro")
            .WithSummary("Negar o sinistro")
            .RequireAuthorization(Politicas.Analista)
            .Validar<NegarSinistroRequest>()
            .ComProblemas(ProblemasDeOperacao);

        sinistros.MapPost("/{id:guid}/pagamento", RegistrarPagamentoAsync)
            .WithName("RegistrarPagamentoSinistro")
            .WithSummary("Registrar o pagamento da indenização")
            .RequireAuthorization(Politicas.Analista)
            .ComProblemas(ProblemasDeOperacao);
    }

    private static async Task<Results<Created<SinistroDto>, ProblemHttpResult>> AbrirAsync(
        AbrirSinistroRequest request, AbrirSinistroHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Criado(
            await handler.HandleAsync(
                new AbrirSinistroCommand(request.ApoliceId, request.CoberturaCodigo, request.DataOcorrencia, request.Descricao, request.ValorReclamado),
                cancellationToken),
            s => RotasApi.Recurso(Colecao, s.Id));

    private static async Task<Results<Ok<Pagina<SinistroResumoDto>>, ProblemHttpResult>> ListarAsync(
        [AsParameters] ListarSinistrosQuery query, ListarSinistrosHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(query, cancellationToken));

    private static async Task<Results<Ok<SinistroDto>, ProblemHttpResult>> ObterAsync(
        Guid id, ObterSinistroHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new ObterSinistroQuery(id), cancellationToken));

    private static async Task<Results<Ok<SinistroDto>, ProblemHttpResult>> IniciarAnaliseAsync(
        Guid id, IniciarAnaliseSinistroHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new IniciarAnaliseSinistroCommand(id), cancellationToken));

    private static async Task<Results<Ok<SinistroDto>, ProblemHttpResult>> AprovarAsync(
        Guid id, AprovarSinistroRequest request, AprovarSinistroHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new AprovarSinistroCommand(id, request.ValorIndenizacao, request.Observacao), cancellationToken));

    private static async Task<Results<Ok<SinistroDto>, ProblemHttpResult>> NegarAsync(
        Guid id, NegarSinistroRequest request, NegarSinistroHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new NegarSinistroCommand(id, request.Motivo), cancellationToken));

    private static async Task<Results<Ok<SinistroDto>, ProblemHttpResult>> RegistrarPagamentoAsync(
        Guid id, RegistrarPagamentoSinistroHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new RegistrarPagamentoSinistroCommand(id), cancellationToken));
}
