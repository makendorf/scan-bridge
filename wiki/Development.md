# Разработка

## Требования

- .NET 10 SDK
- Windows (для ClipboardPaste — WinAPI)
- COM-порты или COM-to-Ethernet преобразователь

## Структура решения

```
ScanBridge.slnx
├── src/ScanBridge.csproj          # Основной проект
├── tests/ScanBridge.Tests.csproj  # Тесты
├── hub/ScanBridgeHub.csproj       # Hub-проект
└── hub.Tests/                     # Тесты Hub
```

## Запуск

```bash
cd src
dotnet run
```

Веб-интерфейс: `http://localhost:5000` (порт настраивается в `appsettings.json`)

## Зависимости

| Пакет | Версия | Назначение |
|-------|--------|------------|
| FluentFTP | 54.2.0 | FTP-клиент |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.* | SQLite ORM |
| Microsoft.Extensions.Hosting.WindowsServices | 10.0.9 | Windows Service |
| Serilog.AspNetCore | 10.0.0 | Логирование |
| Serilog.Sinks.File | 7.0.0 | Логирование в файл |
| SSH.NET | 2024.* | SFTP-клиент |
| System.IO.Ports | 10.0.9 | COM-порты |

## DI-контейнер

```csharp
// Singleton-сервисы
builder.Services.AddSingleton<LogCollector>();
builder.Services.AddSingleton<IBarcodeParser, SimpleBarcodeParser>();
builder.Services.AddSingleton<IPostScanActionFactory, PostScanActionFactory>();
builder.Services.AddSingleton<PostScanManager>();
builder.Services.AddSingleton<ScanProcessorService>();
builder.Services.AddSingleton<ScannerManager>();

// Фабрика SerialPortService (для тестирования и гибкости)
builder.Services.AddSingleton<Func<SerialPortConfig, ReconnectConfig?, SerialPortService>>(sp =>
{
    return (config, reconnect) => new SerialPortService(
        sp.GetRequiredService<ILogger<SerialPortService>>(),
        config,
        sp.GetRequiredService<IBarcodeParser>(),
        sp.GetRequiredService<ScanProcessorService>(),
        reconnect);
});

// DbContext (путь настраивается)
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite($"Data Source={builder.Configuration.GetValue(\"Database\", \"scanbridge.db\")}"));
```

## API Endpoints

API-эндпоинты вынесены из `Program.cs` в отдельные extension-методы:

| Файл | Эндпоинты |
|------|-----------|
| `src/Api/ScannerEndpoints.cs` | `GET/POST/PUT/DELETE /api/scanners`, `POST /api/scanners/{name}/restart` |
| `src/Api/LogEndpoints.cs` | `GET/DELETE /api/logs` |
| `src/Api/PortEndpoints.cs` | `GET /api/ports` |
| `src/Api/PostScanEndpoints.cs` | `GET/PUT /api/postscan/groups` |
| `src/Api/SettingsEndpoints.cs` | `GET/PUT /api/settings/reconnect`, `GET/PUT /api/settings/reconnect/config` |
| `src/Api/DbHelpers.cs` | `ReadScanners()`, `ReadReconnectConfig()`, `GetReconnectMode()`, `SetSetting()` |

## Добавление нового пост-скан действия

1. Создайте класс в `src/Services/PostScanActions/`:

```csharp
public class MyAction : IPostScanAction
{
    public string Type => "MyAction";

    public MyAction(ILogger<MyAction> logger, Dictionary<string, string> settings) { }

    public Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        // Логика действия
        return Task.CompletedTask;
    }
}
```

2. Зарегистрируйте в `PostScanActionFactory.Create()`:

```csharp
return config.Type switch
{
    // ... существующие
    "MyAction" => new MyAction(_loggerFactory.CreateLogger<MyAction>(), settings),
    _ => null
};
```

3. Добавьте в веб-интерфейс в `index.html` в объект `ACTION_TYPES`.

## Добавление нового формата штрихкода

1. Отредактируйте `SimpleBarcodeParser.DetectFormat()`:

```csharp
return data.Length switch
{
    8 => "EAN-8",
    12 => "UPC-A",
    13 => "EAN-13",
    14 => "GTIN-14",
    // Добавьте новый формат
    15 => "MyFormat",
    _ => "Numeric"
};
```

## Кодировка

Проект использует `ImplicitUsings` и `Nullable`:

```xml
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
```

## Логирование

Используется Serilog с двумя sink'ами:

1. **Console** — вывод в консоль
2. **CollectorSink** — запись в БД через `LogCollector`

```csharp
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Sink(new CollectorSink(() => app.Services.GetRequiredService<LogCollector>()))
    .CreateLogger();
```

## Конфигурация

Порт и путь к БД настраиваются через `appsettings.json`:

```json
{
  "Port": 5000,
  "Database": "scanbridge.db",
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  }
}
```
