using MongoDB.Driver;
using url_shortener.Models;

namespace url_shortener.Services;

public class MongoUrlRepository : IUrlRepository
{
    private readonly IMongoCollection<ShortUrl> _collection;

    public MongoUrlRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ShortUrl>("urls");
    }

    public Task InsertAsync(ShortUrl shortUrl) =>
        _collection.InsertOneAsync(shortUrl);

    public async Task<ShortUrl?> FindByIdAsync(long id) =>
        await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
}
