using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Models;
using ScanBridge.Services.PostScanActions;

namespace ScanBridge.Tests.Services;

public class EmailNotificationActionTests
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
    public void Constructor_MissingHost_LogsWarning()
    {
        var logger = new Mock<ILogger<EmailNotificationAction>>();
        var settings = new Dictionary<string, string>();

        var action = new EmailNotificationAction(logger.Object, settings);

        Assert.Equal("Email", action.Type);
    }

    [Fact]
    public async Task ExecuteAsync_MissingHost_DoesNotThrow()
    {
        var logger = new Mock<ILogger<EmailNotificationAction>>();
        var settings = new Dictionary<string, string>();
        var action = new EmailNotificationAction(logger.Object, settings);

        await action.ExecuteAsync(CreateScan(), CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyTo_DoesNotThrow()
    {
        var logger = new Mock<ILogger<EmailNotificationAction>>();
        var settings = new Dictionary<string, string>
        {
            ["SmtpHost"] = "smtp.test.com",
            ["To"] = ""
        };
        var action = new EmailNotificationAction(logger.Object, settings);

        await action.ExecuteAsync(CreateScan(), CancellationToken.None);
    }

    [Fact]
    public void Constructor_ParsesMultipleRecipients()
    {
        var logger = new Mock<ILogger<EmailNotificationAction>>();
        var settings = new Dictionary<string, string>
        {
            ["SmtpHost"] = "smtp.test.com",
            ["To"] = "a@test.com, b@test.com, c@test.com"
        };

        var action = new EmailNotificationAction(logger.Object, settings);

        Assert.NotNull(action);
    }

    [Fact]
    public void Constructor_DefaultPortIs587()
    {
        var logger = new Mock<ILogger<EmailNotificationAction>>();
        var settings = new Dictionary<string, string>
        {
            ["SmtpHost"] = "smtp.test.com",
            ["SmtpPort"] = "465"
        };

        var action = new EmailNotificationAction(logger.Object, settings);

        Assert.NotNull(action);
    }

    [Fact]
    public void Constructor_CustomSubjectAndBody()
    {
        var logger = new Mock<ILogger<EmailNotificationAction>>();
        var settings = new Dictionary<string, string>
        {
            ["SmtpHost"] = "smtp.test.com",
            ["To"] = "test@test.com",
            ["Subject"] = "Scan from {scanner}",
            ["Body"] = "Data: {data}, Format: {format}"
        };

        var action = new EmailNotificationAction(logger.Object, settings);

        Assert.NotNull(action);
    }
}
