using System.IO.Ports;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ScanBridge.Api;
using ScanBridge.Data;
using ScanBridge.Data.Entities;
using ScanBridge.Models;
using ScanBridge.Parsers;
using ScanBridge.Services;
using ScanBridge.Services.VisualScripting;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls($"http://0.0.0.0:{builder.Configuration.GetValue("Port", 5000)}");

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite($"Data Source={builder.Configuration.GetValue("Database", "scanbridge.db")}"));

builder.Host.UseSerilog();

builder.Services.AddSingleton<LogCollector>();
builder.Services.AddSingleton<IBarcodeParser, SimpleBarcodeParser>();
builder.Services.AddSingleton<IPostScanActionFactory>(sp =>
    new PostScanActionFactory(
        sp.GetRequiredService<ILoggerFactory>(),
        sp.GetRequiredService<IServiceScopeFactory>()));
builder.Services.AddSingleton<ScanDispatcher>();
builder.Services.AddSingleton<PostScanManager>();
builder.Services.AddSingleton<ScanProcessorService>();
builder.Services.AddSingleton<Func<SerialPortConfig, ReconnectConfig?, SerialPortService>>(sp =>
{
    return (config, reconnect) => new SerialPortService(
        sp.GetRequiredService<ILogger<SerialPortService>>(),
        config,
        sp.GetRequiredService<IBarcodeParser>(),
        sp.GetRequiredService<ScanProcessorService>(),
        sp.GetRequiredService<ScanHistoryService>(),
        reconnect);
});
builder.Services.AddSingleton<ScannerManager>();
builder.Services.AddSingleton<ScanTracker>();
builder.Services.AddSingleton<ScanHistoryService>();
builder.Services.AddSingleton<ScenarioService>();
builder.Services.AddSingleton<ScenarioExecutor>();
builder.Services.AddSingleton<ConditionEvaluator>();
builder.Services.AddSingleton<TriggerDispatcher>();
builder.Services.AddHostedService<ScheduleService>();
builder.Services.AddHostedService<FileWatcherService>();

if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "ScanBridge";
    });
}

var app = builder.Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Sink(new CollectorSink(() => app.Services.GetRequiredService<LogCollector>()))
    .CreateLogger();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Ошибка миграции БД, попыткаEnsureCreated");
        db.Database.EnsureCreated();
    }
    SeedFromLegacyConfig(db);
    var historyService = app.Services.GetRequiredService<ScanHistoryService>();
    await historyService.CleanupOldRecordsAsync();
}

var postScanManager = app.Services.GetRequiredService<PostScanManager>();

// Загрузка визуальных сценариев
var scenarioService = app.Services.GetRequiredService<ScenarioService>();
var scenarioExecutor = app.Services.GetRequiredService<ScenarioExecutor>();
var scenarioConfigs = scenarioService.GetAllWithGraph();
postScanManager.ConfigureScenarios(scenarioConfigs, scenarioExecutor);

List<SerialPortConfig> ReadScanners()
{
    return DbHelpers.ReadScanners(app.Services);
}

var manager = app.Services.GetRequiredService<ScannerManager>();
manager.StartAll(ReadScanners());

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        ctx.Context.Response.Headers.Append("Pragma", "no-cache");
        ctx.Context.Response.Headers.Append("Expires", "0");
    }
});

// Fallback: serve layout.html for any non-API, non-static-file route
app.MapFallbackToFile("layout.html");

app.MapScannerEndpoints(manager, ReadScanners);
app.MapLogEndpoints();
app.MapPortEndpoints();
app.MapSettingsEndpoints(manager, ReadScanners);
app.MapDashboardEndpoints(manager);
app.MapScenarioEndpoints();
app.MapTriggerEndpoints();
app.MapCredentialEndpoints();

using (var scope = app.Services.CreateScope())
{
    var scanners = ReadScanners();
    Log.Information("Запуск {Count} сканер(ов): {Names}",
        scanners.Count, string.Join(", ", scanners.Select(s => $"{s.Name} ({s.PortName})")));

    try
    {
        var ports = SerialPort.GetPortNames();
        Log.Information("Доступные COM-порты: {Ports}", ports.Length > 0 ? string.Join(", ", ports) : "НЕТ");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Не удалось получить список COM-портов");
    }

    Log.Information("База данных: {Path}", Path.GetFullPath("scanbridge.db"));
}

app.Run();

static void SeedFromLegacyConfig(AppDbContext db)
{
    if (db.Scanners.Any()) return;

    var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    if (!File.Exists(configPath)) return;

    try
    {
        var json = File.ReadAllText(configPath);
        var doc = System.Text.Json.JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("Scanners", out var scannersArr))
        {
            var scanners = JsonSerializer.Deserialize<List<SerialPortConfig>>(
                scannersArr.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            var order = 0;
            foreach (var s in scanners)
            {
                db.Scanners.Add(new ScannerConfig
                {
                    Name = s.Name, PortName = s.PortName, BaudRate = s.BaudRate,
                    DataBits = s.DataBits, Parity = s.Parity, StopBits = s.StopBits,
                    Handshake = s.Handshake, ReadTimeout = s.ReadTimeout,
                    WriteTimeout = s.WriteTimeout, SortOrder = order++,
                    ReconnectDelayMs = s.Reconnect.DelayMs,
                    ReconnectMaxRetries = s.Reconnect.MaxRetries,
                    ReconnectContinuous = s.Reconnect.Continuous
                });
            }
        }

        if (doc.RootElement.TryGetProperty("ReconnectMode", out var modeProp))
        {
            db.Settings.Add(new AppSetting { Key = "ReconnectMode", Value = modeProp.GetString() ?? "single" });
        }

        if (doc.RootElement.TryGetProperty("ReconnectDelayMs", out var delayProp))
        {
            db.Settings.Add(new AppSetting { Key = "ReconnectDelayMs", Value = delayProp.GetInt32().ToString() });
        }

        if (doc.RootElement.TryGetProperty("ReconnectMaxRetries", out var retriesProp))
        {
            db.Settings.Add(new AppSetting { Key = "ReconnectMaxRetries", Value = retriesProp.GetInt32().ToString() });
        }

        if (doc.RootElement.TryGetProperty("ReconnectContinuous", out var continuousProp))
        {
            db.Settings.Add(new AppSetting { Key = "ReconnectContinuous", Value = continuousProp.GetBoolean().ToString().ToLower() });
        }

        db.SaveChanges();
        Log.Information("Конфигурация импортирована из appsettings.json");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Не удалось импортировать конфигурацию из appsettings.json");
    }
}
