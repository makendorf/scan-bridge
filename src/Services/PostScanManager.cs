using ScanBridge.Models;
using ScanBridge.Services.PostScanActions;

namespace ScanBridge.Services;

public class PostScanManager
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PostScanManager> _logger;
    private readonly List<PostScanActionEntry> _actions = new();

    public PostScanManager(IServiceProvider services, ILogger<PostScanManager> logger)
    {
        _services = services;
        _logger = logger;
    }

    public virtual void Configure(List<PostScanActionConfig> configs)
    {
        _actions.Clear();

        foreach (var config in configs)
        {
            if (!config.Enabled) continue;

            var action = CreateAction(config);
            if (action != null)
            {
                _actions.Add(new PostScanActionEntry(action, config));
                _logger.LogInformation("Пост-скан действие: {Type}", action.Type);
            }
            else
            {
                _logger.LogWarning("Неизвестный тип пост-скан действия: {Type}", config.Type);
            }
        }
    }

    public virtual async Task ExecuteAllAsync(ScanResult scan, CancellationToken ct)
    {
        foreach (var entry in _actions)
        {
            if (!string.IsNullOrEmpty(entry.Config.ScannerName) &&
                !entry.Config.ScannerName.Equals(scan.ScannerName, StringComparison.Ordinal))
                continue;

            try
            {
                await entry.Action.ExecuteAsync(scan, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в пост-скан действии {Type}", entry.Action.Type);
            }
        }
    }

    public virtual IReadOnlyList<string> GetEnabledActions()
    {
        return _actions.Select(a => a.Action.Type).ToList().AsReadOnly();
    }

    private IPostScanAction? CreateAction(PostScanActionConfig config)
    {
        var loggerFactory = _services.GetRequiredService<ILoggerFactory>();

        return config.Type switch
        {
            "Log" => new LogAction(loggerFactory.CreateLogger<LogAction>()),
            "ClipboardPaste" => new ClipboardPasteAction(
                loggerFactory.CreateLogger<ClipboardPasteAction>(),
                config.Settings),
            "Replacement" => new ReplacementAction(
                loggerFactory.CreateLogger<ReplacementAction>(),
                config.Settings),
            "Export" => new ExportAction(
                loggerFactory.CreateLogger<ExportAction>(),
                config.Settings),
            _ => null
        };
    }

    private record PostScanActionEntry(IPostScanAction Action, PostScanActionConfig Config);
}
