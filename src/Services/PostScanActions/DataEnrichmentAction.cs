using System.Net.Http.Json;
using System.Text.Json;
using ScanBridge.Models;

namespace ScanBridge.Services.PostScanActions;

/// <summary>
/// Действие обогащения данных сканирования.
/// Отправляет данные на внешний API и сохраняет результат в Metadata.
/// </summary>
public class DataEnrichmentAction : IPostScanAction, IDisposable
{
    public string Type => "DataEnrichment";

    private readonly ILogger<DataEnrichmentAction> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _url;
    private readonly string _method;
    private readonly string _headers;
    private readonly string _responseField;
    private readonly int _timeoutSeconds;
    private readonly string _queryParam;

    public DataEnrichmentAction(ILogger<DataEnrichmentAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;
        _httpClient = new HttpClient();

        _url = settings.TryGetValue("Url", out var url) ? url : "";
        _method = (settings.TryGetValue("Method", out var method) ? method : "GET").ToUpperInvariant();
        _headers = settings.TryGetValue("Headers", out var headers) ? headers : "{}";
        _responseField = settings.TryGetValue("ResponseField", out var field) ? field : "";
        _timeoutSeconds = settings.TryGetValue("TimeoutSeconds", out var timeoutStr)
            && int.TryParse(timeoutStr, out var timeout) ? timeout : 10;
        _queryParam = settings.TryGetValue("QueryParam", out var qp) && !string.IsNullOrWhiteSpace(qp)
            ? qp : "data";

        _httpClient.Timeout = TimeSpan.FromSeconds(_timeoutSeconds);

        if (string.IsNullOrWhiteSpace(_url))
            _logger.LogWarning("DataEnrichment: URL не задан, обогащение не будет выполняться");
    }

    public async Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_url))
            return;

        try
        {
            string responseText;

            if (_method == "POST")
            {
                var body = new Dictionary<string, string> { [_queryParam] = scan.ParsedData };
                using var request = new HttpRequestMessage(HttpMethod.Post, _url);
                ApplyHeaders(request);
                request.Content = JsonContent.Create(body);
                using var response = await _httpClient.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
                responseText = await response.Content.ReadAsStringAsync(ct);
            }
            else
            {
                var escapedData = Uri.EscapeDataString(scan.ParsedData);
                var separator = _url.Contains('?') ? '&' : '?';
                var fullUrl = $"{_url}{separator}{_queryParam}={escapedData}";
                using var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
                ApplyHeaders(request);
                using var response = await _httpClient.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
                responseText = await response.Content.ReadAsStringAsync(ct);
            }

            if (!string.IsNullOrWhiteSpace(_responseField) && !string.IsNullOrWhiteSpace(responseText))
            {
                try
                {
                    using var doc = JsonDocument.Parse(responseText);
                    if (doc.RootElement.TryGetProperty(_responseField, out var field))
                        responseText = field.ToString();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "DataEnrichment: ошибка парсинга JSON-ответа");
                }
            }

            scan.Metadata["enriched"] = responseText;
            _logger.LogInformation("DataEnrichment: обогащение выполнено, ключ 'enriched' добавлен в Metadata");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DataEnrichment: ошибка обогащения данных");
        }
    }

    private void ApplyHeaders(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(_headers) || _headers == "{}")
            return;

        try
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(_headers);
            if (headers == null) return;
            foreach (var (key, value) in headers)
            {
                if (!string.IsNullOrWhiteSpace(key))
                    request.Headers.TryAddWithoutValidation(key, value);
            }
        }
        catch (JsonException)
        {
            _logger.LogWarning("DataEnrichment: ошибка парсинга заголовков JSON");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
