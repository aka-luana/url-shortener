namespace url_shortener.Options;

public class UrlShortenerOptions
{
    public const string SectionName = "UrlShortener";

    public required string BaseUrl { get; init; }
    public required string HashidsSalt { get; init; }
    public int MinCodeLength { get; init; } = 5;
    public int MaxCodeLength { get; init; } = 7;
    public required string RedisCounterKey { get; init; }
    public required string DynamoTableName { get; init; }
}
