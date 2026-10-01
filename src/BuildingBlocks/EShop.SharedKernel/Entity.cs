namespace EShop.SharedKernel;

/// <summary>Base type for aggregate roots and entities identified by <typeparamref name="TId"/>.</summary>
public abstract class Entity<TId>
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;
}
