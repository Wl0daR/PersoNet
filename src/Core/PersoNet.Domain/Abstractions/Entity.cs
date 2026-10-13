namespace PersoNet.Domain.Abstractions;

/// <summary>
/// Base type for entities identified by a time-ordered GUID (version 7).
/// </summary>
public abstract class Entity
{
    protected Entity()
        : this(Guid.CreateVersion7())
    {
    }

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Entity id cannot be empty.", nameof(id));
        }

        Id = id;
    }

    public Guid Id { get; }
}
