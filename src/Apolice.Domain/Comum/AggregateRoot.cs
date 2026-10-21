namespace Apolice.Domain.Comum;

public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

    // Token de concorrência otimista: só a infraestrutura incrementa, ao salvar o agregado modificado.
    public int Versao { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _domainEvents.Add(e);
    }
}
