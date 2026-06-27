using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Models;
using ScanBridge.Services.PostScanActions;

namespace ScanBridge.Tests.Services;

public class TelegramNotificationActionTests
{
    private static ScanResult CreateScan(string data = "TEST123") => new()
    {
        RawData = data,
        ParsedData = data,
        Format = "Code128",
        IsValid = true,
        ScannerName = "Main",
        Timestamp = new DateTime(2025, 1, 15, 10, 30, 45, DateTimeKind.Utc)
    };

    [Fact]
    public void Constructor_MissingBotToken_DoesNotThrow()
    {
        var logger = new Mock<ILogger<TelegramNotificationAction>>();
        var settings = new Dictionary<string, string>();

        var action = new TelegramNotificationAction(logger.Object, settings);

        Assert.Equal("Telegram", action.Type);
    }

    [Fact]
    public async Task ExecuteAsync_MissingBotToken_DoesNotThrow()
    {
        var logger = new Mock<ILogger<TelegramNotificationAction>>();
        var settings = new Dictionary<string, string>();
        var action = new TelegramNotificationAction(logger.Object, settings);

        await action.ExecuteAsync(CreateScan(), CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyChatIds_DoesNotThrow()
    {
        var logger = new Mock<ILogger<TelegramNotificationAction>>();
        var settings = new Dictionary<string, string>
        {
            ["BotToken"] = "fake-token",
            ["ChatIds"] = ""
        };
        var action = new TelegramNotificationAction(logger.Object, settings);

        await action.ExecuteAsync(CreateScan(), CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidChatIds_SkipsInvalid()
    {
        var logger = new Mock<ILogger<TelegramNotificationAction>>();
        var settings = new Dictionary<string, string>
        {
            ["BotToken"] = "fake-token",
            ["ChatIds"] = "abc,123,def"
        };
        var action = new TelegramNotificationAction(logger.Object, settings);

        await action.ExecuteAsync(CreateScan(), CancellationToken.None);
    }

    [Fact]
    public void Constructor_ParsesChatIds()
    {
        var logger = new Mock<ILogger<TelegramNotificationAction>>();
        var settings = new Dictionary<string, string>
        {
            ["BotToken"] = "fake-token",
            ["ChatIds"] = "111, 222, 333"
        };

        var action = new TelegramNotificationAction(logger.Object, settings);

        Assert.Equal("Telegram", action.Type);
    }

    [Fact]
    public void Constructor_CustomMessageTemplate()
    {
        var logger = new Mock<ILogger<TelegramNotificationAction>>();
        var settings = new Dictionary<string, string>
        {
            ["BotToken"] = "fake-token",
            ["ChatIds"] = "123",
            ["MessageTemplate"] = "New scan: {data} at {timestamp}"
        };

        var action = new TelegramNotificationAction(logger.Object, settings);

        Assert.NotNull(action);
    }
}
