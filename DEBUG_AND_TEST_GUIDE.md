# Debugging, Testing, and Learning Redis Caching Patterns

This guide walks you through starting the application, setting up debug breakpoints to understand the code execution flow, and visually inspecting keys in Redis for **all 6 caching concepts** exposed in Swagger.

---

## 🛠️ Step 1: Start Redis and Redis Commander

Since Redis is an external database, you must start it before running the .NET application.

1. Open your terminal in the project root (`c:\Study\.NET-Projects\Redis-01`).
2. Start the Docker containers:
   ```bash
   docker-compose up -d
   ```
3. Open your browser and navigate to the Redis Commander dashboard:
   👉 **[http://localhost:8081](http://localhost:8081)**
   *(This UI lets you see keys, values, and TTLs in real-time!)*
4. Alternatively, open **RedisInsight** and connect to:
   - **Host:** `localhost`
   - **Port:** `6380` *(Changed from 6379 to avoid conflicts with other local Redis instances)*

---

## 💻 Step 2: Run the Web API in Debug Mode

To step through the code and inspect variables, run the project with a debugger attached.

### Option A: Using Visual Studio 2022
1. Open the solution file `RedisCacheLearning.slnx`.
2. Set `RedisCacheLearning` as the startup project (it should be set automatically).
3. Press **F5** (or click the green **Start/Debug** button).
4. Visual Studio will launch the application and open your browser to the Swagger UI page:
   👉 **[http://localhost:5028/swagger](http://localhost:5028/swagger)**

### Option B: Using VS Code
1. Open the root workspace folder in VS Code.
2. If prompted to add missing assets to build/debug, select **Yes**.
3. Go to the **Run and Debug** view (`Ctrl+Shift+D`).
4. Select **.NET Core Launch (web)** and click the **Start Debugging** icon (or press **F5**).

---

## 🎯 Step 3: Walkthroughs for All Caching Concepts

To understand the mechanics of each concept, open the corresponding controller file in your IDE, set the recommended breakpoints, and execute the requests via Swagger or the `RedisCacheLearning.http` file.

---

### Concept 1: Basic Caching (`BasicCacheController`)
* **API Endpoints:**
  - `POST /api/BasicCache/set` (Store a raw string key-value)
  - `GET /api/BasicCache/get` (Retrieve a string value)
  - `GET /api/BasicCache/exists` (Check if key exists)
  - `DELETE /api/BasicCache/delete` (Remove a key)
* **What it teaches:** Direct, low-level usage of `StackExchange.Redis` (`StringSetAsync`, `StringGetAsync`, `KeyExistsAsync`, `KeyDeleteAsync`).
* **Debugging Walkthrough:**
  1. Open [BasicCacheController.cs](file:///c:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Controllers/BasicCacheController.cs).
  2. Place a breakpoint on `StringGetAsync` inside `GetString`.
  3. Execute `POST /api/BasicCache/set?key=learn_key&value=RedisIsFun`.
  4. Execute `GET /api/BasicCache/get?key=learn_key`.
  5. Step over (**F10**) and inspect the `value` variable. You will see it holds the string `"RedisIsFun"`.
  6. Look at **RedisInsight**. You will see the key `learn_key` with value `"RedisIsFun"` and no expiration (TTL = -1).

---

### Concept 2: Absolute & Sliding Expiration (`ExpirationController`)
* **API Endpoints:**
  - `POST /api/Expiration/absolute` (Set key with absolute expiry)
  - `POST /api/Expiration/sliding` (Set key with sliding expiry)
  - `GET /api/Expiration/get` (Get key and check/update sliding TTL)
* **What it teaches:** Difference between absolute expiration (expired at a fixed time) and sliding expiration (extended on every read). Teaches how sliding expiration is implemented in raw Redis by resetting TTL programmatically.
* **Debugging Walkthrough:**
  1. Open [ExpirationController.cs](file:///c:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Controllers/ExpirationController.cs).
  2. Place a breakpoint inside `GetWithExpirationCheck` (around line 105).
  3. Call `POST /api/Expiration/sliding?key=session_key&value=ActiveSession&seconds=30`.
  4. Call `GET /api/Expiration/get?key=session_key&slidingSeconds=30`.
  5. In the debugger, check the `isSliding` boolean. Step through the `if (isSliding)` block. Notice the call to `KeyExpireAsync(key, newTtl)`. This extends the lifetime of the key by another 30 seconds.

---

### Concept 3: Complex Objects Caching (`ObjectCacheController`)
* **API Endpoints:**
  - `POST /api/ObjectCache/set` (Serialize and save C# object)
  - `GET /api/ObjectCache/get` (Read and deserialize back to C# object)
* **What it teaches:** Redis only stores text/bytes. To cache custom C# classes, you must serialize them (e.g., to JSON using `System.Text.Json`) and deserialize them when loading.
* **Debugging Walkthrough:**
  1. Open [ObjectCacheController.cs](file:///c:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Controllers/ObjectCacheController.cs).
  2. Place breakpoints in both `SetObject` and `GetObject`.
  3. Send a `POST /api/ObjectCache/set` with a `Product` JSON body (e.g. key `product:99`).
  4. Observe `JsonSerializer.Serialize(product)` converting the class to a JSON string.
  5. Call `GET /api/ObjectCache/get?key=product:99`.
  6. Observe `JsonSerializer.Deserialize<Product>(jsonResult)` converting the raw string back into a C# `Product` instance.

---

### Concept 4: Cache-Aside (Lazy Loading) Pattern (`CacheAsideController`)
* **API Endpoints:**
  - `GET /api/CacheAside/product/{id}` (Get product via Cache-Aside)
* **What it teaches:** The most common caching pattern. Check cache first. If hit, return instantly. If miss, load from slow DB, save to cache, and return.
* **Debugging Walkthrough:**
  1. Open [CacheAsideController.cs](file:///c:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Controllers/CacheAsideController.cs).
  2. Place a breakpoint on `StringGetAsync` (around line 53).
  3. Call `GET /api/CacheAside/product/1`.
  4. **First Call (Cache Miss):** Step through the code using **F10**. You'll see that `cachedProduct.HasValue` is `false`. The execution goes down to `MockDatabase.GetByIdAsync(id)`, pauses for 1.5 seconds (simulated database delay), stores the serialized JSON string in Redis via `StringSetAsync`, and returns the product.
  5. **Second Call (Cache Hit):** Execute the same request again. Step over (**F10**). You'll see that `cachedProduct.HasValue` is now `true`! The execution immediately goes inside the `if` block, deserializes the JSON, and returns instantly without hitting the slow database.

---

### Concept 5: IDistributedCache Abstraction (`DistributedCacheController`)
* **API Endpoints:**
  - `POST /api/DistributedCache/product/set` (Save product using IDistributedCache)
  - `GET /api/DistributedCache/product/get` (Retrieve product)
  - `DELETE /api/DistributedCache/product/delete` (Remove product)
* **What it teaches:** Standard .NET caching abstraction. It hides Redis-specific commands so you can swap caching engines easily. Notice how keys are automatically prefixed (e.g. `RedisCacheLearning_[yourKey]`).
* **Debugging Walkthrough:**
  1. Open [DistributedCacheController.cs](file:///c:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Controllers/DistributedCacheController.cs).
  2. Place a breakpoint in `SetProduct`.
  3. POST a product to `/api/DistributedCache/product/set?key=dist_item`.
  4. Look at **RedisInsight**. Notice that the key created in Redis is actually named `RedisCacheLearning_dist_item`. This prefixing prevents key collisions when sharing a Redis database.
  5. Notice that we set both Absolute and Sliding expiration options in `DistributedCacheEntryOptions` object, which is handled automatically by the .NET caching framework.

---

### Concept 6: Cache Refresh Strategies (`CacheRefreshController`)
* **API Endpoints:**
  - `POST /api/CacheRefresh/simulate-data-change` (Modify DB data)
  - `POST /api/CacheRefresh/full-refresh` (Flush matching keys via SCAN and reload all)
  - `POST /api/CacheRefresh/delta-refresh` (Pull & update modified records only)
  - `GET /api/CacheRefresh/compare` (Run both back-to-back and audit performance)
* **What it teaches:** How to refresh cache stores. Full Refresh is simple but resource-intensive. Delta Refresh is highly optimized, updating only changed keys by tracking sync timestamps.
* **Debugging Walkthrough:**
  1. Open [CacheRefreshController.cs](file:///c:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Controllers/CacheRefreshController.cs).
  2. Place a breakpoint inside `DeltaRefresh()` (around line 160) at `GetModifiedSinceAsync`.
  3. Run the following sequence:
     - **Step A:** Call `POST /api/CacheRefresh/full-refresh` to ensure cache is fully loaded.
     - **Step B:** Call `POST /api/CacheRefresh/simulate-data-change`. This updates products 2 and 4 in our database and sets their `LastModified` to now.
     - **Step C:** Call `POST /api/CacheRefresh/delta-refresh`.
  4. Step through the `DeltaRefresh` method. You will notice that `modifiedProducts` returns **only 2 products** (IDs 2 and 4). Only those 2 specific keys are rewritten to Redis. The other 3 keys in Redis remain completely untouched. This saves database bandwidth and Redis write operations!

---

## 👁️ Step 4: Visually Inspect Keys in Redis Insight

Keep **RedisInsight** open while testing:

1. **View Keys:** Click **Browse** to see keys like `product:1`, `product:2`, etc.
2. **Inspect Contents:** Click on any key to inspect its stringified JSON payload, TTL remaining, and memory footprint.
3. **Trace TTL:** Watch the sliding expiration keys extend their lifespan in real-time when you execute a `GET` request.

---

## 🚀 Step 5: Test Endpoints using the HTTP File

You don't need to use Swagger! If you are using VS Code, install the extension **REST Client**. 

1. Open **[RedisCacheLearning.http](file:///c:/Study/.NET-Projects/Redis-01/RedisCacheLearning/RedisCacheLearning.http)**.
2. Click `Send Request` above any endpoint block to execute the request.
3. The response headers, body, execution time, and caching headers will display in a split window on the right.
4. Run the requests in the recommended testing sequence at the bottom of the file to see the cache-aside and refresh behaviors execute cleanly.
