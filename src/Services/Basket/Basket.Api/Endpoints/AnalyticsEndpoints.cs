using Basket.Core.Application.Analytics;
using EShop.ServiceDefaults;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Basket.Api.Endpoints;

internal static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/basket/analytics/active-users", ActiveUsersAsync)
            .WithTags("Analytics")
            .RequireAuthorization(ServiceDefaultsExtensions.AdminPolicy);
        return app;
    }

    private static async Task<Ok<ActiveUsersDto>> ActiveUsersAsync(ActiveUsersHandler handler, int days = 1) =>
        TypedResults.Ok(await handler.CountAsync(days));
}
