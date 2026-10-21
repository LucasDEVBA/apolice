using Apolice.Domain.Comum;

namespace Apolice.Domain.Notificacoes;

// E-mail simulado: é gravado como caixa de saída exibida na UI, nunca enviado de fato.
public sealed class Notificacao : AggregateRoot<Guid>
{
    private const int AssuntoTamanhoMaximo = 200;
    private const int CorpoTamanhoMaximo = 10_000;

    private Notificacao()
    {
        Destinatario = null!;
        Assunto = null!;
        Corpo = null!;
    }

    private Notificacao(Guid referenciaId, TipoNotificacao tipo, Email destinatario, string assunto, string corpo, DateTimeOffset agora)
        : base(Guid.CreateVersion7(agora))
    {
        ReferenciaId = referenciaId;
        Tipo = tipo;
        Destinatario = destinatario;
        Assunto = assunto;
        Corpo = corpo;
        CriadaEm = agora;
    }

    public Guid ReferenciaId { get; private set; }

    public TipoNotificacao Tipo { get; private set; }

    public Email Destinatario { get; private set; }

    public string Assunto { get; private set; }

    public string Corpo { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    public static Result<Notificacao> Criar(
        Guid referenciaId,
        TipoNotificacao tipo,
        Email destinatario,
        string assunto,
        string corpo,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(destinatario);

        // Referência e tipo vêm sempre de um evento interno (o consumidor escolhe): inválidos são bug, não entrada.
        if (referenciaId == Guid.Empty)
        {
            throw new ArgumentException("A notificação deve referenciar uma apólice ou sinistro.", nameof(referenciaId));
        }

        if (!Enum.IsDefined(tipo))
        {
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de notificação inválido.");
        }

        if (string.IsNullOrWhiteSpace(assunto) || assunto.Trim().Length > AssuntoTamanhoMaximo)
        {
            return NotificacaoErros.AssuntoInvalido(AssuntoTamanhoMaximo);
        }

        return string.IsNullOrWhiteSpace(corpo) || corpo.Length > CorpoTamanhoMaximo
            ? NotificacaoErros.CorpoInvalido(CorpoTamanhoMaximo)
            : new Notificacao(referenciaId, tipo, destinatario, assunto.Trim(), corpo, agora);
    }
}
