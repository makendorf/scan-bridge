using System.ServiceProcess;
using ScanBridge.Models;

namespace ScanBridge.Services.PostScanActions;

/// <summary>
/// Действие управления Windows-службой: старт, остановка, перезапуск.
/// </summary>
public class ServiceAction : IPostScanAction
{
    public string Type => "Service";

    private readonly ILogger<ServiceAction> _logger;
    private readonly string _serviceName;
    private readonly string _action;
    private readonly int _timeoutMs;

    public ServiceAction(ILogger<ServiceAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;
        _serviceName = settings.GetValueOrDefault("ServiceName", "") ?? "";
        _action = settings.GetValueOrDefault("Action", "restart") ?? "restart";
        _timeoutMs = settings.TryGetValue("TimeoutSeconds", out var ts)
            && int.TryParse(ts, out var t) ? t * 1000 : 30000;
    }

    public Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_serviceName))
        {
            _logger.LogWarning("Служба: имя службы не указано");
            scan.ParsedData = "error: service name is empty";
            scan.Metadata["serviceSuccess"] = "false";
            scan.Metadata["serviceError"] = "service name is empty";
            return Task.CompletedTask;
        }

        try
        {
            using var controller = new ServiceController(_serviceName);
            var currentStatus = controller.Status;

            switch (_action.ToLowerInvariant())
            {
                case "stop":
                    ExecuteStop(controller, currentStatus);
                    break;
                case "start":
                    ExecuteStart(controller, currentStatus);
                    break;
                case "restart":
                    ExecuteRestart(controller, currentStatus);
                    break;
                default:
                    _logger.LogWarning("Служба: неизвестное действие «{Action}»", _action);
                    scan.ParsedData = $"error: unknown action '{_action}'";
                    scan.Metadata["serviceSuccess"] = "false";
                    scan.Metadata["serviceError"] = $"unknown action '{_action}'";
                    return Task.CompletedTask;
            }

            controller.Refresh();
            var finalStatus = controller.Status;
            var message = $"{_serviceName}: {_action} completed → {finalStatus}";
            _logger.LogInformation("Служба: {Message}", message);
            scan.ParsedData = message;
            scan.Metadata["serviceSuccess"] = "true";
            scan.Metadata["serviceStatus"] = finalStatus.ToString();
            scan.Metadata["serviceAction"] = _action;
            scan.Metadata["serviceName"] = _serviceName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка управления службой «{Name}» ({Action})", _serviceName, _action);
            scan.ParsedData = $"error: {ex.Message}";
            scan.Metadata["serviceSuccess"] = "false";
            scan.Metadata["serviceError"] = ex.Message;
        }

        return Task.CompletedTask;
    }

    private void ExecuteStop(ServiceController sc, ServiceControllerStatus current)
    {
        if (current == ServiceControllerStatus.Stopped || current == ServiceControllerStatus.StopPending)
        {
            _logger.LogInformation("Служба «{Name}» уже остановлена", _serviceName);
            return;
        }
        sc.Stop();
        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMilliseconds(_timeoutMs));
    }

    private void ExecuteStart(ServiceController sc, ServiceControllerStatus current)
    {
        if (current == ServiceControllerStatus.Running || current == ServiceControllerStatus.StartPending)
        {
            _logger.LogInformation("Служба «{Name}» уже запущена", _serviceName);
            return;
        }
        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(_timeoutMs));
    }

    private void ExecuteRestart(ServiceController sc, ServiceControllerStatus current)
    {
        if (current == ServiceControllerStatus.Running || current == ServiceControllerStatus.StartPending)
        {
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMilliseconds(_timeoutMs));
        }

        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(_timeoutMs));
    }
}
