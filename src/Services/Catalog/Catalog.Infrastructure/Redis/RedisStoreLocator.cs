using System.Globalization;
using Catalog.Core.Application.Abstractions;
using Catalog.Core.Application.Dtos;
using EShop.Caching.Redis;
using StackExchange.Redis;

namespace Catalog.Infrastructure.Redis;

/// <summary>
/// GEO set (a sorted set whose score is a 52-bit geohash).
/// <code>
/// GEOADD    stores:geo 77.2167 28.6315 "1"
/// GEOSEARCH stores:geo FROMLONLAT 77.2 28.6 BYRADIUS 25 km ASC COUNT 10 WITHDIST
/// </code>
/// Without it you would compute haversine distances for every store in SQL on every request.
/// </summary>
internal sealed class RedisStoreLocator(IRedisStore redis) : IStoreLocator
{
    private static readonly RedisKey Key = "stores:geo";

    public Task IndexAsync(IEnumerable<StoreDto> stores)
    {
        var entries = stores
            .Select(s => new GeoEntry(s.Longitude, s.Latitude, s.Id.ToString(CultureInfo.InvariantCulture)))
            .ToArray();
        return entries.Length == 0 ? Task.CompletedTask : redis.Database.GeoAddAsync(Key, entries);
    }

    public async Task<IReadOnlyList<(int StoreId, double DistanceKm)>> FindNearbyAsync(double latitude, double longitude, double radiusKm, int take)
    {
        var results = await redis.Database.GeoSearchAsync(
            Key,
            longitude,
            latitude,
            new GeoSearchCircle(radiusKm, GeoUnit.Kilometers),
            count: take,
            demandClosest: true,
            order: Order.Ascending,
            options: GeoRadiusOptions.WithDistance).ConfigureAwait(false);

        return [.. results.Select(r => (int.Parse(r.Member.ToString(), CultureInfo.InvariantCulture), r.Distance ?? 0d))];
    }
}
