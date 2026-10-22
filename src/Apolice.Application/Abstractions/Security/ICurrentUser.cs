using Apolice.Application.Common;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Abstractions.Security;

public interface ICurrentUser
{
    Guid? UsuarioId { get; }

    string? Nome { get; }

    Papel? Papel { get; }

    bool Autenticado { get; }

    // Analista e Admin enxergam a carteira inteira; Corretor só o que é dele (CorretorId == UsuarioId).
    // Derivado do Papel e não usado nas decisões da aplicação (que olham o Papel): a infra não precisa implementar,
    // e uma implementação divergente não amplia o escopo de ninguém.
    bool PodeVerTudo => AcessoUsuario.TemVisaoGlobal(Papel);
}
