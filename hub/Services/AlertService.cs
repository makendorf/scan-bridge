using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScanBridgeHub.Data;
using ScanBridgeHub.Models;

namespace ScanBridgeHub.Services;

public class AlertService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlertService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<int, bool> _previousOnlineState = new();
    private readonly ConcurrentQueue<AlertNotification> _notificationQueue = new();

    public AlertService(
        IServiceScopeFactory scopeFactory,
        ILogger<AlertService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public void CheckForAlerts(int instanceId, string instanceName, bool isOnline, List<object>? scanners)
    {
        var wasOnline = _previousOnlineState.GetOrAdd(instanceId, true);
        _previousOnlineState[instanceId] = isOnline;

        if (wasOnline && !isOnline)
            QueueNotification(instanceId, instanceName, "ServerOffline", $"Сервер «{instanceName}» стал недоступен");
        else if (!wasOnline && isOnline)
            QueueNotification(instanceId, instanceName, "ServerOnline", $"Сервер «{instanceName}» вернулся в онлайн");
    }

    private void QueueNotification(int instanceId, string instanceName, string alertType, string message)
    {
        _notificationQueue.Enqueue(new AlertNotification(instanceId, instanceName, alertType, message, DateTime.UtcNow));
        _logger.LogInformation("Alert triggered: {Type} for {Instance}: {Message}", alertType, instanceName, message);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            while (_notificationQueue.TryDequeue(out var notification))
            {
                await SendNotifications(notification, stoppingToken);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task SendNotifications(AlertNotification notification, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var rules = await db.Alerts.Where(a => a.Enabled).ToListAsync(ct);

        foreach (var rule in rules)
        {
            try
            {
                var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(rule.SettingsJson) ?? new();
                await SendByRule(rule.Type, settings, notification, ct);
                rule.LastTriggered = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send alert via {Type}", rule.Type);
            }
        }
    }

    private async Task SendByRule(string type, Dictionary<string, string> settings, AlertNotification notification, CancellationToken ct)
    {
        switch (type)
        {
            case "Telegram":
                await SendTelegram(settings, notification, ct);
                break;
            case "Webhook":
                await SendWebhook(settings, notification, ct);
                break;
        }
    }

    private async Task SendTelegram(Dictionary<string, string> settings, AlertNotification notification, CancellationToken ct)
    {
        if (!settings.TryGetValue("BotToken", out var botToken) || !settings.TryGetValue("ChatId", out var chatId))
            return;

        var client = _httpClientFactory.CreateClient();
        var text = $"🔔 *{notification.AlertType}*\n{notification.Message}\n⏱ {notification.Timestamp:HH:mm:ss}";
        var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
        var payload = new { chat_id = chatId, text, parse_mode = "Markdown" };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        await client.PostAsync(url, content, ct);
    }

    private async Task SendWebhook(Dictionary<string, string> settings, AlertNotification notification, CancellationToken ct)
    {
        if (!settings.TryGetValue("Url", out var url) || string.IsNullOrWhiteSpace(url))
            return;

        var client = _httpClientFactory.CreateClient();
        var payload = JsonSerializer.Serialize(notification);
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        await client.PostAsync(url, content, ct);
    }
}
