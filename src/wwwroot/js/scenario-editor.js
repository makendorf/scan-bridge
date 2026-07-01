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
    { type: 'Start', label: 'Старт', icon: 'play' },
    ...Object.keys(ACTION_TYPES).map(key => ({
        type: key,
        label: ACTION_TYPES[key].name,
        icon: ACTION_ICONS[key] || 'zap',
    })),
    { type: 'Condition', label: 'Условие', icon: 'git-branch' },
    { type: 'End', label: 'Конец', icon: 'square' },
];

let NODE_COUNTER = 0;

/* ── Initialize Editor ── */
function initScenarioEditor(scenario) {
    editingScenarioConfig = scenario;
    NODE_COUNTER = 0;

    const titleEl = document.getElementById('scenarioEditorTitle');
    titleEl.textContent = scenario ? `Редактирование: ${scenario.name}` : 'Новый сценарий';

    const container = document.getElementById('scenarioDrawflow');
    container.innerHTML = '';

    if (drawflowEditor) {
        drawflowEditor.destroy();
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
        const inputCount = node.type === 'Start' ? 0 : 1;
        const outputCount = node.type === 'Condition' ? 2 : (node.type === 'End' ? 0 : 1);
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

            const inputCount = type === 'Start' ? 0 : 1;
            const outputCount = type === 'Condition' ? 2 : (type === 'End' ? 0 : 1);

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

    } else if (nodeClass === 'Condition') {
        title.textContent = 'Настройки: Условие';
        const settings = getDrawflowNodeSettings(id);
        content.innerHTML = `
            <div class="form-grid">
                <div class="form-group">
                    <label>Поле</label>
                    <select class="vs-setting" data-key="field">
                        <option value="data" ${settings.field === 'data' ? 'selected' : ''}>Данные (parsed)</option>
                        <option value="raw" ${settings.field === 'raw' ? 'selected' : ''}>Сырые данные (raw)</option>
                        <option value="format" ${settings.field === 'format' ? 'selected' : ''}>Формат</option>
                        <option value="scanner" ${settings.field === 'scanner' ? 'selected' : ''}>Имя сканера</option>
                        <option value="isValid" ${settings.field === 'isValid' ? 'selected' : ''}>Валиден</option>
                    </select>
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
        // Use shared getActionSettings for action nodes
        _activeSettingsContainer = 'nodeActionSettings';
        settings = getActionSettings(nodeClass);
    } else {
        // For Condition and other nodes, use the old selector
        settings = {};
        document.querySelectorAll('#nodeSettingsContent .vs-setting').forEach(el => {
            settings[el.dataset.key] = el.value;
        });
    }

    // Only update if we got settings (not empty object from failed lookup)
    if (Object.keys(settings).length > 0) {
        drawflowEditor.updateNodeDataFromId(parseInt(id), { settings });
    }
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
        let res;
        if (scenario.id) {
            res = await fetch(`/api/scenarios/${scenario.id}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(scenario)
            });
        } else {
            res = await fetch('/api/scenarios', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(scenario)
            });
        }

        if (!res.ok) {
            const err = await res.json();
            throw new Error(err.errors ? err.errors.join('; ') : 'Save failed');
        }

        showToast('Сценарий сохранён', 'success');
        editingScenarioConfig = null;
        navigateTo('scenarios');
        loadScenarios();
    } catch (e) {
        console.error('Failed to save scenario:', e);
        showToast(`Ошибка сохранения: ${e.message}`, 'error');
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
