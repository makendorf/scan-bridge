using System.Text.RegularExpressions;
using ScanBridge.Models;

namespace ScanBridge.Services.PostScanActions;

/// <summary>
/// Действие валидации данных сканирования.
/// Поддерживает regex, словарь, числовой диапазон и формат (встроенная валидация парсера).
/// При ошибке может прервать цепочку (skip) или только предупредить (warn).
/// </summary>
public class ValidationAction : IPostScanAction
{
    public string Type => "Validation";

    private readonly ILogger<ValidationAction> _logger;
    private readonly string _validationType;
    private readonly Regex? _regex;
    private readonly int _minLength;
    private readonly int _maxLength;
    private readonly HashSet<string> _dictionary = new(StringComparer.OrdinalIgnoreCase);
    private readonly double _minValue;
    private readonly double _maxValue;
    private readonly string _onFailure;

    private readonly bool _requireNonEmpty;
    private readonly bool _requireBarcode;
    private readonly HashSet<string> _allowedFormats = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _allowedContentTypes = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;
    private Dictionary<string, string> _settings;

    public ValidationAction(ILogger<ValidationAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;
        _settings = settings;

        _validationType = (settings.TryGetValue("ValidationType", out var vt) ? vt : "regex").ToLowerInvariant();
        _onFailure = (settings.TryGetValue("OnFailure", out var of) ? of : "skip").ToLowerInvariant();

        if (_validationType == "format")
        {
            _requireNonEmpty = settings.TryGetValue("RequireNonEmpty", out var ne)
                && bool.TryParse(ne, out var neb) && neb;
            _requireBarcode = settings.TryGetValue("RequireBarcode", out var rb)
                && bool.TryParse(rb, out var rbb) && rbb;

            if (settings.TryGetValue("AllowedFormats", out var af) && !string.IsNullOrWhiteSpace(af))
            {
                foreach (var f in af.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    _allowedFormats.Add(f);
            }

            if (settings.TryGetValue("AllowedContentTypes", out var act) && !string.IsNullOrWhiteSpace(act))
            {
                foreach (var ct in act.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    _allowedContentTypes.Add(ct);
            }
        }
        else if (_validationType == "regex")
        {
            var pattern = settings.TryGetValue("Pattern", out var p) ? p : "";
            if (!string.IsNullOrWhiteSpace(pattern))
            {
                try
                {
                    _regex = new Regex(pattern, RegexOptions.Compiled);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Validation: ошибка компиляции regex '{Pattern}'", pattern);
                }
            }

            _minLength = settings.TryGetValue("MinLength", out var minLenStr)
                && int.TryParse(minLenStr, out var minLen) ? minLen : 0;
            _maxLength = settings.TryGetValue("MaxLength", out var maxLenStr)
                && int.TryParse(maxLenStr, out var maxLen) ? maxLen : 9999;
        }
        else if (_validationType == "range")
        {
            _minValue = settings.TryGetValue("MinValue", out var minStr)
                && double.TryParse(minStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var min) ? min : double.MinValue;
            _maxValue = settings.TryGetValue("MaxValue", out var maxStr)
                && double.TryParse(maxStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var max) ? max : double.MaxValue;
        }
    }

    public async Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        if (_validationType == "dictionary" && !_loaded)
        {
            await LoadDictionaryAsync(_settings).ConfigureAwait(false);
            _loaded = true;
        }

        var valid = _validationType switch
        {
            "format" => ValidateFormat(scan),
            "regex" => ValidateRegex(scan.ParsedData),
            "dictionary" => ValidateDictionary(scan.ParsedData),
            "range" => ValidateRange(scan.ParsedData),
            _ => true
        };

        if (!valid)
        {
            if (_onFailure == "skip")
            {
                scan.IsValid = false;
                _logger.LogWarning("Validation: данные не прошли валидацию ({Type}), IsValid=false", _validationType);
            }
            else
            {
                _logger.LogWarning("Validation: данные не прошли валидацию ({Type}), предупреждение", _validationType);
            }
        }

        await Task.CompletedTask;
    }

    private bool ValidateFormat(ScanResult scan)
    {
        if (_requireNonEmpty && string.IsNullOrWhiteSpace(scan.ParsedData))
            return false;

        if (_requireBarcode && scan.Format == "Empty")
            return false;

        if (_allowedFormats.Count > 0 && !_allowedFormats.Contains(scan.Format))
            return false;

        if (_allowedContentTypes.Count > 0 && !_allowedContentTypes.Contains(scan.ContentType))
            return false;

        return true;
    }

    private bool ValidateRegex(string data)
    {
        if (_regex != null && !_regex.IsMatch(data))
            return false;

        if (data.Length < _minLength || data.Length > _maxLength)
            return false;

        return true;
    }

    private bool ValidateDictionary(string data)
    {
        return _dictionary.Contains(data);
    }

    private bool ValidateRange(string data)
    {
        if (!double.TryParse(data, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
            return false;

        return value >= _minValue && value <= _maxValue;
    }

    private async Task LoadDictionaryAsync(Dictionary<string, string> settings)
    {
        var lines = new List<string>();

        if (settings.TryGetValue("DictionaryPath", out var filePath) && !string.IsNullOrWhiteSpace(filePath))
        {
            try
            {
                if (File.Exists(filePath))
                    lines.AddRange(await File.ReadAllLinesAsync(filePath).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Validation: ошибка чтения файла словаря {Path}", filePath);
            }
        }

        if (settings.TryGetValue("DictionaryUrl", out var url) && !string.IsNullOrWhiteSpace(url))
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var content = await client.GetStringAsync(url).ConfigureAwait(false);
                lines.AddRange(content.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Validation: ошибка загрузки словаря с {Url}", url);
            }
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                _dictionary.Add(trimmed);
        }

        if (_dictionary.Count == 0)
            _logger.LogWarning("Validation: словарь пуст, все значения будут считаться невалидными");
    }
}
