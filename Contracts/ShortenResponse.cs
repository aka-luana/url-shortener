namespace url_shortener.Contracts;

public record ShortenResponse(string ShortUrl, string Code, string LongUrl);
