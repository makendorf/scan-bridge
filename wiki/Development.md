# Разработка

## Требования

- .NET 10 SDK
- Windows (для ClipboardPaste — WinAPI)
- COM-порты или COM-to-Ethernet преобразователь

## Структура решения

```
xTrack-Mercury-Skan.slnx
├── src/ScanBridge.csproj        # Основной проект
└── tests/ScanBridge.Tests.csproj # Тесты
```

## Запуск

```bash
cd src
dotnet run
```

Веб-интерфейс: `http://localhost:5000`

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
builder.Services.AddSingleton<PostScanManager>();
builder.Services.AddSingleton<ScanProcessorService>();
builder.Services.AddSingleton<ScannerManager>();

// DbContext
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite("Data Source=scanbridge.db"));
```

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

2. Зарегистрируйте в `PostScanManager.CreateAction()`:

```csharp
return config.Type switch
{
    // ... существующие
    "MyAction" => new MyAction(logger, config.Settings),
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
