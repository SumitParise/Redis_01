using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.OpenApi;
using RedisCacheLearning.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

#region Redis & Caching Configuration

// Retrieve the Redis connection string from configuration (appsettings.json)
// Defaults to "localhost:6379" if not found.
string redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

// ============================================================================
// 1. StackExchange.Redis ConnectionMultiplexer Registration (Singleton)
// 
// WHY SINGLETON?
// The ConnectionMultiplexer object is designed to be shared and reused. It manages connection 
// pools and handles auto-reconnection internally. Recreating this object on every request 
// is extremely expensive, leads to socket exhaustion, and destroys performance.
// 
// WHAT DOES IT PROVIDE?
// It gives direct, low-level access to the Redis database via the IDatabase interface. 
// This allows executing Redis-specific commands (like EXISTS, keys scanning/SCAN, hashes, 
// pub/sub, sets, sorted sets, etc.) that aren't available in standard abstractions.
// ============================================================================
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    // Connect to the Redis server using the provided connection string.
    return ConnectionMultiplexer.Connect(redisConnectionString);
});

// ============================================================================
// 2. IDistributedCache Registration (using Microsoft.Extensions.Caching.StackExchangeRedis)
// 
// WHAT IS IDistributedCache?
// It is a standard abstraction provided by Microsoft (.NET Core) for caching. 
// It exposes simple methods like GetAsync, SetAsync, RefreshAsync, and RemoveAsync 
// accepting string keys and byte[] array values.
// 
// STACKEXCHANGE.REDIS DIRECT vs IDistributedCache:
// - IDistributedCache:
//   * Pros: Decouples code from a specific caching database. You can swap Redis for SQL Server 
//     or NCache simply by changing the service registration in Program.cs.
//   * Cons: Limited API. Only operates on byte[]/strings. You cannot run Redis-specific commands 
//     like checking key expiration remaining time, lists, SCAN, hashes, or transaction scripting.
// - Direct StackExchange.Redis (IConnectionMultiplexer):
//   * Pros: Full access to all 400+ Redis commands and rich data types. Higher performance 
//     options (like pipeline execution and batching).
//   * Cons: Hard-locks your codebase to Redis.
// ============================================================================
builder.Services.AddStackExchangeRedisCache(options =>
{
    // The Redis connection string
    options.Configuration = redisConnectionString;
    
    // A prefix prepended to every key created by this IDistributedCache instance (e.g. RedisCacheLearning_key).
    // This helps prevent key collision when sharing a single Redis server across multiple applications.
    options.InstanceName = "RedisCacheLearning_";
});

// ============================================================================
// 3. Register our custom production-safe Redis key scanning helper service.
// ============================================================================
builder.Services.AddSingleton<RedisKeysHelper>();

#endregion

#region OpenAPI / Swagger Setup

// Configure Swagger Generator with API Information
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Redis Caching Learning API (.NET 10)",
        Version = "v1",
        Description = "An educational API demonstrating Redis Caching concepts, absolute/sliding expiration, object serialization, cache-aside, and cache refresh strategies (Full vs. Delta refresh)."
    });
    
    // Enable XML comments to be displayed in Swagger UI
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

#endregion

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Enable Swagger middleware to serve the generated JSON document and the UI
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Redis Cache Learning API v1");
        c.RoutePrefix = "swagger"; // Serves Swagger UI at /swagger
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
