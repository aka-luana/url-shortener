using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;
using url_shortener.Models;
using url_shortener.Options;

namespace url_shortener.Services;

public class RedisCounterSeeder(
    IConnectionMultiplexer redis,
    IMongoDatabase database,
    IOptions<UrlShortenerOptions> options,
    ILogger<RedisCounterSeeder> logger) : IHostedService
{
    private const string RaiseCounterScript = """
        local current = tonumber(redis.call('GET', KEYS[1])) or 0
        if current < tonumber(ARGV[1]) then
            redis.call('SET', KEYS[1], ARGV[1])
        end
        return redis.call('GET', KEYS[1])
        """;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var highestId = await database.GetCollection<ShortUrl>("urls")
            .Find(FilterDefinition<ShortUrl>.Empty)
            .SortByDescending(x => x.Id)
            .Limit(1)
            .Project(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var counterKey = new RedisKey(options.Value.RedisCounterKey);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var result = await redis.GetDatabase()
                    .ScriptEvaluateAsync(RaiseCounterScript, [counterKey], [highestId]);
                logger.LogInformation("Redis counter is at {Counter} (highest stored id: {HighestId})", result, highestId);
                return;
            }
            catch (RedisException) when (attempt < 10)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
