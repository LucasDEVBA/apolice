using FluentValidation;

namespace Apolice.Api.Http;

// Valida o argumento T (corpo ou [AsParameters]) com o validator da Application antes do handler: 400 com
// ValidationProblemDetails. As regras de negócio continuam no domínio; aqui é forma e tamanho.
internal sealed class ValidationFilter<T>(IValidator<T> validador) : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        // Corpo ausente nem chega aqui: o binding das Minimal APIs já responde 400.
        if (context.Arguments.OfType<T>().FirstOrDefault() is not { } argumento)
        {
            return await next(context);
        }

        var resultado = await validador.ValidateAsync(argumento, context.HttpContext.RequestAborted);
        return resultado.IsValid ? await next(context) : Problemas.Validacao(resultado.Errors);
    }
}

internal static class ValidacaoEndpoints
{
    public static RouteHandlerBuilder Validar<T>(this RouteHandlerBuilder builder)
        where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>().ProducesValidationProblem();
}
