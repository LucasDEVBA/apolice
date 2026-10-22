namespace Apolice.Application.Features.Auth.Logout;

// O token vem do cookie HttpOnly "refresh_token"; logout é idempotente (sempre 204).
public sealed record LogoutCommand(string? RefreshToken)
{
    public override string ToString() => "LogoutCommand { RefreshToken = *** }";
}
