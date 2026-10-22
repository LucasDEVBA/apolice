using Apolice.Api.Http;
using Apolice.Api.Seguranca;
using Apolice.Application.Common;
using Apolice.Application.Features.Apolices;
using Apolice.Application.Features.Apolices.Cancelar;
using Apolice.Application.Features.Apolices.Emitir;
using Apolice.Application.Features.Apolices.Endossar;
using Apolice.Application.Features.Apolices.GerarDocumento;
using Apolice.Application.Features.Apolices.Listar;
using Apolice.Application.Features.Apolices.Obter;
using Apolice.Application.Features.Apolices.SimularCancelamento;
using Apolice.Application.Features.Apolices.SimularEndosso;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apolice.Api.Endpoints;

internal static class ApolicesEndpoints
{
    private const string Colecao = "apolices";
    private const string TipoPdf = "application/pdf";

    private static readonly int[] ProblemasDeOperacao =
    [
        StatusCodes.Status403Forbidden,
        StatusCodes.Status404NotFound,
        StatusCodes.Status409Conflict,
        StatusCodes.Status422UnprocessableEntity,
    ];

    public static IEndpointRouteBuilder MapApolicesEndpoints(this IEndpointRouteBuilder api)
    {
        var apolices = api.MapGroup($"/{Colecao}").WithTags(TagsApi.Apolices).ProducesProblem(StatusCodes.Status401Unauthorized);

        apolices.MapPost("", EmitirAsync)
            .WithName("EmitirApolice")
            .WithSummary("Emitir uma apólice")
            .WithDescription("A partir de uma cotação aceita e válida: início da vigência entre hoje e hoje + 30 dias, " +
                "parcelas entre 1 e o máximo da cotação. Aceita Idempotency-Key.")
            .RequireAuthorization(Politicas.Corretor)
            .Validar<EmitirApoliceRequest>()
            .Idempotente<EmitirApoliceRequest>()
            .ComProblemas(ProblemasDeOperacao);

        apolices.MapGet("", ListarAsync)
            .WithName("ListarApolices")
            .WithSummary("Listar apólices")
            .WithDescription("Paginação por cursor; status é o efetivo (Programada, Ativa, Vencida, Cancelada). CPF mascarado.")
            .Validar<ListarApolicesQuery>();

        apolices.MapGet("/{id:guid}", ObterAsync)
            .WithName("ObterApolice")
            .WithSummary("Detalhe de uma apólice")
            .WithDescription("Com coberturas, cronograma de parcelas, endossos, sinistros e notificações enviadas.")
            .ComProblemas(StatusCodes.Status404NotFound);

        apolices.MapGet("/{id:guid}/documento", BaixarDocumentoAsync)
            .WithName("BaixarDocumentoApolice")
            .WithSummary("Baixar o PDF da apólice")
            .WithDescription("Documento gerado na hora a partir do mesmo conteúdo do detalhe. Requer a flag DocumentoPdf.")
            .Produces(StatusCodes.Status200OK, typeof(Stream), TipoPdf)
            .ComProblemas(StatusCodes.Status404NotFound);

        MapEndossos(apolices);
        MapCancelamento(apolices);
        return api;
    }

    private static void MapEndossos(RouteGroupBuilder apolices)
    {
        apolices.MapPost("/{id:guid}/endossos/simular", SimularEndossoAsync)
            .WithName("SimularEndosso")
            .WithSummary("Simular um endosso")
            .WithDescription("Ajuste pro rata do prêmio pela nova tarifação do conjunto alterado. Requer a flag EndossoOnline.")
            .RequireAuthorization(Politicas.Corretor)
            .Validar<EndossoRequest>()
            .ComProblemas(ProblemasDeOperacao);

        apolices.MapPost("/{id:guid}/endossos", EndossarAsync)
            .WithName("EndossarApolice")
            .WithSummary("Endossar a apólice")
            .WithDescription("Alteração de LMI, inclusão ou exclusão de cobertura. Requer a flag EndossoOnline.")
            .RequireAuthorization(Politicas.Corretor)
            .Validar<EndossoRequest>()
            .ComProblemas(ProblemasDeOperacao);
    }

    private static void MapCancelamento(RouteGroupBuilder apolices)
    {
        apolices.MapPost("/{id:guid}/cancelamento/simular", SimularCancelamentoAsync)
            .WithName("SimularCancelamento")
            .WithSummary("Simular o cancelamento")
            .WithDescription("Restituição integral (antes do início), prazo curto (pedido do segurado) ou pro rata die.")
            .RequireAuthorization(Politicas.Corretor)
            .Validar<CancelamentoRequest>()
            .ComProblemas(ProblemasDeOperacao);

        apolices.MapPost("/{id:guid}/cancelamento", CancelarAsync)
            .WithName("CancelarApolice")
            .WithSummary("Cancelar a apólice")
            .RequireAuthorization(Politicas.Corretor)
            .Validar<CancelamentoRequest>()
            .ComProblemas(ProblemasDeOperacao);
    }

    private static async Task<Results<Created<ApoliceDto>, ProblemHttpResult>> EmitirAsync(
        EmitirApoliceRequest request, EmitirApoliceHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Criado(
            await handler.HandleAsync(new EmitirApoliceCommand(request.CotacaoId, request.InicioVigencia, request.Parcelas), cancellationToken),
            a => RotasApi.Recurso(Colecao, a.Id));

    private static async Task<Results<Ok<Pagina<ApoliceResumoDto>>, ProblemHttpResult>> ListarAsync(
        [AsParameters] ListarApolicesQuery query, ListarApolicesHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(query, cancellationToken));

    private static async Task<Results<Ok<ApoliceDto>, ProblemHttpResult>> ObterAsync(
        Guid id, ObterApoliceHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new ObterApoliceQuery(id), cancellationToken));

    // attachment: o browser baixa em vez de renderizar inline na origem da API.
    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> BaixarDocumentoAsync(
        Guid id, GerarDocumentoApoliceHandler handler, CancellationToken cancellationToken)
    {
        var resultado = await handler.HandleAsync(new GerarDocumentoApoliceQuery(id), cancellationToken);
        if (resultado.IsFailure)
        {
            return Problemas.De(resultado.Error);
        }

        var documento = resultado.Value;
        return TypedResults.File(documento.Conteudo, documento.TipoConteudo, documento.NomeArquivo);
    }

    private static async Task<Results<Ok<SimulacaoEndossoDto>, ProblemHttpResult>> SimularEndossoAsync(
        Guid id, EndossoRequest request, SimularEndossoHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new SimularEndossoCommand(id, request.Tipo, request.CoberturaCodigo, request.Lmi), cancellationToken));

    // 201 com o Location da apólice: o endosso não tem recurso próprio, é uma nova versão da apólice.
    private static async Task<Results<Created<ApoliceDto>, ProblemHttpResult>> EndossarAsync(
        Guid id, EndossoRequest request, EndossarApoliceHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Criado(
            await handler.HandleAsync(new EndossarApoliceCommand(id, request.Tipo, request.CoberturaCodigo, request.Lmi), cancellationToken),
            a => RotasApi.Recurso(Colecao, a.Id));

    private static async Task<Results<Ok<SimulacaoCancelamentoDto>, ProblemHttpResult>> SimularCancelamentoAsync(
        Guid id, CancelamentoRequest request, SimularCancelamentoHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new SimularCancelamentoCommand(id, request.Motivo), cancellationToken));

    private static async Task<Results<Ok<ApoliceDto>, ProblemHttpResult>> CancelarAsync(
        Guid id, CancelamentoRequest request, CancelarApoliceHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new CancelarApoliceCommand(id, request.Motivo), cancellationToken));
}
