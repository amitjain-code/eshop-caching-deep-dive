using System.Security.Claims;
using Basket.Core.Application.Abstractions;
using Basket.Core.Application.Sessions;
using EShop.Caching.Http;
using EShop.ServiceDefaults;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Basket.Api.Endpoints;

internal static class SessionEndpoints
{
    public const string SessionHeader = "X-Session-Id";

    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        // 1) Explicit user sessions (Hash + Set) - list devices, revoke one, sign out everywhere.
        var sessions = app.MapGroup("/api/sessions").WithTags("Sessions").CacheHttp(HttpCacheProfiles.NoStore);
        sessions.MapPost("/", CreateAsync).RequireAuthorization();
        sessions.MapGet("/", ListAsync).RequireAuthorization();
        sessions.MapGet("/current", CurrentAsync);
        sessions.MapDelete("/{sessionId}", RevokeAsync).RequireAuthorization();
        sessions.MapDelete("/", RevokeAllAsync).RequireAuthorization();

        // 2) ASP.NET Core Session (IDistributedCache -> Redis String) for anonymous shopper preferences.
        var preferences = app.MapGroup("/api/session/preferences").WithTags("Anonymous session").CacheHttp(HttpCacheProfiles.NoStore);
        preferences.MapGet("/", GetPreferencesAsync);
        preferences.MapPut("/", SetPreferencesAsync);

        return app;
    }

    public sealed record PreferencesDto(string Currency, string Locale);

    private static async Task<Ok<UserSession>> CreateAsync(ClaimsPrincipal user, HttpContext http, SessionHandlers handlers) =>
        TypedResults.Ok(await handlers.CreateAsync(
            user.GetRequiredUserId(),
            http.Request.Headers.UserAgent.ToString(),
            http.Connection.RemoteIpAddress?.ToString()));

    private static async Task<Ok<IReadOnlyList<UserSession>>> ListAsync(ClaimsPrincipal user, SessionHandlers handlers) =>
        TypedResults.Ok(await handlers.ListAsync(user.GetRequiredUserId()));

    private static async Task<Results<Ok<UserSession>, ProblemHttpResult>> CurrentAsync(HttpContext http, SessionHandlers handlers)
    {
        var sessionId = http.Request.Headers[SessionHeader].ToString();
        var result = await handlers.ValidateAsync(sessionId);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<NoContent, NotFound>> RevokeAsync(ClaimsPrincipal user, string sessionId, SessionHandlers handlers) =>
        await handlers.RevokeAsync(user.GetRequiredUserId(), sessionId) ? TypedResults.NoContent() : TypedResults.NotFound();

    private static async Task<Ok<int>> RevokeAllAsync(ClaimsPrincipal user, SessionHandlers handlers) =>
        TypedResults.Ok(await handlers.RevokeAllAsync(user.GetRequiredUserId()));

    private static async Task<Ok<PreferencesDto>> GetPreferencesAsync(HttpContext http)
    {
        await http.Session.LoadAsync(http.RequestAborted);
        return TypedResults.Ok(new PreferencesDto(
            http.Session.GetString("currency") ?? "INR",
            http.Session.GetString("locale") ?? "en-IN"));
    }

    private static async Task<Ok<PreferencesDto>> SetPreferencesAsync(HttpContext http, PreferencesDto preferences)
    {
        await http.Session.LoadAsync(http.RequestAborted);
        http.Session.SetString("currency", preferences.Currency);
        http.Session.SetString("locale", preferences.Locale);
        await http.Session.CommitAsync(http.RequestAborted);
        return TypedResults.Ok(preferences);
    }
}
