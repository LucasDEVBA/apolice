namespace Apolice.Application.Abstractions.Data;

// Exceção (e não Result) porque o conflito só aparece no SaveChanges, longe do handler, e a
// infraestrutura não deve depender de Result para isso; a API a mapeia uniformemente para 409.
public sealed class ConflitoDeConcorrenciaException : Exception
{
    public ConflitoDeConcorrenciaException()
        : base("O registro foi alterado por outra operação. Recarregue e tente novamente.")
    {
    }

    public ConflitoDeConcorrenciaException(string mensagem)
        : base(mensagem)
    {
    }

    public ConflitoDeConcorrenciaException(string mensagem, Exception inner)
        : base(mensagem, inner)
    {
    }
}
