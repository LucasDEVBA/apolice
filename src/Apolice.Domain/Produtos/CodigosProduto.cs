namespace Apolice.Domain.Produtos;

public static class CodigosProduto
{
    public const string Auto = "AUTO";
    public const string Residencial = "RESID";
    public const string Vida = "VIDA";

    public static IReadOnlyList<string> Todos { get; } = [Auto, Residencial, Vida];

    public static bool EhValido(string? codigo) => codigo is not null && Todos.Contains(codigo);
}
