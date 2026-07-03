/* ── ScanBridge Action Type Definitions ── */

const ACTION_TYPES = {
    Log: {
        name: 'Логирование',
        description: 'Записывает данные сканирования в системный лог приложения.',
        settings: []
    },
    Replacement: {
        name: 'Замена символов',
        description: 'Заменяет или удаляет подстроки в данных сканирования по заданным правилам. Поддерживает замену всех вхождений, только начала или конца строки.',
        settings: [{ key: 'Replacements', label: 'Правила замены', type: 'replacements' }]
    },
    ClipboardPaste: {
        name: 'Вставка в активное окно',
        description: 'Вставляет данные сканирования в текущее активное окно. Работает только на Windows.',
        settings: [
            { key: 'Mode', label: 'Режим вставки', type: 'select',
              options: ['clipboard', 'keyboard'], default: 'clipboard',
              optionLabels: { clipboard: 'Ctrl+V (буфер обмена)', keyboard: 'Клавиатура (печать)' },
              hint: 'Ctrl+V — быстрый способ через буфер обмена.\nКлавиатура — эмулирует посимвольный ввод, как будто текст печатают с клавиатуры.' },
            { key: 'AppendNewline', label: 'Добавить перенос строки', type: 'select', options: ['false', 'true'], default: 'false' }
        ]
    },
    WindowPaste: {
        name: 'Вставка в выбранное окно',
        description: 'Находит окно по заголовку, активирует его и вставляет данные. Работает только на Windows.',
        settings: [
            { key: 'WindowTitle', label: 'Заголовок окна', type: 'text', default: '',
              hint: 'Часть заголовка окна (регистр не важен). Например: «Notepad», «Excel», «1С»' },
            { key: 'Mode', label: 'Режим вставки', type: 'select',
              options: ['clipboard', 'keyboard'], default: 'clipboard',
              optionLabels: { clipboard: 'Ctrl+V (буфер обмена)', keyboard: 'Клавиатура (печать)' },
              hint: 'Ctrl+V — быстрый способ через буфер обмена.\nКлавиатура — эмулирует посимвольный ввод.' },
            { key: 'ActivationDelay', label: 'Задержка после активации (мс)', type: 'number', default: '200',
              hint: 'Время ожидания после активации окна перед вставкой. Увеличьте, если окно не успевает открыться.' },
            { key: 'AppendNewline', label: 'Добавить перенос строки', type: 'select', options: ['false', 'true'], default: 'false' }
        ]
    },
    Export: {
        name: 'Экспорт',
        description: 'Сохраняет результат сканирования в файл (локально, FTP, SFTP) или отправляет на HTTP-эндпоинт. Поддерживает форматы JSON и XML.',
        settings: [
            { key: 'Destination', label: 'Назначение', type: 'select', options: ['folder', 'ftp', 'sftp', 'http'], default: 'folder',
              optionLabels: { folder: 'Локальная папка', ftp: 'FTP-сервер', sftp: 'SFTP-сервер', http: 'HTTP POST' } },
            { key: 'FolderPath', label: 'Папка для файлов', type: 'text', default: 'C:\\Output', showWhen: 'Destination=folder' },
            { key: 'CredentialId', label: 'Учётные данные', type: 'credential', showWhen: 'Destination=ftp|sftp' },
            { key: 'FtpRemotePath', label: 'Удалённая папка', type: 'text', default: '/', showWhen: 'Destination=ftp|sftp' },
            { key: 'HttpUrl', label: 'URL API', type: 'text', default: 'http://localhost/api/scan', showWhen: 'Destination=http',
              hint: 'Полный URL эндпоинта для POST-запроса' },
            { key: 'HttpContentType', label: 'Content-Type', type: 'text', default: '', showWhen: 'Destination=http',
              hint: 'Оставьте пустым для автопределения (application/json или application/xml)' },
            { key: 'HttpHeaders', label: 'Заголовки (JSON)', type: 'text', default: '{}', showWhen: 'Destination=http',
              hint: 'Дополнительные заголовки в формате JSON:\n{"Authorization": "Bearer token", "X-Custom": "value"}' },
            { key: 'Format', label: 'Формат', type: 'select', options: ['json', 'xml'], default: 'json' },
            { key: 'FilenameTemplate', label: 'Шаблон имени файла', type: 'text', default: '{timestamp}_{scanner}_{data}',
              hint: '{timestamp} — дата/время (yyyyMMdd_HHmmss_fff)\n{scanner} — имя сканера\n{data} — распознанные данные\n{format} — формат штрихкода' },
            { key: 'Tags', label: 'Теги в файле', type: 'tags' }
        ]
    },
    Telegram: {
        name: 'Уведомление Telegram',
        description: 'Отправляет сообщение о сканировании в Telegram-чат через бота. Создайте бота через @BotFather и укажите его токен и ID чата.',
        settings: [
            { key: 'BotToken', label: 'Токен бота', type: 'text', default: '',
              hint: 'Токен от @BotFather' },
            { key: 'ChatIds', label: 'ID чатов (через запятую)', type: 'text', default: '',
              hint: 'Перешлите сообщение боту @userinfobot чтобы узнать ваш ID' },
            { key: 'MessageTemplate', label: 'Шаблон сообщения', type: 'text',
              default: 'Scan: {data}',
              hint: '{data} — данные\n{format} — формат\n{scanner} — сканер\n{timestamp} — время\n{raw} — исходные' }
        ]
    },
    Email: {
        name: 'Уведомление Email',
        description: 'Отправляет письмо с данными сканирования через SMTP. Поддерживает шифрование SSL/TLS и плейсхолдеры в теме и теле письма.',
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
    },
    DataEnrichment: {
        name: 'Обогащение данных',
        description: 'Отправляет данные сканирования на внешний API и сохраняет полученный ответ в метаданные скана. Поддерживает GET и POST запросы.',
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
    },
    Validation: {
        name: 'Валидация',
        description: 'Проверяет данные сканирования по заданным правилам. Режим «Формат» заменяет встроенную валидацию парсера — можно задать допустимые форматы, типы контента и требования к содержимому.',
        settings: [
            { key: 'ValidationType', label: 'Тип', type: 'select',
              options: ['format', 'regex', 'dictionary', 'range'], default: 'format',
              optionLabels: { format: 'Формат скана', regex: 'Регулярное выражение', dictionary: 'Словарь значений', range: 'Числовой диапазон' } },
            { key: 'RequireNonEmpty', label: 'Отклонять пустые', type: 'select', options: ['false', 'true'], default: 'true',
              showWhen: 'ValidationType=format',
              hint: 'Отклонять сканы с пустыми данными' },
            { key: 'RequireBarcode', label: 'Только штрихкоды', type: 'select', options: ['false', 'true'], default: 'false',
              showWhen: 'ValidationType=format',
              hint: 'Пропускать только распознанные форматы штрихкодов (EAN, UPC, Code128, UUID)' },
            { key: 'AllowedFormats', label: 'Допустимые форматы', type: 'text', default: '',
              showWhen: 'ValidationType=format',
              hint: 'Через запятую: EAN-8, EAN-13, UPC-A, GTIN-14, Code128, GS1-128, UUID, Numeric, QR, Unknown.\nПусто = все форматы' },
            { key: 'AllowedContentTypes', label: 'Допустимые типы QR', type: 'text', default: '',
              showWhen: 'ValidationType=format',
              hint: 'Через запятую: Url, Json, VCard, Wifi, Text.\nПусто = все типы' },
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
    },
    Aggregation: {
        name: 'Агрегация',
        description: 'Накапливает результаты сканирования и отправляет пакетами: по количеству (после N сканов) или по времени (каждые N секунд). Пакет передаётся следующему действию в цепочке.',
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
    },
    DatabaseQuery: {
        name: 'Запрос к БД',
        description: 'Выполняет SQL-запрос к базе данных, подставляя данные сканирования в шаблон запроса. Результат заменяет данные скана для последующих действий. Поддерживает MySQL, PostgreSQL и MSSQL.',
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
    },
    Pause: {
        name: 'Пауза',
        description: 'Приостанавливает выполнение цепочки действий на указанное время. Полезно для добавления задержки между действиями.',
        settings: [
            { key: 'DelayMs', label: 'Задержка (мс)', type: 'number', default: '1000',
              hint: 'Время ожидания в миллисекундах (от 1 до 60000)' }
        ]
    }
};

const TAG_SOURCES = [
    { value: 'Timestamp', label: 'Время сканирования' },
    { value: 'ScannerName', label: 'Имя сканера' },
    { value: 'RawData', label: 'Исходные данные' },
    { value: 'ParsedData', label: 'Распознанные данные' },
    { value: 'Format', label: 'Формат кода' },
    { value: 'IsValid', label: 'Валидность' },
    { value: 'ContentType', label: 'Тип контента (URL/JSON/WiFi/...)' },
    { value: 'ParsedContent', label: 'Распарсенный контент QR' },
    { value: 'Custom', label: 'Своё значение' }
];

/* ── Action Settings Editor (shared with scenario editor) ── */
let _activeSettingsContainer = 'actionSettings';
let replacementRules = [];
let tagRules = [];

function updateActionSettings(type, existing, containerId) {
    const targetId = containerId || 'actionSettings';
    _activeSettingsContainer = targetId;
    const container = document.getElementById(targetId);
    const descEl = containerId ? null : document.getElementById('actionDescription');
    const def = ACTION_TYPES[type];
    if (descEl) descEl.innerHTML = def?.description ? `<strong>${esc(def.name)}</strong> — ${esc(def.description)}` : '';
    if (!def || def.settings.length === 0) { container.innerHTML = ''; return; }
    container.innerHTML = def.settings.map(s => {
        if (s.type === 'replacements') {
            const json = existing[s.key] || '[]';
            replacementRules = [];
            try { replacementRules = JSON.parse(json); } catch { replacementRules = []; }
            return `<div class="form-group full" data-showwhen="${s.showWhen || ''}">
                <label>${s.label}</label>
                <div class="replacements-container"></div>
                <button class="btn btn-sm btn-secondary" style="margin-top:6px" onclick="addReplacement()"><i data-lucide="plus" style="width:12px;height:12px"></i> Добавить замену</button>
            </div>`;
        }
        if (s.type === 'tags') {
            const json = existing[s.key] || '[]';
            tagRules = [];
            try { tagRules = JSON.parse(json); } catch { tagRules = []; }
            if (tagRules.length === 0) {
                tagRules = TAG_SOURCES.filter(t => t.value !== 'Custom').map(t => ({ Key: t.value, Source: t.value }));
            }
            return `<div class="form-group full" data-showwhen="${s.showWhen || ''}">
                <label>${s.label}</label>
                <div class="tags-container"></div>
                <button class="btn btn-sm btn-secondary" style="margin-top:6px" onclick="addTag()"><i data-lucide="plus" style="width:12px;height:12px"></i> Добавить тег</button>
            </div>`;
        }
        if (s.type === 'credential') {
            return CredentialField.render(s.key, s.label, s.showWhen, existing[s.key]);
        }
        const val = existing[s.key] || s.default || '';
        if (s.type === 'select') {
            const opts = s.options.map(o => {
                const lbl = s.optionLabels?.[o] || o;
                return `<option value="${o}" ${o === val ? 'selected' : ''}>${lbl}</option>`;
            }).join('');
            return `<div class="form-group" data-showwhen="${s.showWhen || ''}"><label>${s.label}</label><select class="set-field" data-key="${s.key}" onchange="onSettingChange()">${opts}</select></div>`;
        }
        const hint = s.hint ? `<span class="hint-trigger"><i data-lucide="help-circle"></i><div class="hint-popup">${esc(s.hint)}</div></span>` : '';
        return `<div class="form-group" data-showwhen="${s.showWhen || ''}"><label>${s.label}${hint}</label><input class="set-field" data-key="${s.key}" type="${s.type}" value="${esc(val)}"></div>`;
    }).join('');
    renderReplacements();
    renderTags();
    applyShowWhen();
    if (typeof lucide !== 'undefined') lucide.createIcons();
}

// Call this after updateActionSettings to load credential dropdowns
function loadCredentialSelects(existing) {
    CredentialField.loadAll(_activeSettingsContainer, existing);
}

// Reload credentials for visible selects (called on setting change)
function reloadVisibleCredentialSelects() {
    CredentialField.reloadVisible(_activeSettingsContainer);
}

function onSettingChange() {
    applyShowWhen();
    CredentialField.onSettingChange(_activeSettingsContainer);
}

function applyShowWhen() {
    const container = document.getElementById(_activeSettingsContainer);
    if (!container) return;
    container.querySelectorAll('[data-showwhen]').forEach(g => {
        const rule = g.getAttribute('data-showwhen');
        if (!rule) { g.style.display = ''; return; }
        const match = rule.match(/^(\w+)=(.+)$/);
        if (!match) { g.style.display = ''; return; }
        const [, key, values] = match;
        const allowed = values.split('|');
        const el = container.querySelector(`.set-field[data-key="${key}"]`);
        const current = el ? el.value : '';
        g.style.display = allowed.includes(current) ? '' : 'none';
    });
}

/* ── Replacements ── */
function renderReplacements() {
    const container = document.querySelector(`#${_activeSettingsContainer} .replacements-container`);
    if (!container) return;
    if (replacementRules.length === 0) {
        container.innerHTML = '<div style="color:var(--color-text-muted);font-size:12px;padding:6px 0">Нет замен</div>';
        return;
    }
    container.innerHTML = replacementRules.map((r, i) => `
        <div class="rule-row">
            <input id="rep_find_${i}" value="${esc(r.Find || '')}" placeholder="Найти" style="flex:1">
            <span class="arrow">→</span>
            <input id="rep_replace_${i}" value="${esc(r.Replace || '')}" placeholder="Заменить на" style="flex:1">
            <select id="rep_mode_${i}">
                <option value="All" ${r.Mode === 'All' ? 'selected' : ''}>Везде</option>
                <option value="Start" ${r.Mode === 'Start' ? 'selected' : ''}>В начале</option>
                <option value="End" ${r.Mode === 'End' ? 'selected' : ''}>В конце</option>
            </select>
            <button class="btn-icon danger" onclick="removeReplacement(${i})"><i data-lucide="x"></i></button>
        </div>
    `).join('');
    if (typeof lucide !== 'undefined') lucide.createIcons();
}

function addReplacement() { replacementRules.push({ Find: '', Replace: '', Mode: 'All' }); renderReplacements(); }
function removeReplacement(i) { replacementRules.splice(i, 1); renderReplacements(); }

function collectReplacements() {
    return replacementRules.map((r, i) => ({
        Find: document.getElementById('rep_find_' + i)?.value ?? r.Find,
        Replace: document.getElementById('rep_replace_' + i)?.value ?? r.Replace,
        Mode: document.getElementById('rep_mode_' + i)?.value ?? r.Mode
    }));
}

/* ── Tags ── */
function renderTags() {
    const container = document.querySelector(`#${_activeSettingsContainer} .tags-container`);
    if (!container) return;
    if (tagRules.length === 0) {
        container.innerHTML = '<div style="color:var(--color-text-muted);font-size:12px;padding:6px 0">Нет тегов</div>';
        return;
    }
    container.innerHTML = tagRules.map((t, i) => {
        const srcOpts = TAG_SOURCES.map(s =>
            `<option value="${s.value}" ${s.value === t.Source ? 'selected' : ''}>${s.label}</option>`
        ).join('');
        const isCustom = t.Source === 'Custom';
        const valInput = isCustom
            ? `<input id="tag_val_${i}" value="${esc(t.Value || '')}" placeholder="Значение" style="flex:1">`
            : '';
        return `<div class="rule-row">
            <input id="tag_key_${i}" value="${esc(t.Key || '')}" placeholder="Имя тега" style="width:140px">
            <select id="tag_src_${i}" onchange="onTagSourceChange(${i})" style="width:180px">${srcOpts}</select>
            ${valInput}
            <button class="btn-icon danger" onclick="removeTag(${i})"><i data-lucide="x"></i></button>
        </div>`;
    }).join('');
    if (typeof lucide !== 'undefined') lucide.createIcons();
}

function onTagSourceChange(i) {
    const sel = document.getElementById('tag_src_' + i);
    if (sel) tagRules[i].Source = sel.value;
    renderTags();
}

function addTag() { tagRules.push({ Key: '', Source: 'ParsedData', Value: '' }); renderTags(); }
function removeTag(i) { tagRules.splice(i, 1); renderTags(); }

function collectTags() {
    return tagRules.map((t, i) => ({
        Key: document.getElementById('tag_key_' + i)?.value ?? t.Key,
        Source: document.getElementById('tag_src_' + i)?.value ?? t.Source,
        Value: document.getElementById('tag_val_' + i)?.value ?? t.Value ?? ''
    })).filter(t => t.Key);
}

function getActionSettings(type) {
    const def = ACTION_TYPES[type];
    if (!def) return {};
    const container = document.getElementById(_activeSettingsContainer) || document.getElementById('nodeActionSettings') || document.getElementById('actionSettings');
    const settings = {};
    def.settings.forEach(s => {
        if (s.type === 'replacements') { settings[s.key] = JSON.stringify(collectReplacements()); }
        else if (s.type === 'tags') { settings[s.key] = JSON.stringify(collectTags()); }
        else {
            const el = container ? container.querySelector(`.set-field[data-key="${s.key}"]`) : null;
            if (el) settings[s.key] = el.value;
        }
    });
    return settings;
}
