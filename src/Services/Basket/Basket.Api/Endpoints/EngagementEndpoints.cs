using System.Security.Claims;
using Basket.Core.Application.Engagement;
using EShop.Caching.Http;
using EShop.ServiceDefaults;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Basket.Api.Endpoints;

internal static class EngagementEndpoints
{
    public static IEndpointRouteBuilder MapEngagementEndpoints(this IEndpointRouteBuilder app)
    {
        var recent = app.MapGroup("/api/basket/recently-viewed").WithTags("Recently viewed").RequireAuthorization();
        recent.MapGet("/", GetRecentAsync).CacheHttp(HttpCacheProfiles.PrivateShortLived);
        recent.MapPost("/{productId:int}", AddRecentAsync);

        var wishlist = app.MapGroup("/api/basket/wishlist").WithTags("Wishlist").RequireAuthorization();
        wishlist.MapGet("/", GetWishlistAsync).CacheHttp(HttpCacheProfiles.PrivateRevalidate);
        wishlist.MapGet("/{productId:int}", ContainsAsync).CacheHttp(HttpCacheProfiles.PrivateShortLived);
        wishlist.MapPut("/{productId:int}", AddWishlistAsync);
        wishlist.MapDelete("/{productId:int}", RemoveWishlistAsync);
        wishlist.MapGet("/common/{otherUserId}", CommonAsync).CacheHttp(HttpCacheProfiles.PrivateShortLived);

        return app;
    }

    public sealed record ContainsDto(int ProductId, bool InWishlist);

    private static async Task<Ok<ProductIdsDto>> GetRecentAsync(ClaimsPrincipal user, RecentlyViewedHandlers handlers, int take = 10) =>
        TypedResults.Ok(await handlers.GetAsync(user.GetRequiredUserId(), take));

    private static async Task<Results<NoContent, ProblemHttpResult>> AddRecentAsync(ClaimsPrincipal user, int productId, RecentlyViewedHandlers handlers)
    {
        var result = await handlers.AddAsync(user.GetRequiredUserId(), productId);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
    }

    private static async Task<Ok<ProductIdsDto>> GetWishlistAsync(ClaimsPrincipal user, WishlistHandlers handlers) =>
        TypedResults.Ok(await handlers.GetAsync(user.GetRequiredUserId()));

    private static async Task<Ok<ContainsDto>> ContainsAsync(ClaimsPrincipal user, int productId, WishlistHandlers handlers) =>
        TypedResults.Ok(new ContainsDto(productId, await handlers.ContainsAsync(user.GetRequiredUserId(), productId)));

    private static async Task<Results<NoContent, ProblemHttpResult>> AddWishlistAsync(ClaimsPrincipal user, int productId, WishlistHandlers handlers)
    {
        var result = await handlers.AddAsync(user.GetRequiredUserId(), productId);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
    }

    private static async Task<NoContent> RemoveWishlistAsync(ClaimsPrincipal user, int productId, WishlistHandlers handlers)
    {
        await handlers.RemoveAsync(user.GetRequiredUserId(), productId);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<ProductIdsDto>> CommonAsync(ClaimsPrincipal user, string otherUserId, WishlistHandlers handlers) =>
        TypedResults.Ok(await handlers.CommonAsync(user.GetRequiredUserId(), otherUserId));
}
