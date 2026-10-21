namespace Apolice.Domain.Comum;

public static class SeguradoErros
{
    public static Error DataNascimentoInvalida { get; } =
        Error.Validacao("Segurado.DataNascimentoInvalida", "Data de nascimento inválida.");

    public static Error NomeInvalido(int minimo, int maximo) =>
        Error.Validacao("Segurado.NomeInvalido", $"O nome do segurado deve ter entre {minimo} e {maximo} caracteres.");
}
