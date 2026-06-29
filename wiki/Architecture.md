# Архитектура

## Общая схема

```
COM-порт → SerialPortService → SimpleBarcodeParser → ScanProcessorService
    → ScanTracker + ScanHistoryService → PostScanManager → [PostScanActionFactory] → [actions...]
```

## Поток данных

1. **SerialPortService** — фоновый сервис, слушающий COM-порт
2. **SimpleBarcodeParser** — парсит сырые данные, определяет формат
3. **ScanProcessorService** — координирует обработку
4. **ScanTracker** — записывает время последнего сканирования (память)
5. **ScanHistoryService** — сохраняет историю сканирований в SQLite (fire-and-forget)
6. **PostScanManager** — выполняет группы пост-скан действий
7. **PostScanActionFactory** — создаёт экземпляры действий по типу

## Ключевые компоненты

### Program.cs (Точка входа)

Конфигурирует DI-контейнер, регистрирует сервисы. API-эндпоинты вынесены в extension-методы в `src/Api/`.

```csharp
builder.Services.AddSingleton<LogCollector>();
builder.Services.AddSingleton<IBarcodeParser, SimpleBarcodeParser>();
builder.Services.AddSingleton<IPostScanActionFactory, PostScanActionFactory>();
builder.Services.AddSingleton<PostScanManager>();
builder.Services.AddSingleton<ScanProcessorService>();
builder.Services.AddSingleton<ScannerManager>();
builder.Services.AddSingleton<ScanTracker>();
builder.Services.AddSingleton<ScanHistoryService>();
```

### ScannerManager

Управляет жизненным циклом сканеров. Каждый сканер — отдельный экземпляр `SerialPortService`, создаваемый через factory delegate.

- `StartAll(List<SerialPortConfig>)` — запуск всех сканеров
- `StartScanner(SerialPortConfig)` — запуск одного сканера
- `StopScanner(string name)` — остановка по имени
- `RestartAll(...)` — перезапуск всех
- `CheckPortConflict(port, name)` — проверка конфликта портов

### SerialPortService (BackgroundService)

Фоновый сервис для одного сканера. Логика:

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    var bytesRead = _serialPort.Read(buffer, 0, buffer.Length);
    var raw = Encoding.ASCII.GetString(buffer, 0, bytesRead);
    var result = _parser.Parse(raw);
    await _processor.ProcessAsync(result, stoppingToken);
}
```

При ошибке COM-порта — экспоненциальная задержка переподключения (1с → 30с), макс. 10 попыток.

### PostScanManager

Управляет группами пост-скан действий. Группы выполняются параллельно, действия внутри группы — последовательно. Потокобезопасность через `volatile IReadOnlyList<>` с immutable снапшотами.

### PostScanActionFactory

Фабрика, создающая экземпляры действий по строковому типу. Реестр типов:

```csharp
return config.Type switch
{
    "Log" => new LogAction(...),
    "ClipboardPaste" => new ClipboardPasteAction(...),
    "Replacement" => new ReplacementAction(...),
    "Export" => new ExportAction(...),
    "WindowPaste" => new WindowPasteAction(...),
    // ...
};
```

### SimpleBarcodeParser

Парсер штрихкодов. Определяет формат по длине и содержимому:

| Формат | Описание |
|--------|----------|
| UUID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| EAN-8 | 8 цифр |
| UPC-A | 12 цифр |
| EAN-13 | 13 цифр |
| GTIN-14 | 14 цифр |
| Code128 | Алфавитно-цифровой, ≤64 символов |
| GS1-128 | 22 символа |
| QR | QR-контент (URL, JSON, WiFi, vCard) |

### QRContentDetector

Определяет тип содержимого QR-кода:

- **URL** — начинается с `http://` или `https://`
- **JSON** — начинается с `{` или `[`, валидный JSON
- **VCard** — содержит `BEGIN:VCARD`
- **Wifi** — начинается с `WIFI:`
- **Text** — всё остальное

## Структура проекта

```
ScanBridge/
├── src/
│   ├── Api/                       # Extension-методы для API endpoints
│   │   ├── DbHelpers.cs           # Хелперы для работы с БД
│   │   ├── DashboardEndpoints.cs  # /api/dashboard/*
│   │   ├── ScannerEndpoints.cs    # /api/scanners
│   │   ├── LogEndpoints.cs        # /api/logs
│   │   ├── PortEndpoints.cs       # /api/ports
│   │   ├── PostScanEndpoints.cs   # /api/postscan/groups
│   │   └── SettingsEndpoints.cs   # /api/settings/*
│   ├── Data/                      # EF Core DbContext
│   │   ├── AppDbContext.cs
│   │   └── Entities/              # Сущности БД
│   ├── Models/                    # Модели данных
│   ├── Parsers/                   # Парсеры штрихкодов
│   ├── Services/                  # Бизнес-логика
│   │   ├── PostScanActions/       # Типы действий
│   │   ├── IPostScanActionFactory.cs
│   │   ├── PostScanActionFactory.cs
│   │   └── ...
│   ├── Utils/                     # Утилиты
│   │   ├── Win32Clipboard.cs      # Win32 API для буфера обмена
│   │   └── ControlCharDisplay.cs
│   ├── wwwroot/                   # Веб-интерфейс (включая дашборд)
│   └── Program.cs                 # Точка входа (~200 строк)
├── tests/                         # Тесты (xUnit + Moq)
├── wiki/                          # Документация
└── ScanBridge.slnx
```

## Модели данных

### ScanResult

```csharp
public class ScanResult
{
    public string RawData { get; set; }      // Исходные данные
    public string ParsedData { get; set; }   // Обработанные данные
    public string Format { get; set; }        // UUID/EAN-8/Code128/QR...
    public bool IsValid { get; set; }         // Валидность
    public string ScannerName { get; set; }   // Имя сканера
    public DateTime Timestamp { get; set; }   // Время сканирования
    public string ContentType { get; set; }   // Тип QR-контента
    public string? ParsedContent { get; set; } // Распарсенный QR
    public Dictionary<string, string> Metadata { get; set; } // Метаданные
}
```

### SerialPortConfig

```csharp
public class SerialPortConfig
{
    public string Name { get; set; }           // Имя сканера
    public string PortName { get; set; }       // COM-порт
    public int BaudRate { get; set; }          // Скорость (по умолчанию 9600)
    public int DataBits { get; set; }          // Биты данных (8)
    public string Parity { get; set; }         // Чётность (None)
    public string StopBits { get; set; }       // Стоп-биты (One)
    public string Handshake { get; set; }      // Управление потоком
    public int ReadTimeout { get; set; }       // Таймаут чтения (5000мс)
    public int WriteTimeout { get; set; }      // Таймаут записи (5000мс)
    public string ControlCharMode { get; set; } // Режим контрольных символов
    public ReconnectConfig Reconnect { get; set; } // Настройки переподключения
}
```

## База данных

SQLite файл: `scanbridge.db` (путь настраивается через `appsettings.json`)

Таблицы:
- **Scanners** — конфигурации сканеров
- **PostScanActionGroups** — группы пост-скан действий
- **PostScanActions** — настройки пост-скан действий
- **PostScanActionGroupScanners** — связи групп со сканерами
- **Settings** — общие настройки приложения
- **Logs** — записи логов
- **ScanHistory** — история сканирований (для дашборда)
- **ReconnectEvents** — события переподключения
