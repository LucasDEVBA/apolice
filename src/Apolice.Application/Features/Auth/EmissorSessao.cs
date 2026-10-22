using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Security;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Features.Auth;

public sealed record SessaoEmitida(ResultadoAutenticacao Resultado, RefreshToken Token);

// Login, login demo e refresh emitem a sessão do mesmo jeito: access token curto + refresh token rotativo.
public sealed class EmissorSessao(IRefreshTokenRepository refreshTokens, ITokenService tokens)
{
    // familiaId nulo = novo login (nova família); preenchido = rotação dentro da família existente.
    public SessaoEmitida Emitir(Usuario usuario, Guid? familiaId, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var tokenTexto = tokens.GerarRefreshToken();
        var hash = RefreshToken.CalcularHash(tokenTexto);
        var refresh = familiaId is { } familia
            ? RefreshToken.Criar(usuario.Id, hash, familia, agora, tokens.ValidadeRefreshToken)
            : RefreshToken.NovaFamilia(usuario.Id, hash, agora, tokens.ValidadeRefreshToken);
        refreshTokens.Adicionar(refresh);

        var (accessToken, expiraEm) = tokens.GerarAccessToken(usuario);
        var resposta = new AuthResponse(accessToken, expiraEm, UsuarioDto.De(usuario));
        return new SessaoEmitida(new ResultadoAutenticacao(resposta, tokenTexto, refresh.ExpiraEm), refresh);
    }
}
