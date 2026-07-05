# 🐛 Redis Caching Learning Project - Debug & Test Guide

## 🚀 1. STARTUP SECTION

### Starting External Dependencies
This project uses Docker to run Redis and a Redis GUI management tool. Based on [docker-compose.yml](file:///C:/Study/.NET-Projects/Redis-01/docker-compose.yml), you need to start these services before running the application:

```bash
docker-compose up -d
```
- **Redis Server**: Runs on host port `6380` (mapped from 6379 internally) to avoid conflicts with other local Redis instances.
- **Redis Commander (GUI)**: Available at `http://localhost:8081`. You can use this to visually inspect the keys stored in Redis.

### Running the .NET Application
Based on [launchSettings.json](file:///C:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Properties/launchSettings.json) and [Program.cs](file:///C:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Program.cs):
- **Visual Studio**: Press `F5` to run with debugging.
- **VS Code**: Press `F5` or select `Run and Debug` from the side panel.
- **dotnet CLI**: Open a terminal in the `RedisCacheLearning` folder and run:
  ```bash
  dotnet run
  ```

### Swagger UI
Once running, the Swagger UI is available at:
👉 **`http://localhost:5028/swagger`**


---

## 🧠 2. CONCEPT-BY-CONCEPT LEARNING WALKTHROUGH

### 1️⃣ Basic Redis Operations
🔗 **[BasicCacheController.cs](file:///C:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Controllers/BasicCacheController.cs)**

- `POST /api/BasicCache/set`
- `GET /api/BasicCache/get`
- `GET /api/BasicCache/exists`
- `DELETE /api/BasicCache/delete`

**What this teaches**: Demonstrates the foundational Redis operations (GET, SET, DELETE, EXISTS) using the direct `StackExchange.Redis` client (`IConnectionMultiplexer`).

**Step-by-step debugging walkthrough**:
1. Open `BasicCacheController.cs` and set a breakpoint on **Line 41** inside `SetString()`.
2. Go to Swagger and call `POST /api/BasicCache/set` with a test key and value.
3. When the breakpoint hits, step over (F10). Inspect the `success` boolean to ensure it's `true`.
4. Open **Redis Commander** (`http://localhost:8081`) and observe your key appearing in the database.
5. Trigger a second call to `GET /api/BasicCache/get` for the same key to see the retrieval in action.


### 2️⃣ Expiration (Absolute vs. Sliding)
🔗 **[ExpirationController.cs](file:///C:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Controllers/ExpirationController.cs)**

- `POST /api/Expiration/absolute`
- `POST /api/Expiration/sliding`
- `GET /api/Expiration/get`

**What this teaches**: Explains how to set a cache key to expire at a fixed time (Absolute) vs resetting the timer every time the key is accessed (Sliding).

**Step-by-step debugging walkthrough**:
1. Open `ExpirationController.cs` and set a breakpoint on **Line 125** inside `GetWithExpirationCheck()`.
2. Call `POST /api/Expiration/sliding` via Swagger to create a sliding key.
3. Call `GET /api/Expiration/get` using the key you just created.
4. When the breakpoint hits, inspect `currentTtl`. Press F10 to observe `isSliding` evaluating to `true`.
5. Observe how `KeyExpireAsync` resets the TTL.
6. Check Redis Commander to visually confirm the TTL (Time-To-Live) resetting back to the full window.


### 3️⃣ Complex Object Serialization
🔗 **[ObjectCacheController.cs](file:///C:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Controllers/ObjectCacheController.cs)**

- `POST /api/ObjectCache/set`
- `GET /api/ObjectCache/get`

**What this teaches**: Redis stores raw strings or binary data. This concept demonstrates how to serialize complex C# objects (like a `Product` model) into JSON before storing, and how to deserialize them back upon retrieval.

**Step-by-step debugging walkthrough**:
1. Set a breakpoint on **Line 59** inside `SetObject()`.
2. Call `POST /api/ObjectCache/set` via Swagger and pass in a JSON product body.
3. Observe the `product` object converting into the `jsonPayload` string.
4. Set a breakpoint on **Line 98** inside `GetObject()` and fetch the key. Inspect how `JsonSerializer.Deserialize` recreates the `Product` instance from the raw string.


### 4️⃣ Cache-Aside Pattern
🔗 **[CacheAsideController.cs](file:///C:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Controllers/CacheAsideController.cs)**

- `GET /api/CacheAside/product/{id}`

**What this teaches**: The most common caching pattern. It attempts to load from the cache first; if missing (cache miss), it queries the slow primary database, caches the result, and returns it.

**Step-by-step debugging walkthrough**:
1. Set a breakpoint on **Line 57** inside `GetProduct()`.
2. Call `GET /api/CacheAside/product/1` (this represents a Cache Miss).
3. Follow the execution path: observe `cachedProduct.HasValue` is `false`. It hits the slow mock database (simulated 1.5s delay) and populates Redis on line 95.
4. Trigger a second call to the same endpoint. Watch the execution path jump into the `if (cachedProduct.HasValue)` block, skipping the database entirely for a lightning-fast response (Cache Hit).


### 5️⃣ Distributed Cache Abstraction
🔗 **[DistributedCacheController.cs](file:///C:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Controllers/DistributedCacheController.cs)**

- `POST /api/DistributedCache/product/set`
- `GET /api/DistributedCache/product/get`
- `DELETE /api/DistributedCache/product/delete`

**What this teaches**: Using Microsoft's built-in `IDistributedCache` interface instead of direct `StackExchange.Redis`. This abstraction allows you to swap out caching providers (Redis, SQL Server, NCache) easily but restricts you to simpler byte/string operations.

**Step-by-step debugging walkthrough**:
1. Set a breakpoint on **Line 63** inside `SetProduct()`.
2. Call `POST /api/DistributedCache/product/set` via Swagger.
3. Observe how the options (Absolute & Sliding expiration) are bundled into `DistributedCacheEntryOptions`.
4. Check Redis Commander: notice that keys are automatically prefixed (e.g., `RedisCacheLearning_product_dist`) based on the `InstanceName` configuration in [Program.cs](file:///C:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Program.cs).


### 6️⃣ Cache Refresh Strategies (Full vs Delta)
🔗 **[CacheRefreshController.cs](file:///C:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Controllers/CacheRefreshController.cs)**

- `POST /api/CacheRefresh/simulate-data-change`
- `POST /api/CacheRefresh/full-refresh`
- `POST /api/CacheRefresh/delta-refresh`
- `GET /api/CacheRefresh/compare`

**What this teaches**: How to efficiently update cache data. Compares a "Full Refresh" (clearing all keys and reloading everything) versus a "Delta Refresh" (using a timestamp to only update records modified since the last sync).

**Step-by-step debugging walkthrough**:
1. Set a breakpoint on **Line 102** (`FullRefresh`) and **Line 161** (`DeltaRefresh`).
2. Run `POST /api/CacheRefresh/simulate-data-change` to modify some mock data.
3. Run `POST /api/CacheRefresh/delta-refresh`. Note how it pulls the `last-refresh-timestamp` and queries only the modified records.
4. Call `GET /api/CacheRefresh/compare` to see the performance differences side-by-side.


---

## 👁️ 3. VISUAL INSPECTION SECTION

To truly understand what is happening under the hood, keep **Redis Commander** open at `http://localhost:8081` while running these endpoints.

| Feature Tested | What to Look For in Redis Commander |
| :--- | :--- |
| **Basic Sets** | Refresh the tree on the left. You should see raw keys appearing with string values. |
| **Expirations** | Select a key and look at the `TTL` property in the main window. Refresh the UI to watch it count down in real-time. |
| **Sliding Expirations** | Watch a sliding key's TTL. It will decrease, but the moment you call the API's `GET` endpoint, refresh the UI and watch the TTL jump back up to the maximum window limit. |
| **Object Serialization** | The value of the key will be raw JSON format (e.g., `{"Id":99,"Name":"Noise Cancelling Earbuds"...}`). |
| **Distributed Cache** | Look for keys with the `RedisCacheLearning_` prefix. Notice that `IDistributedCache` handles serialization (including metadata like absolute/sliding values) differently inside Redis compared to raw strings. |


---

## 🧪 4. HTTP FILE TESTING SECTION

You can test the entire API flow without a browser using the included `.http` file:
🔗 **[RedisCacheLearning.http](file:///C:/Study/.NET-Projects/Redis-01/RedisCacheLearning/RedisCacheLearning.http)**

### Recommended Testing Order
To get the most out of the project, run the `.http` requests in this logical sequence:

1. **Basic Operations** (Section 1): Test `SetBasicString`, `GetBasicString`, `CheckExists`, and `DeleteKey` to verify connectivity.
2. **Expirations** (Section 2): Test `SetAbsoluteExpiry`, wait 10 seconds, then try to get it (it should fail). Then test `SetSlidingExpiry` and hit it repeatedly within 15 seconds to keep it alive.
3. **Complex Objects** (Section 3): Store a JSON object and retrieve it.
4. **Cache-Aside Pattern** (Section 4): Run `CacheAsideMiss` (observe the slow response time). Then run `CacheAsideHit` immediately after (observe the sub-10ms response time).
5. **Distributed Cache** (Section 5): Test Microsoft's abstraction wrapper operations.
6. **Refresh Strategies** (Section 6): Follow the specific 5-step sequence (`Step A` through `Step E`) documented in the `.http` file to see the performance difference between Full and Delta cache refreshes.
