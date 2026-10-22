namespace Apolice.Application.Abstractions.Messaging;

// Tipo de mensagem que esta versão não conhece (ex.: a API já emite um evento novo e o Worker ainda não foi
// atualizado). Exceção, e não descarte: a mensagem volta à fila e, esgotadas as entregas, vai para a DLQ, de onde
// pode ser reenviada depois do deploy. Tipo próprio para o consumidor da infra poder tratá-la à parte, se quiser.
public sealed class MensagemDesconhecidaException : Exception
{
    public MensagemDesconhecidaException()
        : base("Tipo de mensagem desconhecido.")
    {
    }

    public MensagemDesconhecidaException(string mensagem)
        : base(mensagem)
    {
    }

    public MensagemDesconhecidaException(string mensagem, Exception inner)
        : base(mensagem, inner)
    {
    }
}
