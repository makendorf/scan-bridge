# Конфигурация

## Хранение настроек

Все настройки хранятся в SQLite базе данных `scanbridge.db`. Управление — через веб-интерфейс или API.

Путь к БД настраивается через `appsettings.json`:

```json
{
  "Database": "scanbridge.db"
}
```

## Сетевые настройки

Порт сервера настраивается через `appsettings.json`:

```json
{
  "Port": 5000
}
```

По умолчанию сервер слушает на `http://0.0.0.0:5000`.

## Режим переподключения

Параметр `ReconnectMode` определяет поведение при сохранении изменений сканера:

| Режим | Описание |
|-------|----------|
| `single` | Переподключается только изменённый сканер (по умолчанию) |
| `all` | Переподключаются все сканеры |

## Параметры переподключения

| Параметр | Диапазон | По умолчанию |
|----------|----------|--------------|
| `ReconnectDelayMs` | 100–60000 мс | 1000 |
| `ReconnectMaxRetries` | 1–10000 | 10 |
| `ReconnectContinuous` | true/false | false |

## Импорт из appsettings.json

При первом запуске, если БД пуста, конфигурация импортируется из `appsettings.json`:

```json
{
  "ReconnectMode": "single",
  "Scanners": [
    {
      "Name": "Main",
      "PortName": "COM2",
      "BaudRate": 9600,
      "DataBits": 8,
      "Parity": "None",
      "StopBits": "One",
      "Handshake": "RequestToSend",
      "ReadTimeout": 5000,
      "WriteTimeout": 5000,
      "Reconnect": {
        "DelayMs": 1000,
        "MaxRetries": 10,
        "Continuous": false
      }
    }
  ]
}
```

## Параметры логирования (Serilog)

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

Уровни логирования: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`

## Windows Service

При запуске на Windows приложение может работать как Windows Service:

```csharp
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ScanBridge";
});
```

## Публикация

```bash
dotnet publish -c Release -o ./publish
```
