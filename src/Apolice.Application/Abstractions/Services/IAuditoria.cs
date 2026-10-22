namespace Apolice.Application.Abstractions.Services;

// Só enfileira o registro no mesmo UoW do caso de uso: a auditoria grava junto com a mudança ou não grava.
// O usuário, o instante e o correlation id são resolvidos pela infraestrutura.
public interface IAuditoria
{
    void Registrar(string acao, string entidade, string? entidadeId = null, string? detalhes = null);
}
