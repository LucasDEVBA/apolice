namespace Apolice.Api.Http;

// SPEC 7. CSP fechada para a API (só JSON/PDF: nada para carregar ou embutir); o Scalar é uma página com scripts e
// estilos próprios, então ganha uma política mínima de mesma origem ('unsafe-inline' porque a página inicializa por
// script inline). Via OnStarting para valer também nas respostas de erro escritas pelo ExceptionHandler.
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string CspApi = "default-src 'none'; frame-ancestors 'none'";
    private const string CspScalar =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
        "font-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    private const string PermissionsPolicy =
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

    // Resposta da API pode carregar token, CPF ou PDF da apólice: nenhum cache intermediário ou do browser guarda.
    private const string SemCache = "no-store";

    public Task InvokeAsync(HttpContext http)
    {
        var caminho = http.Request.Path;
        var csp = caminho.StartsWithSegments(RotasApi.Scalar) ? CspScalar : CspApi;
        var ehApi = caminho.StartsWithSegments(RotasApi.Api);
        http.Response.OnStarting(() =>
        {
            var headers = http.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers.ContentSecurityPolicy = csp;
            headers["Permissions-Policy"] = PermissionsPolicy;
            if (ehApi)
            {
                headers.CacheControl = SemCache;
            }

            return Task.CompletedTask;
        });
        return next(http);
    }
}
