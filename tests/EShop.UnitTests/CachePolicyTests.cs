using EShop.Caching;

namespace EShop.UnitTests;

public sealed class CachePolicyTests
{
    [Fact]
    public void Jitter_extends_lifetime_within_bounds_and_keeps_local_below_distributed()
    {
        var policy = new CachePolicy(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(1), JitterFactor: 0.2);
        var random = new Random(7);

        for (var i = 0; i < 1_000; i++)
        {
            var (expiration, local) = policy.WithJitter(random);
            Assert.InRange(expiration, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(12));
            Assert.True(local <= expiration);
        }
    }

    [Fact]
    public void Zero_jitter_returns_configured_values()
    {
        var policy = new CachePolicy(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30), JitterFactor: 0);

        Assert.Equal((TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30)), policy.WithJitter());
    }
}
