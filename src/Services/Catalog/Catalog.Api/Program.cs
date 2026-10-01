using Catalog.Api.Endpoints;
using Catalog.Core.Application;
using Catalog.Infrastructure;
using EShop.Caching.DependencyInjection;
using EShop.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddEShopCaching(o => o.ServiceName = "catalog");
builder.AddCatalogInfrastructure();
builder.Services.AddCatalogApplication();

var app = builder.Build();

app.UseServiceDefaults();
app.MapDefaultEndpoints();

app.MapProductEndpoints();
app.MapMenuEndpoints();
app.MapDiscoveryEndpoints();
app.MapAdminEndpoints();

await app.RunAsync();
