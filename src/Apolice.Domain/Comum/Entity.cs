namespace Apolice.Domain.Comum;

public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    protected Entity(TId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        Id = id;
    }

    // Exigido pelo EF Core na materialização; o Id é preenchido logo em seguida.
    protected Entity()
    {
        Id = default!;
    }

    public TId Id { get; private init; }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other.GetType() == GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? esquerda, Entity<TId>? direita) =>
        esquerda?.Equals(direita) ?? direita is null;

    public static bool operator !=(Entity<TId>? esquerda, Entity<TId>? direita) => !(esquerda == direita);
}
