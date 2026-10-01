using EShop.Caching.DependencyInjection;
using EShop.Caching.OutputCaching;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Redis connection + invalidation subscriber (so catalog writes evict gateway entries),
// then the Redis-backed output cache acting as the reverse-proxy cache layer.
builder.AddEShopCaching(o => o.ServiceName = "gateway");
builder.AddEShopOutputCache();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// Diagnostic header so you can see which layer answered: X-Gateway-Cache: HIT | MISS
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        if (!context.Response.Headers.ContainsKey("X-Gateway-Cache"))
        {
            context.Response.Headers["X-Gateway-Cache"] = context.Response.Headers.Age.Count > 0 ? "HIT" : "MISS";
        }

        return Task.CompletedTask;
    });
    await next(context);
});

// Must run before the proxy so cached responses short-circuit the upstream call.
app.UseOutputCache();

app.MapHealthChecks("/health/ready");
app.MapReverseProxy();

await app.RunAsync();
