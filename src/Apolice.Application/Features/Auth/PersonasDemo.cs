using System.Collections.Frozen;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Features.Auth;

// Usuário de demonstração do SPEC 6: o DataSeeder da infra deve criar exatamente estes (e não uma cópia própria),
// senão o login demo aponta para e-mails que não existem.
public sealed record UsuarioDemo(string Persona, string Email, string Nome, Papel Papel);

// Personas do login de demonstração → usuários criados pelo DataSeeder (SPEC 6).
public static class PersonasDemo
{
    public const string Corretora = "corretora";
    public const string Corretor = "corretor";
    public const string Analista = "analista";
    public const string Admin = "admin";

    public static IReadOnlyList<UsuarioDemo> Usuarios { get; } =
    [
        new(Corretora, "ana.corretora@apolice.dev", "Ana Ribeiro", Papel.Corretor),
        new(Corretor, "bruno.corretor@apolice.dev", "Bruno Lima", Papel.Corretor),
        new(Analista, "carla.analista@apolice.dev", "Carla Souza", Papel.Analista),
        new(Admin, "admin@apolice.dev", "Diego Martins", Papel.Admin),
    ];

    private static readonly FrozenDictionary<string, string> EmailsPorPersona =
        Usuarios.ToFrozenDictionary(u => u.Persona, u => u.Email, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> Todas => EmailsPorPersona.Keys;

    public static string? EmailDe(string? persona) =>
        persona is not null && EmailsPorPersona.TryGetValue(persona.Trim(), out var email) ? email : null;
}
