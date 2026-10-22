using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Apolice.Api.Http;

// SPEC 7: "auth" = janela fixa de 10/min por IP (login e demo: alvo de força bruta); "refresh" = janela própria de
// 30/min por IP (o cookie rotativo de 256 bits não se adivinha, e a SPA renova a cada recarga de página: no mesmo balde
// do login, recarregar algumas vezes derrubaria a sessão); global = token bucket de 120/min por usuário autenticado,
// ou por IP para anônimos. Recarga contínua (2 fichas/s) em vez de 120 de uma vez
// a cada minuto: absorve rajadas legítimas sem abrir uma janela de 240 requisições na virada do minuto.
internal static class LimitesRequisicao
{
    public const string PoliticaAuth = "auth";
    public const string PoliticaRefresh = "refresh";
    private const int LimiteAuthPorJanela = 10;
    private const int LimiteRefreshPorJanela = 30;
    private const int LimiteGlobal = 120;
    private const int FichasPorRecarga = 2;
    private const string IpDesconhecido = "desconhecido";
    private static readonly TimeSpan JanelaAuth = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PeriodoRecarga = TimeSpan.FromSeconds(1);

    public static IServiceCollection AddLimitesRequisicao(this IServiceCollection services) =>
        services.AddRateLimiter(opcoes =>
        {
            opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opcoes.OnRejected = RejeitarAsync;
            opcoes.AddPolicy(PoliticaAuth, http => JanelaPorIp(http, LimiteAuthPorJanela));
            opcoes.AddPolicy(PoliticaRefresh, http => JanelaPorIp(http, LimiteRefreshPorJanela));
            opcoes.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http => RateLimitPartition.GetTokenBucketLimiter(ChaveGlobal(http), _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = LimiteGlobal,
                TokensPerPeriod = FichasPorRecarga,
                ReplenishmentPeriod = PeriodoRecarga,
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
        });

    private static RateLimitPartition<string> JanelaPorIp(HttpContext http, int limite) =>
        RateLimitPartition.GetFixedWindowLimiter(Ip(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limite,
            Window = JanelaAuth,
            QueueLimit = 0,
        });

    // O usuário só é confiável porque a autenticação (JWT validado) roda antes do limitador; ver UsePipelineApolice.
    private static string ChaveGlobal(HttpContext http) =>
        http.User.Identity?.IsAuthenticated == true && http.User.FindFirstValue(JwtRegisteredClaimNames.Sub) is { Length: > 0 } sub
            ? $"usuario:{sub}"
            : $"ip:{Ip(http)}";

    private static string Ip(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? IpDesconhecido;

    private static async ValueTask RejeitarAsync(OnRejectedContext contexto, CancellationToken cancellationToken)
    {
        var http = contexto.HttpContext;
        var problema = Problemas.Criar(
            StatusCodes.Status429TooManyRequests,
            Problemas.CodigoLimiteRequisicoes,
            "Muitas requisições em pouco tempo. Aguarde e tente novamente.");

        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
        {
            var segundos = Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds));
            http.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);
            problema.Extensions["retryAfter"] = segundos;
        }

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await http.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problema });
    }
}
