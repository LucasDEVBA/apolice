using Apolice.Domain.Comum;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apolice.Api.Http;

// Result/Error da aplicação → HTTP (SPEC 7). Os endpoints não decidem status de erro: só escolhem o de sucesso.
internal static class ResultadosHttp
{
    public static Results<Ok<T>, ProblemHttpResult> Ok<T>(Result<T> resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        return resultado.IsSuccess ? TypedResults.Ok(resultado.Value) : Problemas.De(resultado.Error);
    }

    public static Results<Created<T>, ProblemHttpResult> Criado<T>(Result<T> resultado, Func<T, string> localizacao)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(localizacao);
        return resultado.IsSuccess ? TypedResults.Created(localizacao(resultado.Value), resultado.Value) : Problemas.De(resultado.Error);
    }
}

internal static class DocumentacaoRespostas
{
    // Declara no OpenAPI os ProblemDetails que a rota pode devolver (os genéricos 429/500 ficam no grupo raiz).
    public static RouteHandlerBuilder ComProblemas(this RouteHandlerBuilder builder, params int[] status)
    {
        ArgumentNullException.ThrowIfNull(builder);
        foreach (var codigo in status)
        {
            builder.ProducesProblem(codigo);
        }

        return builder;
    }
}
