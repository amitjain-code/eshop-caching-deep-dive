using System.Globalization;
using System.Text;
using Catalog.Core.Application.Abstractions;
using Catalog.Core.Application.Dtos;
using EShop.Caching.Redis;
using StackExchange.Redis;

namespace Catalog.Infrastructure.Redis;

/// <summary>
/// Autocomplete with a lexicographic Sorted Set: every member has score 0, so Redis orders by bytes.
/// <code>
/// ZADD suggest:products 0 "alpine jacket contoso|Alpine Jacket Contoso|17"
/// ZRANGE suggest:products "[alp" "[alp\xff" BYLEX LIMIT 0 10
/// </code>
/// </summary>
internal sealed class RedisProductSuggestionIndex(IRedisStore redis) : IProductSuggestionIndex
{
    private const char Separator = '|';
    private static readonly RedisKey Key = "suggest:products";

    public Task IndexAsync(IEnumerable<(int ProductId, string Name)> products)
    {
        var entries = products
            .Select(p => new SortedSetEntry(
                $"{Normalize(p.Name)}{Separator}{p.Name.Replace(Separator, ' ')}{Separator}{p.ProductId.ToString(CultureInfo.InvariantCulture)}",
                0))
            .ToArray();
        return entries.Length == 0 ? Task.CompletedTask : redis.Database.SortedSetAddAsync(Key, entries);
    }

    public async Task<IReadOnlyList<SuggestionDto>> SuggestAsync(string prefix, int take)
    {
        var normalized = Normalize(prefix);
        var min = Encoding.UTF8.GetBytes(normalized);
        var max = new byte[min.Length + 1];
        min.CopyTo(max, 0);
        max[^1] = 0xFF; // greater than any UTF-8 continuation of the prefix

        var members = await redis.Database.SortedSetRangeByValueAsync(
            Key, min, max, Exclude.None, Order.Ascending, skip: 0, take: take).ConfigureAwait(false);

        var suggestions = new List<SuggestionDto>(members.Length);
        foreach (var member in members)
        {
            var parts = member.ToString().Split(Separator);
            if (parts.Length == 3 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                suggestions.Add(new SuggestionDto(id, parts[1]));
            }
        }

        return suggestions;
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
