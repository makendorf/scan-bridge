using Microsoft.EntityFrameworkCore;
using ScanBridge.Data;
using ScanBridge.Models;
using ScanBridge.Services;
using Serilog;

namespace ScanBridge.Api;

public static class SettingsEndpoints
{
    public static WebApplication MapSettingsEndpoints(this WebApplication app, ScannerManager manager, Func<List<SerialPortConfig>> readScanners)
    {
        app.MapGet("/api/settings/reconnect", () =>
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return Results.Ok(new { mode = DbHelpers.GetReconnectMode(db) });
        });

        app.MapPut("/api/settings/reconnect", (ReconnectModeRequest req) =>
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            DbHelpers.SetSetting(db, "ReconnectMode", req.Mode);
            Log.Information("Режим переподключения изменён на: {Mode}", req.Mode);
            return Results.Ok(new { mode = req.Mode });
        });

        app.MapGet("/api/settings/reconnect/config", () =>
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = DbHelpers.ReadReconnectConfig(db);
            return Results.Ok(new { config.DelayMs, config.MaxRetries, config.Continuous });
        });

        app.MapPut("/api/settings/reconnect/config", (ReconnectConfigRequest req) =>
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            DbHelpers.SetSetting(db, "ReconnectDelayMs", Math.Clamp(req.DelayMs, 100, 60000).ToString());
            DbHelpers.SetSetting(db, "ReconnectMaxRetries", Math.Clamp(req.MaxRetries, 1, 10000).ToString());
            DbHelpers.SetSetting(db, "ReconnectContinuous", req.Continuous.ToString().ToLower());
            var config = DbHelpers.ReadReconnectConfig(db);
            Log.Information("Настройки переподключения обновлены: задержка={Delay}мс, попытки={MaxRetries}, непрерывно={Continuous}",
                config.DelayMs, config.MaxRetries, config.Continuous);

            var scanners = readScanners();
            manager.RestartAll(scanners);

            return Results.Ok(new { config.DelayMs, config.MaxRetries, config.Continuous });
        });

        return app;
    }
}

internal record ReconnectModeRequest(string Mode);

internal record ReconnectConfigRequest(int DelayMs, int MaxRetries, bool Continuous);
