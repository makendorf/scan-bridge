using System.Text.RegularExpressions;
using ScanBridge.Models;

namespace ScanBridge.Services.VisualScripting;

/// <summary>
/// Простой evaluator для Condition узлов.
/// </summary>
public static class ConditionEvaluator
{
    /// <summary>
    /// Оценивает условие на основе настроек узла и результата сканирования.
    /// </summary>
    /// <param name="settings">Настройки условия (operator, field, value).</param>
    /// <param name="scan">Результат сканирования.</param>
    /// <returns>true или false.</returns>
    public static bool Evaluate(Dictionary<string, string> settings, ScanResult scan)
    {
        if (!settings.TryGetValue("operator", out var op))
            return false;

        var field = settings.GetValueOrDefault("field", "data");
        var value = settings.GetValueOrDefault("value", "");

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
