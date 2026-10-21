using Apolice.Domain.Comum;

namespace Apolice.Domain.Usuarios;

public static class RefreshTokenErros
{
    public static Error Revogado { get; } =
        Error.NaoAutorizado("RefreshToken.Revogado", "O refresh token já foi revogado ou utilizado.");

    public static Error Expirado { get; } =
        Error.NaoAutorizado("RefreshToken.Expirado", "O refresh token expirou.");
}
