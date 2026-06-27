using ScanBridge.Models;
using ScanBridge.Services.PostScanActions;

namespace ScanBridge.Services;

/// <summary>
/// Менеджер пост-скан действий.
/// Управляет группами действий, привязанными к сканерам.
/// Группы, привязанные к одному сканеру, выполняются параллельно.
/// Действия внутри группы выполняются последовательно.
/// </summary>
public class PostScanManager
{
    private readonly IPostScanActionFactory _factory;
    private readonly ILogger<PostScanManager> _logger;
    private volatile IReadOnlyList<CompiledGroup> _groups = Array.Empty<CompiledGroup>();
    private readonly object _lock = new();

    /// <summary>
    /// Создаёт экземпляр менеджера пост-скан действий.
    /// </summary>
    /// <param name="factory">Фабрика для создания экшенов.</param>
    /// <param name="logger">Логгер.</param>
    public PostScanManager(IPostScanActionFactory factory, ILogger<PostScanManager> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <summary>
    /// Настраивает группы действий из конфигурации.
    /// Атомарно заменяет текущий список групп на новый.
    /// </summary>
    /// <param name="groupConfigs">Список конфигураций групп.</param>
    public virtual void Configure(List<PostScanActionGroupConfig> groupConfigs)
    {
        var newGroups = new List<CompiledGroup>();

        foreach (var groupConfig in groupConfigs)
        {
            if (!groupConfig.Enabled) continue;

            var compiledActions = new List<CompiledAction>();
            foreach (var actionConfig in groupConfig.Actions)
            {
                if (!actionConfig.Enabled) continue;

                var action = CreateAction(actionConfig);
                if (action != null)
                {
                    compiledActions.Add(new CompiledAction(action, actionConfig));
                }
                else
                {
                    _logger.LogWarning("Неизвестный тип пост-скан действия: {Type}", actionConfig.Type);
                }
            }

            if (compiledActions.Count > 0)
            {
                newGroups.Add(new CompiledGroup(groupConfig, compiledActions));
                _logger.LogInformation("Группа «{Name}»: {Count} действий, сканеры: {Scanners}",
                    groupConfig.Name, compiledActions.Count,
                    groupConfig.ScannerNames.Count > 0 ? string.Join(", ", groupConfig.ScannerNames) : "все");
            }
        }

        lock (_lock)
        {
            _groups = newGroups.AsReadOnly();
        }
    }

    /// <summary>
    /// Выполняет все подходящие группы для результата сканирования.
    /// Группы, привязанные к данному сканеру, выполняются параллельно.
    /// Действия внутри каждой группы — последовательно.
    /// </summary>
    /// <param name="scan">Результат сканирования.</param>
    /// <param name="ct">Токен отмены.</param>
    public virtual async Task ExecuteAllAsync(ScanResult scan, CancellationToken ct)
    {
        var groups = _groups;

        var matchingGroups = groups
            .Where(g => MatchesScanner(g.Config, scan.ScannerName))
            .ToList();

        if (matchingGroups.Count == 0) return;

        var tasks = matchingGroups.Select(group => ExecuteGroupAsync(group, scan, ct));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Возвращает список типов активных пост-скан действий.
    /// </summary>
    /// <returns>Только для чтения список строк с типами действий.</returns>
    public virtual IReadOnlyList<string> GetEnabledActions()
    {
        lock (_lock)
        {
            return _groups
                .SelectMany(g => g.Actions)
                .Select(a => a.Action.Type)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Проверяет, подходит ли группа для данного сканера.
    /// Пустой список сканеров группы означает привязку ко всем.
    /// </summary>
    private static bool MatchesScanner(PostScanActionGroupConfig groupConfig, string scannerName)
    {
        if (groupConfig.ScannerNames.Count == 0)
            return true;

        return groupConfig.ScannerNames.Contains(scannerName, StringComparer.Ordinal);
    }

    /// <summary>
    /// Выполняет все действия группы последовательно.
    /// </summary>
    private async Task ExecuteGroupAsync(CompiledGroup group, ScanResult scan, CancellationToken ct)
    {
        foreach (var compiledAction in group.Actions)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await compiledAction.Action.ExecuteAsync(scan, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в пост-скан действии {Type} (группа «{Group}»)",
                    compiledAction.Action.Type, group.Config.Name);
            }
        }
    }

    /// <summary>
    /// Создаёт экземпляр пост-скан действия по конфигурации.
    /// </summary>
    /// <param name="config">Конфигурация действия.</param>
    /// <returns>Экземпляр действия или null, если тип неизвестен.</returns>
    private IPostScanAction? CreateAction(PostScanActionConfig config)
    {
        var settings = config.Settings ?? new();
        return _factory.Create(config.Type, settings);
    }

    private record CompiledAction(IPostScanAction Action, PostScanActionConfig Config);
    private record CompiledGroup(PostScanActionGroupConfig Config, List<CompiledAction> Actions);
}
