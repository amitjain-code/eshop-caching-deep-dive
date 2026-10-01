# 01 · Architecture

> How the solution is organised, why it is organised that way, and how a request travels through every cache layer.

## 1. Pragmatic clean architecture

Classic clean architecture splits every service into four projects (Domain, Application, Infrastructure, Presentation). That is a lot of ceremony for a service with a handful of use cases. This solution keeps the **dependency rule** but collapses the two inner rings into one project:

| Ring | Project | Depends on | Contains |
|---|---|---|---|
| Core (Domain + Application) | `Catalog.Core`, `Basket.Core` | `EShop.SharedKernel`, `EShop.Caching.Abstractions` | Entities, domain errors, use-case handlers, **ports** (interfaces) such as `ICartRepository`, `IStoreLocator`, `IAppCache` |
| Infrastructure | `Catalog.Infrastructure`, `Basket.Infrastructure` | Core + `EShop.Caching` | EF Core, Redis adapters (one class per data structure), hosted services |
| Presentation | `Catalog.Api`, `Basket.Api`, `EShop.Gateway` | Infrastructure + `EShop.ServiceDefaults` | Minimal-API endpoints, HTTP cache profiles, composition root |

The rule that matters: **Core never references Redis, EF Core or ASP.NET Core.** It talks to caches through `IAppCache`, `ICacheInvalidator` and `IDistributedLock`, which live in a dependency-free abstractions package. Swapping Redis for Garnet/Valkey/Azure Managed Redis, or HybridCache for FusionCache, touches Infrastructure only.

```mermaid
flowchart TB
    subgraph BB["BuildingBlocks (framework as a library)"]
        SK["EShop.SharedKernel<br/>Result · Error · Entity · stream contracts"]
        CA["EShop.Caching.Abstractions<br/>IAppCache · CachePolicy · ICacheInvalidator · IDistributedLock"]
        C["EShop.Caching<br/>HTTP headers/ETag · Output cache · CDN purge<br/>HybridCache L1/L2 · Pub/Sub invalidation · Redis primitives"]
        SD["EShop.ServiceDefaults<br/>ProblemDetails · Health · OpenAPI · Auth"]
    end

    subgraph CAT["Catalog service"]
        CC["Catalog.Core"]
        CI["Catalog.Infrastructure"]
        CAPI["Catalog.Api"]
    end

    subgraph BAS["Basket service"]
        BC["Basket.Core"]
        BI["Basket.Infrastructure"]
        BAPI["Basket.Api"]
    end

    GW["EShop.Gateway (YARP)"]

    CC --> SK & CA
    BC --> SK & CA
    C --> CA
    SD --> SK
    CI --> CC & C
    BI --> BC & C
    CAPI --> CI & SD
    BAPI --> BI & SD
    GW --> C
```

### Why one Core project instead of Domain + Application?

* Use cases are small (`GetProductByIdHandler` is ~30 lines). Splitting them from the entities they use adds navigation cost and no protection.
* The **port interfaces** are what keep infrastructure out, and they live in Core either way.
* If a service grows, splitting `Core` into `Domain` and `Application` is a mechanical refactor because the dependency direction is already correct.

### Why no MediatR?

Handlers are plain classes registered in DI and injected straight into endpoints. Fewer abstractions, explicit call graph, zero reflection. Cross-cutting behaviour (caching headers, rate limiting, activity tracking) is applied with **endpoint filters**, which is the native ASP.NET Core pipeline for that job.

## 2. The framework library: `EShop.Caching`

Everything reusable about caching lives in one library so each service only *declares* intent:

```csharp
builder.AddEShopCaching(o => o.ServiceName = "catalog");      // Redis, HybridCache, invalidation, lock, rate limiter
group.MapGet("/{id:int}", GetAsync).CacheHttp(HttpCacheProfiles.PublicCatalog);  // browser + CDN + proxy headers
```

| Folder | What it gives you | Main types |
|---|---|---|
| `Http/` | Cache-Control / CDN-Cache-Control / Vary / ETag / Last-Modified / Cache-Tag headers and 304 handling | `HttpCacheProfile`, `HttpCacheProfiles`, `HttpCacheEndpointFilter`, `.CacheHttp()` |
| `OutputCaching/` | Reverse-proxy cache that obeys the origin's headers, Redis store, tag eviction | `OriginControlledOutputCachePolicy`, `AddEShopOutputCache()` |
| `Cdn/` | Purge by tag at the CDN edge | `ICdnPurger`, `CloudflareCdnPurger`, `NoOpCdnPurger` |
| `Hybrid/` | Two-level application cache with stampede protection, jitter and metrics | `HybridAppCache : IAppCache` |
| `Invalidation/` | One call invalidates L1 on every node, L2, gateway and CDN | `RedisCacheInvalidator`, `CacheInvalidationSubscriber`, `ICacheInvalidationHandler` |
| `Redis/` | Key-prefixed database, distributed lock, health check | `IRedisStore`, `RedisDistributedLock`, `RedisHealthCheck` |
| `RateLimiting/` | Distributed fixed-window (String) and sliding-window (Sorted Set) limiter | `RedisRateLimiter`, `.RequireRedisRateLimit()` |
| `Diagnostics/` | `eshop.cache.lookups{cache.name,cache.result}` and invalidation counters | `CacheMetrics` |
| `DependencyInjection/` | One-line registration | `AddEShopCaching()` |

**Extension points.** Add a new CDN by implementing `ICdnPurger`. Add a new cache layer that must react to writes (for example a search index cache) by implementing `ICacheInvalidationHandler` and registering it; the invalidator and Pub/Sub subscriber call every registered handler.

## 3. Read path: one product request through every layer

```mermaid
sequenceDiagram
    autonumber
    participant B as Browser
    participant E as CDN edge (nginx)
    participant G as Gateway (YARP + output cache)
    participant A as Catalog.Api
    participant L1 as L1 MemoryCache
    participant L2 as Redis (L2)
    participant DB as PostgreSQL

    B->>B: fresh copy? (max-age=60) -> serve locally, no network
    B->>E: GET /api/catalog/products/42  (If-None-Match: W/"42-v3")
    alt edge HIT (s-maxage / CDN-Cache-Control)
        E-->>B: 200 or 304 from edge (X-Edge-Cache: HIT)
    else edge MISS
        E->>G: GET (collapsed by proxy_cache_lock)
        alt output cache HIT (Redis store)
            G-->>E: 200 (X-Gateway-Cache: HIT)
        else MISS
            G->>A: GET (AllowLocking = request coalescing)
            A->>L1: GetOrCreate("catalog:product:42")
            alt L1 HIT
                L1-->>A: ProductDto
            else L1 MISS
                A->>L2: GET eshop:catalog:l2:catalog:product:42
                alt L2 HIT
                    L2-->>A: bytes -> deserialize, fill L1
                else L2 MISS (one factory call per node)
                    A->>DB: SELECT ... WHERE id = 42
                    DB-->>A: row
                    A->>L2: SET ... EX 600 (+jitter)
                end
            end
            A-->>G: 200 + Cache-Control, ETag, Cache-Tag: product:42
            G->>G: store (tags: product:42), TTL = s-maxage
            G-->>E: 200
        end
        E->>E: store for s-maxage
        E-->>B: 200 (or 304 if ETag matches)
    end
```

Each layer only does work when the layer in front of it missed. Under normal traffic most requests never leave the browser or the edge.

## 4. Write path: invalidating every layer

```mermaid
sequenceDiagram
    autonumber
    participant Admin
    participant A1 as Catalog.Api node 1
    participant DB as PostgreSQL
    participant R as Redis
    participant A2 as Catalog.Api node 2
    participant G as Gateway
    participant CDN as CDN API

    Admin->>A1: PUT /api/catalog/admin/products/42/price
    A1->>DB: UPDATE products SET price, version = version + 1 (optimistic concurrency)
    A1->>A1: HybridCache.RemoveByTag("product:42","products") -> local L1 + L2
    A1->>R: PUBLISH eshop:cache-invalidation {tags, keys, origin}
    R-->>A2: message
    A2->>A2: drop L1 entries for product:42
    R-->>G: message
    G->>R: output cache EvictByTag("product:42")
    A1->>CDN: purge_cache {tags:["product:42","products"]}
    Note over Admin,CDN: Browser copies cannot be purged -> keep public max-age short (60 s)<br/>and rely on ETag revalidation after that.
```

Order matters: **write the source of truth first, then invalidate.** Invalidating first opens a window where a concurrent reader re-populates the cache with the old row.

## 5. Deployment topology (docker-compose)

```mermaid
flowchart LR
    U(("Client")) -->|":8080"| CDN["cdn<br/>nginx proxy_cache"]
    CDN --> GW["gateway<br/>YARP + OutputCache"]
    GW --> C1["catalog-api #1"] & C2["catalog-api #2"]
    GW --> B1["basket-api #1"] & B2["basket-api #2"]
    C1 & C2 --> PG[("PostgreSQL")]
    C1 & C2 & B1 & B2 & GW --> R[("Redis 8<br/>AOF · volatile-lru")]
    RI["RedisInsight :5540"] -.-> R
```

Two replicas of each API are deliberate: they make the classic distributed-cache problems visible (stale L1 on the other node, sticky sessions, duplicate checkout) so you can watch the solution handle them.

## 6. Key naming

All service-owned keys are prefixed automatically by `IRedisStore.Database` (`WithKeyPrefix`):

```
eshop:{service}:{entity}:{id}[:{qualifier}]

eshop:catalog:l2:catalog:product:42      HybridCache L2 entry (String)
eshop:catalog:bestsellers:20261001       Sorted Set
eshop:catalog:stores:geo                 GEO set
eshop:basket:cart:alice                  Hash
eshop:basket:recent:alice                List
eshop:basket:wishlist:alice              Set
eshop:basket:session:{id}                Hash
eshop:basket:dau:20261001                Bitmap (String)
eshop:gateway:output:...                 Output cache entries + tag sets
eshop:streams:checkout                   Stream (shared, un-prefixed)
```

A prefix per service gives you `SCAN MATCH eshop:basket:*` for debugging, ACL rules per service (`~eshop:basket:*`) and safe co-location of several services on one Redis.
