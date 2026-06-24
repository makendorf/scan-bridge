using System.Text.Json;
using ScanBridge.Models;

namespace ScanBridge.Services.PostScanActions;

public class ReplacementAction : IPostScanAction
{
    public string Type => "Replacement";

    private readonly ILogger<ReplacementAction> _logger;
    private readonly List<ReplacementRule> _replacements = new();

    public ReplacementAction(ILogger<ReplacementAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;

        if (settings.TryGetValue("Replacements", out var json) && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var rules = JsonSerializer.Deserialize<List<ReplacementRuleDto>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (rules != null)
                {
                    foreach (var r in rules)
                    {
                        if (!string.IsNullOrEmpty(r.Find))
                        {
                            _replacements.Add(new ReplacementRule
                            {
                                Find = r.Find,
                                Replace = r.Replace ?? string.Empty,
                                Mode = ParseMode(r.Mode)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка разбора замен");
            }
        }
    }

    public Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        if (_replacements.Count == 0)
            return Task.CompletedTask;

        if (!scan.IsValid)
        {
            _logger.LogDebug("Пропуск замены: невалидный штрихкод {Raw}", scan.RawData);
            return Task.CompletedTask;
        }

        var original = scan.ParsedData;
        var result = ApplyReplacements(original);

        if (result != original)
        {
            scan.ParsedData = result;
            _logger.LogInformation("Замена: {Original} → {Result}", original, result);
        }

        return Task.CompletedTask;
    }

    private string ApplyReplacements(string text)
    {
        var result = text;
        foreach (var rule in _replacements)
        {
            result = rule.Mode switch
            {
                ReplacementMode.Start when result.StartsWith(rule.Find, StringComparison.Ordinal) =>
                    rule.Replace + result[rule.Find.Length..],
                ReplacementMode.End when result.EndsWith(rule.Find, StringComparison.Ordinal) =>
                    result[..^rule.Find.Length] + rule.Replace,
                ReplacementMode.All =>
                    result.Replace(rule.Find, rule.Replace),
                _ => result
            };
        }
        return result;
    }

    private static ReplacementMode ParseMode(string? mode)
    {
        return mode?.ToLowerInvariant() switch
        {
            "start" => ReplacementMode.Start,
            "end" => ReplacementMode.End,
            _ => ReplacementMode.All
        };
    }

    private class ReplacementRule
    {
        public string Find { get; set; } = string.Empty;
        public string Replace { get; set; } = string.Empty;
        public ReplacementMode Mode { get; set; }
    }

    private class ReplacementRuleDto
    {
        public string? Find { get; set; }
        public string? Replace { get; set; }
        public string? Mode { get; set; }
    }

    private enum ReplacementMode { All, Start, End }
}
