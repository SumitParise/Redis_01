using Microsoft.AspNetCore.Mvc;
using RedisCacheLearning.Data;
using RedisCacheLearning.Services;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace RedisCacheLearning.Controllers
{
    /// <summary>
    /// Demonstrates Cache Refresh Strategies: Full Refresh vs. Delta Refresh.
    /// Explains how to update caches efficiently, avoid performance bottlenecks (like using SCAN instead of KEYS),
    /// and track modifications using timestamps.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CacheRefreshController : ControllerBase
    {
        private readonly IDatabase _database;
        private readonly RedisKeysHelper _keysHelper;
        private const string LastRefreshKey = "cache:last-refresh-timestamp";
        private const string ProductKeyPattern = "product:*";

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheRefreshController"/>.
        /// </summary>
        /// <param name="redis">The Singleton ConnectionMultiplexer instance.</param>
        /// <param name="keysHelper">The Redis key scanning helper service.</param>
        public CacheRefreshController(IConnectionMultiplexer redis, RedisKeysHelper keysHelper)
        {
            _database = redis.GetDatabase();
            _keysHelper = keysHelper;
        }

        /// <summary>
        /// Simulates updates inside the primary "database" by modifying random product descriptions 
        /// and updating their LastModified timestamp.
        /// </summary>
        /// <returns>Details of the modified products.</returns>
        /// <remarks>
        /// RUN THIS FIRST when testing Delta Refresh. It gives Delta Refresh modified records to detect!
        /// </remarks>
        [HttpPost("simulate-data-change")]
        public async Task<IActionResult> SimulateDataChange()
        {
            // Pick two random IDs to update
            var random = new Random();
            var idsToUpdate = new List<int> { 2, 4 };
            var updatedProducts = new List<object>();

            foreach (var id in idsToUpdate)
            {
                string newDesc = $"[Updated at {DateTime.UtcNow:HH:mm:ss} UTC] - This item has been edited in the database.";
                bool success = MockDatabase.SimulateUpdate(id, newDesc);
                if (success)
                {
                    var updated = await MockDatabase.GetByIdAsync(id);
                    if (updated != null)
                    {
                        updatedProducts.Add(new
                        {
                            Id = updated.Id,
                            Name = updated.Name,
                            NewDescription = updated.Description,
                            LastModified = updated.LastModified
                        });
                    }
                }
            }

            return Ok(new
            {
                Message = "Simulated database modifications successfully. Run Delta Refresh next to pick up these changes.",
                ModifiedRecords = updatedProducts
            });
        }

        /// <summary>
        /// Clears ALL cached product keys matching "product:*" and reloads the entire dataset from the mock database.
        /// </summary>
        /// <returns>Statistics about the full refresh execution cost.</returns>
        /// <remarks>
        /// WHEN TO USE FULL REFRESH:
        /// - When the entire dataset changes at once (e.g., nightly batch jobs, data warehouse synchronizations, catalog imports).
        /// - When simplicity is preferred over optimization (it's very easy to write: flush all -> write all).
        /// - When the dataset is small enough that the database load of fetching everything is negligible.
        /// 
        /// PRODUCTION WARNING:
        /// We use SCAN to retrieve the keys. Never use the KEYS command in production as it locks the Redis server.
        /// </remarks>
        [HttpPost("full-refresh")]
        public async Task<IActionResult> FullRefresh()
        {
            var stopwatch = Stopwatch.StartNew();

            // STEP 1: Find all keys matching "product:*" using our SCAN wrapper helper.
            // This is safe for production!
            var keys = _keysHelper.GetKeysByPattern(ProductKeyPattern).ToArray();

            // STEP 2: Delete all found keys. 
            // In a real system, you might do this in batches or using a pipeline.
            int deletedCount = 0;
            foreach (var key in keys)
            {
                // DEL key
                bool deleted = await _database.KeyDeleteAsync(key);
                if (deleted) deletedCount++;
            }

            // STEP 3: Re-fetch the full dataset from our slow mock database.
            // This includes a simulated 1.5-second latency.
            List<Product> products = await MockDatabase.GetAllAsync();

            // STEP 4: Repopulate the Redis cache.
            foreach (var product in products)
            {
                string cacheKey = $"product:{product.Id}";
                string json = JsonSerializer.Serialize(product);
                
                // Store in Redis (we set 10 minutes cache lifespan)
                await _database.StringSetAsync(cacheKey, json, TimeSpan.FromMinutes(10));
            }

            // Update the last refresh timestamp to mark that we are fully synced
            await _database.StringSetAsync(LastRefreshKey, DateTime.UtcNow.ToString("o"));

            stopwatch.Stop();

            return Ok(new
            {
                Strategy = "Full Refresh",
                KeysCleared = deletedCount,
                KeysReloaded = products.Count,
                TimeElapsedMs = stopwatch.ElapsedMilliseconds,
                DatabaseCallLatencyMs = 1500, // known simulation delay
                Message = "Full Refresh completed: Cache cleared and repopulated with entire dataset."
            });
        }

        /// <summary>
        /// Updates ONLY cache keys for products that have changed since the last refresh timestamp.
        /// Unmodified items remain untouched in the cache.
        /// </summary>
        /// <returns>Statistics showing the efficiency of delta refresh.</returns>
        /// <remarks>
        /// WHEN TO USE DELTA REFRESH:
        /// - For large datasets (e.g. hundreds of thousands of products, users, or transactions).
        /// - When you want to minimize load on the primary database (avoiding full table scans/queries).
        /// - When you want to minimize network payload and write overhead in Redis.
        /// </remarks>
        [HttpPost("delta-refresh")]
        public async Task<IActionResult> DeltaRefresh()
        {
            var stopwatch = Stopwatch.StartNew();

            // STEP 1: Determine the last refresh time by reading the timestamp key from Redis
            RedisValue lastRefreshRaw = await _database.StringGetAsync(LastRefreshKey);
            DateTime lastRefreshTime;

            if (lastRefreshRaw.HasValue && DateTime.TryParse(lastRefreshRaw.ToString(), out var parsedTime))
            {
                lastRefreshTime = parsedTime;
            }
            else
            {
                // If no timestamp exists in Redis (e.g., first run), fall back to checking changes in the last 10 hours.
                lastRefreshTime = DateTime.UtcNow.AddHours(-10);
            }

            // STEP 2: Query the database for records modified since the last refresh.
            // This is a fast, targeted query (simulated with 1-second delay).
            List<Product> modifiedProducts = await MockDatabase.GetModifiedSinceAsync(lastRefreshTime);

            // STEP 3: Update only those specific keys in Redis
            int updatedCount = 0;
            foreach (var product in modifiedProducts)
            {
                string cacheKey = $"product:{product.Id}";
                string json = JsonSerializer.Serialize(product);
                
                // Write/Overwrite only the updated product (set 10 minutes cache lifespan)
                await _database.StringSetAsync(cacheKey, json, TimeSpan.FromMinutes(10));
                updatedCount++;
            }

            // STEP 4: Store the current time as the new last-refresh-timestamp
            DateTime currentRefreshTime = DateTime.UtcNow;
            await _database.StringSetAsync(LastRefreshKey, currentRefreshTime.ToString("o"));

            stopwatch.Stop();

            return Ok(new
            {
                Strategy = "Delta Refresh",
                LastSyncTimestamp = lastRefreshTime.ToString("o"),
                NewSyncTimestamp = currentRefreshTime.ToString("o"),
                DatabaseRecordsQueried = modifiedProducts.Count,
                CacheKeysUpdated = updatedCount,
                TimeElapsedMs = stopwatch.ElapsedMilliseconds,
                DatabaseCallLatencyMs = 1000, // known simulation delay
                Message = updatedCount > 0 
                    ? $"Delta Refresh completed: Updated {updatedCount} modified keys. Untouched keys remained untouched."
                    : "Delta Refresh completed: No modifications found in database. Redis remained untouched."
            });
        }

        /// <summary>
        /// Educational comparison endpoint. Runs Delta Refresh and Full Refresh 
        /// sequentially to display performance metrics side-by-side.
        /// </summary>
        /// <returns>Comparative report.</returns>
        [HttpGet("compare")]
        public async Task<IActionResult> CompareStrategies()
        {
            // 1. Force a database modification first, so both runs have modifications to process
            string newDesc = $"[Compare Demo Update - {DateTime.UtcNow:HH:mm:ss} UTC]";
            MockDatabase.SimulateUpdate(1, newDesc); // Change product 1

            // 2. Set an old sync timestamp in Redis to force Delta Refresh to detect the modification
            DateTime customSyncTime = DateTime.UtcNow.AddSeconds(-5);
            await _database.StringSetAsync(LastRefreshKey, customSyncTime.ToString("o"));

            // 3. Measure Delta Refresh
            var deltaStopwatch = Stopwatch.StartNew();
            List<Product> deltaProducts = await MockDatabase.GetModifiedSinceAsync(customSyncTime);
            foreach (var p in deltaProducts)
            {
                await _database.StringSetAsync($"product:{p.Id}", JsonSerializer.Serialize(p), TimeSpan.FromMinutes(10));
            }
            await _database.StringSetAsync(LastRefreshKey, DateTime.UtcNow.ToString("o"));
            deltaStopwatch.Stop();

            // 4. Measure Full Refresh
            var fullStopwatch = Stopwatch.StartNew();
            var keys = _keysHelper.GetKeysByPattern(ProductKeyPattern).ToArray();
            foreach (var key in keys)
            {
                await _database.KeyDeleteAsync(key);
            }
            List<Product> allProducts = await MockDatabase.GetAllAsync();
            foreach (var p in allProducts)
            {
                await _database.StringSetAsync($"product:{p.Id}", JsonSerializer.Serialize(p), TimeSpan.FromMinutes(10));
            }
            await _database.StringSetAsync(LastRefreshKey, DateTime.UtcNow.ToString("o"));
            fullStopwatch.Stop();

            return Ok(new
            {
                Analysis = "Side-by-side performance audit of Cache Refresh strategies.",
                DatasetSize = 5,
                DeltaRefresh = new
                {
                    QueryTimeMs = deltaStopwatch.ElapsedMilliseconds,
                    KeysTouched = deltaProducts.Count,
                    Efficiency = "High: Only retrieved & updated modified records. database did not have to return the entire table."
                },
                FullRefresh = new
                {
                    QueryTimeMs = fullStopwatch.ElapsedMilliseconds,
                    KeysTouched = allProducts.Count + keys.Length, // Delete + Writes
                    Efficiency = "Low: Cleared every key and pulled all records. High network and CPU cost on database."
                },
                Comparison = $"Delta Refresh was {fullStopwatch.ElapsedMilliseconds - deltaStopwatch.ElapsedMilliseconds}ms faster than Full Refresh even on this tiny 5-item dataset. On enterprise databases with thousands of items, the gap grows exponentially."
            });
        }
    }
}
