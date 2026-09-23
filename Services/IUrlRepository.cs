using url_shortener.Models;

namespace url_shortener.Services;

public interface IUrlRepository
{
    Task InsertAsync(ShortUrl shortUrl);

    Task<ShortUrl?> FindByIdAsync(long id);
}
