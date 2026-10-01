using EShop.Caching;
using EShop.Caching.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace EShop.UnitTests;

public sealed class HttpCacheEndpointFilterTests
{
    private sealed record VersionedProduct(int Id, int Version) : IVersionedResource, IHasCacheTags
    {
        public string ResourceVersion => $"{Id}-v{Version}";

        public IEnumerable<string> CacheTags => [$"product:{Id}"];
    }

    private static DefaultHttpContext NewContext(string? ifNoneMatch = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };
        context.Request.Method = HttpMethods.Get;
        if (ifNoneMatch is not null)
        {
            context.Request.Headers.IfNoneMatch = ifNoneMatch;
        }

        return context;
    }

    private static ValueTask<object?> Invoke(HttpContext context, object? handlerResult) =>
        new HttpCacheEndpointFilter(HttpCacheProfiles.PublicCatalog).InvokeAsync(
            new DefaultEndpointFilterInvocationContext(context),
            _ => ValueTask.FromResult(handlerResult));

    [Fact]
    public async Task Success_sets_cache_headers_etag_and_tags()
    {
        var context = NewContext();

        var result = await Invoke(context, TypedResults.Ok(new VersionedProduct(42, 3)));

        Assert.IsType<Ok<VersionedProduct>>(result);
        Assert.Equal(HttpCacheProfiles.PublicCatalog.ToCacheControlHeaderValue(), context.Response.Headers.CacheControl.ToString());
        Assert.Equal("W/\"42-v3\"", context.Response.Headers.ETag.ToString());
        Assert.Equal("product:42", context.Response.Headers[HttpCacheEndpointFilter.CacheTagHeader].ToString());
    }

    [Fact]
    public async Task Matching_if_none_match_short_circuits_with_304()
    {
        var context = NewContext(ifNoneMatch: "W/\"42-v3\"");

        var result = await Invoke(context, TypedResults.Ok(new VersionedProduct(42, 3)));

        var status = Assert.IsType<StatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status304NotModified, status.StatusCode);
    }

    [Fact]
    public async Task Stale_etag_returns_full_response()
    {
        var context = NewContext(ifNoneMatch: "W/\"42-v2\"");

        var result = await Invoke(context, TypedResults.Ok(new VersionedProduct(42, 3)));

        Assert.IsType<Ok<VersionedProduct>>(result);
    }

    [Fact]
    public async Task Errors_are_marked_no_store()
    {
        var context = NewContext();

        await Invoke(context, TypedResults.NotFound());

        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.False(context.Response.Headers.ContainsKey("ETag"));
    }

    [Fact]
    public async Task Unversioned_payload_gets_stable_hash_etag()
    {
        var first = NewContext();
        var second = NewContext();

        await Invoke(first, TypedResults.Ok(new { Name = "menu", Items = new[] { 1, 2, 3 } }));
        await Invoke(second, TypedResults.Ok(new { Name = "menu", Items = new[] { 1, 2, 3 } }));

        Assert.StartsWith("W/\"", first.Response.Headers.ETag.ToString(), StringComparison.Ordinal);
        Assert.Equal(first.Response.Headers.ETag.ToString(), second.Response.Headers.ETag.ToString());
    }
}
