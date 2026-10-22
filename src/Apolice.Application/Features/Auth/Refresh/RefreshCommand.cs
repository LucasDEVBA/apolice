namespace Apolice.Application.Features.Auth.Refresh;

// O token vem do cookie HttpOnly "refresh_token" (a API o lê); não há corpo.
public sealed record RefreshCommand(string? RefreshToken)
{
    public override string ToString() => "RefreshCommand { RefreshToken = *** }";
}
