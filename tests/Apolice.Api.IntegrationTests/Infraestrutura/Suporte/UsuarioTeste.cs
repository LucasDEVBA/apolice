using Apolice.Application.Abstractions.Security;
using Apolice.Domain.Usuarios;

namespace Apolice.Api.IntegrationTests.Infraestrutura.Suporte;

// No lugar do usuário do HttpContext: o teste escolhe quem está "logado" antes de chamar o handler.
public sealed class UsuarioTeste : ICurrentUser
{
    public Guid? UsuarioId { get; private set; }

    public string? Nome { get; private set; }

    public Papel? Papel { get; private set; }

    public bool Autenticado => UsuarioId is not null;

    public void Definir(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        UsuarioId = usuario.Id;
        Nome = usuario.Nome;
        Papel = usuario.Papel;
    }

    public void Anonimo()
    {
        UsuarioId = null;
        Nome = null;
        Papel = null;
    }
}
