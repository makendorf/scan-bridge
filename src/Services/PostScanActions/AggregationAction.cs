using System.Collections.Concurrent;
using System.Text;
using ScanBridge.Models;

namespace ScanBridge.Services.PostScanActions;

/// <summary>
/// Действие агрегации результатов сканирования.
/// Накапливает сканы и отправляет пакетами: по количеству или по времени.
/// </summary>
public class AggregationAction : IPostScanAction, IDisposable
{
    public string Type => "Aggregation";

    private readonly ILogger<AggregationAction> _logger;
    private readonly string _mode;
    private readonly int _countThreshold;
    private readonly int _maxBufferSize;
    private readonly string _batchFormat;
    private readonly ConcurrentQueue<ScanResult> _queue = new();
    private readonly Timer? _timer;
    private bool _disposed;

    public AggregationAction(ILogger<AggregationAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;

        _mode = (settings.TryGetValue("Mode", out var mode) ? mode : "count").ToLowerInvariant();
        _countThreshold = settings.TryGetValue("CountThreshold", out var ctStr)
            && int.TryParse(ctStr, out var ct) ? ct : 10;
        _maxBufferSize = settings.TryGetValue("MaxBufferSize", out var mbStr)
            && int.TryParse(mbStr, out var mb) ? mb : 1000;
        _batchFormat = (settings.TryGetValue("BatchFormat", out var fmt) ? fmt : "json").ToLowerInvariant();

        if (_mode == "time")
        {
            var intervalSeconds = settings.TryGetValue("IntervalSeconds", out var intStr)
                && int.TryParse(intStr, out var i) ? i : 60;
            _timer = new Timer(_ => Flush(), null, TimeSpan.FromSeconds(intervalSeconds),
                TimeSpan.FromSeconds(intervalSeconds));
        }
    }

    public Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        if (_queue.Count >= _maxBufferSize)
        {
            _logger.LogWarning("Aggregation: буфер переполнен ({Max}), принудительная очистка", _maxBufferSize);
            Flush();
        }

        _queue.Enqueue(scan);

        if (_mode == "count" && _queue.Count >= _countThreshold)
        {
            Flush();
        }

        return Task.CompletedTask;
    }

    private void Flush()
    {
        if (_queue.IsEmpty) return;

        var items = new List<ScanResult>();
        while (_queue.TryDequeue(out var item))
            items.Add(item);

        if (items.Count == 0) return;

        var batchData = _batchFormat == "csv" ? FormatCsv(items) : FormatJson(items);

        var lastScan = items[^1];
        lastScan.ParsedData = batchData;

        _logger.LogInformation("Aggregation: отправлен пакет из {Count} элементов", items.Count);
    }

    private static string FormatJson(List<ScanResult> items)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('{');
            sb.Append($"\"data\":\"{EscapeJson(items[i].ParsedData)}\",");
            sb.Append($"\"format\":\"{EscapeJson(items[i].Format)}\",");
            sb.Append($"\"scanner\":\"{EscapeJson(items[i].ScannerName)}\",");
            sb.Append($"\"timestamp\":\"{items[i].Timestamp:O}\"");
            sb.Append('}');
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string FormatCsv(List<ScanResult> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("data,format,scanner,timestamp");
        foreach (var item in items)
        {
            sb.AppendLine($"{EscapeCsv(item.ParsedData)},{EscapeCsv(item.Format)},{EscapeCsv(item.ScannerName)},{item.Timestamp:O}");
        }
        return sb.ToString();
    }

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
        Flush();
    }
}
