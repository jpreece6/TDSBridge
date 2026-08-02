using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TDSBridge.Common.Cache;

public interface ICache
{
    public void Set<T>(object key, T value);
    public bool TryGetValue(object key, out object value);
    public bool TryGetValue<T>(object key, out T value);
}


public abstract class BaseCache : ICache
{
    public abstract void Set<T>(object key, T value);
    public abstract bool TryGetValue(object key, out object value);
    public abstract bool TryGetValue<T>(object key, out T value);
}

public class InMemoryCacheWrapper : BaseCache
{
    readonly IMemoryCache _memoryCache;
    readonly MemoryCacheEntryOptions _options;

    public InMemoryCacheWrapper(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
        _options = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));
    }

    public override void Set<T>(object key, T value)
    {
        _memoryCache.Set(key, value, _options);
    }

    public override bool TryGetValue(object key, out object value)
    {
        return _memoryCache.TryGetValue(key, out value);
    }

    public override bool TryGetValue<T>(object key, out T value)
    {
        return _memoryCache.TryGetValue(key, out value);
    }
}

public class RedisCacheWrapper : BaseCache
{
    readonly IDistributedCache _distributedCache;

    public RedisCacheWrapper(IDistributedCache distributedCache)
    {
        _distributedCache = distributedCache;   
    }
    
    public override void Set<T>(object key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        _distributedCache.SetString(key.ToString(), json);
    }

    public override bool TryGetValue(object key, out object value)
    {
        return TryGetValue<object>(key, out value);
    }

    public override bool TryGetValue<T>(object key, out T value)
    {
        var json = _distributedCache.GetString(key.ToString());

        if (string.IsNullOrEmpty(json))
        {
            value = default;
            return false;
        }

        value = (T) JsonSerializer.Deserialize(json, typeof(T));
        
        return true;
    }
}

public static class HostedExtensions
{
    public static IServiceCollection UseInMemoryCache(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ICache, InMemoryCacheWrapper>();

        return services;
    }
    
    public static IServiceCollection UseRedis(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            //options.Configuration = "192.168.1.159:6379"; // Redis connection
            //options.InstanceName = "TDSBridge_";          // Optional key prefix
            
            options.Configuration = configuration.GetSection("RedisSettings").GetValue<string>("Server");
            options.InstanceName = configuration.GetSection("RedisSettings").GetValue<string>("InstanceName");
        });
        
        services.AddSingleton<ICache, RedisCacheWrapper>();
        
        return services;
    }
}