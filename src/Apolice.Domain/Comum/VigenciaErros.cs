namespace Apolice.Domain.Comum;

public static class VigenciaErros
{
    public static Error Invalida { get; } =
        Error.Validacao("Vigencia.Invalida", "O fim da vigência deve ser posterior ao início.");
}
