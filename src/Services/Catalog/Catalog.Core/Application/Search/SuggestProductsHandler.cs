using Catalog.Core.Application.Abstractions;
using Catalog.Core.Application.Dtos;
using Catalog.Core.Domain;
using EShop.SharedKernel;

namespace Catalog.Core.Application.Search;

/// <summary>Type-ahead in O(log N + M) without hitting the database or a search cluster.</summary>
public sealed class SuggestProductsHandler(IProductSuggestionIndex index)
{
    public async Task<Result<IReadOnlyList<SuggestionDto>>> HandleAsync(string? prefix, int take)
    {
        prefix = prefix?.Trim();
        if (string.IsNullOrEmpty(prefix) || prefix.Length is < 2 or > 50)
        {
            return CatalogErrors.InvalidQuery;
        }

        var suggestions = await index.SuggestAsync(prefix, Math.Clamp(take, 1, 20)).ConfigureAwait(false);
        return Result<IReadOnlyList<SuggestionDto>>.Success(suggestions);
    }
}
