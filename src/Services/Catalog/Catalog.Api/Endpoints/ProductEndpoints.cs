using System.Security.Claims;
using Catalog.Core.Application.Analytics;
using Catalog.Core.Application.Dtos;
using Catalog.Core.Application.Products;
using EShop.Caching.Http;
using EShop.ServiceDefaults;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Catalog.Api.Endpoints;

internal static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog/products").WithTags("Products");

        // Public + cacheable everywhere: browser (60 s), gateway (5 min), CDN (10 min, purge by tag).
        group.MapGet("/", ListAsync).CacheHttp(HttpCacheProfiles.PublicCatalog);
        group.MapGet("/{id:int}", GetAsync).CacheHttp(HttpCacheProfiles.PublicCatalog);

        // Side effects never ride on a cacheable GET (a CDN hit would never reach us): use a POST beacon.
        group.MapPost("/{id:int}/views", RecordViewAsync);
        group.MapGet("/{id:int}/views/unique", UniqueViewsAsync).CacheHttp(HttpCacheProfiles.PublicShortLived);

        return app;
    }

    private static async Task<Results<Ok<ProductListDto>, ProblemHttpResult>> ListAsync(
        GetProductsHandler handler, CancellationToken ct, int page = 1, int pageSize = 12, int? categoryId = null, int? brandId = null)
    {
        var result = await handler.HandleAsync(page, pageSize, categoryId, brandId, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<ProductDto>, ProblemHttpResult>> GetAsync(int id, GetProductByIdHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Accepted, ProblemHttpResult>> RecordViewAsync(int id, HttpContext http, ProductViewHandlers handler)
    {
        var visitorId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? http.Request.Headers["X-Visitor-Id"].FirstOrDefault()
            ?? http.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        var result = await handler.RecordAsync(id, visitorId);
        return result.IsSuccess ? TypedResults.Accepted((string?)null) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<UniqueViewsDto>, ProblemHttpResult>> UniqueViewsAsync(int id, ProductViewHandlers handler, int days = 7)
    {
        var result = await handler.CountAsync(id, days);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }
}
