using Microsoft.AspNetCore.Mvc;
using RedisCacheLearning.Data;
using StackExchange.Redis;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace RedisCacheLearning.Controllers
{
    /// <summary>
    /// Demonstrates how to cache complex C# objects in Redis.
    /// Explains how objects are serialized to JSON strings before being saved, and deserialized on retrieval.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ObjectCacheController : ControllerBase
    {
        private readonly IDatabase _database;

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectCacheController"/>.
        /// </summary>
        /// <param name="redis">The Singleton ConnectionMultiplexer instance.</param>
        public ObjectCacheController(IConnectionMultiplexer redis)
        {
            _database = redis.GetDatabase();
        }

        /// <summary>
        /// Serializes and stores a Product object in Redis.
        /// </summary>
        /// <param name="key">The cache key identifier (e.g. "product:1").</param>
        /// <param name="product">The product details provided in the request body.</param>
        /// <param name="expiryMinutes">Expiration duration in minutes (default: 10).</param>
        /// <returns>Confirmation message and the JSON payload stored.</returns>
        /// <remarks>
        /// WHY SERIALIZE?
        /// Redis stores key-value pairs where values can be strings, blobs, lists, sets, hashes.
        /// It does not know what a C# Class or Struct is. We must convert C# objects to a string 
        /// representation (like JSON) or binary representation (like Protobuf or MessagePack) 
        /// before sending them over the wire to Redis.
        /// </remarks>
        [HttpPost("set")]
        public async Task<IActionResult> SetObject(string key, [FromBody] Product product, int expiryMinutes = 10)
        {
            if (product == null)
            {
                return BadRequest("Product data cannot be null.");
            }

            // Always assign or preserve a LastModified timestamp for delta refresh testing
            if (product.LastModified == default)
            {
                product.LastModified = DateTime.UtcNow;
            }

            // Serialize the C# object to a JSON string using System.Text.Json.
            // JsonSerializer.Serialize is highly optimized in modern .NET.
            string jsonPayload = JsonSerializer.Serialize(product);

            var expiry = TimeSpan.FromMinutes(expiryMinutes);

            // Store the JSON string in Redis.
            bool success = await _database.StringSetAsync(key, jsonPayload, expiry);

            if (success)
            {
                return Ok(new
                {
                    Message = $"Successfully serialized and stored product in key '{key}'.",
                    StoredPayload = jsonPayload,
                    ExpiresInMinutes = expiryMinutes
                });
            }

            return BadRequest("Failed to store product in cache.");
        }

        /// <summary>
        /// Retrieves and deserializes a Product object from Redis.
        /// </summary>
        /// <param name="key">The cache key identifier.</param>
        /// <returns>The deserialized Product object.</returns>
        [HttpGet("get")]
        public async Task<IActionResult> GetObject(string key)
        {
            // Retrieve the raw JSON string from Redis.
            RedisValue jsonResult = await _database.StringGetAsync(key);

            if (!jsonResult.HasValue)
            {
                return NotFound(new { Message = $"Object with key '{key}' was not found in Redis.", CacheHit = false });
            }

            try
            {
                // Deserialize the JSON string back into our C# Product object.
                Product? product = JsonSerializer.Deserialize<Product>(jsonResult.ToString());

                return Ok(new
                {
                    Key = key,
                    Product = product,
                    CacheHit = true,
                    Message = "Successfully retrieved and deserialized object from Redis."
                });
            }
            catch (JsonException ex)
            {
                // Handle scenarios where the cached data isn't a valid JSON representation of Product.
                return BadRequest(new
                {
                    Message = $"Failed to deserialize cache payload. The content under key '{key}' may not be a valid Product JSON.",
                    RawPayload = jsonResult.ToString(),
                    Error = ex.Message
                });
            }
        }
    }
}
