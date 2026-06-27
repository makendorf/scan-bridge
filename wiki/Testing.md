# Тестирование

## Запуск тестов

```bash
dotnet test
```

## Структура тестов

```
tests/
├── Models/
│   ├── ScanResultTests.cs
│   └── SerialPortConfigTests.cs
├── Parsers/
│   └── SimpleBarcodeParserTests.cs
├── Services/
│   ├── ScanProcessorServiceTests.cs
│   ├── ScannerManagerTests.cs
│   ├── SaveToFileActionTests.cs
│   ├── LogCollectorTests.cs
│   └── CollectorSinkTests.cs
├── ScanBridge.Tests.csproj
hub.Tests/
├── HubDbContextTests.cs
├── InstancePollerTests.cs
├── ScanTrackerTests.cs
└── ScanBridgeHub.Tests.csproj
```

## Стек тестирования

| Пакет | Версия | Назначение |
|-------|--------|------------|
| xunit | 2.9.3 | Фреймворк тестов |
| Moq | 4.20.72 | Моки зависимостей |
| Microsoft.NET.Test.Sdk | 18.7.0 | Тестовый хост |
| coverlet.collector | 10.0.1 | Покрытие кода |

## Типы тестов

### Модели

- Проверка свойств `ScanResult`
- Проверка значений по умолчанию `SerialPortConfig`

### Парсеры

- Определение UUID
- Определение EAN-8/13, UPC-A, GTIN-14
- Определение Code128, GS1-128
- Определение QR-контента (URL, JSON, WiFi, vCard)
- Обработка пустых и невалидных данных

### Сервисы

- `ScanProcessorService` — вызов пост-скан действий
- `ScannerManager` — запуск/остановка сканеров, конфликты портов (использует `StubSerialPortService` для тестов)
- `LogCollector` — запись логов в БД
- `CollectorSink` — интеграция с Serilog

### Пост-скан действия

- `ExportAction` (SaveToFile) — экспорт в JSON/XML/FTP/SFTP/HTTP
- `ReplacementAction` — замена символов
- `LogAction` — логирование
- `ClipboardPasteAction` — вставка в окно

### Hub

- `HubDbContextTests` — тесты БД hub
- `InstancePollerTests` — тесты опроса экземпляров
- `ScanTrackerTests` — тесты трекера сканирований

## Генерация отчёта покрытия

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Отчёт: `TestResults/*/coverage.cobertura.xml`
