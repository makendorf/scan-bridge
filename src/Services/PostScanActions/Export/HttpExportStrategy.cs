namespace ScanBridge.Services.PostScanActions.Export;

/// <summary>
/// Стратегия отправки данных на HTTP-сервер через POST.
/// </summary>
public class HttpExportStrategy : IExportStrategy
{
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly string _url;
    private readonly string _contentType;
    private readonly Dictionary<string, string> _headers;
    private readonly ILogger _logger;

    public HttpExportStrategy(string url, string contentType,
        Dictionary<string, string> headers, ILogger logger)
    {
        _url = url;
        _contentType = contentType;
        _headers = headers;
        _logger = logger;
    }

    public async Task UploadAsync(byte[] data, string filename, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_url))
        {
            _logger.LogWarning("Export HTTP: URL не указан");
            return;
        }

        var content = System.Text.Encoding.UTF8.GetString(data);

        using var request = new HttpRequestMessage(HttpMethod.Post, _url);
        foreach (var header in _headers)
        {
            if (!request.Headers.Contains(header.Key))
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        request.Content = new StringContent(content, System.Text.Encoding.UTF8, _contentType);
        var response = await SharedHttpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Export HTTP: {Url} → {Status}", _url, (int)response.StatusCode);
    }
}
