# ScanBridge — New Post-Scan Actions Implementation Plan

## 1. Implementation Order

| # | Action | Complexity | Dependencies | Est. Lines |
|---|--------|-----------|--------------|------------|
| 1 | Telegram Notification | Low | Telegram.Bot | ~80 |
| 2 | Email Notification | Low | MailKit | ~120 |
| 3 | Data Enrichment | Medium | HttpClient (built-in) | ~130 |
| 4 | Validation | Medium | None (pure logic) | ~200 |
| 5 | Aggregation | High | None (stateful) | ~180 |
| 6 | Database Query | High | MySqlConnector, Npgsql, SqlClient | ~160 |

**Rationale**: Telegram/Email are stateless HTTP sends, identical in pattern to the existing ExportAction's HTTP POST path. Enrichment reuses that HttpClient pattern. Validation mutates ScanResult state — architecturally important but self-contained. Aggregation is stateful (accumulates items in memory with timers). DB Query carries the heaviest dependency tree and connection management concerns.

---

## 2. NuGet Packages

Add to `src/ScanBridge.csproj`:

```xml
<PackageReference Include="Telegram.Bot" Version="22.*" />
<PackageReference Include="MailKit" Version="4.*" />
<PackageReference Include="MySqlConnector" Version="2.*" />
<PackageReference Include="Npgsql" Version="9.*" />
<PackageReference Include="Microsoft.Data.SqlClient" Version="6.*" />
```

---

## 3. Integration Points (same for every action)

Three files require changes for each new action:

### A. `src/Services/PostScanActions/<Name>Action.cs` — new file
- Implements `IPostScanAction`
- `Type => "<Name>"`
- Constructor: `(ILogger<XxxAction> logger, Dictionary<string, string> settings)`
- `ExecuteAsync(ScanResult scan, CancellationToken ct)`

### B. `src/Services/PostScanManager.cs` — add case to switch
```csharp
"Telegram" => new TelegramNotificationAction(
    loggerFactory.CreateLogger<TelegramNotificationAction>(),
    config.Settings),
```

### C. `src/wwwroot/index.html` — two changes
1. Add `<option>` to the `fActionType` select (line ~791)
2. Add entry to `ACTION_TYPES` object (line ~1191)

---

## 4. Per-Action Implementation Details

### 4.1 Telegram Notification

**Settings dictionary keys**:
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| BotToken | string | `""` | Telegram bot token from @BotFather |
| ChatIds | string | `""` | Comma-separated chat/channel IDs |
| MessageTemplate | string | `Scan: {data}` | Template with placeholders |

**Placeholders**: `{data}`, `{format}`, `{scanner}`, `{timestamp}`, `{raw}`

**Constructor logic**: Parse `ChatIds` into `List<long>` by splitting on comma and `long.TryParse`. Create `TelegramBotClient` once and store as field (client is thread-safe, reusable).

**ExecuteAsync**: Format message from template, iterate `_chatIds`, call `_client.SendMessage(chatId, text, cancellationToken: ct)` for each. Wrap in try/catch, log errors, never throw.

**Web UI** (`ACTION_TYPES` entry):
```javascript
Telegram: {
    name: 'Уведомление Telegram',
    settings: [
        { key: 'BotToken', label: 'Токен бота', type: 'text', default: '',
          hint: 'Токен от @BotFather' },
        { key: 'ChatIds', label: 'ID чатов (через запятую)', type: 'text', default: '',
          hint: 'Перешлите сообщение боту @userinfobot чтобы узнать ваш ID' },
        { key: 'MessageTemplate', label: 'Шаблон сообщения', type: 'text',
          default: 'Scan: {data}',
          hint: '{data} — данные\n{format} — формат\n{scanner} — сканер\n{timestamp} — время\n{raw} — исходные' }
    ]
}
```

**Test file**: `tests/Services/TelegramNotificationActionTests.cs`
- Test that missing BotToken logs warning and does not throw
- Test message template placeholder replacement (unit-level string formatting)
- Test with empty ChatIds produces no HTTP calls
- Mock-based test: verify `TelegramBotClient` receives formatted message (requires wrapping client or using a thin interface)

---

### 4.2 Email Notification

**Settings**:
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| SmtpHost | string | `""` | SMTP server hostname |
| SmtpPort | int | `587` | Port |
| SmtpSecurity | string | `StartTls` | None / SslOnConnect / StartTls |
| SmtpUser | string | `""` | Auth username |
| SmtpPass | string | `""` | Auth password |
| From | string | `""` | Sender address |
| To | string | `""` | Comma-separated recipients |
| Subject | string | `Scan: {data}` | Subject template |
| Body | string | `{data} ({format})` | Body template |

**Constructor**: Parse `_toAddresses` list from comma-separated string. Store all SMTP config fields.

**ExecuteAsync**: Build `MimeMessage` from MimeKit, connect via `SmtpClient.ConnectAsync`, authenticate, send, disconnect. Create a fresh `SmtpClient` per call — SMTP connections are cheap and avoiding connection expiry bugs is worth it.

**MailKit connection security mapping**:
```
"None"         -> SecurityPolicy.None
"SslOnConnect" -> SecurityPolicy.SslOnConnect
"StartTls"     -> SecurityPolicy.StartTls
```

**Web UI**:
```javascript
Email: {
    name: 'Уведомление Email',
    settings: [
        { key: 'SmtpHost', label: 'SMTP хост', type: 'text', default: 'smtp.gmail.com' },
        { key: 'SmtpPort', label: 'Порт', type: 'number', default: '587' },
        { key: 'SmtpSecurity', label: 'Шифрование', type: 'select',
          options: ['None', 'SslOnConnect', 'StartTls'], default: 'StartTls' },
        { key: 'SmtpUser', label: 'Логин SMTP', type: 'text', default: '' },
        { key: 'SmtpPass', label: 'Пароль SMTP', type: 'text', default: '',
          hint: 'Для Gmail — пароль приложений' },
        { key: 'From', label: 'Отправитель', type: 'text', default: '' },
        { key: 'To', label: 'Получатели (через запятую)', type: 'text', default: '' },
        { key: 'Subject', label: 'Тема письма', type: 'text', default: 'Scan: {data}',
          hint: '{data} — данные\n{format} — формат\n{scanner} — сканер\n{timestamp} — время' },
        { key: 'Body', label: 'Тело письма', type: 'text', default: '{data} ({format})',
          hint: 'Те же плейсхолдеры' }
    ]
}
```

**Test**: `tests/Services/EmailNotificationActionTests.cs`
- Missing host/credentials: logs warning, does not throw
- Empty To list: no SMTP connection attempted
- Template formatting correctness

---

### 4.3 Data Enrichment

**Settings**:
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| Url | string | `""` | Endpoint URL |
| Method | string | `GET` | GET or POST |
| Headers | string | `{}` | JSON dict of extra headers |
| ResponseField | string | `""` | Which JSON field to extract (empty = whole response) |
| TimeoutSeconds | int | `10` | HTTP timeout |
| QueryParam | string | `data` | Query parameter name for GET, or body field for POST |

**ExecuteAsync flow**:
1. Build URL: if GET, append `?{QueryParam}={Uri.EscapeDataString(scan.ParsedData)}`
2. If POST, send JSON body `{ "{QueryParam}": "{scan.ParsedData}" }`
3. Read response as string
4. If `ResponseField` is set, parse as JSON and extract that field
5. Store result in `scan.ParsedContent` (or a new metadata dictionary — see design decision below)

**Design decision — where to store enriched data**: Since `ScanResult.ParsedContent` is already used for QR content, enrichment results should go into a new field. Add `public Dictionary<string, string> Metadata { get; set; } = new();` to `ScanResult`. This is a small model change but avoids overloading existing fields. The enrichment action writes `scan.Metadata["enriched_key"] = value`.

**Web UI**:
```javascript
DataEnrichment: {
    name: 'Обогащение данных',
    settings: [
        { key: 'Url', label: 'URL', type: 'text', default: '' },
        { key: 'Method', label: 'Метод', type: 'select', options: ['GET', 'POST'], default: 'GET' },
        { key: 'Headers', label: 'Заголовки (JSON)', type: 'text', default: '{}' },
        { key: 'ResponseField', label: 'Поле ответа', type: 'text', default: '',
          hint: 'Извлечь конкретное поле из JSON-ответа. Пусто = весь ответ' },
        { key: 'QueryParam', label: 'Имя параметра', type: 'text', default: 'data',
          hint: 'Имя параметра запроса (GET) или ключ в теле (POST)' },
        { key: 'TimeoutSeconds', label: 'Таймаут (сек)', type: 'number', default: '10' }
    ]
}
```

**Test**: `tests/Services/DataEnrichmentActionTests.cs`
- GET request formatting with special characters in scan data
- POST body construction
- ResponseField extraction from nested JSON
- HTTP timeout handling
- Invalid JSON response logging

---

### 4.4 Validation

**Settings**:
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| ValidationType | string | `regex` | `regex` / `dictionary` / `range` |
| Pattern | string | `""` | Regex pattern (for regex mode) |
| MinLength | int | `0` | Minimum string length |
| MaxLength | int | `9999` | Maximum string length |
| AllowedChars | string | `""` | Whitelist regex for individual chars |
| DictionaryPath | string | `""` | File path or URL to allowed values (one per line) |
| DictionaryUrl | string | `""` | URL for allowed values |
| MinValue | double | `-INF` | Numeric min (range mode) |
| MaxValue | double | `+INF` | Numeric max (range mode) |
| OnFailure | string | `skip` | `skip` (stop chain) or `warn` (log + continue) |

**Key behavior**: This is a **mutating** action. On validation failure:
- `OnFailure = "skip"`: sets `scan.IsValid = false` — subsequent actions in `PostScanManager.ExecuteAllAsync` still run but see `IsValid = false`. Most existing actions already check `if (!scan.IsValid) return;`
- `OnFailure = "warn"`: logs a warning but does NOT modify `scan.IsValid`

**Dictionary loading**: Load from file path or URL on construction. Cache in memory. For URL sources, optionally reload periodically (out of scope for v1 — just load once).

**ExecuteAsync**:
```csharp
var valid = _validationType switch
{
    "regex" => ValidateRegex(scan.ParsedData),
    "dictionary" => ValidateDictionary(scan.ParsedData),
    "range" => ValidateRange(scan.ParsedData),
    _ => true
};

if (!valid && _onFailure == "skip")
{
    scan.IsValid = false;
    _logger.LogWarning("Validation failed: {Data}", scan.ParsedData);
}
```

**Web UI**:
```javascript
Validation: {
    name: 'Валидация',
    settings: [
        { key: 'ValidationType', label: 'Тип', type: 'select',
          options: ['regex', 'dictionary', 'range'], default: 'regex',
          optionLabels: { regex: 'Регулярное выражение', dictionary: 'Словарь значений', range: 'Числовой диапазон' } },
        { key: 'Pattern', label: 'Паттерн regex', type: 'text', default: '^[A-Z0-9]+$',
          showWhen: 'ValidationType=regex' },
        { key: 'MinLength', label: 'Мин. длина', type: 'number', default: '0',
          showWhen: 'ValidationType=regex' },
        { key: 'MaxLength', label: 'Макс. длина', type: 'number', default: '9999',
          showWhen: 'ValidationType=regex' },
        { key: 'DictionaryPath', label: 'Путь к файлу', type: 'text', default: '',
          showWhen: 'ValidationType=dictionary',
          hint: 'Текстовый файл, одно значение на строку' },
        { key: 'DictionaryUrl', label: 'URL словаря', type: 'text', default: '',
          showWhen: 'ValidationType=dictionary',
          hint: 'URL со списком значений (по одному на строку)' },
        { key: 'MinValue', label: 'Мин. значение', type: 'text', default: '',
          showWhen: 'ValidationType=range' },
        { key: 'MaxValue', label: 'Макс. значение', type: 'text', default: '',
          showWhen: 'ValidationType=range' },
        { key: 'OnFailure', label: 'При ошибке', type: 'select',
          options: ['skip', 'warn'], default: 'skip',
          optionLabels: { skip: 'Прервать цепочку', warn: 'Только предупредить' } }
    ]
}
```

**Test**: `tests/Services/ValidationActionTests.cs`
- Regex: valid match passes, invalid sets IsValid=false
- Regex: MinLength/MaxLength enforcement
- Dictionary: known value passes, unknown fails
- Dictionary: file loading from path
- Range: within bounds passes, outside fails
- OnFailure="warn" does not modify IsValid
- OnFailure="skip" sets IsValid=false

---

### 4.5 Aggregation

**Settings**:
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| Mode | string | `count` | `count` / `time` / `signal` |
| CountThreshold | int | `10` | Batch size (count mode) |
| IntervalSeconds | int | `60` | Flush interval (time mode) |
| MaxBufferSize | int | `1000` | Max items before forced flush |
| BatchFormat | string | `json` | `json` (JSON array) or `csv` |

**State management**: This action accumulates `ScanResult` objects in a `ConcurrentQueue<ScanResult>`. It implements `IDisposable` to flush remaining items and cancel timers.

**ExecuteAsync flow**:
- **Count mode**: Enqueue scan. If `_queue.Count >= threshold`, flush — serialize batch, set `scan.ParsedData` to JSON array, emit.
- **Time mode**: Enqueue scan. A background `System.Threading.Timer` fires every N seconds and flushes.
- **Signal mode**: Same as count but with threshold=1 (each scan triggers immediately). The "signal" mode is a placeholder for future webhook/event triggers.

**Flush mechanism**: Dequeue all items, serialize to JSON array of scan summaries (not full ScanResult — just data/format/timestamp/scanner), set as `scan.ParsedData` on the LAST scan's context. This is tricky because downstream actions expect a single scan — the batch output is the *result* written to the last scan's `ParsedData`.

**Architecture note**: Since `IPostScanAction.ExecuteAsync` receives a single scan, aggregation requires careful handling. The batched data overwrites the current scan's `ParsedData`. Downstream actions (like Telegram, Email, Export) will then send the batch instead of a single scan. This is the simplest approach that fits the existing interface without modifying `PostScanManager`.

**Web UI**:
```javascript
Aggregation: {
    name: 'Агрегация',
    settings: [
        { key: 'Mode', label: 'Режим', type: 'select',
          options: ['count', 'time'], default: 'count',
          optionLabels: { count: 'По количеству', time: 'По времени' } },
        { key: 'CountThreshold', label: 'Количество для отправки', type: 'number', default: '10',
          showWhen: 'Mode=count' },
        { key: 'IntervalSeconds', label: 'Интервал (сек)', type: 'number', default: '60',
          showWhen: 'Mode=time' },
        { key: 'MaxBufferSize', label: 'Макс. буфер', type: 'number', default: '1000' },
        { key: 'BatchFormat', label: 'Формат пакета', type: 'select',
          options: ['json', 'csv'], default: 'json' }
    ]
}
```

**Test**: `tests/Services/AggregationActionTests.cs`
- Count mode: N scans queued, Nth scan triggers batch output
- Time mode: items flushed after interval
- MaxBufferSize: forced flush at capacity
- JSON format produces valid array
- CSV format produces comma-separated values

---

### 4.6 Database Query

**Settings**:
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| ConnectionType | string | `mysql` | `mysql` / `postgresql` / `mssql` |
| ConnectionString | string | `""` | ADO.NET connection string |
| QueryTemplate | string | `""` | SQL with `{data}` placeholder |
| ResultField | string | `""` | Which column to extract (empty = first) |
| TimeoutSeconds | int | `30` | Query timeout |

**Architecture**: Use ADO.NET `DbProviderFactory` pattern:
```csharp
private readonly DbProviderFactory _factory;

// In constructor based on ConnectionType:
_factory = connectionType switch
{
    "mysql" => MySqlConnector.MySqlConnectorFactory.Instance,
    "postgresql" => Npgsql.NpgsqlFactory.Instance,
    "mssql" => Microsoft.Data.SqlClient.SqlClientFactory.Instance,
    _ => throw new ArgumentException($"Unsupported: {connectionType}")
};
```

**ExecuteAsync**:
```csharp
using var connection = _factory.CreateConnection();
connection.ConnectionString = _connectionString;
await connection.OpenAsync(ct);

using var cmd = connection.CreateCommand();
cmd.CommandText = _queryTemplate.Replace("{data}", scan.ParsedData);
cmd.CommandTimeout = _timeoutSeconds;

using var reader = await cmd.ExecuteReaderAsync(ct);
if (await reader.ReadAsync(ct))
{
    var value = reader[_resultField ?? 0]?.ToString() ?? "";
    scan.ParsedData = value;  // or store in Metadata
}
```

**Security consideration**: The `{data}` replacement is NOT parameterized — this is a deliberate design choice because scan data is short and predictable. However, document that users should sanitize input if their query is complex. A future enhancement could use `DbParameter` instead.

**Web UI**:
```javascript
DatabaseQuery: {
    name: 'Запрос к БД',
    settings: [
        { key: 'ConnectionType', label: 'Тип БД', type: 'select',
          options: ['mysql', 'postgresql', 'mssql'], default: 'mysql' },
        { key: 'ConnectionString', label: 'Строка подключения', type: 'text', default: '' },
        { key: 'QueryTemplate', label: 'SQL запрос', type: 'text', default: '',
          hint: 'Используйте {data} как плейсхолдер для данных сканирования' },
        { key: 'ResultField', label: 'Поле результата', type: 'text', default: '',
          hint: 'Имя колонки. Пусто = первая колонка' },
        { key: 'TimeoutSeconds', label: 'Таймаут (сек)', type: 'number', default: '30' }
    ]
}
```

**Test**: `tests/Services/DatabaseQueryActionTests.cs`
- Connection string validation on construction
- Query template placeholder replacement
- ResultField extraction from first column vs named column
- Empty query logs warning
- Connection failure handling

---

## 5. ScanResult Model Change

Add a `Metadata` dictionary to support enrichment results and other extensibility:

```csharp
// In src/Models/ScanResult.cs, add:
public Dictionary<string, string> Metadata { get; set; } = new();
```

This is backward-compatible — existing code ignores it, and new actions can write/read arbitrary key-value pairs.

---

## 6. PostScanManager Changes

Add 6 new cases to `CreateAction` switch in `src/Services/PostScanManager.cs`:

```csharp
"Telegram" => new TelegramNotificationAction(
    loggerFactory.CreateLogger<TelegramNotificationAction>(), config.Settings),
"Email" => new EmailNotificationAction(
    loggerFactory.CreateLogger<EmailNotificationAction>(), config.Settings),
"DataEnrichment" => new DataEnrichmentAction(
    loggerFactory.CreateLogger<DataEnrichmentAction>(), config.Settings),
"Validation" => new ValidationAction(
    loggerFactory.CreateLogger<ValidationAction>(), config.Settings),
"Aggregation" => new AggregationAction(
    loggerFactory.CreateLogger<AggregationAction>(), config.Settings),
"DatabaseQuery" => new DatabaseQueryAction(
    loggerFactory.CreateLogger<DatabaseQueryAction>(), config.Settings),
```

---

## 7. Web UI Changes (`src/wwwroot/index.html`)

### A. Add `<option>` elements to `fActionType` select (~line 795):
```html
<option value="Telegram">Уведомление Telegram</option>
<option value="Email">Уведомление Email</option>
<option value="DataEnrichment">Обогащение данных</option>
<option value="Validation">Валидация</option>
<option value="Aggregation">Агрегация</option>
<option value="DatabaseQuery">Запрос к БД</option>
```

### B. Add entries to `ACTION_TYPES` object (~line 1228):
Each entry follows the existing pattern: `{ name: string, settings: [{ key, label, type, default, showWhen?, hint?, options?, optionLabels? }] }`

### C. Settings display format
Currently `renderActions` (line 1263) shows settings as `key=value`. For complex types (Replacements, Tags), the key is excluded. Same approach works for new actions — simple text/number settings display naturally.

---

## 8. Testing Strategy

### Unit tests per action
- Follow existing pattern: `xUnit` + `Moq` for `ILogger<T>`
- Constructor with test settings dictionaries
- `ExecuteAsync` with pre-built `ScanResult` instances
- Assert on log output (via `ILogger` mock verification) and ScanResult state changes

### Integration concerns
- Telegram/Email/DB actions make real network calls — tests should be mock-based or use `TestServer`/`WireMock`
- Aggregation timer tests need careful timing — use `TaskCompletionSource` or mock `TimeProvider`
- For v1, focus on unit tests for template formatting, settings parsing, and error handling. Network integration is manual.

### Test file naming convention
Match existing: `tests/Services/<ActionName>Tests.cs`

---

## 9. Shared Infrastructure

### Template helper (optional)
All notification actions share the same `{data}`, `{format}`, `{scanner}`, `{timestamp}` placeholder logic. A small static helper avoids duplication:

```csharp
// src/Utils/ScanTemplateHelper.cs
public static class ScanTemplateHelper
{
    public static string Format(string template, ScanResult scan)
    {
        return template
            .Replace("{data}", scan.ParsedData)
            .Replace("{raw}", scan.RawData)
            .Replace("{format}", scan.Format)
            .Replace("{scanner}", scan.ScannerName)
            .Replace("{timestamp}", scan.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
    }
}
```

### DB connection factory
The `DatabaseQueryAction` creates connections per execution. No connection pooling is needed beyond what ADO.NET providers provide internally. If performance becomes an issue, a shared `DbConnection` pool could be added as a follow-up.

---

## 10. Files Summary

**New files** (13):
```
src/Services/PostScanActions/TelegramNotificationAction.cs
src/Services/PostScanActions/EmailNotificationAction.cs
src/Services/PostScanActions/DataEnrichmentAction.cs
src/Services/PostScanActions/ValidationAction.cs
src/Services/PostScanActions/AggregationAction.cs
src/Services/PostScanActions/DatabaseQueryAction.cs
src/Utils/ScanTemplateHelper.cs
tests/Services/TelegramNotificationActionTests.cs
tests/Services/EmailNotificationActionTests.cs
tests/Services/DataEnrichmentActionTests.cs
tests/Services/ValidationActionTests.cs
tests/Services/AggregationActionTests.cs
tests/Services/DatabaseQueryActionTests.cs
```

**Modified files** (4):
```
src/Models/ScanResult.cs           — add Metadata dictionary
src/Services/PostScanManager.cs    — add 6 switch cases
src/ScanBridge.csproj              — add 5 NuGet packages
src/wwwroot/index.html             — add 6 options + 6 ACTION_TYPES entries
```
