namespace EShop.Caching;

/// <summary>A response model that knows its own version. Used to build cheap, stable ETags without hashing.</summary>
public interface IVersionedResource
{
    string ResourceVersion { get; }
}

/// <summary>A response model that knows which cache tags (surrogate keys) it belongs to.</summary>
public interface IHasCacheTags
{
    IEnumerable<string> CacheTags { get; }
}

/// <summary>A response model that exposes a Last-Modified timestamp.</summary>
public interface IHasLastModified
{
    DateTimeOffset LastModified { get; }
}
