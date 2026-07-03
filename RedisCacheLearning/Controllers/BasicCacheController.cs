using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Threading.Tasks;

namespace RedisCacheLearning.Controllers
{
    /// <summary>
    /// Demonstrates the most basic Redis operations (GET, SET, DELETE, and EXISTS)
    /// using the direct StackExchange.Redis client (ConnectionMultiplexer).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class BasicCacheController : ControllerBase
    {
        private readonly IDatabase _database;

        /// <summary>
        /// Initializes a new instance of the <see cref="BasicCacheController"/> using dependency injection.
        /// </summary>
        /// <param name="redis">The Singleton ConnectionMultiplexer instance.</param>
        public BasicCacheController(IConnectionMultiplexer redis)
        {
            // IDatabase is a lightweight, thread-safe object representing a connection to a specific Redis database.
            // By default, GetDatabase() accesses database index 0 (Redis supports indexes 0-15 by default).
            // It is cheap to call and does not establish a new network connection.
            _database = redis.GetDatabase();
        }

        /// <summary>
        /// Sets a simple string value in Redis.
        /// </summary>
        /// <param name="key">The cache key identifier.</param>
        /// <param name="value">The string value to store.</param>
        /// <returns>A confirmation message.</returns>
        /// <response code="200">Returns confirmation that the key was stored.</response>
        [HttpPost("set")]
        public async Task<IActionResult> SetString(string key, string value)
        {
            // StringSetAsync executes the Redis 'SET key value' command.
            // It stores the raw string against the key. If the key already exists, it overwrites it.
            bool success = await _database.StringSetAsync(key, value);

            if (success)
            {
                return Ok(new { Message = $"Successfully cached key '{key}' with value '{value}'.", CommandExecuted = "SET" });
            }

            return BadRequest("Failed to set cache value.");
        }

        /// <summary>
        /// Gets a string value from Redis by its key.
        /// </summary>
        /// <param name="key">The cache key identifier.</param>
        /// <returns>The stored string value, or a 404 if not found.</returns>
        [HttpGet("get")]
        public async Task<IActionResult> GetString(string key)
        {
            // StringGetAsync executes the Redis 'GET key' command.
            // If the key does not exist, it returns a RedisValue representing Null/Nil.
            RedisValue value = await _database.StringGetAsync(key);

            // In StackExchange.Redis, you can check if a RedisValue is null or empty using HasValue.
            if (!value.HasValue)
            {
                return NotFound(new { Message = $"Key '{key}' was not found in Redis.", CacheHit = false });
            }

            return Ok(new { Key = key, Value = value.ToString(), CacheHit = true, CommandExecuted = "GET" });
        }

        /// <summary>
        /// Checks if a key exists in Redis.
        /// </summary>
        /// <param name="key">The cache key identifier.</param>
        /// <returns>Boolean indicating key existence.</returns>
        /// <remarks>
        /// Real-world Use Case:
        /// Checking key existence is highly performant and does not transfer the payload,
        /// which is ideal for rate-limiting checks or existence validation of tokens.
        /// </remarks>
        [HttpGet("exists")]
        public async Task<IActionResult> CheckExists(string key)
        {
            // KeyExistsAsync executes the Redis 'EXISTS key' command.
            // This returns true (1) if the key exists, or false (0) if it does not.
            bool exists = await _database.KeyExistsAsync(key);

            return Ok(new { Key = key, Exists = exists, CommandExecuted = "EXISTS" });
        }

        /// <summary>
        /// Deletes a key from Redis.
        /// </summary>
        /// <param name="key">The cache key to remove.</param>
        /// <returns>A confirmation message.</returns>
        /// <remarks>
        /// Real-world Use Case:
        /// Used for manual cache invalidation. For example, when a user updates their profile,
        /// we manually delete their cached profile key so the next request pulls fresh data.
        /// </remarks>
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteKey(string key)
        {
            // KeyDeleteAsync executes the Redis 'DEL key' command.
            // It returns true if the key was deleted, or false if the key did not exist.
            bool deleted = await _database.KeyDeleteAsync(key);

            if (deleted)
            {
                return Ok(new { Message = $"Successfully deleted key '{key}' from Redis.", CommandExecuted = "DEL", Success = true });
            }

            return NotFound(new { Message = $"Key '{key}' did not exist in Redis, so it could not be deleted.", CommandExecuted = "DEL", Success = false });
        }
    }
}
