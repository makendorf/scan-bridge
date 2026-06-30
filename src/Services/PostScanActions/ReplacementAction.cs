using System.Text.Json;
using ScanBridge.Models;
using ScanBridge.Utils;

namespace ScanBridge.Services.PostScanActions;

/// <summary>
/// Действие замены текста в результате сканирования.
/// Применяет правила поиска и замены к данным штрихкода перед обработкой.
/// </summary>
public class ReplacementAction : IPostScanAction
{
    /// <summary>
    /// Тип действия.
    /// </summary>
    public string Type => "Replacement";

    private readonly ILogger<ReplacementAction> _logger;
    private readonly List<ReplacementRule> _replacements = new();

    /// <summary>
    /// Создаёт экземпляр действия замены текста.
    /// </summary>
    /// <param name="logger">Логгер.</param>
    /// <param name="settings">Параметры: JSON-массив правил замены в ключе "Replacements".</param>
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

    /// <summary>
    /// Применяет правила замены к результату сканирования.
    /// Модифицирует поле ParsedData если хотя бы одно правило сработало.
    /// </summary>
    /// <param name="scan">Результат сканирования.</param>
    /// <param name="ct">Токен отмены.</param>
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
            _logger.LogInformation("Замена: {Original} → {Result}", ControlCharDisplay.ForDisplay(original), ControlCharDisplay.ForDisplay(result));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Применяет все правила замены к тексту последовательно.
    /// </summary>
    /// <param name="text">Исходный текст.</param>
    /// <returns>Текст с применёнными заменами.</returns>
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

    /// <summary>
    /// Парсит строковый режим замены в перечисление.
    /// </summary>
    /// <param name="mode">Строковое представление режима: "start", "end" или "all" (по умолчанию).</param>
    /// <returns>Значение перечисления ReplacementMode.</returns>
    private static ReplacementMode ParseMode(string? mode)
    {
        return mode?.ToLowerInvariant() switch
        {
            "start" => ReplacementMode.Start,
            "end" => ReplacementMode.End,
            _ => ReplacementMode.All
        };
    }

    /// <summary>
    /// Внутреннее правило замены текста.
    /// </summary>
    private class ReplacementRule
    {
        /// <summary>
        /// Искомый текст для поиска.
        /// </summary>
        public string Find { get; set; } = string.Empty;

        /// <summary>
        /// Текст замены.
        /// </summary>
        public string Replace { get; set; } = string.Empty;

        /// <summary>
        /// Режим замены: замена всех вхождений, только начала или только конца строки.
        /// </summary>
        public ReplacementMode Mode { get; set; }
    }

    /// <summary>
    /// DTO-представление правила замены для десериализации из JSON.
    /// </summary>
    private class ReplacementRuleDto
    {
        /// <summary>
        /// Искомый текст.
        /// </summary>
        public string? Find { get; set; }

        /// <summary>
        /// Текст замены.
        /// </summary>
        public string? Replace { get; set; }

        /// <summary>
        /// Режим замены: "all", "start", "end".
        /// </summary>
        public string? Mode { get; set; }
    }

    /// <summary>
    /// Режим замены текста.
    /// </summary>
    private enum ReplacementMode
    {
        /// <summary>
        /// Замена всех вхождений подстроки.
        /// </summary>
        All,

        /// <summary>
        /// Замена только в начале строки.
        /// </summary>
        Start,

        /// <summary>
        /// Замена только в конце строки.
        /// </summary>
        End
    }
}
