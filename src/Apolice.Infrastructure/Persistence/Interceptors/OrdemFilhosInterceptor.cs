using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Apolice.Infrastructure.Persistence.Interceptors;

// As coleções filhas (coberturas, itens, fatores, histórico) são listas ordenadas no domínio, mas tabela não tem ordem:
// a coluna "ordem" guarda a posição de inserção e as leituras ordenam por ela. Itens novos entram depois dos existentes
// (máximo + 1), então uma cobertura incluída por endosso não colide com a posição de uma excluída.
internal sealed class OrdemFilhosInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Numerar(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Numerar(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void Numerar(DbContext? contexto)
    {
        if (contexto is null)
        {
            return;
        }

        var colecoes = contexto.ChangeTracker.Entries()
            .Where(e => ApoliceDbContext.EhAgregado(e.Metadata))
            .SelectMany(e => e.Collections)
            .Where(c => c.Metadata.TargetEntityType.FindProperty(ApoliceDbContext.PropriedadeOrdem) is not null)
            .ToList();

        foreach (var colecao in colecoes)
        {
            NumerarNovos(contexto, colecao);
        }
    }

    private static void NumerarNovos(DbContext contexto, CollectionEntry colecao)
    {
        var filhos = (colecao.CurrentValue ?? Array.Empty<object>()).Cast<object>().Select(o => contexto.Entry(o)).ToList();
        var proxima = filhos
            .Where(f => f.State != EntityState.Added)
            .Select(f => (int)f.Property(ApoliceDbContext.PropriedadeOrdem).CurrentValue!)
            .DefaultIfEmpty(-1)
            .Max() + 1;

        foreach (var novo in filhos.Where(f => f.State == EntityState.Added))
        {
            novo.Property(ApoliceDbContext.PropriedadeOrdem).CurrentValue = proxima++;
        }
    }
}
