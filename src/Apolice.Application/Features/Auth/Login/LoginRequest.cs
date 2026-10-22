namespace Apolice.Application.Features.Auth.Login;

public sealed record LoginRequest(string Email, string Senha)
{
    // O corpo pode acabar num log de requisição: o ToString gerado do record exporia a senha.
    public override string ToString() => "LoginRequest { Email = ***, Senha = *** }";
}

public sealed record LoginCommand(string Email, string Senha)
{
    // A senha nunca pode aparecer em log por causa do ToString gerado do record.
    public override string ToString() => "LoginCommand { Email = ***, Senha = *** }";
}
