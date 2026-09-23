namespace url_shortener.Services;

public interface IShortCodeCodec
{
    string Encode(long id);

    bool TryDecode(string code, out long id);
}
