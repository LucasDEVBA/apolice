using Apolice.Domain.Comum;

namespace Apolice.Api.Http;

// Defesa em profundidade além do SameSite=Strict (SPEC 7): um formulário ou link de outro site não consegue enviar
// header customizado sem preflight de CORS, então exigir X-Requested-With barra o CSRF clássico no refresh/logout.
internal sealed class CabecalhoCsrfFilter : IEndpointFilter
{
    private static readonly Error CabecalhoAusente = Error.Proibido(
        "Auth.CabecalhoCsrfAusente", $"Requisição recusada: envie o header {CabecalhosApi.RequestedWith}: {CabecalhosApi.ValorRequestedWith}.");

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var valor = context.HttpContext.Request.Headers[CabecalhosApi.RequestedWith];
        return valor.Count == 1 && string.Equals(valor[0], CabecalhosApi.ValorRequestedWith, StringComparison.Ordinal)
            ? await next(context)
            : Problemas.De(CabecalhoAusente);
    }
}

// Marca a rota para o OpenAPI documentar o header obrigatório e o cookie de sessão.
internal sealed class CabecalhoCsrfMetadata;

internal static class CsrfEndpoints
{
    public static RouteHandlerBuilder ExigirCabecalhoCsrf(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<CabecalhoCsrfFilter>().WithMetadata(new CabecalhoCsrfMetadata()).ProducesProblem(StatusCodes.Status403Forbidden);
}
