using HashidsNet;
using Microsoft.Extensions.Options;
using url_shortener.Options;

namespace url_shortener.Services;

public class HashidsShortCodeCodec : IShortCodeCodec
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly Hashids _hashids;
    private readonly int _minCodeLength;
    private readonly int _maxCodeLength;

    public HashidsShortCodeCodec(IOptions<UrlShortenerOptions> options)
    {
        var settings = options.Value;
        _hashids = new Hashids(settings.HashidsSalt, settings.MinCodeLength, Alphabet);
        _minCodeLength = settings.MinCodeLength;
        _maxCodeLength = settings.MaxCodeLength;
    }

    public string Encode(long id)
    {
        var code = _hashids.EncodeLong(id);

        // Guards the security requirement of a hard 7-character ceiling.
        // With MinCodeLength=5 and the 62-char alphabet, this only trips if the
        // counter grows far beyond the volumetry this system was sized for.
        if (code.Length > _maxCodeLength)
        {
            throw new InvalidOperationException(
                $"Generated code '{code}' exceeds the maximum allowed length of {_maxCodeLength}. " +
                "The id space has outgrown the configured code length.");
        }

        return code;
    }

    public bool TryDecode(string code, out long id)
    {
        id = 0;

        if (string.IsNullOrEmpty(code) || code.Length < _minCodeLength || code.Length > _maxCodeLength)
        {
            return false;
        }

        var decoded = _hashids.DecodeLong(code);
        if (decoded.Length != 1)
        {
            return false;
        }

        id = decoded[0];
        return true;
    }
}
