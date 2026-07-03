using System.Collections.Generic;
using System.Linq;
using StackExchange.Redis;

namespace RedisCacheLearning.Services
{
    /// <summary>
    /// Service that wraps low-level key retrieval operations on Redis.
    /// This demonstrates how to query keys using patterns in a production-safe way.
    /// </summary>
    public class RedisKeysHelper
    {
        private readonly IConnectionMultiplexer _multiplexer;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisKeysHelper"/> service.
        /// </summary>
        /// <param name="multiplexer">The Singleton ConnectionMultiplexer instance.</param>
        public RedisKeysHelper(IConnectionMultiplexer multiplexer)
        {
            _multiplexer = multiplexer;
        }

        /// <summary>
        /// Retrieves all keys matching a specific pattern (e.g., "product:*") in a production-safe way.
        /// </summary>
        /// <param name="pattern">The glob-style pattern to search for.</param>
        /// <returns>A collection of matching RedisKeys.</returns>
        /// <remarks>
        /// IMPORTANT EDUCATIONAL NOTE:
        /// 1. Why NOT use the raw Redis 'KEYS' command?
        ///    The 'KEYS' command searches the entire keyspace synchronously. Since Redis is single-threaded,
        ///    running 'KEYS' on a production database with millions of keys will block the server and cause
        ///    latency spikes or timeouts for all other clients.
        /// 
        /// 2. How StackExchange.Redis solves this:
        ///    When you call 'server.Keys()', the StackExchange.Redis library automatically determines if the Redis
        ///    server supports the 'SCAN' command (introduced in Redis 2.8). If supported, it uses 'SCAN' under the
        ///    hood.
        /// 
        /// 3. What does 'SCAN' do?
        ///    'SCAN' is a cursor-based iterator. It returns keys in small, incremental batches (pages).
        ///    This allows Redis to process other incoming commands between page fetches, ensuring the server
        ///    remains responsive.
        /// </remarks>
        public IEnumerable<RedisKey> GetKeysByPattern(string pattern)
        {
            // Get all configured endpoints for the Redis cluster/server connection
            var endpoints = _multiplexer.GetEndPoints();
            var keysList = new List<RedisKey>();

            // Iterate over all endpoints to ensure we collect keys from all nodes in case of clustering/sentinels
            foreach (var endpoint in endpoints)
            {
                var server = _multiplexer.GetServer(endpoint);

                // server.Keys performs a SCAN command iteratively under the hood.
                // It returns an IEnumerable that lazy-loads keys page-by-page.
                var keys = server.Keys(database: -1, pattern: pattern);
                keysList.AddRange(keys);
            }

            // Return unique keys if we queried multiple nodes/endpoints
            return keysList.Distinct();
        }
    }
}
