using System.Diagnostics;
using System.Text.RegularExpressions;
using Apolice.Infrastructure.Correlacao;
using Serilog.Context;

namespace Apolice.Api.Http;

// SPEC 7: aceita o X-Correlation-Id do cliente só se for curto e seguro ([A-Za-z0-9-], até 64), senão gera um.
// O id vai para a resposta, para o LogContext, para o span e para o ContextoCorrelacao (outbox e auditoria).
internal sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    private const int TimeoutRegexMs = 50;

    public async Task InvokeAsync(HttpContext http, ContextoCorrelacao contexto)
    {
        var id = Aceitar(http.Request.Headers[CabecalhosApi.CorrelationId].ToString()) ?? Guid.CreateVersion7().ToString();
        CorrelacaoHttp.Definir(http, id);
        contexto.Definir(id);
        Activity.Current?.SetTag(CorrelacaoHttp.TagSpan, id);

        // OnStarting, e não o header direto: o ExceptionHandler limpa os headers antes de escrever o 500.
        http.Response.OnStarting(() =>
        {
            http.Response.Headers[CabecalhosApi.CorrelationId] = id;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(CorrelacaoHttp.PropriedadeLog, id))
        {
            await next(http);
        }
    }

    private static string? Aceitar(string valor) => Formato().IsMatch(valor) ? valor : null;

    [GeneratedRegex("^[A-Za-z0-9-]{1,64}$", RegexOptions.CultureInvariant, TimeoutRegexMs)]
    private static partial Regex Formato();
}

internal static class CorrelacaoHttp
{
    public const string PropriedadeLog = "CorrelationId";
    public const string TagSpan = "apolice.correlation_id";
    private const string ChaveItem = "apolice.correlationId";

    public static void Definir(HttpContext http, string id) => http.Items[ChaveItem] = id;

    // Nulo só antes do middleware (ex.: erro no ForwardedHeaders): quem lê trata a ausência.
    public static string? Obter(HttpContext http) => http.Items.TryGetValue(ChaveItem, out var id) ? id as string : null;
}
