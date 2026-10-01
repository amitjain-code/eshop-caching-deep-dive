using System.Security.Claims;
using Basket.Core.Application.Cart;
using Basket.Core.Application.Checkout;
using EShop.Caching.Http;
using EShop.Caching.RateLimiting;
using EShop.ServiceDefaults;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Basket.Api.Endpoints;

internal static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/basket")
            .WithTags("Basket")
            .RequireAuthorization()
            .AddEndpointFilter<ActivityTrackingFilter>();

        // private + max-age=0 + must-revalidate + ETag: the browser keeps a copy but always asks
        // "If-None-Match"; an unchanged basket returns 304 with an empty body.
        group.MapGet("/", GetAsync).CacheHttp(HttpCacheProfiles.PrivateRevalidate);

        group.MapPost("/items", AddAsync);
        group.MapPut("/items/{productId:int}", SetQuantityAsync);
        group.MapDelete("/items/{productId:int}", RemoveAsync);
        group.MapDelete("/", ClearAsync);

        // Distributed rate limit (Redis String INCR, fixed window) shared by every replica.
        group.MapPost("/checkout", CheckoutAsync)
            .RequireRedisRateLimit("checkout", limit: 5, window: TimeSpan.FromMinutes(1), RateLimitAlgorithm.FixedWindow);

        return app;
    }

    public sealed record SetQuantityRequest(int Quantity);

    private static async Task<Ok<CartDto>> GetAsync(ClaimsPrincipal user, CartHandlers handlers) =>
        TypedResults.Ok(await handlers.GetAsync(user.GetRequiredUserId()));

    private static async Task<Results<Ok<CartDto>, ProblemHttpResult>> AddAsync(ClaimsPrincipal user, AddCartItemRequest request, CartHandlers handlers)
    {
        var result = await handlers.AddAsync(user.GetRequiredUserId(), request);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<CartDto>, ProblemHttpResult>> SetQuantityAsync(ClaimsPrincipal user, int productId, SetQuantityRequest request, CartHandlers handlers)
    {
        var result = await handlers.SetQuantityAsync(user.GetRequiredUserId(), productId, request.Quantity);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<CartDto>, ProblemHttpResult>> RemoveAsync(ClaimsPrincipal user, int productId, CartHandlers handlers)
    {
        var result = await handlers.RemoveAsync(user.GetRequiredUserId(), productId);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<NoContent> ClearAsync(ClaimsPrincipal user, CartHandlers handlers)
    {
        await handlers.ClearAsync(user.GetRequiredUserId());
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<CheckoutResult>, ProblemHttpResult>> CheckoutAsync(ClaimsPrincipal user, CheckoutHandler handler)
    {
        var result = await handler.HandleAsync(user.GetRequiredUserId());
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }
}
