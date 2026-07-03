using Microsoft.AspNetCore.Mvc;
using RedisCacheLearning.Data;
using StackExchange.Redis;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace RedisCacheLearning.Controllers
{
    /// <summary>
    /// Demonstrates the Cache-Aside (Lazy Loading) pattern.
    /// This is the most common caching pattern, loading data into the cache only when requested.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CacheAsideController : ControllerBase
    {
        private readonly IDatabase _database;

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheAsideController"/>.
        /// </summary>
        /// <param name="redis">The Singleton ConnectionMultiplexer instance.</param>
        public CacheAsideController(IConnectionMultiplexer redis)
        {
            _database = redis.GetDatabase();
        }

        /// <summary>
        /// Retrieves a product by ID, demonstrating the Cache-Aside pattern.
        /// First request will be a cache miss (~1.5 seconds latency).
        /// Subsequent requests will be cache hits (under 10 milliseconds).
        /// </summary>
        /// <param name="id">The product ID to search for.</param>
        /// <returns>A response containing the product data, execution duration, and the source of the data.</returns>
        /// <remarks>
        /// HOW CACHE-ASIDE WORKS:
        /// 1. The application receives a request for data (e.g., Product 1).
        /// 2. The application checks the cache first (e.g., checks key "product:1").
        /// 3. Cache HIT: The data is found! The application deserializes it and returns it immediately.
        /// 4. Cache MISS: The data is NOT found. The application:
        ///    a. Queries the primary database (which is slow).
        ///    b. Stores the retrieved data in the cache (with an expiration) for future requests.
        ///    c. Returns the data to the client.
        /// 
        /// COMMON USE CASES:
        /// - Product catalogs, read-heavy user dashboards, blog posts, and configuration settings.
        /// - Any data that is read frequently but updated infrequently.
        /// </remarks>
        [HttpGet("product/{id}")]
        public async Task<IActionResult> GetProduct(int id)
        {
            string cacheKey = $"product:{id}";
            var stopwatch = Stopwatch.StartNew();

            // STEP 1: Check if the product exists in the Redis Cache
            RedisValue cachedProduct = await _database.StringGetAsync(cacheKey);

            if (cachedProduct.HasValue)
            {
                // CACHE HIT!
                // Deserialize the cached string and stop the stopwatch.
                var product = JsonSerializer.Deserialize<Product>(cachedProduct.ToString());
                stopwatch.Stop();

                return Ok(new
                {
                    Source = "Cache",
                    TimeElapsedMs = stopwatch.ElapsedMilliseconds,
                    Product = product,
                    CacheHit = true,
                    Message = "Cache HIT: Retrieved product directly from Redis."
                });
            }

            // CACHE MISS!
            // STEP 2: Query the mock database (which has a simulated delay of 1.5 seconds)
            Product? dbProduct = await MockDatabase.GetByIdAsync(id);

            if (dbProduct == null)
            {
                stopwatch.Stop();
                return NotFound(new
                {
                    Source = "Database",
                    TimeElapsedMs = stopwatch.ElapsedMilliseconds,
                    Message = $"Product with ID {id} not found in database.",
                    CacheHit = false
                });
            }

            // STEP 3: Store the database result in the cache for future requests.
            // We set an expiration of 2 minutes (Absolute Expiration) to prevent stale cache indefinitely.
            string serializedProduct = JsonSerializer.Serialize(dbProduct);
            await _database.StringSetAsync(cacheKey, serializedProduct, TimeSpan.FromMinutes(2));
            
            stopwatch.Stop();

            return Ok(new
            {
                Source = "Database (Slow Mock Call)",
                TimeElapsedMs = stopwatch.ElapsedMilliseconds,
                Product = dbProduct,
                CacheHit = false,
                Message = "Cache MISS: Retrieved product from slow database and populated Redis."
            });
        }
    }
}
