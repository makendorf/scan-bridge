using Microsoft.EntityFrameworkCore;
using ScanBridge.Data;
using ScanBridge.Data.Entities;
using ScanBridge.Models;

namespace ScanBridge.Api;

public static class DbHelpers
{
    public static List<SerialPortConfig> ReadScanners(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reconnect = ReadReconnectConfig(db);
        return db.Scanners.OrderBy(s => s.SortOrder).Select(s => new SerialPortConfig
        {
            Name = s.Name,
            PortName = s.PortName,
            BaudRate = s.BaudRate,
            DataBits = s.DataBits,
            Parity = s.Parity,
            StopBits = s.StopBits,
            Handshake = s.Handshake,
            ReadTimeout = s.ReadTimeout,
            WriteTimeout = s.WriteTimeout,
            ControlCharMode = s.ControlCharMode,
            Reconnect = new ReconnectConfig
            {
                DelayMs = s.ReconnectDelayMs,
                MaxRetries = s.ReconnectMaxRetries,
                Continuous = s.ReconnectContinuous
            }
        }).ToList();
    }

    public static ReconnectConfig ReadReconnectConfig(AppDbContext db)
    {
        return new ReconnectConfig
        {
            DelayMs = int.TryParse(db.Settings.FirstOrDefault(s => s.Key == "ReconnectDelayMs")?.Value, out var d) ? d : 1000,
            MaxRetries = int.TryParse(db.Settings.FirstOrDefault(s => s.Key == "ReconnectMaxRetries")?.Value, out var r) ? r : 10,
            Continuous = db.Settings.FirstOrDefault(s => s.Key == "ReconnectContinuous")?.Value == "true"
        };
    }

    public static string GetReconnectMode(AppDbContext db)
    {
        return db.Settings.FirstOrDefault(s => s.Key == "ReconnectMode")?.Value ?? "single";
    }

    public static void SetSetting(AppDbContext db, string key, string value)
    {
        var setting = db.Settings.FirstOrDefault(s => s.Key == key);
        if (setting == null)
        {
            db.Settings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }
        db.SaveChanges();
    }
}
