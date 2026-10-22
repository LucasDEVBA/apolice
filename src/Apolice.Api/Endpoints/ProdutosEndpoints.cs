using Apolice.Api.Http;
using Apolice.Api.Seguranca;
using Apolice.Application.Features.Produtos;
using Apolice.Application.Features.Produtos.AtualizarTaxa;
using Apolice.Application.Features.Produtos.Listar;
using Apolice.Application.Features.Produtos.Obter;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apolice.Api.Endpoints;

internal static class ProdutosEndpoints
{
    public static IEndpointRouteBuilder MapProdutosEndpoints(this IEndpointRouteBuilder api)
    {
        var produtos = api.MapGroup("/produtos").WithTags(TagsApi.Produtos).ProducesProblem(StatusCodes.Status401Unauthorized);

        produtos.MapGet("", ListarAsync)
            .WithName("ListarProdutos")
            .WithSummary("Catálogo de produtos")
            .WithDescription("Produtos com coberturas, taxas e faixas de LMI (leitura em cache).");

        produtos.MapGet("/{codigo}", ObterAsync)
            .WithName("ObterProduto")
            .WithSummary("Detalhe de um produto")
            .ComProblemas(StatusCodes.Status404NotFound);

        produtos.MapPut("/{codigo}/coberturas/{coberturaCodigo}/taxa", AtualizarTaxaAsync)
            .WithName("AtualizarTaxaCobertura")
            .WithSummary("Atualizar a taxa de uma cobertura")
            .WithDescription("Só Admin. Vale para novas cotações e endossos; apólices emitidas mantêm o prêmio contratado.")
            .RequireAuthorization(Politicas.Admin)
            .Validar<AtualizarTaxaRequest>()
            .ComProblemas(StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);

        return api;
    }

    private static async Task<Results<Ok<IReadOnlyList<ProdutoDto>>, ProblemHttpResult>> ListarAsync(
        ListarProdutosHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new ListarProdutosQuery(), cancellationToken));

    private static async Task<Results<Ok<ProdutoDto>, ProblemHttpResult>> ObterAsync(
        string codigo, ObterProdutoHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new ObterProdutoQuery(codigo), cancellationToken));

    private static async Task<Results<Ok<ProdutoDto>, ProblemHttpResult>> AtualizarTaxaAsync(
        string codigo, string coberturaCodigo, AtualizarTaxaRequest request, AtualizarTaxaCoberturaHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new AtualizarTaxaCoberturaCommand(codigo, coberturaCodigo, request.Taxa), cancellationToken));
}
