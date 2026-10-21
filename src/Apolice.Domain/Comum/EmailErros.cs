namespace Apolice.Domain.Comum;

public static class EmailErros
{
    public static Error Invalido { get; } = Error.Validacao("Email.Invalido", "E-mail inválido.");
}
