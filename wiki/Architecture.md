# Архитектура

## Общая схема

```
COM-порт → SerialPortService → SimpleBarcodeParser → ScanProcessorService
    → PostScanManager → [Replacement → ClipboardPaste → Export]
```

## Поток данных

1. **SerialPortService** — фоновый сервис, слушающий COM-порт
2. **SimpleBarcodeParser** — парсит сырые данные, определяет формат
3. **ScanProcessorService** — координирует обработку
4. **PostScanManager** — выполняет цепочку пост-скан действий

## Ключевые компоненты

### Program.cs (Точка входа)

Конфигурирует DI-контейнер, регистрирует сервисы, определяет REST API endpoints.

```csharp
builder.Services.AddSingleton<LogCollector>();
builder.Services.AddSingleton<IBarcodeParser, SimpleBarcodeParser>();
builder.Services.AddSingleton<PostScanManager>();
builder.Services.AddSingleton<ScanProcessorService>();
builder.Services.AddSingleton<ScannerManager>();
```

### ScannerManager

Управляет жизненным циклом сканеров. Каждый сканер — отдельный экземпляр `SerialPortService`.

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

### PostScanManager

Управляет цепочкой пост-скан действий. Поддерживает фильтрацию по имени сканера.

## Структура проекта

```
ScanBridge/
├── src/
│   ├── Data/                    # EF Core DbContext
│   │   ├── AppDbContext.cs
│   │   └── Entities/            # Сущности БД
│   ├── Models/                  # Модели данных
│   ├── Parsers/                 # Парсеры штрихкодов
│   ├── Services/                # Бизнес-логика
│   │   ├── PostScanActions/     # Типы действий
│   │   └── ...
│   ├── wwwroot/                 # Веб-интерфейс
│   └── Program.cs               # Точка входа
├── tests/                       # Тесты (xUnit + Moq)
├── wiki/                        # Документация
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
}
```

### PostScanActionConfig

```csharp
public class PostScanActionConfig
{
    public string Type { get; set; }                    // Тип действия
    public bool Enabled { get; set; }                   // Включено
    public string ScannerName { get; set; }             // Фильтр по сканеру
    public Dictionary<string, string> Settings { get; set; } // Настройки
}
```

## База данных

SQLite файл: `scanbridge.db`

Таблицы:
- **Scanners** — конфигурации сканеров
- **PostScanActions** — настройки пост-скан действий
- **Settings** — общие настройки приложения
- **Logs** — записи логов
