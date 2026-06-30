# REST API

Базовый URL: `http://localhost:5000`

## Сканеры

### GET /api/scanners

Получить список всех сканеров.

**Ответ:**
```json
[
  {
    "name": "Main",
    "portName": "COM2",
    "baudRate": 9600,
    "dataBits": 8,
    "parity": "None",
    "stopBits": "One",
    "handshake": "RequestToSend",
    "readTimeout": 5000,
    "writeTimeout": 5000,
    "controlCharMode": "0",
    "reconnect": {
      "delayMs": 1000,
      "maxRetries": 10,
      "continuous": false
    }
  }
]
```

### GET /api/scanners/status

Получить статус сканеров.

**Ответ:**
```json
[
  {
    "name": "Main",
    "portName": "COM2",
    "running": true
  }
]
```

### GET /api/scanners/lastscan

Получить время и имя сканера последнего сканирования.

**Ответ:**
```json
{
  "time": "2026-06-24T10:30:00Z",
  "scannerName": "Main"
}
```

### POST /api/scanners

Добавить новый сканер.

**Тело запроса:**
```json
{
  "name": "Сканер 2",
  "portName": "COM3",
  "baudRate": 9600,
  "dataBits": 8,
  "parity": "None",
  "stopBits": "One",
  "handshake": "RequestToSend",
  "readTimeout": 5000,
  "writeTimeout": 5000
}
```

**Ответ:**
```json
{
  "scanners": [...],
  "conflict": null
}
```

### PUT /api/scanners/{index}

Обновить сканер по индексу.

**Параметры:** `index` — позиция в списке (0-based)

**Тело запроса:** аналогично POST

### DELETE /api/scanners/{index}

Удалить сканер по индексу.

### POST /api/scanners/{name}/restart

Перезапустить сканер по имени.

**Параметры:** `name` — имя сканера (URL-encoded)

## Пост-скан действия (группы)

### GET /api/postscan/groups

Получить все группы с действиями и привязками к сканерам.

**Ответ:**
```json
{
  "groups": [
    {
      "id": 1,
      "name": "Основная группа",
      "enabled": true,
      "scannerNames": ["Main"],
      "actions": [
        {
          "id": 1,
          "type": "Log",
          "enabled": true,
          "settings": {}
        }
      ]
    }
  ],
  "enabled": ["Log", "ClipboardPaste"]
}
```

### PUT /api/postscan/groups

Полная перезапись всех групп.

**Тело запроса:**
```json
[
  {
    "name": "Основная группа",
    "enabled": true,
    "scannerNames": ["Main"],
    "actions": [
      {
        "type": "Log",
        "enabled": true,
        "settings": {}
      },
      {
        "type": "Export",
        "enabled": true,
        "settings": {
          "Destination": "folder",
          "FolderPath": "C:\\Output",
          "Format": "json"
        }
      }
    ]
  }
]
```

## Логи

### GET /api/logs?limit=500

Получить записи логов.

**Параметры:** `limit` — максимальное количество записей (по умолчанию 500)

**Ответ:**
```json
[
  {
    "timestamp": "2026-06-24T10:30:00",
    "level": "INF",
    "message": "Штрихкод EAN-13: 5901234123457",
    "exception": null
  }
]
```

### DELETE /api/logs

Очистить все логи.

## Системные

### GET /api/ports

Получить список доступных COM-портов.

**Ответ:**
```json
["COM1", "COM2", "COM3"]
```

## Настройки переподключения

### GET /api/settings/reconnect

Получить режим переподключения.

**Ответ:**
```json
{
  "mode": "single"
}
```

### PUT /api/settings/reconnect

Изменить режим переподключения.

**Тело запроса:**
```json
{
  "mode": "all"
}
```

### GET /api/settings/reconnect/config

Получить параметры переподключения.

**Ответ:**
```json
{
  "delayMs": 1000,
  "maxRetries": 10,
  "continuous": false
}
```

### PUT /api/settings/reconnect/config

Обновить параметры переподключения. Все сканеры перезапускаются для применения.

**Тело запроса:**
```json
{
  "delayMs": 2000,
  "maxRetries": 20,
  "continuous": true
}
```

## Дашборд

### GET /api/dashboard/stats

Получить KPI-статистику для дашборда.

**Ответ:**
```json
{
  "totalScanners": 3,
  "activeScanners": 2,
  "totalScans": 15420,
  "scansToday": 847,
  "successRate": 98.5,
  "dbSizeMb": 1.23
}
```

### GET /api/dashboard/activity

Получить данные активности по часам/дням.

**Параметры:**
- `period` — период: `24h` (по умолчанию), `7d`, `30d`

**Ответ:**
```json
{
  "labels": ["14:00", "15:00", "16:00"],
  "data": [12, 45, 67]
}
```

### GET /api/dashboard/scans

Получить последние сканирования.

**Параметры:**
- `limit` — максимальное количество (по умолчанию 50)

**Ответ:**
```json
[
  {
    "timestamp": "2026-06-29T10:30:00Z",
    "scannerName": "Main",
    "format": "EAN-13",
    "parsedData": "5901234123457",
    "isValid": true
  }
]
```

### GET /api/dashboard/formats

Получить распределение по форматам штрихкодов.

**Ответ:**
```json
{
  "labels": ["EAN-13", "QR", "Code128"],
  "data": [1200, 450, 890],
  "colors": ["#3B82F6", "#10B981", "#F59E0B"]
}
```

### GET /api/dashboard/per-scanner

Получить статистику по каждому сканеру.

**Ответ:**
```json
[
  {
    "name": "Main",
    "total": 5420,
    "valid": 5380,
    "lastScan": "2026-06-29T10:30:00Z",
    "isActive": true,
    "uptime": "2ч 15м",
    "uptimeSeconds": 8100,
    "avgScansPerHour": 12.5
  }
]
```

### GET /api/dashboard/reconnects

Получить ошибки переподключения.

**Параметры:**
- `hours` — период в часах (по умолчанию 24)

**Ответ:**
```json
[
  {
    "timestamp": "2026-06-29T10:15:00Z",
    "scannerName": "Main",
    "errorMessage": "The port 'COM2' does not exist.",
    "attemptNumber": 3
  }
]
```
