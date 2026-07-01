# ScanBridge Comprehensive Refactoring Plan

> Generated: 2026-07-01 | Scope: Backend + Frontend + Tests
> Estimated total effort: ~40-50 hours across 7 phases

---

## Table of Contents

1. [Phase 1: Test Infrastructure & Critical Coverage](#phase-1-test-infrastructure--critical-coverage)
2. [Phase 2: PostScanManager Decomposition](#phase-2-postscanmanager-decomposition)
3. [Phase 3: Backend Service Decomposition](#phase-3-backend-service-decomposition)
4. [Phase 4: Frontend — API Layer & Error Handling](#phase-4-frontend--api-layer--error-handling)
5. [Phase 5: Frontend — Global State Elimination](#phase-5-frontend--global-state-elimination)
6. [Phase 6: Frontend — Module Extraction](#phase-6-frontend--module-extraction)
7. [Phase 7: Integration Tests & Final Cleanup](#phase-7-integration-tests--final-cleanup)

---

## Phase 1: Test Infrastructure & Critical Coverage

**Goal**: Establish integration test infrastructure and cover the most dangerous untested code.

**Why first**: PostScanManager (319 lines, ZERO tests) is the central orchestration point. Every feature flows through it. Without tests here, any subsequent refactoring is a blind gamble.

### 1.1 — Fix Namespace Inconsistency

Test files use two different namespaces (`Tests.Services` vs `ScanBridge.Tests.Services`). The test project has `<RootNamespace>ScanBridge.Tests</RootNamespace>`, so all tests should use `ScanBridge.Tests.*`.

**Files to modify:**
- `tests/Services/ScenarioExecutorTests.cs` — change `namespace Tests.Services;` → `namespace ScanBridge.Tests.Services;`
- `tests/Services/ScenarioServiceTests.cs` — same fix
- `tests/Services/ConditionEvaluatorTests.cs` — same fix
- `tests/Services/AggregationActionTests.cs` — same fix (if affected)
- `tests/Services/CollectorSinkTests.cs` — same fix (if affected)
- `tests/Services/DataEnrichmentActionTests.cs` — same fix (if affected)
- `tests/Services/DatabaseQueryActionTests.cs` — same fix (if affected)
- `tests/Services/EmailNotificationActionTests.cs` — same fix (if affected)
- `tests/Services/LogCollectorTests.cs` — same fix (if affected)
- `tests/Services/SaveToFileActionTests.cs` — same fix (if affected)
- `tests/Services/ScanProcessorServiceTests.cs` — same fix (if affected)
- `tests/Services/TelegramNotificationActionTests.cs` — same fix (if affected)
- `tests/Services/ValidationActionTests.cs` — same fix (if affected)

**Verification**: `dotnet test` passes, no namespace warnings.

### 1.2 — Add WebApplicationFactory Integration Test Infrastructure

**New files to create:**
- `tests/Integration/IntegrationTestBase.cs` — base class using `WebApplicationFactory<Program>` with in-memory SQLite
- `tests/Integration/ScanBridgeWebApplicationFactory.cs` — custom factory overriding `AppDbContext` with `InMemory` provider, seeding test data

**Key design decisions:**
- Use `Microsoft.AspNetCore.Mvc.Testing` package (add to `tests/ScanBridge.Tests.csproj`)
- In-memory SQLite (`DataSource=:memory:`) per test — real DB behavior without file cleanup
- Override DI registrations to mock external services (SFTP, SMTP, Telegram)
- Each test gets fresh scope → fresh DbContext → isolated state

**Files to modify:**
- `tests/ScanBridge.Tests.csproj` — add `Microsoft.AspNetCore.Mvc.Testing` package reference

### 1.3 — PostScanManager Tests (CRITICAL)

`PostScanManager` (319 lines, 13 methods) is the central integration point with ZERO tests. It orchestrates group loading, scenario compilation, scanner matching, and execution dispatch.

**New file**: `tests/Services/PostScanManagerTests.cs`

**Test cases to cover:**

```
Configure_EmptyList_NoGroupsLoaded
Configure_GroupsLoaded_CorrectCount
Configure_GroupsWithScannerFilter_MatchesCorrectly
Configure_GroupWithEmptyScanners_MatchesAll
ExecuteAllAsync_NoGroups_DoesNothing
ExecuteAllAsync_MatchingGroup_CallsActions
ExecuteAllAsync_NonMatchingGroup_Skipped
ExecuteAllAsync_DisabledGroup_Skipped
ExecuteAllAsync_MultipleGroups_AllMatchingExecuted
ExecuteAllAsync_ActionThrows_ContinuesOtherGroups
ExecuteAllAsync_Cancellation_StopsExecution
GetEnabledActions_ReturnsActionTypes
GetEnabledActions_EmptyGroups_ReturnsEmpty
ConfigureScenarios_LoadsScenarios
ConfigureScenarios_EmptyList_NoScenarios
ExecuteAllAsync_Scenario_CallsExecutor
ExecuteAllAsync_ScenarioNonMatching_Skipped
MatchesScanner_ExactMatch_ReturnsTrue
MatchesScanner_CaseInsensitive_ReturnsTrue
MatchesScanner_EmptyList_ReturnsTrue
MatchesScanner_NoMatch_ReturnsFalse
```

**Approach:**
- Mock `IPostScanActionFactory` to return verifiable action mocks
- Mock `ScenarioExecutor` to verify compilation/execution calls
- Use `ScanResult` with various scanner names for matching tests
- Test `CompiledGroup` and `CompiledAction` internal state after `Configure()`

### 1.4 — PostScanActionFactory Tests

**New file**: `tests/Services/PostScanActionFactoryTests.cs`

```
Create_Log_ReturnsLogAction
Create_Replacement_ReturnsReplacementAction
Create_ClipboardPaste_ReturnsClipboardPasteAction
Create_Export_ReturnsExportAction
Create_Telegram_ReturnsTelegramAction
Create_Email_ReturnsEmailAction
Create_DataEnrichment_ReturnsDataEnrichmentAction
Create_Validation_ReturnsValidationAction
Create_Aggregation_ReturnsAggregationAction
Create_DatabaseQuery_ReturnsDatabaseQueryAction
Create_Pause_ReturnsPauseAction
Create_Unknown_ReturnsNull
```

### 1.5 — ScanHistoryService Tests

`ScanHistoryService` (424 lines, 12 methods, ZERO tests) is a singleton that records scans, reconnects, and provides dashboard data.

**New file**: `tests/Services/ScanHistoryServiceTests.cs`

**Test cases (using in-memory DbContext):**

```
RecordScan_InsertsRecord
RecordScan_NullData_HandledGracefully
RecordReconnect_InsertsReconnectEvent
RecordReconnect_IncrementsAttempt
CleanupOldRecordsAsync_RemovesOldRecords
CleanupOldRecordsAsync_KeepsRecentRecords
GetStatsAsync_ReturnsCorrectCounts
GetStatsAsync_EmptyDb_ReturnsZeros
GetActivityAsync_Day_ReturnsDailyBuckets
GetActivityAsync_Week_ReturnsWeeklyBuckets
GetRecentScansAsync_LimitsResults
GetRecentScansAsync_DefaultLimit50
GetFormatsAsync_ReturnsFormatBreakdown
GetPerScannerAsync_GroupsByScanner
GetReconnectErrorsAsync_FiltersByHours
FormatUptime_SmallTimespan
FormatUptime_LargeTimespan
```

**Approach:**
- Use `DbContextOptions<AppDbContext>` with InMemory provider
- Create real `ScanHistoryService` with `IServiceScopeFactory` mocked to return scope with test DbContext
- Seed data via DbContext directly, then verify query results

### 1.6 — Missing Action Tests

**New files:**
- `tests/Services/LogActionTests.cs` — LogAction is simple (writes to ILogger) but should have smoke tests
- `tests/Services/ClipboardPasteActionTests.cs` — mock Win32Clipboard, verify mode logic
- `tests/Services/WindowPasteActionTests.cs` — mock Win32Clipboard, test window title matching
- `tests/Services/PauseActionTests.cs` — verify delay with mocked Task.Delay
- `tests/Services/ReplacementActionTests.cs` — this is the complex one (189 lines, 3 modes):
  ```
  ExecuteAsync_ModeAll_ReplacesAllOccurrences
  ExecuteAsync_ModeStart_ReplacesOnlyAtStart
  ExecuteAsync_ModeEnd_ReplacesOnlyAtEnd
  ExecuteAsync_NoMatchingRules_NoChange
  ExecuteAsync_MultipleRules_AppliedInOrder
  ExecuteAsync_EmptyData_NoChange
  ExecuteAsync_ComplexRegex_Replacement
  ```

### 1.7 — Model Tests

**New file**: `tests/Models/PostScanActionConfigTests.cs`
```
Defaults_CorrectValues
Settings_Null_DefaultsEmpty
Settings_WithValues_Deserialized
```

**New file**: `tests/Models/ScenarioConfigTests.cs`
```
Defaults_CorrectValues
ScannerNames_Empty_MatchesAll
Nodes_Empty_DefaultsEmpty
```

**Verification for Phase 1:**
```bash
dotnet test --verbosity normal
# Expected: ~230+ tests, all passing
# Coverage: PostScanManager, ScanHistoryService, PostScanActionFactory, all actions
```

---

## Phase 2: PostScanManager Decomposition

**Goal**: Break the 319-line god class into focused, testable components.

**Why second**: PostScanManager does 3 things: (1) group management, (2) scenario management, (3) scan dispatch. These are independent concerns that should be separate classes.

### 2.1 — Extract ScanDispatcher

The core responsibility of PostScanManager — determining what to execute on a scan — should be its own class.

**New file**: `src/Services/ScanDispatcher.cs`
```csharp
public class ScanDispatcher
{
    private readonly IPostScanActionFactory _factory;
    private readonly ScenarioExecutor _scenarioExecutor;
    private volatile IReadOnlyList<CompiledGroup> _groups = Array.Empty<CompiledGroup>();
    private volatile IReadOnlyList<CompiledScenario> _scenarios = Array.Empty<CompiledScenario>();

    public void ConfigureGroups(List<PostScanActionGroupConfig> configs) { ... }
    public void ConfigureScenarios(List<ScenarioConfig> configs) { ... }
    public async Task ExecuteAllAsync(ScanResult scan, CancellationToken ct) { ... }
    public IReadOnlyList<string> GetEnabledActions() { ... }
}
```

**What moves from PostScanManager:**
- `_groups` field + `Configure()` body → `ConfigureGroups()`
- `_scenarios` field + `ConfigureScenarios()` body
- `ExecuteAllAsync()`, `ExecuteGroupAsync()`, `ExecuteScenarioAsync()`, `ExecuteNodeAsync()`
- `MatchesScanner()` overloads
- `GetEnabledActions()`

**PostScanManager becomes a thin facade:**
```csharp
public class PostScanManager
{
    private readonly ScanDispatcher _dispatcher;
    public void Configure(...) => _dispatcher.ConfigureGroups(...);
    public void ConfigureScenarios(...) => _dispatcher.ConfigureScenarios(...);
    public void ReloadScenarios(...) { ... }
    public Task ExecuteAllAsync(...) => _dispatcher.ExecuteAllAsync(...);
}
```

**Why keep PostScanManager**: It's registered in DI and referenced from `Program.cs`, `ScannerEndpoints`, `PostScanEndpoints`, and `ScannerManager`. Keeping it as a facade avoids a massive DI refactor while the real logic lives in `ScanDispatcher`.

### 2.2 — Extract GroupManager

Group-specific CRUD logic that's currently mixed into `PostScanEndpoints.cs` (reading groups from DB, building configs) should move to a dedicated service.

**New file**: `src/Services/GroupManager.cs`
```csharp
public class GroupManager
{
    public List<PostScanActionGroupConfig> LoadGroups(AppDbContext db) { ... }
    public PostScanActionGroupConfig? GetById(AppDbContext db, int id) { ... }
    public async Task CreateAsync(AppDbContext db, PostScanActionGroupConfig config) { ... }
    public async Task UpdateAsync(AppDbContext db, int id, PostScanActionGroupConfig config) { ... }
    public async Task DeleteAsync(AppDbContext db, int id) { ... }
    public async Task ReorderAsync(AppDbContext db, int[] orderedIds) { ... }
}
```

**What moves from Program.cs and PostScanEndpoints.cs:**
- The group loading query from `Program.cs:81-104` → `GroupManager.LoadGroups()`
- Group CRUD operations from `PostScanEndpoints.cs`

### 2.3 — Move Scenario Loading to ScenarioService

Currently `PostScanManager.ConfigureScenarios()` and `ReloadScenarios()` manually load scenario configs from `ScenarioService`. This should be encapsulated.

**Files to modify:**
- `src/Services/VisualScripting/ScenarioService.cs` — add `LoadAndCompile(ScenarioExecutor executor)` method that returns compiled scenarios
- `src/Services/PostScanManager.cs` — `ReloadScenarios()` calls `scenarioService.LoadAndCompile(executor)` instead of doing the work inline

### 2.4 — Add Tests for New Components

**New files:**
- `tests/Services/ScanDispatcherTests.cs` — port existing PostScanManager test cases + add new ones
- `tests/Services/GroupManagerTests.cs` — CRUD operations against in-memory DB

**Verification:**
```bash
dotnet build  # Clean compilation
dotnet test   # All tests pass, new tests for extracted components
# PostScanManager shrinks from 319 → ~60 lines (facade only)
# ScanDispatcher ~250 lines (focused on dispatch)
# GroupManager ~100 lines (focused on CRUD)
```

---

## Phase 3: Backend Service Decomposition

**Goal**: Break down large services and fix remaining code quality issues.

### 3.1 — ScanHistoryService Decomposition

`ScanHistoryService` (424 lines) mixes recording, querying, and formatting. Split into:

**New file**: `src/Services/ScanRecorder.cs`
- `RecordScan()`, `RecordReconnect()` — write operations

**New file**: `src/Services/ScanQueryService.cs`
- `GetStatsAsync()`, `GetActivityAsync()`, `GetRecentScansAsync()`, `GetFormatsAsync()`, `GetPerScannerAsync()`, `GetReconnectErrorsAsync()` — read operations

**Modify**: `src/Services/ScanHistoryService.cs` — becomes a facade delegating to `ScanRecorder` + `ScanQueryService`

**Why**: Read/write separation allows independent scaling and makes the recording path (hot path, called per-scan) lighter.

### 3.2 — SaveToFileAction Decomposition

`SaveToFileAction` (272 lines) handles 4 destinations (folder, FTP, SFTP, HTTP) in one class with a massive switch statement. Apply Strategy pattern.

**New files:**
- `src/Services/PostScanActions/Export/IExportStrategy.cs` — interface with `UploadAsync(byte[] data, string filename, Dictionary<string, string> settings)`
- `src/Services/PostScanActions/Export/FolderExportStrategy.cs`
- `src/Services/PostScanActions/Export/FtpExportStrategy.cs`
- `src/Services/PostScanActions/Export/SftpExportStrategy.cs`
- `src/Services/PostScanActions/Export/HttpExportStrategy.cs`

**Modify**: `src/Services/PostScanActions/SaveToFileAction.cs` — becomes ~60 lines, delegates to strategy based on `Destination` setting

### 3.3 — Fix Remaining Code Quality Issues

**Files to modify:**

1. **`src/Services/PostScanActions/WindowPasteAction.cs`** — Fix memory leak: `Marshal.FreeHGlobal` not called in all paths. Wrap P/Invoke allocations in try/finally.

2. **`src/Services/PostScanActions/DataEnrichmentAction.cs`** — Use `IHttpClientFactory` instead of creating `HttpClient` per request. Register in `Program.cs`:
   ```csharp
   builder.Services.AddHttpClient();
   ```
   Inject `IHttpClientFactory` into `DataEnrichmentAction` via constructor.

3. **`src/Services/ScanHistoryService.cs`** — Fix `GetDbSizeAsync()` fragile connection string parsing (`Split('=').LastOrDefault()`). Use `Microsoft.Data.Sqlite` connection builder or regex.

4. **Silent catch blocks** — Find all empty `catch {}` blocks and add `Log.Warning(ex, "...")` at minimum. Affected files:
   - `src/Services/ScanProcessorService.cs`
   - `src/Services/SerialPortService.cs`
   - `src/Services/PostScanActions/ValidationAction.cs` (dictionary loading)

5. **`src/Services/PostScanActions/AggregationAction.cs`** — Verify thread safety if scans can arrive concurrently (Aggregation buffers state).

### 3.4 — Add Tests for Decomposed Services

**New files:**
- `tests/Services/Export/FolderExportStrategyTests.cs`
- `tests/Services/Export/FtpExportStrategyTests.cs` (mock `IFtpClient`)
- `tests/Services/Export/SftpExportStrategyTests.cs` (mock `ISftpClient`)
- `tests/Services/Export/HttpExportStrategyTests.cs` (use `MockHttpMessageHandler`)
- `tests/Services/ScanRecorderTests.cs`
- `tests/Services/ScanQueryServiceTests.cs`

**Verification:**
```bash
dotnet build  # Clean compilation
dotnet test   # All tests pass
# SaveToFileAction: 272 → ~60 lines + 4 strategy classes ~50 lines each
# ScanHistoryService: 424 → ~80 lines (facade) + 2 focused classes
```

---

## Phase 4: Frontend — API Layer & Error Handling

**Goal**: Create a centralized API layer and add consistent error handling.

**Why before module extraction**: The API layer is cross-cutting — every page uses it. Establishing it first makes subsequent module extraction cleaner.

### 4.1 — Create API Client Module

**New file**: `src/wwwroot/js/api.js`

```javascript
/* ── ScanBridge API Client ── */

const Api = {
    async get(url) {
        const res = await fetch(url);
        if (!res.ok) throw new ApiError(res.status, await res.text());
        return res.json();
    },

    async post(url, data) {
        const res = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
        if (!res.ok) throw new ApiError(res.status, await res.text());
        return res.json();
    },

    async put(url, data) {
        const res = await fetch(url, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
        if (!res.ok) throw new ApiError(res.status, await res.text());
        return res.json();
    },

    async delete(url) {
        const res = await fetch(url, { method: 'DELETE' });
        if (!res.ok) throw new ApiError(res.status, await res.text());
    }
};

class ApiError extends Error {
    constructor(status, body) {
        super(`API error ${status}: ${body}`);
        this.status = status;
        this.body = body;
    }
}
```

### 4.2 — Migrate All fetch() Calls to Api Client

**Files to modify** (replace bare `fetch()` with `Api.*` calls):

| File | Current fetch calls | Change to |
|------|-------------------|-----------|
| `scanners.js` | 5 fetch calls (load, save reconnect, save config, add scanner, delete scanner, save settings) | `Api.get()`, `Api.put()`, `Api.post()`, `Api.delete()` |
| `actions.js` | 3 fetch calls (loadGroups, saveGroups, delete group) | `Api.get()`, `Api.post()`, `Api.delete()` |
| `scenarios.js` | 4 fetch calls (load, create, update, delete) | `Api.get()`, `Api.post()`, `Api.put()`, `Api.delete()` |
| `scenario-editor.js` | 2 fetch calls (load scenario, save scenario) | `Api.get()`, `Api.post()` |
| `dashboard.js` | 5 fetch calls (stats, activity, recent, formats, per-scanner, reconnect-errors) | `Api.get()` |
| `logs.js` | 1 fetch call (load logs) | `Api.get()` |
| `app.js` | 0 fetch calls | (none) |

### 4.3 — Add Error Handling Pattern

**Modify**: `src/wwwroot/js/utils.js` — add error toast helper:

```javascript
function handleApiError(error, context) {
    console.error(`${context}:`, error);
    if (error instanceof ApiError && error.status >= 500) {
        showToast('Сервер недоступен. Попробуйте позже.', 'error');
    } else if (error instanceof ApiError) {
        showToast(`Ошибка: ${error.message}`, 'error');
    } else {
        showToast('Неожиданная ошибка', 'error');
    }
}
```

**Modify**: Each page's load function to wrap in try/catch:
```javascript
// Before:
async function loadGroups() {
    const res = await fetch('/api/postscan/groups');
    postScanGroups = await res.json();
    renderGroupCards();
}

// After:
async function loadGroups() {
    try {
        postScanGroups = await Api.get('/api/postscan/groups');
        renderGroupCards();
    } catch (e) {
        handleApiError(e, 'Загрузка групп');
    }
}
```

### 4.4 — Add Input Validation Before API Calls

**Modify**: `src/wwwroot/js/scanners.js` — validate scanner form before save:
```javascript
function validateScannerForm() {
    const name = document.getElementById('scannerName')?.value?.trim();
    const port = document.getElementById('scannerPort')?.value?.trim();
    if (!name) { showToast('Введите имя сканера', 'error'); return false; }
    if (!port) { showToast('Выберите COM-порт', 'error'); return false; }
    return true;
}
```

**Modify**: `src/wwwroot/js/actions.js` — validate group settings before save
**Modify**: `src/wwwroot/js/scenario-editor.js` — validate scenario name before save

### 4.5 — Add api.js to Script Load Order

**Modify**: `src/wwwroot/layout.html` — add `<script src="/js/api.js"></script>` BEFORE all other JS files (after utils.js)

**Verification:**
```bash
dotnet build
# Open browser, navigate each page
# Verify: no console errors, toast shows on API failures
# Verify: forms validate before submission
# Verify: all CRUD operations work through Api client
```

---

## Phase 5: Frontend — Global State Elimination

**Goal**: Replace 22+ mutable global variables with module-scoped state and a simple state management pattern.

**Why**: Globals cause invisible coupling between files, make testing impossible, and create bugs when SPA pages don't clean up.

### 5.1 — Create State Manager

**New file**: `src/wwwroot/js/state.js`

```javascript
/* ── ScanBridge State Manager ── */

const AppState = {
    _state: {
        scanners: [],
        postScanGroups: [],
        scenarios: [],
        currentGroupId: null,
        editingScenarioId: null,
        drawflowEditor: null,
        selectedNodeId: null,
        editingScenarioConfig: null,
        logEntries: [],
        replacementRules: [],
        tagRules: [],
    },
    _listeners: {},

    get(key) { return this._state[key]; },

    set(key, value) {
        this._state[key] = value;
        this._notify(key, value);
    },

    on(key, callback) {
        if (!this._listeners[key]) this._listeners[key] = [];
        this._listeners[key].push(callback);
    },

    _notify(key, value) {
        (this._listeners[key] || []).forEach(cb => cb(value));
    },

    reset() {
        // Reset to defaults — called on page navigation
        this._state.scanners = [];
        this._state.postScanGroups = [];
        this._state.currentGroupId = null;
        // ... etc
    }
};
```

### 5.2 — Migrate Global Variables to AppState

**Files to modify:**

| File | Globals to migrate | Migration approach |
|------|-------------------|-------------------|
| `scanners.js` | `let scanners = []` | `AppState.set('scanners', [])` — replace reads with `AppState.get('scanners')` |
| `actions.js` | `let postScanGroups = []`, `let currentGroupId = null`, `let replacementRules = []`, `let tagRules = []` | Same pattern |
| `scenarios.js` | `let scenarios = []`, `let editingScenarioId = null` | Same pattern |
| `scenario-editor.js` | `let drawflowEditor = null`, `let selectedNodeId = null`, `let editingScenarioConfig = null`, `let NODE_COUNTER = 0` | Same pattern; `drawflowEditor` keep as module-level (Drawflow instance, not serializable) |
| `logs.js` | `let logEntries = []` | Same pattern |
| `dashboard.js` | Check for globals | Same pattern |

**Migration strategy (per file):**
1. Remove `let variableName = []` declaration
2. Replace reads: `variableName` → `AppState.get('key')`
3. Replace writes: `variableName = value` → `AppState.set('key', value)`
4. Keep page-specific DOM references as local variables (not state)

### 5.3 — Add State Cleanup on Navigation

**Modify**: `src/wwwroot/js/router.js` — call `AppState.reset()` when leaving pages that own state:
```javascript
// In navigate():
if (this.currentPage !== page) {
    // Clean up page-specific state
    if (this.currentPage === 'actions') {
        AppState.set('currentGroupId', null);
    }
}
```

### 5.4 — Keep drawflowEditor Separate

`drawflowEditor` is a Drawflow instance with methods — it can't be serialized. Keep it as a module-level variable in `scenario-editor.js`, but ensure it's cleaned up on navigation (already done in `router.js:27-29`).

**Verification:**
```bash
dotnet build
# Test: navigate between all pages, verify no stale state
# Test: scanners page loads, actions page loads with correct groups
# Test: scenario editor loads, edits, saves, navigates away cleanly
# Test: no "Cannot read property of undefined" errors in console
```

---

## Phase 6: Frontend — Module Extraction

**Goal**: Break monolithic JS files into focused modules, reducing `actions.js` (587 lines) and `scenario-editor.js` (602 lines).

### 6.1 — Extract Action Definitions from actions.js

**Current**: `actions.js` contains ACTION_TYPES config (196 lines), TAG_SOURCES (10 lines), CRUD operations, and UI rendering all in one file.

**New file**: `src/wwwroot/js/action-config.js`
- Move `ACTION_TYPES` constant
- Move `TAG_SOURCES` constant
- Move `ACTION_ICONS` (currently in scenario-editor.js)
- Move helper functions: `updateActionSettings()`, `applyShowWhen()`, `renderReplacements()`, `addReplacement()`, `removeReplacement()`, `collectReplacements()`, `renderTags()`, `addTag()`, `removeTag()`, `collectTags()`, `getActionSettings()`

**Result**: `actions.js` goes from 587 → ~300 lines (CRUD + rendering only)

### 6.2 — Extract Scenario Editor Helpers

**Current**: `scenario-editor.js` (602 lines) mixes node templates, Drawflow init, modal management, settings CRUD, validation, and save logic.

**New file**: `src/wwwroot/js/scenario-templates.js`
- Move `ACTION_ICONS` constant
- Move `VS_NODE_TEMPLATES` object
- Move `PALETTE_NODE_TYPES` array
- Move `createActionTemplate()` function

**New file**: `src/wwwroot/js/scenario-validation.js`
- Move `validateScenario()` function
- Move validation helper functions

**New file**: `src/wwwroot/js/scenario-settings.js`
- Move `openNodeSettingsModal()`, `closeNodeSettingsModal()`, `saveNodeSettingsFromModal()`
- Move `getDrawflowNodeSettings()`, `loadNodeSettingsToUI()`, `saveNodeSettingsFromUI()`
- Move `renderWhileConditions()`, `addWhileCondition()`, `removeWhileCondition()`, `collectWhileConditions()`

**Result**: `scenario-editor.js` goes from 602 → ~200 lines (Drawflow init, import/export, save orchestration)

### 6.3 — Extract Card Rendering Helpers

Several files contain duplicated card rendering patterns (group cards, scenario cards, scanner cards).

**New file**: `src/wwwroot/js/components.js`
```javascript
/* ── Shared UI Components ── */

function renderCard(container, options) { ... }
function renderEmptyState(container, message) { ... }
function renderBadge(text, type) { ... }
function confirmDelete(message) { return confirm(message); }
```

### 6.4 — Replace Inline onclick Handlers

Currently `layout.html` and page HTMLs use inline `onclick` attributes. These create tight coupling.

**Files to modify:**
- `src/wwwroot/layout.html` — replace `onclick="navigateTo('...')"` with `data-page` attributes + event delegation
- `pages/actions.html` — replace `onclick="addGroup()"` etc. with `data-action` attributes
- `pages/scanners.html` — same pattern
- `pages/scenarios.html` — same pattern

**Approach**: Add event delegation in `app.js`:
```javascript
document.addEventListener('click', (e) => {
    const action = e.target.closest('[data-action]')?.dataset.action;
    if (!action) return;
    const handlers = { addGroup, removeGroup, saveGroups, ... };
    if (handlers[action]) handlers[action]();
});
```

### 6.5 — Update Script Load Order

**Modify**: `src/wwwroot/layout.html` — update `<script>` tags:
```html
<!-- Order matters: utils → api → state → config → components → page modules → app -->
<script src="/js/utils.js"></script>
<script src="/js/api.js"></script>
<script src="/js/state.js"></script>
<script src="/js/action-config.js"></script>
<script src="/js/components.js"></script>
<script src="/js/scenario-templates.js"></script>
<script src="/js/scenario-validation.js"></script>
<script src="/js/scenario-settings.js"></script>
<script src="/js/actions.js"></script>
<script src="/js/scenario-editor.js"></script>
<script src="/js/scenarios.js"></script>
<script src="/js/scanners.js"></script>
<script src="/js/dashboard.js"></script>
<script src="/js/logs.js"></script>
<script src="/js/router.js"></script>
<script src="/js/app.js"></script>
```

**Verification:**
```bash
dotnet build
# Test: all pages load correctly
# Test: actions page — CRUD works, settings render correctly
# Test: scenario editor — drag-drop, node settings, save/load
# Test: no "function is not defined" errors in console
# Test: no script load order issues
```

---

## Phase 7: Integration Tests & Final Cleanup

**Goal**: Add API contract tests and clean up remaining issues.

### 7.1 — API Integration Tests

Using the `WebApplicationFactory` infrastructure from Phase 1, add endpoint-level tests.

**New files:**

`tests/Integration/ScannerEndpointsTests.cs`
```
GetScanners_ReturnsOk
GetScanners_ReturnsJsonArray
AddScanner_ValidConfig_ReturnsCreated
AddScanner_DuplicateName_ReturnsBadRequest
DeleteScanner_Nonexistent_ReturnsNotFound
```

`tests/Integration/PostScanEndpointsTests.cs`
```
GetGroups_ReturnsOk
CreateGroup_ValidConfig_ReturnsCreated
UpdateGroup_Existing_ReturnsOk
DeleteGroup_Existing_ReturnsNoContent
```

`tests/Integration/ScenarioEndpointsTests.cs`
```
GetScenarios_ReturnsOk
CreateScenario_ValidConfig_ReturnsCreated
UpdateScenario_Existing_ReturnsOk
DeleteScenario_Existing_ReturnsNoContent
ValidateScenario_ValidGraph_ReturnsOk
```

`tests/Integration/SettingsEndpointsTests.cs`
```
GetReconnectMode_ReturnsOk
UpdateReconnectMode_ValidMode_ReturnsOk
GetReconnectConfig_ReturnsOk
UpdateReconnectConfig_ValidConfig_ReturnsOk
```

`tests/Integration/DashboardEndpointsTests.cs`
```
GetStats_ReturnsOk
GetActivity_ReturnsOk
GetRecentScans_ReturnsOk
GetFormats_ReturnsOk
```

`tests/Integration/LogEndpointsTests.cs`
```
GetLogs_ReturnsOk
```

`tests/Integration/PortEndpointsTests.cs`
```
GetPorts_ReturnsOk
```

### 7.2 — ScenarioService CRUD Tests

Currently `ScenarioService` has validation tests but no CRUD tests (Create/Update/Delete/GetAll/GetById).

**Add to existing file**: `tests/Services/ScenarioServiceTests.cs`
```
Create_ValidConfig_SavesToDb
Create_ValidConfig_ReturnsId
GetAll_ReturnsAllScenarios
GetAll_EnabledOnly_ReturnsEnabled
GetById_Existing_ReturnsScenario
GetById_Nonexistent_ReturnsNull
GetAllWithGraph_IncludesNodesAndConnections
Update_Existing_UpdatesFields
Update_Nonexistent_ReturnsFalse
Delete_Existing_RemovesFromDb
Delete_Nonexistent_ReturnsFalse
Delete_HasNodes_DeletesCascade
```

### 7.3 — QRContentDetector Dedicated Tests

Currently `QRContentDetector` is only tested indirectly through `SimpleBarcodeParserTests`.

**New file**: `tests/Parsers/QRContentDetectorTests.cs`
```
Detect_Url_ReturnsUrlContentType
Detect_Json_ReturnsJsonContentType
Detect_VCard_ReturnsVCardContentType
Detect_Wifi_ReturnsWifiContentType
Detect_PlainText_ReturnsTextContentType
Detect_Empty_ReturnsTextContentType
Detect_InvalidJson_GracefulFallback
ParseContent_Url_ReturnsUrl
ParseContent_Json_ReturnsParsedJson
ParseContent_VCard_ReturnsFields
ParseContent_Wifi_ReturnsSsid
```

### 7.4 — Cleanup

**Files to modify:**
- `tests/ScanBridge.Tests.csproj` — add `Microsoft.AspNetCore.Mvc.Testing` (if not already from Phase 1), `Microsoft.Extensions.Http` for HttpClientFactory tests
- Remove any dead code identified during refactoring
- Verify all `// NOT YET FIXED` items from MEMORY-refactoring-history.md are addressed or intentionally deferred

### 7.5 — Update Memory

**Modify**: `MEMORY.md` — update architecture section with new component structure:
```
- ScanDispatcher replaces PostScanManager for execution logic
- GroupManager handles group CRUD
- ScanRecorder / ScanQueryService replace ScanHistoryService
- Strategy pattern for export destinations
- Frontend: Api client, AppState manager, extracted modules
```

**Verification:**
```bash
dotnet test --verbosity normal
# Expected: ~350+ tests, all passing
# Coverage: all API endpoints, all CRUD operations, QRContentDetector
# No build warnings, no test namespace inconsistencies
```

---

## Summary: What Changes Where

### Backend Files Created (17 new)
| Phase | File | Lines (est.) |
|-------|------|-------------|
| 2 | `src/Services/ScanDispatcher.cs` | ~250 |
| 2 | `src/Services/GroupManager.cs` | ~100 |
| 3 | `src/Services/ScanRecorder.cs` | ~80 |
| 3 | `src/Services/ScanQueryService.cs` | ~250 |
| 3 | `src/Services/PostScanActions/Export/IExportStrategy.cs` | ~15 |
| 3 | `src/Services/PostScanActions/Export/FolderExportStrategy.cs` | ~50 |
| 3 | `src/Services/PostScanActions/Export/FtpExportStrategy.cs` | ~50 |
| 3 | `src/Services/PostScanActions/Export/SftpExportStrategy.cs` | ~50 |
| 3 | `src/Services/PostScanActions/Export/HttpExportStrategy.cs` | ~50 |

### Backend Files Modified (significant)
| Phase | File | Change |
|-------|------|--------|
| 2 | `src/Services/PostScanManager.cs` | 319 → ~60 lines (facade) |
| 2 | `src/Services/VisualScripting/ScenarioService.cs` | Add `LoadAndCompile()` |
| 3 | `src/Services/ScanHistoryService.cs` | 424 → ~80 lines (facade) |
| 3 | `src/Services/PostScanActions/SaveToFileAction.cs` | 272 → ~60 lines |
| 3 | `src/Services/PostScanActions/WindowPasteAction.cs` | Fix memory leak |
| 3 | `src/Services/PostScanActions/DataEnrichmentAction.cs` | Use IHttpClientFactory |
| 3 | `src/Program.cs` | Register `AddHttpClient()` |

### Frontend Files Created (7 new)
| Phase | File | Purpose |
|-------|------|---------|
| 4 | `src/wwwroot/js/api.js` | Centralized API client |
| 5 | `src/wwwroot/js/state.js` | State manager |
| 6 | `src/wwwroot/js/action-config.js` | Action type definitions |
| 6 | `src/wwwroot/js/scenario-templates.js` | Drawflow node templates |
| 6 | `src/wwwroot/js/scenario-validation.js` | Scenario validation |
| 6 | `src/wwwroot/js/scenario-settings.js` | Node settings UI |
| 6 | `src/wwwroot/js/components.js` | Shared UI components |

### Frontend Files Modified (significant)
| Phase | File | Change |
|-------|------|--------|
| 4 | All 6 JS files | Replace `fetch()` with `Api.*()` |
| 5 | All 6 JS files | Replace globals with `AppState` |
| 6 | `src/wwwroot/layout.html` | Script load order, inline onclick removal |
| 6 | `pages/actions.html` | Replace onclick with data-action |
| 6 | `pages/scanners.html` | Replace onclick with data-action |
| 6 | `pages/scenarios.html` | Replace onclick with data-action |

### Test Files Created (~15 new)
| Phase | File | Tests |
|-------|------|-------|
| 1 | `tests/Integration/ScanBridgeWebApplicationFactory.cs` | Infrastructure |
| 1 | `tests/Integration/IntegrationTestBase.cs` | Infrastructure |
| 1 | `tests/Services/PostScanManagerTests.cs` | ~20 tests |
| 1 | `tests/Services/PostScanActionFactoryTests.cs` | ~12 tests |
| 1 | `tests/Services/ScanHistoryServiceTests.cs` | ~15 tests |
| 1 | `tests/Services/ReplacementActionTests.cs` | ~7 tests |
| 1 | `tests/Services/LogActionTests.cs` | ~3 tests |
| 1 | `tests/Services/PauseActionTests.cs` | ~3 tests |
| 1 | `tests/Models/PostScanActionConfigTests.cs` | ~3 tests |
| 1 | `tests/Models/ScenarioConfigTests.cs` | ~3 tests |
| 2 | `tests/Services/ScanDispatcherTests.cs` | ~15 tests |
| 2 | `tests/Services/GroupManagerTests.cs` | ~10 tests |
| 3 | `tests/Services/Export/*StrategyTests.cs` | ~12 tests |
| 6 | `tests/Parsers/QRContentDetectorTests.cs` | ~10 tests |
| 7 | `tests/Integration/*EndpointsTests.cs` | ~25 tests |

---

## Risk Assessment

| Risk | Mitigation |
|------|-----------|
| Breaking SPA routing during frontend refactor | Each phase is independently deployable; test navigation after each |
| PostScanManager refactor breaks scan execution | Phase 1 adds tests BEFORE Phase 2 decomposes |
| Drawflow compatibility breaks in scenario editor | Extract templates only, don't change Drawflow API calls |
| In-memory DB tests pass but SQLite fails | Phase 7 integration tests use real SQLite via WebApplicationFactory |
| Script load order breaks after module extraction | Phase 6.5 explicitly addresses load order; test each page |

---

## Execution Order Recommendation

1. **Phase 1** (1-2 days) — Test infrastructure + critical coverage. Do this FIRST.
2. **Phase 4** (0.5 day) — API client + error handling. Quick win, high impact.
3. **Phase 2** (1 day) — PostScanManager decomposition. Requires Phase 1 tests.
4. **Phase 5** (0.5 day) — Global state elimination. Can parallel with Phase 2.
5. **Phase 3** (1 day) — Backend service decomposition. Can parallel with Phase 5.
6. **Phase 6** (1 day) — Frontend module extraction. Depends on Phases 4+5.
7. **Phase 7** (0.5 day) — Integration tests + cleanup. Final verification.
