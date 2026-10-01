# 05 · Try it: watch every layer work

Start the stack (Docker Desktop or any Docker engine):

```bash
docker compose up --build -d
docker compose ps            # 2x catalog-api, 2x basket-api, gateway, cdn, redis, postgres, redisinsight
```

| Entry point | URL |
|---|---|
| CDN edge (nginx) | http://localhost:8080 |
| Gateway (YARP) | http://localhost:5000 |
| RedisInsight | http://localhost:5540 (add `redis:6379`) |

Keep a Redis monitor open in another terminal to see each command as it happens:

```bash
docker compose exec redis redis-cli MONITOR
```

## 1. CDN + gateway + L1/L2 on a product

```bash
curl -si http://localhost:8080/api/catalog/products/1 | grep -Ei 'HTTP/|cache-control|cdn-cache|etag|cache-tag|x-edge|x-gateway|age'
```
First call - every layer misses:
```
HTTP/1.1 200 OK
Cache-Control: public, max-age=60, s-maxage=300, stale-while-revalidate=60, stale-if-error=3600
CDN-Cache-Control: max-age=600
ETag: W/"1-v1"
Cache-Tag: product:1
X-Gateway-Cache: MISS
X-Edge-Cache: MISS
```
Run it again: `X-Edge-Cache: HIT` - nothing reached the gateway. Bypass the edge and hit the gateway directly:
```bash
curl -si http://localhost:5000/api/catalog/products/1 | grep -Ei 'x-gateway|age'
# X-Gateway-Cache: HIT, Age: 7
```
In `MONITOR` you saw exactly one `GET eshop:catalog:l2:catalog:product:1` (L2) the first time, then nothing.

## 2. Conditional request (304)

```bash
curl -si http://localhost:5000/api/catalog/products/1 -H 'If-None-Match: W/"1-v1"' | head -1
# HTTP/1.1 304 Not Modified
```

## 3. Write path: invalidate every layer

```bash
curl -si -X PUT http://localhost:8080/api/catalog/admin/products/1/price \
  -H 'X-User-Id: admin' -H 'X-User-Roles: admin' -H 'Content-Type: application/json' \
  -d '{"price": 149.99}' | head -1                       # 204

curl -si http://localhost:5000/api/catalog/products/1 | grep -Ei 'etag|x-gateway'
# ETag: W/"1-v2"        <- new version
# X-Gateway-Cache: MISS <- evicted by tag
```
In `MONITOR`: `PUBLISH eshop:cache-invalidation {...}`, then the gateway's tag eviction. `docker compose logs catalog-api` shows both replicas handled the message (the second replica dropped its L1 copy).

> The nginx edge in this demo has no purge API; it refreshes after `s-maxage`. With `Caching:Cdn:Provider=Cloudflare` the same write purges the real CDN by tag.

## 4. Menu: long-lived reference data

```bash
curl -si http://localhost:8080/api/catalog/menu | grep -Ei 'cache-control|cdn-cache|cache-tag'
# Cache-Control: public, max-age=300, s-maxage=3600, stale-while-revalidate=600, stale-if-error=86400
# CDN-Cache-Control: max-age=86400
# Cache-Tag: menu
```

## 5. Basket: Hash + private browser cache

```bash
U='-H X-User-Id:alice'
curl -s -X POST http://localhost:8080/api/basket/items $U -H 'Content-Type: application/json' \
  -d '{"productId":1,"productName":"Alpine Jacket Contoso","unitPrice":149.99,"quantity":2}'
curl -si http://localhost:8080/api/basket $U | grep -Ei 'cache-control|etag|vary|x-edge'
# Cache-Control: private, max-age=0, must-revalidate
# Vary: X-User-Id, Authorization
# ETag: W/"..."
# X-Edge-Cache: BYPASS

docker compose exec redis redis-cli HGETALL eshop:basket:cart:alice
# qty:1 -> 2, item:1 -> {"Name":"Alpine Jacket Contoso","Price":149.99,...}
```
Call `/api/basket` with the returned ETag in `If-None-Match` and you get `304`.

## 6. Recently viewed (List), wishlist (Set)

```bash
for p in 3 5 3 7; do curl -s -X POST http://localhost:8080/api/basket/recently-viewed/$p $U; done
curl -s http://localhost:8080/api/basket/recently-viewed $U        # {"productIds":[7,3,5]}  (deduped, newest first)

curl -s -X PUT http://localhost:8080/api/basket/wishlist/3 $U
curl -s -X PUT http://localhost:8080/api/basket/wishlist/3 -H 'X-User-Id: bob'
curl -s http://localhost:8080/api/basket/wishlist/common/bob $U     # {"productIds":[3]}  (SINTER)
```

## 7. Checkout: lock, rate limit, Stream -> best-sellers (Sorted Set)

```bash
curl -s -X POST http://localhost:8080/api/basket/checkout $U
# {"checkoutId":"...","total":299.98,"streamEntryId":"1727775000000-0"}

docker compose exec redis redis-cli XINFO GROUPS eshop:streams:checkout     # catalog-bestsellers, pending 0
curl -s http://localhost:8080/api/catalog/bestsellers?days=1                # product 1, unitsSold 2

for i in $(seq 1 7); do curl -s -o /dev/null -w '%{http_code} ' -X POST http://localhost:8080/api/basket/checkout $U; done
# 400 400 400 400 429 429 429   <- empty basket (400) until the Redis rate limit (5/min) kicks in
```

## 8. Unique viewers (HyperLogLog)

```bash
for v in a b c a b; do curl -s -X POST http://localhost:8080/api/catalog/products/1/views -H "X-Visitor-Id: $v"; done
curl -s http://localhost:5000/api/catalog/products/1/views/unique?days=7
# {"productId":1,"days":7,"uniqueVisitors":3,"accuracy":"approx ±0.81% (HyperLogLog)"}
docker compose exec redis redis-cli MEMORY USAGE eshop:catalog:views:1:$(date -u +%Y%m%d)
```

## 9. Stores near me (Geo) and type-ahead (lex Sorted Set)

```bash
curl -s 'http://localhost:8080/api/catalog/stores/nearby?lat=28.6139&lon=77.2090&radiusKm=30'
# Connaught Place ~2 km, Noida ~13 km, Cyber Hub ~19 km
curl -s 'http://localhost:8080/api/catalog/search/suggest?q=alp'
```

## 10. Shared sessions

```bash
# Signed-in sessions: Hash per session + Set index per user
S=$(curl -s -X POST http://localhost:8080/api/sessions $U | sed -E 's/.*"sessionId":"([^"]+)".*/\1/')
curl -s http://localhost:8080/api/sessions/current -H "X-Session-Id: $S"     # works on either basket replica
curl -s -X DELETE http://localhost:8080/api/sessions $U                      # sign out everywhere

# Anonymous preferences: ASP.NET Core Session over Redis IDistributedCache
curl -s -c jar -X PUT http://localhost:8080/api/session/preferences -H 'Content-Type: application/json' \
  -d '{"currency":"USD","locale":"en-US"}'
curl -s -b jar http://localhost:8080/api/session/preferences                 # {"currency":"USD",...} from any replica
```

## 11. Daily active users (Bitmap)

```bash
for u in alice bob carol; do curl -s -o /dev/null http://localhost:8080/api/basket -H "X-User-Id: $u"; done
curl -s 'http://localhost:8080/api/basket/analytics/active-users?days=1' -H 'X-User-Id: admin' -H 'X-User-Roles: admin'
# {"activeUsers":3,...}
```

## Run without Docker

```bash
docker run -d -p 6379:6379 redis:8-alpine
docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=catalog postgres:17-alpine
dotnet run --project src/Services/Catalog/Catalog.Api     # :5101
dotnet run --project src/Services/Basket/Basket.Api       # :5102
dotnet run --project src/Gateways/EShop.Gateway           # :5000
```
OpenAPI documents are served at `/openapi/v1.json` on each API in Development.
