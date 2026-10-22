using Apolice.Application.Common;
using Apolice.Domain.Notificacoes;

namespace Apolice.Application.Features.Notificacoes;

public sealed record NotificacaoDto(Guid Id, TipoNotificacao Tipo, string Destinatario, string Assunto, string Corpo, DateTimeOffset CriadaEm)
{
    public static NotificacaoDto De(Notificacao notificacao)
    {
        ArgumentNullException.ThrowIfNull(notificacao);
        return new NotificacaoDto(
            notificacao.Id,
            notificacao.Tipo,
            notificacao.Destinatario.Valor,
            notificacao.Assunto,
            notificacao.Corpo,
            notificacao.CriadaEm);
    }

    // SPEC 9: o destinatário é o e-mail do segurado, e o corpo traz nome e valores — nenhum dos dois vai inteiro para log.
    public override string ToString() =>
        $"NotificacaoDto {{ Id = {Id}, Tipo = {Tipo}, Destinatario = {Mascaramento.Email(Destinatario)}, Assunto = {Assunto}, CriadaEm = {CriadaEm:O} }}";
}
