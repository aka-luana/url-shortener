using Microsoft.Extensions.Options;
using StackExchange.Redis;
using url_shortener.Options;

namespace url_shortener.Services;

public class RedisIdGenerator(IConnectionMultiplexer redis, IOptions<UrlShortenerOptions> options) : IIdGenerator
{
    private readonly RedisKey _counterKey = new(options.Value.RedisCounterKey);

    public async Task<long> NextIdAsync()
    {
        var db = redis.GetDatabase();
        return await db.StringIncrementAsync(_counterKey);
    }
}
