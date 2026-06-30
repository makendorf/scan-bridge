using ScanBridge.Models;
using ScanBridge.Utils;
using Telegram.Bot;

namespace ScanBridge.Services.PostScanActions;

/// <summary>
/// Действие отправки уведомления в Telegram.
/// Отправляет сообщение в указанные чаты через Telegram Bot API.
/// </summary>
public class TelegramNotificationAction : IPostScanAction
{
    public string Type => "Telegram";

    private readonly ILogger<TelegramNotificationAction> _logger;
    private readonly TelegramBotClient? _client;
    private readonly List<long> _chatIds;
    private readonly string _messageTemplate;

    public TelegramNotificationAction(ILogger<TelegramNotificationAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;

        settings.TryGetValue("BotToken", out var botToken);
        settings.TryGetValue("ChatIds", out var chatIdsStr);
        _messageTemplate = settings.TryGetValue("MessageTemplate", out var tpl) && !string.IsNullOrWhiteSpace(tpl)
            ? tpl : "Scan: {data}";

        if (string.IsNullOrWhiteSpace(botToken))
        {
            _logger.LogWarning("Telegram: BotToken не задан, уведомления не будут отправляться");
            _client = null;
            _chatIds = new();
            return;
        }

        try
        {
            _client = new TelegramBotClient(botToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram: невалидный BotToken, уведомления не будут отправляться");
            _client = null;
            _chatIds = new();
            return;
        }

        _chatIds = new();
        if (!string.IsNullOrWhiteSpace(chatIdsStr))
        {
            foreach (var part in chatIdsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (long.TryParse(part, out var id))
                    _chatIds.Add(id);
            }
        }

        if (_chatIds.Count == 0)
            _logger.LogWarning("Telegram: нет валидных ChatIds, уведомления не будут отправляться");
    }

    public async Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        if (_client == null || _chatIds.Count == 0)
            return;

        var text = ScanTemplateHelper.Format(_messageTemplate, scan);

        foreach (var chatId in _chatIds)
        {
            try
            {
                await _client.SendMessage(chatId, text, cancellationToken: ct);
                _logger.LogInformation("Telegram: отправлено в {ChatId}", chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram: ошибка отправки в {ChatId}", chatId);
            }
        }
    }
}
