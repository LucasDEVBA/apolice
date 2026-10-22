using Apolice.Domain.Usuarios;

namespace Apolice.Api.Seguranca;

// SPEC 7: o Admin passa em todas; as regras por recurso (dono da carteira) continuam nos handlers.
internal static class Politicas
{
    public const string Corretor = nameof(Papel.Corretor);
    public const string Analista = nameof(Papel.Analista);
    public const string Admin = nameof(Papel.Admin);
}
