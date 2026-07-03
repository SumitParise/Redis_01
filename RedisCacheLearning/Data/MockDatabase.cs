using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RedisCacheLearning.Data
{
    /// <summary>
    /// Simulates a slow relational database. 
    /// An in-memory static list is used as the data store, and simulated latency is added to demonstrate 
    /// the performance benefits of caching (reducing response time from ~1.5 seconds to milliseconds).
    /// </summary>
    public static class MockDatabase
    {
        // Thread-safe lock object for mutating our mock data
        private static readonly object _lock = new();

        // In-memory product collection seeded with dummy records
        private static readonly List<Product> _products = new()
        {
            new Product { Id = 1, Name = "Developer Laptop Pro", Price = 1299.99m, Description = "High-end developer laptop with 32GB RAM, 1TB SSD.", LastModified = DateTime.UtcNow.AddHours(-5) },
            new Product { Id = 2, Name = "Wireless Ergonomic Mouse", Price = 49.99m, Description = "Comfortable wireless mouse with customizable buttons.", LastModified = DateTime.UtcNow.AddHours(-4) },
            new Product { Id = 3, Name = "Mechanical Gaming Keyboard", Price = 119.99m, Description = "RGB mechanical keyboard with tactile blue switches.", LastModified = DateTime.UtcNow.AddHours(-3) },
            new Product { Id = 4, Name = "4K Ultra-Wide Monitor", Price = 399.99m, Description = "34-inch curved display for immersive multitasking.", LastModified = DateTime.UtcNow.AddHours(-2) },
            new Product { Id = 5, Name = "Noise-Cancelling Headphones", Price = 199.99m, Description = "Active noise cancelling over-ear headphones with long battery life.", LastModified = DateTime.UtcNow.AddHours(-1) }
        };

        /// <summary>
        /// Retrieves all products from the mock database with a simulated delay.
        /// Useful for demonstrating full cache refresh loading.
        /// </summary>
        public static async Task<List<Product>> GetAllAsync()
        {
            // Simulate slow database read latency (1.5 seconds)
            // This is intentional to showcase why we cache expensive queries!
            await Task.Delay(1500);

            lock (_lock)
            {
                // Return a copy of the list to prevent external modification of the internal list references
                return _products.Select(p => CloneProduct(p)).ToList();
            }
        }

        /// <summary>
        /// Retrieves a single product by ID with a simulated delay.
        /// Useful for cache-aside (lazy loading) demonstrations.
        /// </summary>
        public static async Task<Product?> GetByIdAsync(int id)
        {
            // Simulate database latency
            await Task.Delay(1500);

            lock (_lock)
            {
                var product = _products.FirstOrDefault(p => p.Id == id);
                return product != null ? CloneProduct(product) : null;
            }
        }

        /// <summary>
        /// Retrieves only the products that have been modified after the specified timestamp.
        /// Essential for Delta Cache Refresh, fetching only changed records to avoid rewriting everything.
        /// </summary>
        public static async Task<List<Product>> GetModifiedSinceAsync(DateTime since)
        {
            // Simulate database query latency (e.g. searching/filtering records)
            await Task.Delay(1000);

            lock (_lock)
            {
                return _products
                    .Where(p => p.LastModified > since)
                    .Select(p => CloneProduct(p))
                    .ToList();
            }
        }

        /// <summary>
        /// Simulates updates occurring in the database.
        /// Updates the description and sets the LastModified timestamp of the specified product to UtcNow.
        /// </summary>
        public static bool SimulateUpdate(int id, string newDescription)
        {
            lock (_lock)
            {
                var product = _products.FirstOrDefault(p => p.Id == id);
                if (product == null) return false;

                product.Description = newDescription;
                product.LastModified = DateTime.UtcNow;
                return true;
            }
        }

        /// <summary>
        /// Helper to clone product entries to prevent referencing in-memory shared objects directly in controllers.
        /// </summary>
        private static Product CloneProduct(Product original)
        {
            return new Product
            {
                Id = original.Id,
                Name = original.Name,
                Price = original.Price,
                Description = original.Description,
                LastModified = original.LastModified
            };
        }
    }
}
