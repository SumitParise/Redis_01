using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace RedisCacheLearning.Controllers
{
    /// <summary>
    /// Demonstrates caching with Absolute Expiration and Sliding Expiration.
    /// Explains how both work, when to use them, and how sliding expiration is implemented in raw Redis.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ExpirationController : ControllerBase
    {
        private readonly IDatabase _database;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpirationController"/>.
        /// </summary>
        /// <param name="redis">The Singleton ConnectionMultiplexer instance.</param>
        public ExpirationController(IConnectionMultiplexer redis)
        {
            _database = redis.GetDatabase();
        }

        /// <summary>
        /// Caches a value with Absolute Expiration.
        /// </summary>
        /// <param name="key">The cache key identifier.</param>
        /// <param name="value">The string value to store.</param>
        /// <param name="seconds">The lifespan of the cache key in seconds (default: 30s).</param>
        /// <returns>Confirmation message.</returns>
        /// <remarks>
        /// WHAT IS ABSOLUTE EXPIRATION?
        /// The item expires and is deleted from the cache at a specific, fixed time/duration, 
        /// regardless of how many times it was accessed.
        /// 
        /// COMMON USE CASES:
        /// - Hourly weather reports, currency exchange rates, or static content.
        /// - Data that changes on a predictable schedule (e.g., "invalidates every 2 hours").
        /// </remarks>
        [HttpPost("absolute")]
        public async Task<IActionResult> SetAbsoluteExpiration(string key, string value, int seconds = 30)
        {
            var expiryTimeSpan = TimeSpan.FromSeconds(seconds);

            // StringSetAsync executes 'SET key value EX seconds' behind the scenes.
            // Redis will automatically delete this key after the specified duration.
            bool success = await _database.StringSetAsync(key, value, expiryTimeSpan);

            if (success)
            {
                return Ok(new
                {
                    Message = $"Cached key '{key}' with Absolute Expiration of {seconds} seconds.",
                    ExpiresAt = DateTime.UtcNow.Add(expiryTimeSpan).ToString("o")
                });
            }

            return BadRequest("Failed to set cache value.");
        }

        /// <summary>
        /// Caches a value with Sliding Expiration.
        /// </summary>
        /// <param name="key">The cache key identifier.</param>
        /// <param name="value">The string value to store.</param>
        /// <param name="seconds">The sliding window duration in seconds (default: 30s).</param>
        /// <returns>Confirmation message.</returns>
        /// <remarks>
        /// WHAT IS SLIDING EXPIRATION?
        /// The item expires if it is not accessed within a specific window. 
        /// If accessed, the expiration timer resets/extends for another full window.
        /// 
        /// COMMON USE CASES:
        /// - User Session Storage: As long as the user remains active, keep their session cached.
        ///   If they close their browser and go idle for 30 minutes, automatically log them out and free memory.
        /// - Shopping Cart caching during checkout.
        /// </remarks>
        [HttpPost("sliding")]
        public async Task<IActionResult> SetSlidingExpiration(string key, string value, int seconds = 30)
        {
            var slidingWindow = TimeSpan.FromSeconds(seconds);

            // We store the value with an initial expiry.
            // Raw Redis does not have a native "set-with-sliding-expiry" command.
            // Sliding behavior is achieved programmatically: we reset the TTL every time we read it (demonstrated in GET below).
            bool success = await _database.StringSetAsync(key, value, slidingWindow);

            // We also store a metadata key to indicate that this key should slide on read
            // (in a real system, you might prefix keys to determine their expiration behavior)
            await _database.StringSetAsync($"{key}:is_sliding", "true", slidingWindow);

            if (success)
            {
                return Ok(new
                {
                    Message = $"Cached key '{key}' with initial Sliding Expiration window of {seconds} seconds.",
                    InitialExpiry = DateTime.UtcNow.Add(slidingWindow).ToString("o")
                });
            }

            return BadRequest("Failed to set cache value.");
        }

        /// <summary>
        /// Gets a key and handles Sliding Expiration logic.
        /// </summary>
        /// <param name="key">The cache key identifier.</param>
        /// <param name="slidingSeconds">The window to reset the TTL to, if it's a sliding key (default: 30s).</param>
        /// <returns>The stored value and the updated TTL details.</returns>
        [HttpGet("get")]
        public async Task<IActionResult> GetWithExpirationCheck(string key, int slidingSeconds = 30)
        {
            // Execute GET key
            RedisValue value = await _database.StringGetAsync(key);

            if (!value.HasValue)
            {
                return NotFound(new { Message = $"Key '{key}' was not found in Redis (it may have expired).", CacheHit = false });
            }

            // Check remaining TTL (Time-To-Live) using 'TTL key' command
            TimeSpan? currentTtl = await _database.KeyTimeToLiveAsync(key);

            // Check if this key was marked as a sliding expiration key
            bool isSliding = await _database.KeyExistsAsync($"{key}:is_sliding");
            string actionTaken = "None (Absolute Expiration)";

            if (isSliding)
            {
                // To achieve SLIDING expiration in raw Redis, we must manually slide the expiration:
                // We call KeyExpireAsync which executes the 'EXPIRE key seconds' command.
                var newTtl = TimeSpan.FromSeconds(slidingSeconds);
                await _database.KeyExpireAsync(key, newTtl);
                await _database.KeyExpireAsync($"{key}:is_sliding", newTtl);

                currentTtl = newTtl; // reflect updated TTL in response
                actionTaken = $"Extended (Sliding Expiration reset to {slidingSeconds} seconds)";
            }

            return Ok(new
            {
                Key = key,
                Value = value.ToString(),
                IsSliding = isSliding,
                ActionTaken = actionTaken,
                RemainingTtlSeconds = currentTtl?.TotalSeconds,
                NewExpirationUtc = currentTtl.HasValue ? DateTime.UtcNow.Add(currentTtl.Value).ToString("o") : "Never"
            });
        }
    }
}
