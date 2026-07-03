/* ── ScanBridge Scenario Editor (Drawflow) ── */

let drawflowEditor = null;
let selectedNodeId = null;
let editingScenarioConfig = null;

// Action type → icon mapping
const ACTION_ICONS = {
    Log: 'scroll-text',
    Replacement: 'replace',
    ClipboardPaste: 'clipboard-paste',
    WindowPaste: 'app-window',
    Export: 'download',
    Telegram: 'send',
    Email: 'mail',
    DataEnrichment: 'database',
    Validation: 'check-circle',
    Aggregation: 'layers',
    DatabaseQuery: 'hard-drive',
    Pause: 'pause',
};

// Generate templates for each action type
function createActionTemplate(actionType) {
    const def = ACTION_TYPES[actionType];
    const label = def ? def.name : actionType;
    const icon = ACTION_ICONS[actionType] || 'zap';
    return `
    <div class="vs-node vs-node-action vs-node-action-${actionType.toLowerCase()}">
        <div class="vs-node-header">
            <span class="vs-node-icon"><i data-lucide="${icon}"></i></span>
            <span class="vs-node-title">${escapeHtml(label)}</span>
        </div>
    </div>`;
}

const VS_NODE_TEMPLATES = {
    Scanner: () => `
        <div class="vs-node vs-node-scanner">
            <div class="vs-node-header"><span class="vs-node-icon"><i data-lucide="scan-barcode"></i></span> Сканер</div>
            <div class="vs-node-body">
                <div class="vs-scanner-display">все сканеры</div>
            </div>
        </div>`,
    HttpTrigger: () => `
        <div class="vs-node vs-node-trigger-http">
            <div class="vs-node-header"><span class="vs-node-icon"><i data-lucide="globe"></i></span> HTTP</div>
            <div class="vs-node-body">
                <div class="vs-trigger-display">POST /api/trigger/...</div>
            </div>
        </div>`,
    ScheduleTrigger: () => `
        <div class="vs-node vs-node-trigger-schedule">
            <div class="vs-node-header"><span class="vs-node-icon"><i data-lucide="clock"></i></span> Расписание</div>
            <div class="vs-node-body">
                <div class="vs-trigger-display">cron: ...</div>
            </div>
        </div>`,
    FileTrigger: () => `
        <div class="vs-node vs-node-trigger-file">
            <div class="vs-node-header"><span class="vs-node-icon"><i data-lucide="file-text"></i></span> Файл</div>
            <div class="vs-node-body">
                <div class="vs-trigger-display">...</div>
            </div>
        </div>`,
    Start: () => `
        <div class="vs-node vs-node-start">
            <div class="vs-node-header"><span class="vs-node-icon"><i data-lucide="play"></i></span> Старт</div>
        </div>`,
    Condition: () => `
        <div class="vs-node vs-node-condition">
            <div class="vs-node-header"><span class="vs-node-icon"><i data-lucide="git-branch"></i></span> Условие</div>
            <div class="vs-node-body">
                <div class="vs-condition-display" id="cond-display">настройте условие</div>
            </div>
        </div>`,
    Fork: () => `
        <div class="vs-node vs-node-fork">
            <div class="vs-node-header"><span class="vs-node-icon"><i data-lucide="git-merge"></i></span> Ветвление</div>
        </div>`,
    While: () => `
        <div class="vs-node vs-node-while">
            <div class="vs-node-header"><span class="vs-node-icon"><i data-lucide="repeat"></i></span> Цикл</div>
            <div class="vs-node-body">
                <div class="vs-condition-display">настройте условие</div>
            </div>
        </div>`,
    End: () => `
        <div class="vs-node vs-node-end">
            <div class="vs-node-header"><span class="vs-node-icon"><i data-lucide="square"></i></span> Конец</div>
        </div>`,
};

// Add action templates dynamically
Object.keys(ACTION_TYPES).forEach(key => {
    VS_NODE_TEMPLATES[key] = () => createActionTemplate(key);
});

// All node types for the palette
const PALETTE_NODE_TYPES = [
    { type: 'Scanner', label: 'Сканер', icon: 'scan-barcode' },
    ...Object.keys(ACTION_TYPES).map(key => ({
        type: key,
        label: ACTION_TYPES[key].name,
        icon: ACTION_ICONS[key] || 'zap',
    })),
    { type: 'Condition', label: 'Условие', icon: 'git-branch' },
    { type: 'End', label: 'Конец', icon: 'square' },
];

let NODE_COUNTER = 0;

/* ── Shared field select options for Condition/While ── */
function getFieldSelectOptions(selectedField) {
    return `
        <optgroup label="Данные скана">
            <option value="data" ${selectedField === 'data' ? 'selected' : ''}>Данные (parsed)</option>
            <option value="raw" ${selectedField === 'raw' ? 'selected' : ''}>Сырые данные (raw)</option>
            <option value="format" ${selectedField === 'format' ? 'selected' : ''}>Формат</option>
            <option value="scanner" ${selectedField === 'scanner' ? 'selected' : ''}>Имя сканера</option>
            <option value="isValid" ${selectedField === 'isValid' ? 'selected' : ''}>Валиден</option>
            <option value="metadata" ${selectedField === 'metadata' ? 'selected' : ''}>Метаданные (ключ)</option>
        </optgroup>
        <optgroup label="Файловая система">
            <option value="fileExists" ${selectedField === 'fileExists' ? 'selected' : ''}>Файл существует</option>
            <option value="fileContains" ${selectedField === 'fileContains' ? 'selected' : ''}>Файл содержит</option>
            <option value="fileSize" ${selectedField === 'fileSize' ? 'selected' : ''}>Размер файла</option>
            <option value="dirExists" ${selectedField === 'dirExists' ? 'selected' : ''}>Папка существует</option>
        </optgroup>
        <optgroup label="Время">
            <option value="timeOfDay" ${selectedField === 'timeOfDay' ? 'selected' : ''}>Время (HH:mm)</option>
            <option value="dayOfWeek" ${selectedField === 'dayOfWeek' ? 'selected' : ''}>День недели</option>
            <option value="date" ${selectedField === 'date' ? 'selected' : ''}>Дата</option>
        </optgroup>
        <optgroup label="Система">
            <option value="envVar" ${selectedField === 'envVar' ? 'selected' : ''}>Переменная окружения</option>
            <option value="processRunning" ${selectedField === 'processRunning' ? 'selected' : ''}>Процесс запущен</option>
            <option value="hostAvailable" ${selectedField === 'hostAvailable' ? 'selected' : ''}>Хост доступен</option>
            <option value="diskFreeMB" ${selectedField === 'diskFreeMB' ? 'selected' : ''}>Свободно МБ</option>
        </optgroup>
        <optgroup label="Обработка">
            <option value="jsonPath" ${selectedField === 'jsonPath' ? 'selected' : ''}>JSON поле</option>
            <option value="stringLength" ${selectedField === 'stringLength' ? 'selected' : ''}>Длина строки</option>
            <option value="startsWith" ${selectedField === 'startsWith' ? 'selected' : ''}>Начинается на</option>
            <option value="endsWith" ${selectedField === 'endsWith' ? 'selected' : ''}>Заканчивается на</option>
        </optgroup>`;
}

/* ── Palette Section Toggle ── */
function togglePaletteSection(btn) {
    const section = btn.closest('.palette-section');
    const body = section.querySelector('.palette-section-body');
    const isOpen = body.classList.contains('open');

    // Close all other sections
    document.querySelectorAll('.palette-section-body.open').forEach(el => {
        el.classList.remove('open');
        el.closest('.palette-section').classList.remove('open');
    });

    if (!isOpen) {
        body.classList.add('open');
        section.classList.add('open');
    }
}

// Close palette sections when clicking outside
document.addEventListener('click', (e) => {
    if (!e.target.closest('.palette-section')) {
        document.querySelectorAll('.palette-section-body.open').forEach(el => {
            el.classList.remove('open');
            el.closest('.palette-section').classList.remove('open');
        });
    }
});

/* ── Initialize Editor ── */
async function initScenarioEditor(scenario, scenarioId) {
    editingScenarioConfig = scenario;
    NODE_COUNTER = 0;

    // If scenarioId provided, fetch from API
    if (!scenario && scenarioId) {
        try {
            scenario = await Api.get(`/api/scenarios/${scenarioId}`);
        } catch (e) {
            handleApiError(e, 'Загрузка сценария');
        }
    }

    editingScenarioConfig = scenario;

    const titleEl = document.getElementById('scenarioEditorTitle');
    titleEl.textContent = scenario ? `Редактирование: ${scenario.name}` : 'Новый сценарий';

    const container = document.getElementById('scenarioDrawflow');
    container.innerHTML = '';

    if (drawflowEditor) {
        try { drawflowEditor.clear(); } catch(e) {}
    }

    drawflowEditor = new Drawflow(container);
    drawflowEditor.reroute = true;
    drawflowEditor.reroute_fix_curvature = true;
    drawflowEditor.force_first_input = false;

    drawflowEditor.on('nodeCreated', (id) => {
        onNodeCreated(id);
    });

    drawflowEditor.on('nodeRemoved', (id) => {
        onNodeRemoved(id);
    });

    drawflowEditor.start();

    // Double-click to open settings
    container.addEventListener('dblclick', (e) => {
        const nodeEl = e.target.closest('.drawflow-node');
        if (nodeEl) {
            const id = nodeEl.id.replace('node-', '');
            openNodeSettingsModal(id);
        }
    });

    // If editing existing scenario, import it
    if (scenario && scenario.nodes && scenario.nodes.length > 0) {
        importScenarioToDrawflow(scenario);
    }

    setupPaletteDragDrop();
    lucide.createIcons();
}

/* ── ShowWhen Filtering ── */
/* ── Import Scenario to Drawflow ── */
function importScenarioToDrawflow(scenario) {
    const nodeMap = {};

    // Add nodes
    scenario.nodes.forEach(node => {
        const inputCount = (node.type === 'Start' || node.type === 'Scanner' || node.type === 'HttpTrigger' || node.type === 'ScheduleTrigger' || node.type === 'FileTrigger') ? 0 : 1;
        const outputCount = node.type === 'Condition' || node.type === 'While' ? 2 : (node.type === 'Fork' ? 3 : (node.type === 'End' ? 0 : 1));
        const template = VS_NODE_TEMPLATES[node.type];
        if (!template) return;
        const nodeSettings = node.settings || {};
        const id = drawflowEditor.addNode(
            node.type,
            inputCount,
            outputCount,
            node.positionX,
            node.positionY,
            node.type,
            { settings: nodeSettings },
            template()
        );
        nodeMap[node.nodeId] = id.toString();
        NODE_COUNTER = Math.max(NODE_COUNTER, parseInt(id) + 1);

        // Update node display from settings
        if (node.type === 'Scanner' && nodeSettings.scannerName) {
            updateScannerNodeDisplay(id, nodeSettings.scannerName);
        }
        if (node.type === 'Condition') {
            updateConditionNodeDisplay(id, 'Condition', nodeSettings);
        }
        if (node.type === 'While') {
            updateWhileNodeDisplay(id, nodeSettings);
        }
    });

    // Add connections
    scenario.connections.forEach(conn => {
        const srcId = nodeMap[conn.sourceNodeId];
        const tgtId = nodeMap[conn.targetNodeId];
        if (srcId && tgtId) {
            drawflowEditor.addConnection(parseInt(srcId), parseInt(tgtId), conn.sourcePort, conn.targetPort);
        }
    });
}

/* ── Drag & Drop from Palette ── */
function setupPaletteDragDrop() {
    document.querySelectorAll('.palette-node').forEach(el => {
        el.addEventListener('dragstart', (e) => {
            e.dataTransfer.setData('nodeType', el.dataset.type);
        });
    });

    const drawflowContainer = document.getElementById('scenarioDrawflow');
    if (drawflowContainer) {
        drawflowContainer.addEventListener('drop', (e) => {
            e.preventDefault();
            const type = e.dataTransfer.getData('nodeType');
            if (!type || !drawflowEditor) return;

            const rect = drawflowContainer.getBoundingClientRect();
            const x = e.clientX - rect.left + drawflowContainer.parentElement.scrollLeft;
            const y = e.clientY - rect.top + drawflowContainer.parentElement.scrollTop;

            const inputCount = (type === 'Start' || type === 'Scanner' || type === 'HttpTrigger' || type === 'ScheduleTrigger' || type === 'FileTrigger') ? 0 : 1;
            const outputCount = type === 'Condition' || type === 'While' ? 2 : (type === 'Fork' ? 3 : (type === 'End' ? 0 : 1));

            drawflowEditor.addNode(
                type,
                inputCount,
                outputCount,
                x,
                y,
                type,
                {},
                VS_NODE_TEMPLATES[type]()
            );
        });

        drawflowContainer.addEventListener('dragover', (e) => {
            e.preventDefault();
        });
    }
}

/* ── Node Events ── */
function onNodeCreated(id) {
    const nodeInfo = drawflowEditor.getNodeFromId(id);
    if (!nodeInfo) return;

    NODE_COUNTER = Math.max(NODE_COUNTER, parseInt(id) + 1);

    // Update Action select onchange with correct node ID
    if (nodeInfo.class === 'Action') {
        const select = document.querySelector(`#node-${id} .vs-action-select`);
        if (select) {
            select.setAttribute('onchange', `onActionTypeChange(this, '${id}')`);
        }
    }

    lucide.createIcons();
}

function onNodeRemoved(id) {
    if (selectedNodeId === id) {
        selectedNodeId = null;
    }
}

/* ── Node Settings Modal ── */
let modalNodeId = null;

function openNodeSettingsModal(id) {
    const nodeInfo = drawflowEditor.getNodeFromId(parseInt(id));
    if (!nodeInfo) return;

    modalNodeId = id;
    const content = document.getElementById('nodeSettingsContent');
    const title = document.getElementById('nodeSettingsTitle');
    const nodeClass = nodeInfo.class;
    const isAction = ACTION_TYPES.hasOwnProperty(nodeClass);

    if (isAction) {
        const actionDef = ACTION_TYPES[nodeClass];
        title.textContent = `Настройки: ${actionDef.name}`;

        // Load existing settings from node data
        const existing = getDrawflowNodeSettings(id);

        // Use shared updateActionSettings with unique container IDs
        content.innerHTML = `<div id="nodeActionDescription"></div><div class="form-grid" id="nodeActionSettings"></div>`;
        updateActionSettings(nodeClass, existing, 'nodeActionSettings');
        // Load credential dropdowns with saved values
        loadCredentialSelects(existing);

    } else if (nodeClass === 'Scanner') {
        title.textContent = 'Настройки: Сканер';
        const settings = getDrawflowNodeSettings(id);
        const currentScanner = settings.scannerName || '';
        content.innerHTML = `
            <div class="form-grid">
                <div class="form-group">
                    <label>Сканер</label>
                    <select class="vs-setting" data-key="scannerName" id="scannerNodeSelect" onchange="onScannerNodeSelectChange(${id}, this.value)">
                        <option value="" ${!currentScanner ? 'selected' : ''}>Все сканеры</option>
                    </select>
                </div>
                <p style="color:var(--color-text-muted);font-size:12px;margin-top:8px">
                    Узел активируется при получении данных от выбранного сканера.
                    «Все сканеры» — активируется всегда.
                </p>
            </div>
        `;
        // Load scanner list from API
        Api.get('/api/scanners').then(scanners => {
            const sel = document.getElementById('scannerNodeSelect');
            if (!sel) return;
            scanners.forEach(s => {
                const opt = document.createElement('option');
                opt.value = s.name;
                opt.textContent = s.name;
                if (s.name === currentScanner) opt.selected = true;
                sel.appendChild(opt);
            });
        }).catch(() => {});

    } else if (nodeClass === 'HttpTrigger') {
        title.textContent = 'Настройки: HTTP Trigger';
        const settings = getDrawflowNodeSettings(id);
        content.innerHTML = `
            <div class="form-grid">
                <div class="form-group">
                    <label>HTTP метод</label>
                    <select class="vs-setting" data-key="httpMethod">
                        <option value="POST" ${settings.httpMethod === 'POST' ? 'selected' : ''}>POST</option>
                        <option value="GET" ${settings.httpMethod === 'GET' ? 'selected' : ''}>GET</option>
                    </select>
                </div>
                <div class="form-group">
                    <label>Путь маршрута</label>
                    <input class="vs-setting" data-key="routePath" type="text" value="${escapeHtml(settings.routePath || '')}" placeholder="webhook/inventory">
                    <small style="color:var(--color-text-muted)">Полный URL: /api/trigger/{путь}</small>
                </div>
                <div class="form-group">
                    <label>Auth Token (опционально)</label>
                    <input class="vs-setting" data-key="authToken" type="text" value="${escapeHtml(settings.authToken || '')}" placeholder="Bearer token">
                </div>
            </div>`;

    } else if (nodeClass === 'ScheduleTrigger') {
        title.textContent = 'Настройки: Расписание';
        const settings = getDrawflowNodeSettings(id);
        content.innerHTML = `
            <div class="form-grid">
                <div class="form-group">
                    <label>Cron выражение</label>
                    <input class="vs-setting" data-key="cronExpression" type="text" value="${escapeHtml(settings.cronExpression || '')}" placeholder="0 9 * * 1-5">
                    <small style="color:var(--color-text-muted)">Формат: мин час день_мес месяц день_нед</small>
                </div>
                <div class="form-group">
                    <label>Тело запроса (JSON)</label>
                    <textarea class="vs-setting" data-key="schedulePayload" rows="4" placeholder='{"action": "daily_report"}'>${escapeHtml(settings.schedulePayload || '')}</textarea>
                    <small style="color:var(--color-text-muted)">Данные будут переданы как ParsedData</small>
                </div>
            </div>`;

    } else if (nodeClass === 'FileTrigger') {
        title.textContent = 'Настройки: Файловый watcher';
        const settings = getDrawflowNodeSettings(id);
        content.innerHTML = `
            <div class="form-grid">
                <div class="form-group">
                    <label>Путь к папке/файлу</label>
                    <input class="vs-setting" data-key="watchPath" type="text" value="${escapeHtml(settings.watchPath || '')}" placeholder="C:\\Data\\imports">
                </div>
                <div class="form-group">
                    <label>Фильтр (glob)</label>
                    <input class="vs-setting" data-key="watchFilter" type="text" value="${escapeHtml(settings.watchFilter || '*.*')}" placeholder="*.csv">
                </div>
                <div class="form-group">
                    <label>Тип изменений</label>
                    <input class="vs-setting" data-key="watchChangeTypes" type="text" value="${escapeHtml(settings.watchChangeTypes || 'Created,Changed')}" placeholder="Created,Changed">
                </div>
            </div>`;

    } else if (nodeClass === 'Condition') {
        title.textContent = 'Настройки: Условие';
        const settings = getDrawflowNodeSettings(id);
        const isFileField = ['fileExists','fileContains','fileSize','dirExists'].includes(settings.field);
        content.innerHTML = `
            <div class="form-grid">
                <div class="form-group">
                    <label>Поле</label>
                    <select class="vs-setting" data-key="field" onchange="onConditionFieldChange(${id})">
                        ${getFieldSelectOptions(settings.field)}
                    </select>
                </div>
                <!-- Файловые настройки — показываются только для file полей -->
                <div id="conditionFileFields" style="${isFileField ? '' : 'display:none'}">
                    <div class="form-group">
                        <label>Источник</label>
                        <select class="vs-setting" data-key="fileType" onchange="onFileTypeChange(${id})">
                            <option value="windows" ${settings.fileType === 'windows' ? 'selected' : ''}>Windows</option>
                            <option value="ftp" ${settings.fileType === 'ftp' ? 'selected' : ''}>FTP</option>
                            <option value="sftp" ${settings.fileType === 'sftp' ? 'selected' : ''}>SFTP</option>
                        </select>
                    </div>
                    ${CredentialField.render('CredentialId', 'Учётные данные', '', settings.CredentialId)}
                    <div class="form-group">
                        <label>Путь к файлу</label>
                        <input class="vs-setting" data-key="filePath" type="text" value="${escapeHtml(settings.filePath || '')}" placeholder="C:\\path\\file.txt или \\\\server\\share\\file.txt">
                    </div>
                </div>
                <div class="form-group">
                    <label>Оператор</label>
                    <select class="vs-setting" data-key="operator">
                        <option value="equals" ${settings.operator === 'equals' ? 'selected' : ''}>Равно</option>
                        <option value="notEquals" ${settings.operator === 'notEquals' ? 'selected' : ''}>Не равно</option>
                        <option value="contains" ${settings.operator === 'contains' ? 'selected' : ''}>Содержит</option>
                        <option value="notContains" ${settings.operator === 'notContains' ? 'selected' : ''}>Не содержит</option>
                        <option value="regex" ${settings.operator === 'regex' ? 'selected' : ''}>Regex</option>
                        <option value="greaterThan" ${settings.operator === 'greaterThan' ? 'selected' : ''}>Больше</option>
                        <option value="lessThan" ${settings.operator === 'lessThan' ? 'selected' : ''}>Меньше</option>
                        <option value="isValid" ${settings.operator === 'isValid' ? 'selected' : ''}>Валиден (isValid)</option>
                    </select>
                </div>
                <div class="form-group">
                    <label>Значение</label>
                    <input class="vs-setting" data-key="value" type="text" value="${escapeHtml(settings.value || '')}" placeholder="Значение для сравнения">
                </div>
            </div>
        `;
        _activeSettingsContainer = 'nodeSettingsContent';
        loadCredentialSelects(settings);
    } else if (nodeClass === 'While') {
        title.textContent = 'Настройки: Цикл';
        const settings = getDrawflowNodeSettings(id);
        const logic = settings.logic || 'and';
        let conditions = [];
        try { conditions = JSON.parse(settings.conditions || '[]'); } catch(e) { conditions = []; }
        if (conditions.length === 0) {
            conditions = [{ field: 'data', operator: 'contains', value: '' }];
        }
        content.innerHTML = `
            <div class="form-group" style="margin-bottom:12px">
                <label>Логика</label>
                <select class="vs-setting" data-key="logic" onchange="onWhileLogicChange()">
                    <option value="and" ${logic === 'and' ? 'selected' : ''}>И (AND) — все условия</option>
                    <option value="or" ${logic === 'or' ? 'selected' : ''}>Или (OR) — любое условие</option>
                </select>
            </div>
            <div class="form-section-title">Условия</div>
            <div id="whileConditions"></div>
            <button class="btn btn-sm btn-secondary" style="margin-top:8px" onclick="addWhileCondition()">
                <i data-lucide="plus" style="width:12px;height:12px"></i> Добавить условие
            </button>
        `;
        renderWhileConditions(conditions);
    } else {
        title.textContent = `Настройки: ${nodeClass}`;
        content.innerHTML = `<p class="hint">Настройки для типа «${escapeHtml(nodeClass)}» отсутствуют</p>`;
    }

    loadNodeSettingsToUI(id, isAction);
    openModal('nodeSettingsModal');
}

function closeNodeSettingsModal() {
    if (modalNodeId) {
        saveNodeSettingsFromUI(modalNodeId);
    }
    closeModal('nodeSettingsModal');
    modalNodeId = null;
}

/* ── Condition field change handlers ── */
function onConditionFieldChange(nodeId) {
    const field = document.querySelector('#nodeSettingsContent .vs-setting[data-key="field"]')?.value;
    const isFileField = ['fileExists','fileContains','fileSize','dirExists'].includes(field);
    const fileFields = document.getElementById('conditionFileFields');
    if (fileFields) fileFields.style.display = isFileField ? '' : 'none';
    if (isFileField) {
        CredentialField.reloadVisible('nodeSettingsContent');
    }
}

function onFileTypeChange(nodeId) {
    CredentialField.reloadVisible('nodeSettingsContent');
}

function saveNodeSettingsFromModal() {
    if (modalNodeId) {
        saveNodeSettingsFromUI(modalNodeId);
    }
    closeModal('nodeSettingsModal');
    modalNodeId = null;
}

function getDrawflowNodeSettings(id) {
    const data = drawflowEditor.getNodeFromId(parseInt(id));
    return data?.data?.settings || {};
}

function loadNodeSettingsToUI(id, skipForAction) {
    if (skipForAction) return; // updateActionSettings already loaded values
    const settings = getDrawflowNodeSettings(id);
    document.querySelectorAll('#nodeSettingsContent .vs-setting').forEach(el => {
        const key = el.dataset.key;
        if (settings[key] !== undefined) {
            el.value = settings[key];
        }
    });
}

function saveNodeSettingsFromUI(id) {
    if (!drawflowEditor) return;
    const nodeInfo = drawflowEditor.getNodeFromId(parseInt(id));
    if (!nodeInfo) return;

    let settings;
    const nodeClass = nodeInfo.class;
    const isAction = ACTION_TYPES.hasOwnProperty(nodeClass);

    if (isAction) {
        _activeSettingsContainer = 'nodeActionSettings';
        settings = getActionSettings(nodeClass);
    } else if (nodeClass === 'While') {
        settings = {};
        document.querySelectorAll('#nodeSettingsContent .vs-setting').forEach(el => {
            settings[el.dataset.key] = el.value;
        });
        settings.conditions = JSON.stringify(collectWhileConditions());
    } else {
        settings = {};
        document.querySelectorAll('#nodeSettingsContent .vs-setting').forEach(el => {
            settings[el.dataset.key] = el.value;
        });
    }

    if (Object.keys(settings).length > 0) {
        drawflowEditor.updateNodeDataFromId(parseInt(id), { settings });
    }

    // Update node display
    updateScannerNodeDisplay(id, settings.scannerName);
    updateConditionNodeDisplay(id, nodeClass, settings);
    updateWhileNodeDisplay(id, settings);
}

function updateScannerNodeDisplay(nodeId, scannerName) {
    const name = scannerName || '';
    const nodeEl = document.querySelector(`#node-${nodeId} .vs-scanner-display`);
    if (nodeEl) {
        nodeEl.textContent = name || 'все сканеры';
    }
}

function onScannerNodeSelectChange(nodeId, value) {
    updateScannerNodeDisplay(nodeId, value);
}

function onWhileLogicChange() {
    // Logic selector doesn't need live update — it's saved when modal closes
}

/* ── Condition / While display helpers ── */
const FIELD_LABELS = { data: 'Данные', raw: 'Raw', format: 'Формат', scanner: 'Сканер', isValid: 'Валиден' };
const OP_LABELS = { equals: '=', notEquals: '≠', contains: '∈', notContains: '∉', regex: '~', greaterThan: '>', lessThan: '<', isValid: ' Valid?' };

function formatConditionText(settings) {
    if (!settings || !settings.field) return '';
    const field = FIELD_LABELS[settings.field] || settings.field;
    const op = OP_LABELS[settings.operator] || settings.operator || '=';
    const val = settings.value || '';
    if (settings.operator === 'isValid') return `${field} ✓`;
    return `${field} ${op} ${val}`;
}

function updateConditionNodeDisplay(nodeId, nodeClass, settings) {
    if (nodeClass !== 'Condition') return;
    const nodeEl = document.querySelector(`#node-${nodeId} .vs-condition-display`);
    if (!nodeEl) return;
    const text = formatConditionText(settings);
    nodeEl.textContent = text || 'настройте условие';
}

function updateWhileNodeDisplay(nodeId, settings) {
    const nodeEl = document.querySelector(`#node-${nodeId} .vs-condition-display`);
    if (!nodeEl) return;
    let conditions = [];
    try { conditions = JSON.parse(settings.conditions || '[]'); } catch(e) { conditions = []; }
    if (conditions.length === 0) {
        nodeEl.textContent = 'настройте условие';
        return;
    }
    const logic = settings.logic || 'and';
    const logicLabel = logic === 'or' ? ' OR ' : ' AND ';
    const texts = conditions.map(c => formatConditionText(c)).filter(Boolean);
    nodeEl.textContent = texts.join(logicLabel) || 'настройте условие';
}

/* ── While Conditions Management ── */
let whileConditions = [];

function renderWhileConditions(conditions) {
    whileConditions = conditions;
    const container = document.getElementById('whileConditions');
    if (!container) return;

    container.innerHTML = conditions.map((c, i) => {
        const isFileField = ['fileExists','fileContains','fileSize','dirExists'].includes(c.field);
        const credId = c.credentialId || '';
        return `
        <div class="rule-row" style="margin-bottom:6px;flex-wrap:wrap;gap:4px">
            <select class="while-field" data-index="${i}" onchange="onWhileFieldChange(${i})" style="flex:1;min-width:120px">
                ${getFieldSelectOptions(c.field)}
            </select>
            <select class="while-operator" data-index="${i}" style="flex:1;min-width:100px">
                <option value="equals" ${c.operator === 'equals' ? 'selected' : ''}>Равно</option>
                <option value="notEquals" ${c.operator === 'notEquals' ? 'selected' : ''}>Не равно</option>
                <option value="contains" ${c.operator === 'contains' ? 'selected' : ''}>Содержит</option>
                <option value="notContains" ${c.operator === 'notContains' ? 'selected' : ''}>Не содержит</option>
                <option value="regex" ${c.operator === 'regex' ? 'selected' : ''}>Regex</option>
                <option value="greaterThan" ${c.operator === 'greaterThan' ? 'selected' : ''}>Больше</option>
                <option value="lessThan" ${c.operator === 'lessThan' ? 'selected' : ''}>Меньше</option>
                <option value="isValid" ${c.operator === 'isValid' ? 'selected' : ''}>Валиден</option>
            </select>
            <input class="while-value" data-index="${i}" type="text" value="${escapeHtml(c.value || '')}" placeholder="Значение" style="flex:1;min-width:80px">
            ${isFileField ? `
                <select class="while-filetype" data-index="${i}" style="flex:1;min-width:80px">
                    <option value="windows" ${c.fileType === 'windows' ? 'selected' : ''}>Windows</option>
                    <option value="ftp" ${c.fileType === 'ftp' ? 'selected' : ''}>FTP</option>
                    <option value="sftp" ${c.fileType === 'sftp' ? 'selected' : ''}>SFTP</option>
                </select>
                <select class="while-credential" data-index="${i}" id="whileCred_${i}" style="flex:1;min-width:100px">
                    <option value="">Учётные данные</option>
                </select>
            ` : ''}
            <button class="btn-icon danger" onclick="removeWhileCondition(${i})"><i data-lucide="x"></i></button>
        </div>`;
    }).join('');
    lucide.createIcons();
    // Load credentials for file fields
    conditions.forEach((c, i) => {
        if (['fileExists','fileContains','fileSize','dirExists'].includes(c.field)) {
            CredentialField.loadOptions(`whileCred_${i}`, c.credentialId);
        }
    });
}

function onWhileFieldChange(index) {
    // Save current state of all conditions
    const saved = collectWhileConditions();
    // Update the changed field
    const field = document.querySelector(`.rule-row:nth-child(${index + 1}) .while-field`)?.value || 'data';
    saved[index].field = field;
    // Re-render all conditions to show/hide file fields
    renderWhileConditions(saved);
}

function addWhileCondition() {
    whileConditions.push({ field: 'data', operator: 'contains', value: '' });
    renderWhileConditions(whileConditions);
}

function removeWhileCondition(index) {
    whileConditions.splice(index, 1);
    renderWhileConditions(whileConditions);
}

function collectWhileConditions() {
    const container = document.getElementById('whileConditions');
    if (!container) return [];
    const result = [];
    container.querySelectorAll('.rule-row').forEach((row, i) => {
        const field = row.querySelector('.while-field')?.value || 'data';
        const operator = row.querySelector('.while-operator')?.value || 'contains';
        const value = row.querySelector('.while-value')?.value || '';
        const cond = { field, operator, value };
        // Collect file field settings
        const isFileField = ['fileExists','fileContains','fileSize','dirExists'].includes(field);
        if (isFileField) {
            cond.fileType = row.querySelector('.while-filetype')?.value || 'windows';
            cond.credentialId = row.querySelector('.while-credential')?.value || '';
        }
        result.push(cond);
    });
    return result;
}

/* ── Save Scenario from Editor ── */
let pendingScenarioData = null;

function saveScenarioFromEditor() {
    if (!drawflowEditor) return;

    // Save any open node settings first
    if (modalNodeId) {
        saveNodeSettingsFromUI(modalNodeId);
        closeModal('nodeSettingsModal');
        modalNodeId = null;
    }

    const exportData = drawflowEditor.export();
    pendingScenarioData = exportDrawflowToScenario(exportData);

    if (!editingScenarioConfig) {
        // New scenario — show modal for name/description
        document.getElementById('scenarioSaveName').value = '';
        document.getElementById('scenarioSaveDesc').value = '';
        openModal('scenarioSaveModal');
        setTimeout(() => document.getElementById('scenarioSaveName').focus(), 100);
    } else {
        // Existing scenario — save directly
        pendingScenarioData.name = editingScenarioConfig.name;
        pendingScenarioData.description = editingScenarioConfig.description;
        pendingScenarioData.id = editingScenarioConfig.id;
        pendingScenarioData.scannerNames = editingScenarioConfig.scannerNames || [];
        doSaveScenario(pendingScenarioData);
    }
}

function confirmScenarioSave() {
    const name = document.getElementById('scenarioSaveName').value.trim();
    if (!name) {
        document.getElementById('scenarioSaveName').focus();
        return;
    }
    pendingScenarioData.name = name;
    pendingScenarioData.description = document.getElementById('scenarioSaveDesc').value.trim();
    closeModal('scenarioSaveModal');
    doSaveScenario(pendingScenarioData);
}

function cancelScenarioSave() {
    pendingScenarioData = null;
    closeModal('scenarioSaveModal');
}

async function doSaveScenario(scenario) {
    try {
        if (scenario.id) {
            await Api.put(`/api/scenarios/${scenario.id}`, scenario);
        } else {
            await Api.post('/api/scenarios', scenario);
        }

        showToast('Сценарий сохранён', 'success');
        editingScenarioConfig = null;
        navigateTo('scenarios');
        loadScenarios();
    } catch (e) {
        handleApiError(e, 'Сохранение сценария');
    }
}

/* ── Export Drawflow to Scenario DTO ── */
function exportDrawflowToScenario(exportData) {
    const nodes = [];
    const connections = [];

    const modules = exportData.drawflow?.Home?.data || {};

    Object.entries(modules).forEach(([id, node]) => {
        const settings = node.data?.settings || {};
        const nodeClass = node.class;
        // ActionType = class name if it's an action type, else null
        const isAction = ACTION_TYPES.hasOwnProperty(nodeClass);
        const actionType = isAction ? nodeClass : (settings.__actionType || null);

        nodes.push({
            nodeId: id,
            type: nodeClass,
            positionX: node.pos_x,
            positionY: node.pos_y,
            settings: settings,
            ActionType: actionType
        });

        // Connections
        Object.entries(node.outputs).forEach(([outputName, outputData]) => {
            outputData.connections.forEach(conn => {
                connections.push({
                    sourceNodeId: id,
                    targetNodeId: conn.node.toString(),
                    sourcePort: outputName,
                    targetPort: 'input_1'
                });
            });
        });
    });

    return {
        id: editingScenarioConfig?.id || 0,
        name: editingScenarioConfig?.name || '',
        description: editingScenarioConfig?.description || '',
        enabled: editingScenarioConfig?.enabled ?? true,
        scannerNames: editingScenarioConfig?.scannerNames || [],
        nodes: nodes,
        connections: connections
    };
}

/* ── Validate Scenario ── */
async function validateScenario() {
    if (!drawflowEditor) return;

    const exportData = drawflowEditor.export();
    const scenario = exportDrawflowToScenario(exportData);

    // Client-side validation
    const errors = [];
    if (scenario.nodes.length === 0) errors.push('Сценарий не содержит узлов');
    if (!scenario.nodes.some(n => n.type === 'Start')) errors.push('Отсутствует узел Start');
    if (!scenario.nodes.some(n => n.type === 'End')) errors.push('Отсутствует узел End');

    const content = document.getElementById('nodeSettingsContent');
    if (errors.length === 0) {
        content.innerHTML = `
            <div class="validation-results success">
                <strong>Валидация пройдена</strong>
                <p>Граф корректен и готов к выполнению.</p>
            </div>`;
    } else {
        content.innerHTML = `
            <div class="validation-results error">
                <strong>Ошибки валидации:</strong>
                <ul>${errors.map(e => `<li>${escapeHtml(e)}</li>`).join('')}</ul>
            </div>`;
    }
}
