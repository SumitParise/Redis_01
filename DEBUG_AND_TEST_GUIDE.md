# Debugging, Testing, and Learning Redis Caching Patterns

This guide walks you through starting the application, setting up debug breakpoints to understand the code execution flow, and visually inspecting keys in Redis.

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

---

## 💻 Step 2: Run the Web API in Debug Mode

To step through the code and inspect variables, run the project with a debugger attached.

### Option A: Using Visual Studio 2022
1. Open the solution file `RedisCacheLearning.slnx`.
2. Set `RedisCacheLearning` as the startup project (it should be set automatically).
3. Press **F5** (or click the green **Start/Debug** button).
4. Visual Studio will launch the application and open your browser to the Swagger UI page:
   👉 **http://localhost:5028/swagger**

### Option B: Using VS Code
1. Open the root workspace folder in VS Code.
2. If prompted to add missing assets to build/debug, select **Yes**.
3. Go to the **Run and Debug** view (`Ctrl+Shift+D`).
4. Select **.NET Core Launch (web)** and click the **Start Debugging** icon (or press **F5**).

### Option C: Using the command line (No Debugger)
If you just want to run the project without stepping through code:
```bash
dotnet run --project RedisCacheLearning
```
Then navigate to **http://localhost:5028/swagger**.

---

## 🎯 Step 3: Set Breakpoints to Learn the Patterns

To truly understand how Redis caching works, you should place breakpoints in the code and inspect the variables. Here are the two best spots to place breakpoints:

### Learning Session A: The Cache-Aside Pattern
1. Open **[CacheAsideController.cs](file:///c:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Controllers/CacheAsideController.cs)**.
2. Find the method `GetProduct(int id)` (around line 50) and place a breakpoint on:
   ```csharp
   RedisValue cachedProduct = await _database.StringGetAsync(cacheKey);
   ```
3. Go to Swagger (or the HTTP file) and execute a request for Product 1:
   `GET /api/CacheAside/product/1`
4. The debugger will hit your breakpoint:
   - **First Call (Cache Miss):** Step through the code using **F10**. You'll see that `cachedProduct.HasValue` is `false`. The execution goes down to `MockDatabase.GetByIdAsync(id)`, pauses for 1.5 seconds (simulated database delay), stores the serialized JSON string in Redis via `StringSetAsync`, and returns the product.
   - **Second Call (Cache Hit):** Execute the same request again. The debugger hits the breakpoint. Step over it (**F10**). You'll see that `cachedProduct.HasValue` is now `true`! The execution immediately goes inside the `if` block, deserializes the JSON, and returns instantly without hitting the slow database.

### Learning Session B: Expiration and Sliding TTLs
1. Open **[ExpirationController.cs](file:///c:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Controllers/ExpirationController.cs)**.
2. Place a breakpoint inside the sliding expiration setup endpoint `SetSlidingExpiration` (around line 77) and the retrieval endpoint `GetWithExpirationCheck` (around line 105).
3. Execute `POST /api/Expiration/sliding` with key `my-sliding-key` and expiry `20` seconds.
4. Execute `GET /api/Expiration/get?key=my-sliding-key&slidingSeconds=20`.
5. In the debugger, inspect `currentTtl`. You will see the sliding expiration TTL reset back to 20 seconds on every request, keeping the cache active. If you wait 20 seconds without executing the get request, it will expire and disappear.

### Learning Session C: Delta Cache Refresh (Advanced)
1. Open **[CacheRefreshController.cs](file:///c:/Study/.NET-Projects/Redis-01/RedisCacheLearning/Controllers/CacheRefreshController.cs)**.
2. Place a breakpoint inside `DeltaRefresh()` (around line 160) at:
   ```csharp
   List<Product> modifiedProducts = await MockDatabase.GetModifiedSinceAsync(lastRefreshTime);
   ```
3. Run the following sequence in Swagger or the `.http` file:
   - **Step 1:** Call `POST /api/CacheRefresh/full-refresh`. This wipes all product keys and does a full mock database pull (1.5 seconds delay).
   - **Step 2:** Call `POST /api/CacheRefresh/simulate-data-change`. This updates products 2 and 4 in our database with a fresh `LastModified` timestamp.
   - **Step 3:** Call `POST /api/CacheRefresh/delta-refresh`.
4. Step through the `DeltaRefresh` method. You will notice:
   - `lastRefreshTime` holds the timestamp of the last full sync.
   - `modifiedProducts` returns **only 2 products** (IDs 2 and 4).
   - Only those 2 specific keys are rewritten to Redis. The other 3 keys in Redis remain completely untouched. This saves database bandwidth and Redis write operations!

---

## 👁️ Step 4: Visually Inspect Keys in Redis Commander

While executing the actions above, keep the **Redis Commander** tab open at **http://localhost:8081**:

1. **View Keys:** Expand the `local` node on the left panel. You will see keys like `product:1`, `product:2`, etc.
2. **Inspect Contents:** Click on any key. You will see its type (`string`), its stringified JSON payload, and its remaining Time-to-Live (TTL).
3. **View IDistributedCache Keys:** Keys written via the `DistributedCacheController` will appear with the prefix `RedisCacheLearning_` (e.g., `RedisCacheLearning_product_dist`). This prefix is handled automatically by the `IDistributedCache` configuration in `Program.cs`.
4. **Delete Keys manually:** You can click the **Delete** button in the top right of Redis Commander to manually delete a key and force a cache miss in your API, simulating database updates.

---

## 🚀 Step 5: Test Endpoints using the HTTP File

You don't need to use Swagger! If you are using VS Code, install the extension **REST Client**. 

1. Open **[RedisCacheLearning.http](file:///c:/Study/.NET-Projects/Redis-01/RedisCacheLearning/RedisCacheLearning.http)**.
2. You will see a `Send Request` button above each endpoint block.
3. Click `Send Request` to execute the endpoint. The response headers and body will display in a split window on the right.
4. Run the requests in the recommended testing sequence at the bottom of the file to see the cache-aside and refresh behaviors execute cleanly.
