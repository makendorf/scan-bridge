namespace ScanBridge.Services.PostScanActions.Export;

/// <summary>
/// Стратегия отправки данных на HTTP-сервер через GET.
/// Данные (query string, собранный из тегов) передаются как параметры URL.
/// Пример: http://host/api?connectName=GLP&message=ДАННЫЕ&timeout=2000
/// </summary>
public class HttpGetExportStrategy : IExportStrategy
{
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly string _url;
    private readonly Dictionary<string, string> _headers;
    private readonly ILogger _logger;

    public HttpGetExportStrategy(string url,
        Dictionary<string, string> headers, ILogger logger)
    {
        _url = url;
        _headers = headers;
        _logger = logger;
    }

    public async Task UploadAsync(byte[] data, string filename, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_url))
        {
            _logger.LogWarning("Export HTTP GET: URL не указан");
            return;
        }

        // data содержит query string, собранный из тегов (key=value&key2=value2)
        var queryString = System.Text.Encoding.UTF8.GetString(data);

        // Добавляем query string к URL
        var separator = _url.Contains('?') ? '&' : '?';
        var fullUrl = $"{_url}{separator}{queryString}";

        _logger.LogInformation("Export HTTP GET → {Url}", fullUrl);
        foreach (var header in _headers)
            _logger.LogInformation("Export HTTP GET Header: {Key}: {Value}", header.Key, header.Value);

        using var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
        foreach (var header in _headers)
        {
            if (!request.Headers.Contains(header.Key))
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        var response = await SharedHttpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Export HTTP GET: {Status}", (int)response.StatusCode);
    }
}
