using Microsoft.EntityFrameworkCore;
using ScanBridge.Data;

namespace ScanBridge.Api;

public static class LogEndpoints
{
    public static WebApplication MapLogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/logs", (int limit = 500) =>
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entries = db.Logs
                .OrderByDescending(l => l.Timestamp)
                .Take(limit)
                .Select(l => new { l.Timestamp, l.Level, l.Message, l.Exception })
                .ToList();
            return Results.Ok(entries);
        });

        app.MapDelete("/api/logs", () =>
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Logs.ExecuteDelete();
            db.SaveChanges();
            return Results.Ok(new { message = "Логи очищены" });
        });

        return app;
    }
}
