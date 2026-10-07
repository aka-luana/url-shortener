using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using url_shortener.Models;
using url_shortener.Options;

namespace url_shortener.Services;

public class DynamoUrlRepository(IAmazonDynamoDB dynamoDb, IOptions<UrlShortenerOptions> options) : IUrlRepository
{
    // Valor fixo de "Shard" em todo item: é a hash key do índice secundário
    // (GSI) usado só para achar o maior Id gravado (ver RedisCounterSeeder).
    // O DynamoDB não tem "ORDER BY" sem uma partição/sort key definida, então
    // esse valor constante funciona como uma única "fila" ordenável por Id.
    private const string ShardValue = "all";

    private string TableName => options.Value.DynamoTableName;

    public Task InsertAsync(ShortUrl shortUrl) =>
        dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = TableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new() { N = shortUrl.Id.ToString() },
                ["Shard"] = new() { S = ShardValue },
                ["Code"] = new() { S = shortUrl.Code },
                ["LongUrl"] = new() { S = shortUrl.LongUrl },
                ["CreatedAtUtc"] = new() { S = shortUrl.CreatedAtUtc.ToString("O") },
            },
        });

    public async Task<ShortUrl?> FindByIdAsync(long id)
    {
        var response = await dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new() { N = id.ToString() },
            },
        });

        if (!response.IsItemSet)
        {
            return null;
        }

        var item = response.Item;
        return new ShortUrl
        {
            Id = long.Parse(item["Id"].N),
            Code = item["Code"].S,
            LongUrl = item["LongUrl"].S,
            CreatedAtUtc = DateTime.Parse(item["CreatedAtUtc"].S).ToUniversalTime(),
        };
    }
}
