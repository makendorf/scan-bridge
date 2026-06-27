using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using ScanBridge.Models;
using ScanBridge.Utils;

namespace ScanBridge.Services.PostScanActions;

/// <summary>
/// Действие отправки уведомления по email через SMTP.
/// Создаёт новое SMTP-соединение на каждый вызов для избежания проблем с таймаутами.
/// </summary>
public class EmailNotificationAction : IPostScanAction
{
    public string Type => "Email";

    private readonly ILogger<EmailNotificationAction> _logger;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly SecureSocketOptions _security;
    private readonly string _smtpUser;
    private readonly string _smtpPass;
    private readonly string _from;
    private readonly List<string> _toAddresses;
    private readonly string _subjectTemplate;
    private readonly string _bodyTemplate;

    public EmailNotificationAction(ILogger<EmailNotificationAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;

        _smtpHost = settings.TryGetValue("SmtpHost", out var host) ? host : "";
        _smtpPort = settings.TryGetValue("SmtpPort", out var portStr) && int.TryParse(portStr, out var port) ? port : 587;
        _smtpUser = settings.TryGetValue("SmtpUser", out var user) ? user : "";
        _smtpPass = settings.TryGetValue("SmtpPass", out var pass) ? pass : "";
        _from = settings.TryGetValue("From", out var from) ? from : "";
        _subjectTemplate = settings.TryGetValue("Subject", out var subj) && !string.IsNullOrWhiteSpace(subj)
            ? subj : "Scan: {data}";
        _bodyTemplate = settings.TryGetValue("Body", out var body) && !string.IsNullOrWhiteSpace(body)
            ? body : "{data} ({format})";

        _security = ParseSecurity(settings.TryGetValue("SmtpSecurity", out var sec) ? sec : "StartTls");

        _toAddresses = new();
        if (settings.TryGetValue("To", out var toStr) && !string.IsNullOrWhiteSpace(toStr))
        {
            foreach (var addr in toStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(addr))
                    _toAddresses.Add(addr);
            }
        }

        if (string.IsNullOrWhiteSpace(_smtpHost))
            _logger.LogWarning("Email: SmtpHost не задан, уведомления не будут отправляться");
        if (_toAddresses.Count == 0)
            _logger.LogWarning("Email: нет получателей, уведомления не будут отправляться");
    }

    public async Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_smtpHost) || _toAddresses.Count == 0)
            return;

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_from));
        foreach (var addr in _toAddresses)
            message.To.Add(MailboxAddress.Parse(addr));
        message.Subject = ScanTemplateHelper.Format(_subjectTemplate, scan);
        message.Body = new TextPart("plain") { Text = ScanTemplateHelper.Format(_bodyTemplate, scan) };

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_smtpHost, _smtpPort, _security, ct);

            if (!string.IsNullOrWhiteSpace(_smtpUser))
                await client.AuthenticateAsync(_smtpUser, _smtpPass, ct);

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Email: отправлено {Count} получателям", _toAddresses.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email: ошибка отправки");
        }
    }

    private static SecureSocketOptions ParseSecurity(string value)
    {
        return value?.ToLowerInvariant() switch
        {
            "sslonconnect" => SecureSocketOptions.SslOnConnect,
            "starttls" => SecureSocketOptions.StartTls,
            "none" => SecureSocketOptions.None,
            _ => SecureSocketOptions.StartTls
        };
    }
}
