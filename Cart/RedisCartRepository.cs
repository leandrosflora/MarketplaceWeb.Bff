using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace MarketplaceWeb.Bff.Cart;

public sealed class RedisCartRepository : ICartRepository
{
    private const string OwnerIndexKey = "cart:owner-index";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisCartRepository(IDistributedCache cache, IConnectionMultiplexer connectionMultiplexer)
    {
        _cache = cache;
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<StoredCart?> GetAsync(string cartOwnerId, CancellationToken cancellationToken)
    {
        var json = await _cache.GetStringAsync(BuildKey(cartOwnerId), cancellationToken);

        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<StoredCart>(json, JsonOptions);
    }

    public async Task SaveAsync(string cartOwnerId, StoredCart cart, CancellationToken cancellationToken)
    {
        await _cache.SetStringAsync(
            BuildKey(cartOwnerId),
            JsonSerializer.Serialize(cart, JsonOptions),
            new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromDays(30)
            },
            cancellationToken);

        var db = _connectionMultiplexer.GetDatabase();
        await db.SetAddAsync(OwnerIndexKey, cartOwnerId);
    }

    public async Task DeleteAsync(string cartOwnerId, CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync(BuildKey(cartOwnerId), cancellationToken);

        var db = _connectionMultiplexer.GetDatabase();
        await db.SetRemoveAsync(OwnerIndexKey, cartOwnerId);
    }

    public async Task<IReadOnlyCollection<string>> ListOwnerIdsAsync(CancellationToken cancellationToken)
    {
        var db = _connectionMultiplexer.GetDatabase();
        var members = await db.SetMembersAsync(OwnerIndexKey);

        return members.Select(member => member.ToString()).ToArray();
    }

    private static string BuildKey(string cartOwnerId) => $"cart:{cartOwnerId}";
}
