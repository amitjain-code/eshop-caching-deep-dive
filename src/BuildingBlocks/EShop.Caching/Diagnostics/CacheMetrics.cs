using System.Diagnostics.Metrics;

namespace EShop.Caching.Diagnostics;

/// <summary>
/// OpenTelemetry-friendly cache metrics. Hit ratio = 1 - (misses / lookups).
/// Export with <c>AddMeter(CacheMetrics.MeterName)</c>.
/// </summary>
public sealed class CacheMetrics
{
    public const string MeterName = "EShop.Caching";

    private readonly Counter<long> _lookups;
    private readonly Counter<long> _invalidations;

    public CacheMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        _lookups = meter.CreateCounter<long>("eshop.cache.lookups", description: "Cache lookups by cache name and result (hit/miss).");
        _invalidations = meter.CreateCounter<long>("eshop.cache.invalidations", description: "Invalidations by origin (local/remote).");
    }

    public void RecordLookup(string cacheName, bool hit) =>
        _lookups.Add(1, new KeyValuePair<string, object?>("cache.name", cacheName), new KeyValuePair<string, object?>("cache.result", hit ? "hit" : "miss"));

    public void RecordInvalidation(string origin, int tagCount, int keyCount) =>
        _invalidations.Add(tagCount + keyCount, new KeyValuePair<string, object?>("origin", origin));
}
