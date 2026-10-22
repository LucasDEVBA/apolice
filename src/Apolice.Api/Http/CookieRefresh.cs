namespace Apolice.Api.Http;

// SPEC 7: refresh_token — HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth; Max-Age = validade do token (7 d).
// O Path restringe o envio às rotas de sessão: o cookie não viaja em nenhuma outra requisição da API.
internal static class CookieRefresh
{
    public const string Nome = "refresh_token";
    public const string Caminho = RotasApi.BaseV1 + "/auth";

    public static string? Ler(HttpRequest request) =>
        request.Cookies.TryGetValue(Nome, out var valor) && !string.IsNullOrWhiteSpace(valor) ? valor : null;

    // Max-Age = validade configurada (Jwt:RefreshTokenDias), e não "expiração − agora": o tempo gasto no request
    // (1–2 s num cold start) viraria 604798. O cookie viver esse instante a mais é inofensivo: o servidor recusa o
    // token expirado pela data gravada no banco.
    public static void Gravar(HttpResponse response, string token, TimeSpan validade) =>
        response.Cookies.Append(Nome, token, Opcoes(validade));

    // Max-Age=0 explícito (além do Expires no passado) e os mesmos Path/flags: só assim o browser casa e apaga o cookie.
    public static void Remover(HttpResponse response)
    {
        var opcoes = Opcoes(TimeSpan.Zero);
        opcoes.Expires = DateTimeOffset.UnixEpoch;
        response.Cookies.Append(Nome, string.Empty, opcoes);
    }

    private static CookieOptions Opcoes(TimeSpan validade) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Caminho,
        MaxAge = validade > TimeSpan.Zero ? validade : TimeSpan.Zero,
        IsEssential = true,
    };
}
