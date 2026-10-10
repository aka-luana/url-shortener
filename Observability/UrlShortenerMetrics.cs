using System.Diagnostics.Metrics;

namespace url_shortener.Observability;

// Métricas de negócio (além das automáticas de HTTP que o
// OpenTelemetry.Instrumentation.AspNetCore já expõe: contagem de requests,
// status code, latência). Ficam junto no mesmo "Meter" pra aparecer juntas
// no Grafano.
public class UrlShortenerMetrics
{
    public const string MeterName = "UrlShortener";

    private readonly Counter<long> _shortenedCounter;
    private readonly Counter<long> _resolvedCounter;

    public UrlShortenerMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _shortenedCounter = meter.CreateCounter<long>(
            "url_shortener.shorten.count",
            unit: "{url}",
            description: "Quantidade de URLs encurtadas, por resultado (success | validation_error).");

        _resolvedCounter = meter.CreateCounter<long>(
            "url_shortener.resolve.count",
            unit: "{request}",
            description: "Quantidade de resoluções de código curto, por resultado (redirected | not_found).");
    }

    public void RecordShortened(string result) =>
        _shortenedCounter.Add(1, new KeyValuePair<string, object?>("result", result));

    public void RecordResolved(string result) =>
        _resolvedCounter.Add(1, new KeyValuePair<string, object?>("result", result));
}
