using ScanBridge.Services;

namespace ScanBridge.Api;

/// <summary>
/// Эндпоинты дашборда — предоставляет API для статистики и аналитики сканеров.
/// </summary>
public static class DashboardEndpoints
{
    /// <summary>
    /// Маппит эндпоинты дашборда на WebApplication.
    /// </summary>
    /// <param name="app">Экземпляр WebApplication.</param>
    /// <param name="manager">Менеджер сканеров.</param>
    /// <returns>WebApplication с замапленными эндпоинтами.</returns>
    public static WebApplication MapDashboardEndpoints(this WebApplication app, ScannerManager manager)
    {
        app.MapGet("/api/dashboard/stats", async () =>
        {
            var historyService = app.Services.GetRequiredService<ScanHistoryService>();
            return Results.Ok(await historyService.GetStatsAsync(manager));
        });

        app.MapGet("/api/dashboard/activity", async (string? period) =>
        {
            var historyService = app.Services.GetRequiredService<ScanHistoryService>();
            return Results.Ok(await historyService.GetActivityAsync(period ?? "24h"));
        });

        app.MapGet("/api/dashboard/scans", async (int? limit) =>
        {
            var historyService = app.Services.GetRequiredService<ScanHistoryService>();
            return Results.Ok(await historyService.GetRecentScansAsync(limit ?? 50));
        });

        app.MapGet("/api/dashboard/formats", async () =>
        {
            var historyService = app.Services.GetRequiredService<ScanHistoryService>();
            return Results.Ok(await historyService.GetFormatsAsync());
        });

        app.MapGet("/api/dashboard/per-scanner", async () =>
        {
            var historyService = app.Services.GetRequiredService<ScanHistoryService>();
            return Results.Ok(await historyService.GetPerScannerAsync(manager));
        });

        app.MapGet("/api/dashboard/reconnects", async (int? hours) =>
        {
            var historyService = app.Services.GetRequiredService<ScanHistoryService>();
            return Results.Ok(await historyService.GetReconnectErrorsAsync(hours ?? 24));
        });

        return app;
    }
}
