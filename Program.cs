using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;
using url_shortener.Contracts;
using url_shortener.Models;
using url_shortener.Options;
using url_shortener.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services
    .AddOptions<UrlShortenerOptions>()
    .Bind(builder.Configuration.GetSection(UrlShortenerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration.GetConnectionString("Mongo")));

builder.Services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase("url_shortener"));

builder.Services.AddSingleton<IIdGenerator, RedisIdGenerator>();
builder.Services.AddSingleton<IShortCodeCodec, HashidsShortCodeCodec>();
builder.Services.AddSingleton<IUrlRepository, MongoUrlRepository>();
builder.Services.AddHostedService<RedisCounterSeeder>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "url-shortener v1"));
    app.UseHttpsRedirection();
}

app.MapPost("/shorten", async (
    ShortenRequest request,
    IIdGenerator idGenerator,
    IShortCodeCodec codec,
    IUrlRepository repository,
    IOptions<UrlShortenerOptions> options) =>
{
    if (!Uri.TryCreate(request.LongUrl, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.LongUrl)] = ["LongUrl must be an absolute http(s) URL."]
        });
    }

    var id = await idGenerator.NextIdAsync();
    var code = codec.Encode(id);

    var shortUrl = new ShortUrl
    {
        Id = id,
        Code = code,
        LongUrl = request.LongUrl
    };

    await repository.InsertAsync(shortUrl);

    var response = new ShortenResponse(
        ShortUrl: $"{options.Value.BaseUrl.TrimEnd('/')}/{code}",
        Code: code,
        LongUrl: request.LongUrl);

    return Results.Created(response.ShortUrl, response);
})
.WithName("ShortenUrl");

app.MapGet("/{code}", async (
    string code,
    IShortCodeCodec codec,
    IUrlRepository repository) =>
{
    if (!codec.TryDecode(code, out var id))
    {
        return Results.NotFound();
    }

    var shortUrl = await repository.FindByIdAsync(id);
    if (shortUrl is null)
    {
        return Results.NotFound();
    }

    return Results.Redirect(shortUrl.LongUrl, permanent: true);
})
.WithName("ResolveUrl")
.Produces(StatusCodes.Status301MovedPermanently)
.Produces(StatusCodes.Status404NotFound);

app.Run();
