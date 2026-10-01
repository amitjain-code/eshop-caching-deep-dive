using Basket.Core.Application.Analytics;
using EShop.ServiceDefaults;

namespace Basket.Api.Endpoints;

/// <summary>Marks the caller as active today (one SETBIT) on every authenticated basket call.</summary>
internal sealed class ActivityTrackingFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (http.User.Identity?.IsAuthenticated == true)
        {
            await http.RequestServices.GetRequiredService<ActiveUsersHandler>()
                .TrackAsync(http.User.GetRequiredUserId()).ConfigureAwait(false);
        }

        return await next(context).ConfigureAwait(false);
    }
}
