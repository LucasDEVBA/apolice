namespace Apolice.Application.Abstractions.Data;

// Contrato com a infraestrutura: conflito de versão (concorrência otimista) é sinalizado com
// ConflitoDeConcorrenciaException, que a API traduz para ConcorrenciaErros.Conflito (409).
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    // Descarta tudo o que foi rastreado ou enfileirado e ainda não foi salvo, inclusive a auditoria pendente (no EF:
    // ChangeTracker.Clear()). Serve para refazer uma operação depois de um conflito: as leituras seguintes pelos
    // repositórios voltam a trazer o estado gravado, com a versão atual.
    void DescartarAlteracoes();
}
