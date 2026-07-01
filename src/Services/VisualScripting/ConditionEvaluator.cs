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
    /// Оценивает несколько условий для While с указанным логическим оператором.
    /// </summary>
    /// <param name="conditionsJson">JSON-массив условий.</param>
    /// <param name="scan">Результат сканирования.</param>
    /// <param name="logic">Логический оператор: "and" (по умолчанию) или "or".</param>
    public static bool EvaluateMultiple(string conditionsJson, ScanResult scan, string logic = "and")
    {
        if (string.IsNullOrEmpty(conditionsJson)) return false;

        try
        {
            var conditions = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(conditionsJson);
            if (conditions == null || conditions.Count == 0) return false;

            if (string.Equals(logic, "or", StringComparison.OrdinalIgnoreCase))
            {
                // OR: хотя бы одно условие истинно
                foreach (var condition in conditions)
                {
                    if (Evaluate(condition, scan))
                        return true;
                }
                return false;
            }
            else
            {
                // AND: все условия должны быть истинны
                foreach (var condition in conditions)
                {
                    if (!Evaluate(condition, scan))
                        return false;
                }
                return true;
            }
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
