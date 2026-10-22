using Apolice.Domain.Notificacoes;

namespace Apolice.Application.Abstractions.Data;

public interface INotificacaoRepository
{
    void Adicionar(Notificacao notificacao);
}
