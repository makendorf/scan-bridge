# Конфигурация

## Хранение настроек

Все настройки хранятся в SQLite базе данных `scanbridge.db`. Управление — через веб-интерфейс или API.

## Режим переподключения

Параметр `ReconnectMode` определяет поведение при сохранении изменений сканера:

| Режим | Описание |
|-------|----------|
| `single` | Переподключается только изменённый сканер (по умолчанию) |
| `all` | Переподключаются все сканеры |

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
      "WriteTimeout": 5000
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

## Сетевые настройки

По умолчанию сервер слушает на `http://0.0.0.0:5000`. Для изменения — редактируйте `Program.cs`:

```csharp
builder.WebHost.UseUrls("http://0.0.0.0:5000");
```

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
