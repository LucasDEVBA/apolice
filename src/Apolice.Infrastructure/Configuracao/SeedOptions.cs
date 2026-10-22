namespace Apolice.Infrastructure.Configuracao;

// Sem ValidateOnStart: só o verbo "seed" lê a senha (a API em produção nem a recebe); quem a exige é o DataSeeder.
public sealed class SeedOptions
{
    public const string Secao = "Seed";

    public string? SenhaUsuarios { get; set; }

    public bool DadosDemo { get; set; } = true;
}
