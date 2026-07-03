using System;

namespace RedisCacheLearning.Data
{
    /// <summary>
    /// Represents a Product entity in our mock database.
    /// This object is serialized to JSON before storing in Redis, as Redis natively stores strings or bytes.
    /// </summary>
    public class Product
    {
        /// <summary>
        /// Unique identifier for the product.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Name of the product.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Price of the product.
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Detailed description of the product.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Indicates when the product record was last modified.
        /// Essential for demonstrating Delta Cache Refresh, where we only fetch records modified since a specific timestamp.
        /// </summary>
        public DateTime LastModified { get; set; }
    }
}
