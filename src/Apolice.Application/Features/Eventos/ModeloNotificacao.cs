using Apolice.Domain.Comum;
using Apolice.Domain.Notificacoes;

namespace Apolice.Application.Features.Eventos;

public sealed record ModeloNotificacao(string Assunto, string Corpo);

internal static class FabricaNotificacao
{
    // Os modelos são montados aqui, a partir de eventos internos: e-mail ou texto inválido é bug, não entrada
    // do usuário — a exceção devolve a mensagem para nova tentativa e, persistindo, para a DLQ, onde fica visível.
    public static Notificacao Criar(Guid referenciaId, TipoNotificacao tipo, string destinatario, ModeloNotificacao modelo, DateTimeOffset agora)
    {
        var email = Email.Criar(destinatario);
        if (email.IsFailure)
        {
            throw new InvalidOperationException($"Evento com e-mail de destinatário inválido para a notificação {tipo}.");
        }

        var notificacao = Notificacao.Criar(referenciaId, tipo, email.Value, modelo.Assunto, modelo.Corpo, agora);
        return notificacao.IsSuccess
            ? notificacao.Value
            : throw new InvalidOperationException($"Modelo de notificação {tipo} inválido: {notificacao.Error.Codigo}.");
    }
}
