namespace url_shortener.Services;

public interface IIdGenerator
{
    Task<long> NextIdAsync();
}
