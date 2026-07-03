using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using RedisCacheLearning.Data;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace RedisCacheLearning.Controllers
{
    /// <summary>
    /// Demonstrates caching using Microsoft's standard IDistributedCache abstraction.
    /// Explains how it differs from StackExchange.Redis and shows how to set absolute and sliding expirations.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DistributedCacheController : ControllerBase
    {
        private readonly IDistributedCache _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedCacheController"/>.
        /// </summary>
        /// <param name="cache">The IDistributedCache abstraction instance resolved via DI.</param>
        public DistributedCacheController(IDistributedCache cache)
        {
            // IDistributedCache is registered in Program.cs using .AddStackExchangeRedisCache(...)
            // Behind the scenes, it connects to Redis.
            _cache = cache;
        }

        /// <summary>
        /// Stores a product in the cache using IDistributedCache.
        /// </summary>
        /// <param name="key">The key identifier.</param>
        /// <param name="product">The product details from the request body.</param>
        /// <returns>Confirmation message.</returns>
        /// <remarks>
        /// KEY DIFFERENCES WITH IDistributedCache:
        /// 1. Auto-Prefixing: All keys written will be prefixed with the "InstanceName" we configured 
        ///    in Program.cs ("RedisCacheLearning_"). Redis will see the key as "RedisCacheLearning_[yourKey]".
        /// 2. Options object: Instead of passing TimeSpan directly, we use DistributedCacheEntryOptions.
        /// 3. String and Byte support: The direct API works with byte arrays (byte[]). We use the 
        ///    Microsoft.Extensions.Caching.Distributed extension methods (like SetStringAsync) 
        ///    to easily work with strings.
        /// </remarks>
        [HttpPost("product/set")]
        public async Task<IActionResult> SetProduct(string key, [FromBody] Product product)
        {
            if (product == null) return BadRequest("Product cannot be null.");

            // Set up cache options
            var options = new DistributedCacheEntryOptions()
                // Absolute Expiration: The item will expire in 2 minutes no matter what.
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(2))
                // Sliding Expiration: The item will expire in 45 seconds if it is not accessed.
                // Each access extends its life by another 45 seconds, up to the 2-minute absolute ceiling.
                .SetSlidingExpiration(TimeSpan.FromSeconds(45));

            string jsonString = JsonSerializer.Serialize(product);

            // SetStringAsync is an extension method in Microsoft.Extensions.Caching.Distributed.
            // It automatically serializes the string to a UTF-8 byte array and writes to Redis.
            await _cache.SetStringAsync(key, jsonString, options);

            return Ok(new
            {
                Message = $"Stored key '{key}' via IDistributedCache.",
                StoredKeyInRedis = $"RedisCacheLearning_{key}",
                Rules = "Absolute Expiry: 2 mins, Sliding Expiry: 45 secs."
            });
        }

        /// <summary>
        /// Retrieves a product from the cache using IDistributedCache.
        /// </summary>
        /// <param name="key">The key identifier.</param>
        /// <returns>The deserialized Product object.</returns>
        [HttpGet("product/get")]
        public async Task<IActionResult> GetProduct(string key)
        {
            // GetStringAsync reads the UTF-8 byte array from Redis and converts it back to a string.
            // This operation automatically triggers sliding expiration updates inside the Redis provider!
            string? cachedJson = await _cache.GetStringAsync(key);

            if (string.IsNullOrEmpty(cachedJson))
            {
                return NotFound(new { Message = $"Key '{key}' not found or has expired.", CacheHit = false });
            }

            var product = JsonSerializer.Deserialize<Product>(cachedJson);

            return Ok(new
            {
                Key = key,
                Product = product,
                CacheHit = true,
                Message = "Retrieved via IDistributedCache (sliding window reset if active)."
            });
        }

        /// <summary>
        /// Removes a cached item by key using IDistributedCache.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <returns>Confirmation message.</returns>
        [HttpDelete("product/delete")]
        public async Task<IActionResult> RemoveProduct(string key)
        {
            // RemoveAsync deletes the key. (Internally maps to Redis 'DEL' command)
            await _cache.RemoveAsync(key);

            return Ok(new { Message = $"Invoked RemoveAsync for key '{key}'. If it existed, it has been removed.", Key = key });
        }
    }
}
