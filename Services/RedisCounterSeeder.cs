using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using url_shortener.Options;

namespace url_shortener.Services;

public class RedisCounterSeeder(
    IConnectionMultiplexer redis,
    IAmazonDynamoDB dynamoDb,
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
        var highestId = await GetHighestStoredIdAsync(cancellationToken);
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

    private async Task<long> GetHighestStoredIdAsync(CancellationToken cancellationToken)
    {
        var response = await dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = options.Value.DynamoTableName,
            IndexName = "HighestIdIndex",
            // "Shard" é palavra reservada do DynamoDB (ver lista de reserved
            // words), por isso precisa do alias "#shard" em vez do nome direto.
            KeyConditionExpression = "#shard = :shard",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#shard"] = "Shard",
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":shard"] = new() { S = "all" },
            },
            ScanIndexForward = false,
            Limit = 1,
        }, cancellationToken);

        return response.Items.Count == 0 ? 0 : long.Parse(response.Items[0]["Id"].N);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
