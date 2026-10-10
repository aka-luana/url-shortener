using Amazon.DynamoDBv2;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using url_shortener.Contracts;
using url_shortener.Models;
using url_shortener.Observability;
using url_shortener.Options;
using url_shortener.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Observabilidade: métricas, logs e traces saem via OTLP, lendo endpoint e
// credenciais das variáveis de ambiente padrão (OTEL_EXPORTER_OTLP_*), sem
// nada de endpoint/token escrito em código — isso vem do sst.config.ts.
builder.Services.AddMetrics();
builder.Services.AddSingleton<UrlShortenerMetrics>();

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter(UrlShortenerMetrics.MeterName)
        .AddOtlpExporter())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
    options.AddOtlpExporter();
});

builder.Services
    .AddOptions<UrlShortenerOptions>()
    .Bind(builder.Configuration.GetSection(UrlShortenerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient());

builder.Services.AddSingleton<IIdGenerator, RedisIdGenerator>();
builder.Services.AddSingleton<IShortCodeCodec, HashidsShortCodeCodec>();
builder.Services.AddSingleton<IUrlRepository, DynamoUrlRepository>();
builder.Services.AddHostedService<RedisCounterSeeder>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "url-shortener v1"));
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();
// Roteamento explícito DEPOIS dos estáticos: por padrão ele roda primeiro, e a
// rota catch-all "/{code}" casaria com /style.css antes do UseStaticFiles.
app.UseRouting();

app.MapPost("/shorten", async (
    ShortenRequest request,
    IIdGenerator idGenerator,
    IShortCodeCodec codec,
    IUrlRepository repository,
    IOptions<UrlShortenerOptions> options,
    UrlShortenerMetrics metrics) =>
{
    if (!Uri.TryCreate(request.LongUrl, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
    {
        metrics.RecordShortened("validation_error");
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
    metrics.RecordShortened("success");

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
    IUrlRepository repository,
    UrlShortenerMetrics metrics) =>
{
    if (!codec.TryDecode(code, out var id))
    {
        metrics.RecordResolved("not_found");
        return Results.NotFound();
    }

    var shortUrl = await repository.FindByIdAsync(id);
    if (shortUrl is null)
    {
        metrics.RecordResolved("not_found");
        return Results.NotFound();
    }

    metrics.RecordResolved("redirected");
    return Results.Redirect(shortUrl.LongUrl, permanent: true);
})
.WithName("ResolveUrl")
.Produces(StatusCodes.Status301MovedPermanently)
.Produces(StatusCodes.Status404NotFound);

app.Run();
