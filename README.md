# Redis Caching Learning Project (.NET 10)

Welcome! This is an educational ASP.NET Core Web API project designed to teach the fundamentals of **Redis Caching** in .NET. It is built using a structured **Controller-based architecture** and is heavily commented to explain *why* and *what* each piece of code does.

---

## 🚀 Key Caching Concepts Demonstrated

This project is divided into distinct controllers, each demonstrating a core Redis caching pattern:

### 1. Basic Caching (`BasicCacheController`)
- **Direct Connection:** Demonstrates the use of the `StackExchange.Redis` `ConnectionMultiplexer`.
- **Primary Commands:** Shows how to execute low-level Redis operations: `SET`, `GET`, `DEL`, and `EXISTS`.
- **DI Registration:** Configured as a **Singleton** in dependency injection.

### 2. Expiration Strategies (`ExpirationController`)
- **Absolute Expiration:** The cache item expires at a specific, fixed time (e.g., after 30 seconds), regardless of how often it is accessed. Great for static, predictable data.
- **Sliding Expiration:** The cache item's expiration resets every time it is accessed. Great for active user sessions.
- **Redis Detail:** Since Redis has no native "sliding expiration" command, the project shows how to slide expiration programmatically in C# by resetting the TTL (Time-To-Live) on read.

### 3. Caching Complex Objects (`ObjectCacheController`)
- **Serialization:** Redis natively stores bytes or strings. This demonstrates how to serialize C# objects into JSON strings using `System.Text.Json` on write, and deserialize them back on read.

### 4. Cache-Aside Pattern (`CacheAsideController`)
- **Lazy Loading:** Data is loaded into the cache only when requested.
- **Workflow:** 
  1. Check Redis cache. 
  2. *Cache Hit:* Return immediately (takes < 10ms).
  3. *Cache Miss:* Pull from the slow database (simulated with `Task.Delay` of 1.5 seconds), store in Redis, and return.
- **Response Metrics:** Includes source indicators and execution times to clearly demonstrate the caching performance boost.

### 5. IDistributedCache Abstraction (`DistributedCacheController`)
- **Abstraction Integration:** Uses Microsoft's standard caching package (`Microsoft.Extensions.Caching.StackExchangeRedis`).
- **Pros & Cons:** Explains the trade-offs of using `IDistributedCache` (which decouples database providers but has a restricted key/value-only API) versus using the direct `StackExchange.Redis` library (which locks your code to Redis but opens up all 400+ Redis-specific commands).

### 6. Cache Refresh Strategies (`CacheRefreshController`)
- **Full Refresh:** Clears all cached products using the safe **SCAN** command (instead of the blocking `KEYS` command) and reloads the entire dataset from the database.
- **Delta Refresh:** Checks a tracked synchronization key (`cache:last-refresh-timestamp`) and queries the database *only* for records modified since that time, updating only the changed entries.
- **Comparison Audit:** Runs both refresh methods back-to-back, demonstrating the performance and query payload savings of Delta Refresh.

---

## 🛠️ Infrastructure Setup (Docker)

To run Redis and view cached data, we use Docker. A `docker-compose.yml` file is provided in the project root.

### Docker Services Configured:
1. **Redis Server:** Running on port `6379`.
2. **Redis Commander:** A web UI visualization tool running on port `8081`.

### Spin up the services:
Open a terminal in the root workspace and run:
```bash
docker-compose up -d
```
You can now access the Redis Commander UI by navigating to:
👉 **[http://localhost:8081](http://localhost:8081)**

---

## 💻 How to Run the Web API

1. Ensure your Docker containers are running.
2. In your terminal, navigate to the Web API project or run from the root:
   ```bash
   dotnet run --project RedisCacheLearning
   ```
3. The API will start and display the local hosting URLs. By default, it runs on:
   - HTTP: `http://localhost:5028`
   - HTTPS: `https://localhost:7146`
4. Open the interactive API documentation (Swagger UI) in your browser:
   👉 **[http://localhost:5028/swagger](http://localhost:5028/swagger)**

---

## 🧪 Testing the Endpoints

There are two primary ways to test this learning API:

### Method A: Swagger UI (Browser)
Open the Swagger UI URL above, click on any endpoint, select **Try it out**, fill in the parameters, and click **Execute**.

### Method B: REST Client (`RedisCacheLearning.http`)
If your IDE supports `.http` files (like VS Code REST Client or Visual Studio), open [RedisCacheLearning.http](file:///c:/Study/.NET-Projects/Redis-01/RedisCacheLearning/RedisCacheLearning.http) to execute requests directly.

---

## 🔄 Walkthrough: Testing Cache Refresh Strategies

Follow this recommended sequence to see Full vs. Delta refresh in action:

1. **Step 1: Perform Initial Sync (Full Refresh)**
   - Execute: `POST /api/CacheRefresh/full-refresh`
   - *Result:* Clears any keys and populates the cache with all 5 mock products. Takes ~1.5 seconds due to database latency.
2. **Step 2: Verify Caching Works**
   - Execute: `GET /api/CacheAside/product/2`
   - *Result:* This returns instantly (under 10ms) from the cache.
3. **Step 3: Simulate Database Modifications**
   - Execute: `POST /api/CacheRefresh/simulate-data-change`
   - *Result:* Marks products **2** and **4** as updated in the mock database (updates their descriptions and sets `LastModified` to now).
4. **Step 4: Execute Delta Refresh**
   - Execute: `POST /api/CacheRefresh/delta-refresh`
   - *Result:* Queries only products updated since the last sync. Notice that `CacheKeysUpdated` is **2** (only product 2 and 4 were rewritten). This avoids querying unchanged items and updating all keys.
5. **Step 5: Run the Audit Comparison**
   - Execute: `GET /api/CacheRefresh/compare`
   - *Result:* Performs both Delta Refresh and Full Refresh back-to-back and outputs comparison timings. Even on a tiny 5-item list, you will see a clear performance benefit with Delta Refresh.
