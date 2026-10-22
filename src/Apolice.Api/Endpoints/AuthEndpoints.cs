using Apolice.Api.Http;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Auth.Login;
using Apolice.Application.Features.Auth.LoginDemo;
using Apolice.Application.Features.Auth.Logout;
using Apolice.Application.Features.Auth.Me;
using Apolice.Application.Features.Auth.Refresh;
using Apolice.Domain.Comum;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apolice.Api.Endpoints;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags(TagsApi.Autenticacao);

        auth.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Entrar com e-mail e senha")
            .WithDescription("Devolve o access token (15 min) e grava o refresh token rotativo no cookie HttpOnly refresh_token. " +
                "E-mail inexistente e senha errada respondem igual; a 5ª falha seguida bloqueia a conta por 15 minutos.")
            .AllowAnonymous()
            .RequireRateLimiting(LimitesRequisicao.PoliticaAuth)
            .Validar<LoginRequest>()
            .ComProblemas(StatusCodes.Status401Unauthorized);

        auth.MapPost("/demo", LoginDemoAsync)
            .WithName("LoginDemo")
            .WithSummary("Entrar como persona de demonstração")
            .WithDescription("Personas: corretora, corretor, analista e admin. Disponível enquanto a flag LoginDemo estiver ligada. " +
                "Demonstração pública: qualquer visitante entra como qualquer persona (inclusive Admin), então não use dados reais.")
            .AllowAnonymous()
            .RequireRateLimiting(LimitesRequisicao.PoliticaAuth)
            .Validar<LoginDemoRequest>()
            .ComProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound);

        auth.MapPost("/refresh", RenovarAsync)
            .WithName("RenovarSessao")
            .WithSummary("Renovar a sessão")
            .WithDescription("Troca o refresh token do cookie por um novo par (rotação). Reapresentar um token já trocado " +
                "encerra todas as sessões daquele login. 409 = duas renovações simultâneas do mesmo cookie (ex.: duas abas): tente de novo.")
            .AllowAnonymous()
            .RequireRateLimiting(LimitesRequisicao.PoliticaRefresh)
            .ExigirCabecalhoCsrf()
            .ComProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status409Conflict);

        auth.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .WithSummary("Encerrar a sessão")
            .WithDescription("Revoga o refresh token do cookie (e a família dele) e apaga o cookie. Idempotente.")
            .AllowAnonymous()
            .ExigirCabecalhoCsrf();

        auth.MapGet("/me", ObterUsuarioAtualAsync)
            .WithName("ObterUsuarioAtual")
            .WithSummary("Usuário autenticado")
            .ComProblemas(StatusCodes.Status401Unauthorized);

        return api;
    }

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request, LoginHandler handler, ITokenService tokens, HttpContext http, CancellationToken cancellationToken) =>
        Sessao(await handler.HandleAsync(new LoginCommand(request.Email, request.Senha), cancellationToken), http.Response, tokens);

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> LoginDemoAsync(
        LoginDemoRequest request, LoginDemoHandler handler, ITokenService tokens, HttpContext http, CancellationToken cancellationToken) =>
        Sessao(await handler.HandleAsync(new LoginDemoCommand(request.Persona), cancellationToken), http.Response, tokens);

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> RenovarAsync(
        HttpContext http, RefreshHandler handler, ITokenService tokens, CancellationToken cancellationToken)
    {
        var resultado = await handler.HandleAsync(new RefreshCommand(CookieRefresh.Ler(http.Request)), cancellationToken);

        // Cookie recusado (expirado, revogado ou reutilizado) não serve mais para nada: o browser deixa de reenviá-lo.
        if (resultado.IsFailure)
        {
            CookieRefresh.Remover(http.Response);
        }

        return Sessao(resultado, http.Response, tokens);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> LogoutAsync(
        HttpContext http, LogoutHandler handler, CancellationToken cancellationToken)
    {
        var resultado = await handler.HandleAsync(new LogoutCommand(CookieRefresh.Ler(http.Request)), cancellationToken);
        CookieRefresh.Remover(http.Response);
        return resultado.IsSuccess ? TypedResults.NoContent() : Problemas.De(resultado.Error);
    }

    private static async Task<Results<Ok<UsuarioDto>, ProblemHttpResult>> ObterUsuarioAtualAsync(
        ObterUsuarioAtualHandler handler, CancellationToken cancellationToken) =>
        ResultadosHttp.Ok(await handler.HandleAsync(new ObterUsuarioAtualQuery(), cancellationToken));

    // O refresh token em texto só sai no cookie; o corpo leva o access token e o usuário (ResultadoAutenticacao.Resposta).
    private static Results<Ok<AuthResponse>, ProblemHttpResult> Sessao(Result<ResultadoAutenticacao> resultado, HttpResponse response, ITokenService tokens)
    {
        if (resultado.IsFailure)
        {
            return Problemas.De(resultado.Error);
        }

        var sessao = resultado.Value;
        CookieRefresh.Gravar(response, sessao.RefreshToken, tokens.ValidadeRefreshToken);
        return TypedResults.Ok(sessao.Resposta);
    }
}
