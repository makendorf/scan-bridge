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
