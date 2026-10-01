using Catalog.Core.Application.Dtos;
using Catalog.Core.Application.Menu;
using EShop.Caching.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Catalog.Api.Endpoints;

internal static class MenuEndpoints
{
    public static IEndpointRouteBuilder MapMenuEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog/menu", GetMenuAsync)
            .WithTags("Menu")
            .CacheHttp(HttpCacheProfiles.PublicReferenceData);
        return app;
    }

    private static async Task<Ok<MenuDto>> GetMenuAsync(GetMenuHandler handler, CancellationToken ct) =>
        TypedResults.Ok(await handler.HandleAsync(ct));
}
