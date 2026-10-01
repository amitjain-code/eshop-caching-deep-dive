using Basket.Api.Endpoints;
using Basket.Core.Application;
using Basket.Infrastructure;
using EShop.Caching.DependencyInjection;
using EShop.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddEShopCaching(o => o.ServiceName = "basket");
builder.AddBasketInfrastructure();
builder.Services.AddBasketApplication();

// ASP.NET Core Session on top of IDistributedCache = Redis (registered by AddEShopCaching).
// Any replica can serve any request: no sticky sessions at the load balancer.
builder.Services.AddSession(o =>
{
    o.Cookie.Name = ".eshop.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.IsEssential = true;
    o.IdleTimeout = TimeSpan.FromMinutes(20);
});

var app = builder.Build();

app.UseServiceDefaults();
app.UseSession();
app.MapDefaultEndpoints();

app.MapCartEndpoints();
app.MapEngagementEndpoints();
app.MapSessionEndpoints();
app.MapAnalyticsEndpoints();

await app.RunAsync();
