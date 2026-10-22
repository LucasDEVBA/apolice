using Apolice.Api.Http;
using Apolice.Api.Seguranca;
using Apolice.Application.Abstractions.Security;
using Apolice.Domain.Usuarios;
using Apolice.Infrastructure;
using Apolice.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Apolice.Api.Extensions;

internal static class SegurancaExtensions
{
    public const string SecaoOrigensCors = "Cors:Origens";
    private const int SaltosEncaminhados = 2;
    private static readonly TimeSpan ToleranciaRelogio = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IdadeHsts = TimeSpan.FromDays(365);

    public static IServiceCollection AddSegurancaApi(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddHttpContextAccessor();

        // Substitui o UsuarioSistema que a infraestrutura registra como padrão para hosts sem usuário humano.
        services.Replace(ServiceDescriptor.Scoped<ICurrentUser, HttpCurrentUser>());
        services.ExigirJwtNaInicializacao();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IOptions<JwtOptions>>(ConfigurarJwtBearer);
        AdicionarAutorizacao(services);
        AdicionarCors(services, configuration);
        AdicionarCabecalhosEncaminhados(services);
        services.AddHsts(opcoes =>
        {
            opcoes.MaxAge = IdadeHsts;
            opcoes.IncludeSubDomains = true;
        });
        return services;
    }

    // Mesmas options que o JwtTokenService usa para emitir: emissor, audiência e chave nunca divergem.
    // Algoritmo fixo: um token com "alg" diferente (none, RS256 com a chave HMAC como pública...) é recusado.
    private static void ConfigurarJwtBearer(JwtBearerOptions opcoes, IOptions<JwtOptions> jwt)
    {
        var configuracao = jwt.Value;
        opcoes.MapInboundClaims = false;
        opcoes.SaveToken = false;
        opcoes.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuracao.Issuer,
            ValidateAudience = true,
            ValidAudience = configuracao.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = configuracao.ChaveAssinatura(),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireSignedTokens = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = ToleranciaRelogio,
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = JwtOptions.ClaimPapel,
        };
    }

    // Fallback autenticado: rota nova sem metadado de autorização nasce protegida (as anônimas declaram AllowAnonymous).
    private static void AdicionarAutorizacao(IServiceCollection services) =>
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Politicas.Corretor, p => p.RequireRole(nameof(Papel.Corretor), nameof(Papel.Admin)))
            .AddPolicy(Politicas.Analista, p => p.RequireRole(nameof(Papel.Analista), nameof(Papel.Admin)))
            .AddPolicy(Politicas.Admin, p => p.RequireRole(nameof(Papel.Admin)));

    // Vazio = sem CORS: em produção o browser fala com a mesma origem (rewrite da Vercel), e o cookie é first-party.
    private static void AdicionarCors(IServiceCollection services, IConfiguration configuration)
    {
        var origens = configuration.GetSection(SecaoOrigensCors).Get<string[]>() ?? [];
        services.AddCors(opcoes =>
        {
            if (origens.Length == 0)
            {
                return;
            }

            opcoes.AddDefaultPolicy(politica => politica
                .WithOrigins(origens)
                .AllowCredentials()
                .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put)
                .WithHeaders(
                    "Authorization",
                    "Content-Type",
                    CabecalhosApi.IdempotencyKey,
                    CabecalhosApi.RequestedWith,
                    CabecalhosApi.CorrelationId)
                .WithExposedHeaders(CabecalhosApi.CorrelationId, "Location", "Retry-After", "Content-Disposition"));
        });
    }

    // A API roda atrás de proxy (rewrite da Vercel → Function URL; no compose, direto). O IP do cliente só chega pelo
    // X-Forwarded-For, e sem ele o rate limit por IP colocaria todos os usuários no mesmo balde.
    // ForwardLimit = 2: a Vercel SOBRESCREVE o X-Forwarded-For com o IP do cliente, e a Function URL pode acrescentar o
    // IP de quem a chamou (o nó de saída da Vercel), como API Gateway e ALB fazem. Com 1 salto, nesse caso, o "cliente"
    // seria a Vercel e todos os visitantes do mesmo nó dividiriam o limite; com 2 vale o IP que a Vercel informou, com
    // ou sem o acréscimo da AWS.
    // RISCO (aceito e documentado): os IPs da Vercel não são fixos, então qualquer remetente é aceito como proxy. Quem
    // chama a Function URL direto pode forjar o X-Forwarded-For e trocar de partição no limite por IP (login/demo,
    // refresh e anônimos). O limite por usuário autenticado não depende disso. Mitigação de produção se necessário:
    // aceitar só tráfego do proxy (header secreto ou CloudFront na frente). O limitador é em memória por instância.
    private static void AdicionarCabecalhosEncaminhados(IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(opcoes =>
        {
            opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            opcoes.ForwardLimit = SaltosEncaminhados;
            opcoes.KnownIPNetworks.Clear();
            opcoes.KnownProxies.Clear();
        });
}
