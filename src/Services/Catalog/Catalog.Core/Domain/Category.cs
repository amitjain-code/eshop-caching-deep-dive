using EShop.SharedKernel;

namespace Catalog.Core.Domain;

public sealed class Category : Entity<int>
{
    private Category()
    {
    }

    public Category(string name, string slug, int displayOrder, int? parentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        Name = name;
        Slug = slug;
        DisplayOrder = displayOrder;
        ParentId = parentId;
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public int DisplayOrder { get; private set; }

    public int? ParentId { get; private set; }
}
