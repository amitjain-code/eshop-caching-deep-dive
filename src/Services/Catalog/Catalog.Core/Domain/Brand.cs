using EShop.SharedKernel;

namespace Catalog.Core.Domain;

public sealed class Brand : Entity<int>
{
    private Brand()
    {
    }

    public Brand(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public string Name { get; private set; } = string.Empty;
}
