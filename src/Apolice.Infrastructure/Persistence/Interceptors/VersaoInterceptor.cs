using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apolice.Infrastructure.Persistence.Interceptors;

// Concorrência otimista por agregado: todo agregado alterado (na raiz OU em qualquer filho) tem a Versao incrementada,
// o que gera "UPDATE ... SET versao = n + 1 WHERE id = @id AND versao = n". Sem olhar os filhos, dois endossos
// concorrentes (que só inserem linha filha e alteram coberturas) passariam sem conflito pela raiz.
internal sealed class VersaoInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Incrementar(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Incrementar(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void Incrementar(DbContext? contexto)
    {
        if (contexto is null)
        {
            return;
        }

        var entradas = contexto.ChangeTracker.Entries().ToList();
        var raizes = entradas
            .Where(e => ApoliceDbContext.EhAgregado(e.Metadata))
            .ToDictionary(e => (e.Metadata, Chave: ChaveDe(e)));

        var alteradas = raizes.Values.Where(e => e.State == EntityState.Modified).ToHashSet();
        foreach (var filho in entradas.Where(e => !ApoliceDbContext.EhAgregado(e.Metadata) && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            alteradas.UnionWith(RaizesDe(filho, raizes));
        }

        foreach (var raiz in alteradas.Where(r => r.State is EntityState.Modified or EntityState.Unchanged))
        {
            var versao = raiz.Property(ApoliceDbContext.PropriedadeVersao);
            versao.CurrentValue = (int)versao.OriginalValue! + 1;
        }
    }

    // Só a relação de composição conta (FK com navegação de coleção na raiz, ex.: ApoliceSeguro.Endossos); FKs de
    // referência para outro agregado (endosso → usuário autor) não tornam o usuário "alterado".
    private static IEnumerable<EntityEntry> RaizesDe(EntityEntry filho, Dictionary<(IEntityType, object?), EntityEntry> raizes)
    {
        foreach (var fk in filho.Metadata.GetForeignKeys().Where(fk => fk.PrincipalToDependent is not null && ApoliceDbContext.EhAgregado(fk.PrincipalEntityType)))
        {
            var valor = filho.State == EntityState.Deleted
                ? filho.Property(fk.Properties[0].Name).OriginalValue
                : filho.Property(fk.Properties[0].Name).CurrentValue;
            if (raizes.TryGetValue((fk.PrincipalEntityType, valor), out var raiz))
            {
                yield return raiz;
            }
        }
    }

    private static object? ChaveDe(EntityEntry entrada) =>
        entrada.Property(entrada.Metadata.FindPrimaryKey()!.Properties[0].Name).CurrentValue;
}
