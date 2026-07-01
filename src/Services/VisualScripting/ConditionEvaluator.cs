using System.Text.Json;
using System.Text.RegularExpressions;
using ScanBridge.Models;

namespace ScanBridge.Services.VisualScripting;

/// <summary>
/// Простой evaluator для Condition и While узлов.
/// </summary>
public static class ConditionEvaluator
{
    /// <summary>
    /// Оценивает单一 условие.
    /// </summary>
    public static bool Evaluate(Dictionary<string, string> settings, ScanResult scan)
    {
        if (!settings.TryGetValue("operator", out var op))
            return false;

        var field = settings.GetValueOrDefault("field", "data");
        var value = settings.GetValueOrDefault("value", "");

        return EvaluateSingle(field, op, value, scan);
    }

    /// <summary>
    /// Оценивает несколько условий для While (все должны быть истинны — AND-логика).
    /// </summary>
    public static bool EvaluateMultiple(string conditionsJson, ScanResult scan)
    {
        if (string.IsNullOrEmpty(conditionsJson)) return false;

        try
        {
            var conditions = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(conditionsJson);
            if (conditions == null || conditions.Count == 0) return false;

            foreach (var condition in conditions)
            {
                if (!Evaluate(condition, scan))
                    return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool EvaluateSingle(string field, string op, string value, ScanResult scan)
    {
        var data = field switch
        {
            "data" => scan.ParsedData ?? "",
            "raw" => scan.RawData ?? "",
            "format" => scan.Format ?? "",
            "scanner" => scan.ScannerName ?? "",
            "isValid" => scan.IsValid.ToString().ToLower(),
            _ => ""
        };

        return op switch
        {
            "equals" => string.Equals(data, value, StringComparison.OrdinalIgnoreCase),
            "notEquals" => !string.Equals(data, value, StringComparison.OrdinalIgnoreCase),
            "contains" => data.Contains(value, StringComparison.OrdinalIgnoreCase),
            "notContains" => !data.Contains(value, StringComparison.OrdinalIgnoreCase),
            "regex" => Regex.IsMatch(data, value),
            "greaterThan" => CompareValues(data, value) > 0,
            "lessThan" => CompareValues(data, value) < 0,
            "isValid" => scan.IsValid,
            _ => false
        };
    }

    private static int CompareValues(string a, string b)
    {
        if (double.TryParse(a, out var aNum) && double.TryParse(b, out var bNum))
            return aNum.CompareTo(bNum);

        return string.Compare(a, b, StringComparison.Ordinal);
    }
}
