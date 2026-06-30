using Microsoft.EntityFrameworkCore;
using ScanBridge.Data;
using ScanBridge.Data.Entities;
using ScanBridge.Models;
using ScanBridge.Services;

namespace ScanBridge.Api;

public static class ScannerEndpoints
{
    public static WebApplication MapScannerEndpoints(this WebApplication app, ScannerManager manager, Func<List<SerialPortConfig>> readScanners)
    {
        app.MapGet("/api/scanners", () => Results.Ok(readScanners()));

        app.MapGet("/api/scanners/status", () =>
        {
            var running = manager.GetRunning();
            var scanners = readScanners();
            return Results.Ok(scanners.Select(s => new
            {
                s.Name,
                s.PortName,
                Running = running.Contains(s.Name)
            }));
        });

        app.MapGet("/api/scanners/lastscan", () =>
        {
            var tracker = app.Services.GetRequiredService<ScanTracker>();
            var (time, scannerName) = tracker.GetLastScan();
            return Results.Ok(new { time, scannerName });
        });

        app.MapPost("/api/scanners/{name}/restart", (string name) =>
        {
            var scanners = readScanners();
            var config = scanners.FirstOrDefault(s => s.Name == name);
            if (config == null) return Results.NotFound(new { error = $"Сканер '{name}' не найден" });
            manager.RestartScanner(config);
            return Results.Ok(new { message = $"Сканер '{name}' перезапущен" });
        });

        app.MapPost("/api/scanners", (SerialPortConfig scanner) =>
        {
            if (string.IsNullOrWhiteSpace(scanner.Name) || string.IsNullOrWhiteSpace(scanner.PortName))
                return Results.BadRequest(new { error = "Name и PortName обязательны" });

            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var maxOrder = db.Scanners.Any() ? db.Scanners.Max(s => s.SortOrder) + 1 : 0;
            db.Scanners.Add(new ScannerConfig
            {
                Name = scanner.Name,
                PortName = scanner.PortName,
                BaudRate = scanner.BaudRate,
                DataBits = scanner.DataBits,
                Parity = scanner.Parity,
                StopBits = scanner.StopBits,
                Handshake = scanner.Handshake,
                ReadTimeout = scanner.ReadTimeout,
                WriteTimeout = scanner.WriteTimeout,
                SortOrder = maxOrder,
                ControlCharMode = scanner.ControlCharMode,
                ReconnectDelayMs = scanner.Reconnect?.DelayMs ?? 1000,
                ReconnectMaxRetries = scanner.Reconnect?.MaxRetries ?? 10,
                ReconnectContinuous = scanner.Reconnect?.Continuous ?? false
            });
            db.SaveChanges();
            var conflict = manager.StartScanner(scanner);
            return Results.Ok(new { scanners = readScanners(), conflict });
        });

        app.MapPut("/api/scanners/{index:int}", (int index, SerialPortConfig scanner) =>
        {
            if (string.IsNullOrWhiteSpace(scanner.Name) || string.IsNullOrWhiteSpace(scanner.PortName))
                return Results.BadRequest(new { error = "Name и PortName обязательны" });

            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var all = db.Scanners.OrderBy(s => s.SortOrder).ToList();
            if (index < 0 || index >= all.Count) return Results.NotFound();
            var entity = all[index];
            entity.Name = scanner.Name;
            entity.PortName = scanner.PortName;
            entity.BaudRate = scanner.BaudRate;
            entity.DataBits = scanner.DataBits;
            entity.Parity = scanner.Parity;
            entity.StopBits = scanner.StopBits;
            entity.Handshake = scanner.Handshake;
            entity.ReadTimeout = scanner.ReadTimeout;
            entity.WriteTimeout = scanner.WriteTimeout;
            entity.ControlCharMode = scanner.ControlCharMode;
            entity.ReconnectDelayMs = scanner.Reconnect?.DelayMs ?? 1000;
            entity.ReconnectMaxRetries = scanner.Reconnect?.MaxRetries ?? 10;
            entity.ReconnectContinuous = scanner.Reconnect?.Continuous ?? false;
            db.SaveChanges();

            var list = readScanners();
            string? conflict = null;
            var mode = DbHelpers.GetReconnectMode(db);
            if (mode == "all")
                manager.RestartAll(list);
            else
            {
                conflict = manager.CheckPortConflict(scanner.PortName, scanner.Name);
                manager.RestartScanner(scanner);
            }

            return Results.Ok(new { scanners = list, conflict });
        });

        app.MapDelete("/api/scanners/{index:int}", (int index) =>
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var all = db.Scanners.OrderBy(s => s.SortOrder).ToList();
            if (index < 0 || index >= all.Count) return Results.NotFound();
            var removed = all[index];
            db.Scanners.Remove(removed);
            db.SaveChanges();
            manager.StopScanner(removed.Name);
            return Results.Ok(readScanners());
        });

        return app;
    }
}
