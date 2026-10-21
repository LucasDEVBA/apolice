namespace Apolice.Domain.Comum;

public static class CpfErros
{
    public static Error Invalido { get; } = Error.Validacao("Cpf.Invalido", "CPF inválido.");
}
