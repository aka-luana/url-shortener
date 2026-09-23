using MongoDB.Bson.Serialization.Attributes;

namespace url_shortener.Models;

public class ShortUrl
{
    [BsonId]
    public required long Id { get; init; }

    public required string Code { get; init; }

    public required string LongUrl { get; init; }

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
