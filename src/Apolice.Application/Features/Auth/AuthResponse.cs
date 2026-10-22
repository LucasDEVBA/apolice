using Apolice.Application.Common;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Features.Auth;

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiraEm, UsuarioDto Usuario)
{
    // O access token é credencial de portador: não pode vazar pelo ToString gerado do record.
    public override string ToString() => $"AuthResponse {{ UsuarioId = {Usuario.Id}, ExpiraEm = {ExpiraEm:O} }}";
}

public sealed record UsuarioDto(Guid Id, string Nome, string Email, Papel Papel)
{
    public static UsuarioDto De(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        return new UsuarioDto(usuario.Id, usuario.Nome, usuario.Email.Valor, usuario.Papel);
    }

    // SPEC 9: e-mail completo nunca vai para log, nem por interpolação descuidada do record.
    public override string ToString() => $"UsuarioDto {{ Id = {Id}, Nome = {Nome}, Email = {Mascaramento.Email(Email)}, Papel = {Papel} }}";
}

// Resultado de login/refresh: o corpo (Resposta) vai no JSON e o refresh token em texto vai SÓ para o cookie
// HttpOnly (refresh_token, Max-Age até RefreshTokenExpiraEm). Nunca serializar este tipo inteiro na resposta.
public sealed record ResultadoAutenticacao(AuthResponse Resposta, string RefreshToken, DateTimeOffset RefreshTokenExpiraEm)
{
    // Evita que o token em texto vaze por log ou depuração do record (o ToString padrão listaria tudo).
    public override string ToString() => $"ResultadoAutenticacao {{ UsuarioId = {Resposta.Usuario.Id} }}";
}
