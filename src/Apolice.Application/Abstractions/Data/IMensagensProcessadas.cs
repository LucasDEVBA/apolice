namespace Apolice.Application.Abstractions.Data;

// Inbox de idempotência (tabela mensagens_processadas, PK mensagem_id + consumidor).
// Registrar só enfileira no UoW: o registro é gravado junto com o efeito do consumidor.
public interface IMensagensProcessadas
{
    Task<bool> JaProcessadaAsync(Guid mensagemId, string consumidor, CancellationToken cancellationToken);

    void Registrar(Guid mensagemId, string consumidor, DateTimeOffset agora);
}
