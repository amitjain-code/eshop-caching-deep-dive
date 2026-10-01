using EShop.Caching.Http;

namespace EShop.UnitTests;

public sealed class HttpCacheProfileTests
{
    [Fact]
    public void Public_profile_emits_browser_shared_and_stale_directives()
    {
        var header = HttpCacheProfiles.PublicCatalog.ToCacheControlHeaderValue();

        Assert.Equal("public, max-age=60, s-maxage=300, stale-while-revalidate=60, stale-if-error=3600", header);
        Assert.Equal("max-age=600", HttpCacheProfiles.PublicCatalog.ToCdnCacheControlHeaderValue());
    }

    [Fact]
    public void Private_profile_never_emits_shared_cache_directives()
    {
        var profile = HttpCacheProfiles.PrivateRevalidate with { SharedMaxAge = TimeSpan.FromHours(1), CdnMaxAge = TimeSpan.FromHours(1) };

        Assert.Equal("private, max-age=0, must-revalidate", profile.ToCacheControlHeaderValue());
        Assert.Null(profile.ToCdnCacheControlHeaderValue());
    }

    [Fact]
    public void NoStore_profile_is_just_no_store()
    {
        Assert.Equal("no-store", HttpCacheProfiles.NoStore.ToCacheControlHeaderValue());
    }
}
