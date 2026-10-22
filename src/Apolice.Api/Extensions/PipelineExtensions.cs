using Apolice.Api.Http;

namespace Apolice.Api.Extensions;

internal static class PipelineExtensions
{
    // Ordem do SPEC 7, com um ajuste: Authentication vem ANTES do RateLimiter. O limitador global particiona por
    // usuário, e só o JWT já validado diz quem é o usuário; ler o "sub" sem validar deixaria um atacante trocar de
    // partição a cada token forjado. A Authorization continua depois do limitador.
    public static WebApplication UsePipelineApolice(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        if (app.Environment.IsProduction())
        {
            app.UseHsts();
        }

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseLogRequisicoes();

        // Segundo ExceptionHandler, por dentro do log de requisições: o do Serilog registra QUALQUER exceção que o
        // atravessa como "respondeu 500" em Error, antes de o handler de fora decidir o status (409 de concorrência,
        // 400 de corpo ilegível). Tratada aqui, o log mostra o status real; o de fora segue cobrindo os middlewares acima.
        app.UseExceptionHandler();
        app.UseCors();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        return app;
    }
}
