using System.Diagnostics;
using Apolice.Domain.Comum;
using Apolice.Infrastructure.Correlacao;
using Apolice.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Apolice.Infrastructure.Persistence.Interceptors;

// Eventos de domínio viram linhas do outbox no MESMO SaveChanges do agregado: ou gravam juntos ou nenhum grava.
// Os eventos saem do agregado aqui (e não depois do commit): a partir deste ponto eles SÃO as linhas rastreadas, então
// uma nova tentativa de SaveChanges não os duplica e DescartarAlteracoes os descarta junto com o agregado.
internal sealed class DomainEventsToOutboxInterceptor(ContextoCorrelacao correlacao) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Converter(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Converter(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Converter(DbContext? contexto)
    {
        if (contexto is null)
        {
            return;
        }

        var traceparent = Activity.Current is { IdFormat: ActivityIdFormat.W3C } atividade ? atividade.Id : null;
        var agregados = contexto.ChangeTracker.Entries()
            .Select(e => EventosDe(e.Entity))
            .Where(a => a is not null && a.Value.Eventos.Count > 0)
            .Select(a => a!.Value)
            .ToList();

        var mensagens = agregados
            .SelectMany(a => a.Eventos)
            .OrderBy(e => e.OcorridoEm)
            .Select(e => OutboxMessage.De(e, traceparent, correlacao.CorrelationId))
            .ToList();
        contexto.Set<OutboxMessage>().AddRange(mensagens);
        agregados.ForEach(a => a.Limpar());
    }

    // Os agregados do domínio têm id Guid ou string (Produto, cujo id é o código).
    private static (IReadOnlyCollection<IDomainEvent> Eventos, Action Limpar)? EventosDe(object entidade) => entidade switch
    {
        AggregateRoot<Guid> agregado => (agregado.DomainEvents, agregado.ClearDomainEvents),
        AggregateRoot<string> agregado => (agregado.DomainEvents, agregado.ClearDomainEvents),
        _ => null,
    };
}
