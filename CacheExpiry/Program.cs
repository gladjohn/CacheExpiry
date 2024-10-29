using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CacheExample
{
    // Cache entry with a fixed expiration
    public class FixedExpirationCacheEntry<TValue>
    {
        public TValue Value { get; }
        public DateTime Expiration { get; }

        public FixedExpirationCacheEntry(TValue value, TimeSpan ttl)
        {
            Value = value;
            Expiration = DateTime.UtcNow.Add(ttl);
        }

        public bool IsExpired => DateTime.UtcNow > Expiration;
    }

    // Cache with fixed expiration
    public class ConcurrentCacheWithFixedExpiration<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, FixedExpirationCacheEntry<TValue>> _cache = new();
        private readonly TimeSpan _defaultTtl;

        public ConcurrentCacheWithFixedExpiration(TimeSpan defaultTtl)
        {
            _defaultTtl = defaultTtl;
        }

        // Add or update cache entry
        public void Set(TKey key, TValue value)
        {
            var entry = new FixedExpirationCacheEntry<TValue>(value, _defaultTtl);
            _cache.AddOrUpdate(key, entry, (k, oldEntry) => entry);
            Console.WriteLine($"Added or updated key '{key}' with value '{value}' in the cache. Expiration: {entry.Expiration}");
        }

        // Try to get a cache entry, removing it if expired
        public bool TryGet(TKey key, out TValue value)
        {
            if (_cache.TryGetValue(key, out FixedExpirationCacheEntry<TValue> entry))
            {
                if (entry.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                    Console.WriteLine($"Key '{key}' has expired and was removed from the cache.");
                    value = default;
                    return false;
                }
                Console.WriteLine($"Retrieved key '{key}' from cache with value '{entry.Value}'. Expiration: {entry.Expiration}");
                value = entry.Value;
                return true;
            }
            value = default;
            return false;
        }

        // Periodically clean expired items
        public async Task CleanExpiredEntriesAsync(TimeSpan cleanInterval, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var key in _cache.Keys)
                {
                    if (_cache.TryGetValue(key, out FixedExpirationCacheEntry<TValue> entry) && entry.IsExpired)
                    {
                        _cache.TryRemove(key, out _);
                        Console.WriteLine($"Expired key '{key}' was removed from the cache.");
                    }
                }

                await Task.Delay(cleanInterval, cancellationToken);
            }
        }
    }

    // Cache entry with sliding expiration
    public class SlidingExpirationCacheEntry<TValue>
    {
        public TValue Value { get; private set; }
        public DateTime Expiration { get; private set; }
        private readonly TimeSpan _slidingExpiration;

        public SlidingExpirationCacheEntry(TValue value, TimeSpan slidingExpiration)
        {
            Value = value;
            _slidingExpiration = slidingExpiration;
            Expiration = DateTime.UtcNow.Add(_slidingExpiration);
        }

        // Extend expiration time each time the item is accessed
        public void ExtendExpiration()
        {
            Expiration = DateTime.UtcNow.Add(_slidingExpiration);
            Console.WriteLine($"Expiration for this entry has been extended to {Expiration}.");
        }

        public bool IsExpired => DateTime.UtcNow > Expiration;
    }

    // Cache with sliding expiration
    public class ConcurrentCacheWithSlidingExpiration<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, SlidingExpirationCacheEntry<TValue>> _cache = new();
        private readonly TimeSpan _defaultSlidingExpiration;

        public ConcurrentCacheWithSlidingExpiration(TimeSpan slidingExpiration)
        {
            _defaultSlidingExpiration = slidingExpiration;
        }

        // Add or update cache entry
        public void Set(TKey key, TValue value)
        {
            var entry = new SlidingExpirationCacheEntry<TValue>(value, _defaultSlidingExpiration);
            _cache.AddOrUpdate(key, entry, (k, oldEntry) => entry);
            Console.WriteLine($"Added or updated key '{key}' with value '{value}' in the cache. Initial Expiration: {entry.Expiration}");
        }

        // Try to get a cache entry, extending its expiration time if accessed
        public bool TryGet(TKey key, out TValue value)
        {
            if (_cache.TryGetValue(key, out SlidingExpirationCacheEntry<TValue> entry))
            {
                if (entry.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                    Console.WriteLine($"Key '{key}' has expired and was removed from the cache.");
                    value = default;
                    return false;
                }
                entry.ExtendExpiration();  // Extend expiration time on access
                Console.WriteLine($"Retrieved key '{key}' from cache with value '{entry.Value}'. New Expiration: {entry.Expiration}");
                value = entry.Value;
                return true;
            }
            value = default;
            return false;
        }

        // Periodically clean expired items
        public async Task CleanExpiredEntriesAsync(TimeSpan cleanInterval, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var key in _cache.Keys)
                {
                    if (_cache.TryGetValue(key, out SlidingExpirationCacheEntry<TValue> entry) && entry.IsExpired)
                    {
                        _cache.TryRemove(key, out _);
                        Console.WriteLine($"Expired key '{key}' was removed from the cache.");
                    }
                }
                await Task.Delay(cleanInterval, cancellationToken);
            }
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Choose cache type:");
            Console.WriteLine("1. Fixed Expiration Cache");
            Console.WriteLine("2. Sliding Expiration Cache");

            string choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await RunFixedExpirationCacheAsync();
                    break;

                case "2":
                    await RunSlidingExpirationCacheAsync();
                    break;

                default:
                    Console.WriteLine("Invalid choice");
                    break;
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        // Demonstrates fixed expiration cache
        private static async Task RunFixedExpirationCacheAsync()
        {
            Console.WriteLine("Running Fixed Expiration Cache...");

            var cache = new ConcurrentCacheWithFixedExpiration<string, string>(TimeSpan.FromSeconds(5));
            cache.Set("Key1", "Value1");
            cache.Set("Key2", "Value2");

            if (cache.TryGet("Key1", out var value1))
            {
                Console.WriteLine($"Key1 found: {value1}");
            }

            Console.WriteLine("Waiting 6 seconds to allow Key1 to expire...");
            await Task.Delay(TimeSpan.FromSeconds(6));

            if (!cache.TryGet("Key1", out var expiredValue1))
            {
                Console.WriteLine("Key1 has expired.");
            }

            // Clean up expired entries in the background
            var cts = new CancellationTokenSource();
            _ = cache.CleanExpiredEntriesAsync(TimeSpan.FromSeconds(2), cts.Token);

            cache.Set("Key3", "Value3");
            if (cache.TryGet("Key3", out var value3))
            {
                Console.WriteLine($"Key3 found: {value3}");
            }

            Console.WriteLine("Stopping background cleanup.");
            cts.Cancel();
        }

        // Demonstrates sliding expiration cache
        private static async Task RunSlidingExpirationCacheAsync()
        {
            Console.WriteLine("Running Sliding Expiration Cache...");

            var cache = new ConcurrentCacheWithSlidingExpiration<string, string>(TimeSpan.FromSeconds(5));
            cache.Set("Key1", "Value1");
            cache.Set("Key2", "Value2");

            if (cache.TryGet("Key1", out var value1))
            {
                Console.WriteLine($"Key1 found: {value1}");
            }

            Console.WriteLine("Waiting 3 seconds before accessing Key1 to extend expiration...");
            await Task.Delay(TimeSpan.FromSeconds(3));

            if (cache.TryGet("Key1", out value1))
            {
                Console.WriteLine($"Key1 accessed and expiration extended. New Value: {value1}");
            }

            Console.WriteLine("Waiting 6 seconds to allow Key1 to expire...");
            await Task.Delay(TimeSpan.FromSeconds(6));

            if (!cache.TryGet("Key1", out var expiredValue1))
            {
                Console.WriteLine("Key1 has expired.");
            }

            // Clean up expired entries in the background
            var cts = new CancellationTokenSource();
            _ = cache.CleanExpiredEntriesAsync(TimeSpan.FromSeconds(2), cts.Token);

            cache.Set("Key3", "Value3");
            if (cache.TryGet("Key3", out var value3))
            {
                Console.WriteLine($"Key3 found: {value3}");
            }

            Console.WriteLine("Stopping background cleanup.");
            cts.Cancel();
        }
    }
}
