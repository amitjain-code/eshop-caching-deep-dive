using Catalog.Core.Application.Products;
using EShop.ServiceDefaults;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Catalog.Api.Endpoints;

internal static class AdminEndpoints
{
    public sealed record ChangePriceRequest(decimal Price);

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/catalog/admin/products/{id:int}/price", ChangePriceAsync)
            .WithTags("Admin")
            .RequireAuthorization(ServiceDefaultsExtensions.AdminPolicy);
        return app;
    }

    /// <summary>Triggers the full invalidation chain: L1 (all nodes) -> L2 -> gateway -> CDN.</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> ChangePriceAsync(
        int id, ChangePriceRequest request, UpdateProductPriceHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, request.Price, ct);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
    }
}
