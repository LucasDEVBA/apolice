using Apolice.Application.Abstractions.Security;
using Apolice.Domain.Usuarios;

namespace Apolice.Infrastructure.Security;

// Padrão para hosts sem usuário humano (Worker, seed, relay): ninguém autenticado. A API registra a própria
// implementação (a partir do HttpContext), que prevalece sobre esta.
internal sealed class UsuarioSistema : ICurrentUser
{
    public Guid? UsuarioId => null;

    public string? Nome => null;

    public Papel? Papel => null;

    public bool Autenticado => false;
}
