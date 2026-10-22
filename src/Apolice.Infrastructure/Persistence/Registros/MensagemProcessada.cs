namespace Apolice.Infrastructure.Persistence.Registros;

// Inbox de idempotência: a PK (mensagem_id, consumidor) faz a segunda gravação concorrente da mesma entrega falhar
// junto com o efeito dela, na mesma transação.
internal sealed class MensagemProcessada
{
    public const int TamanhoMaximoConsumidor = 200;

    private MensagemProcessada()
    {
        Consumidor = null!;
    }

    public MensagemProcessada(Guid mensagemId, string consumidor, DateTimeOffset processadaEm)
    {
        MensagemId = mensagemId;
        Consumidor = consumidor;
        ProcessadaEm = processadaEm;
    }

    public Guid MensagemId { get; private set; }

    public string Consumidor { get; private set; }

    public DateTimeOffset ProcessadaEm { get; private set; }
}
