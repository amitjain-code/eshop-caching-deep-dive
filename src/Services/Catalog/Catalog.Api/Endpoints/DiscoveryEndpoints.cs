using Catalog.Core.Application.Analytics;
using Catalog.Core.Application.Dtos;
using Catalog.Core.Application.Search;
using Catalog.Core.Application.Stores;
using EShop.Caching.Http;
using EShop.ServiceDefaults;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Catalog.Api.Endpoints;

/// <summary>Endpoints backed directly by Redis data structures (sorted sets, geo).</summary>
internal static class DiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog").WithTags("Discovery");

        group.MapGet("/bestsellers", BestSellersAsync).CacheHttp(HttpCacheProfiles.PublicShortLived);
        group.MapGet("/search/suggest", SuggestAsync).CacheHttp(HttpCacheProfiles.PublicShortLived);
        group.MapGet("/stores/nearby", NearbyStoresAsync).CacheHttp(HttpCacheProfiles.PublicShortLived);

        return app;
    }

    private static async Task<Ok<IReadOnlyList<BestSellerDto>>> BestSellersAsync(GetBestSellersHandler handler, CancellationToken ct, int days = 7, int top = 10) =>
        TypedResults.Ok(await handler.HandleAsync(days, top, ct));

    private static async Task<Results<Ok<IReadOnlyList<SuggestionDto>>, ProblemHttpResult>> SuggestAsync(SuggestProductsHandler handler, string? q, int take = 8)
    {
        var result = await handler.HandleAsync(q, take);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<IReadOnlyList<NearbyStoreDto>>, ProblemHttpResult>> NearbyStoresAsync(
        FindNearbyStoresHandler handler, double lat, double lon, CancellationToken ct, double radiusKm = 25)
    {
        var result = await handler.HandleAsync(lat, lon, radiusKm, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }
}
