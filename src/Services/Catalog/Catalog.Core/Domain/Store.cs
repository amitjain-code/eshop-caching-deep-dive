using EShop.SharedKernel;

namespace Catalog.Core.Domain;

/// <summary>Physical pick-up store, indexed in a Redis GEO set for "stores near me".</summary>
public sealed class Store : Entity<int>
{
    private Store()
    {
    }

    public Store(string name, string city, double latitude, double longitude)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Math.Abs(latitude), 85.05112878);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Math.Abs(longitude), 180);
        Name = name;
        City = city;
        Latitude = latitude;
        Longitude = longitude;
    }

    public string Name { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }
}
