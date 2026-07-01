/* ── ScanBridge Scenarios Module ── */

let scenarios = [];
let editingScenarioId = null;

/* ── Load Scenarios ── */
async function loadScenarios() {
    try {
        const res = await fetch('/api/scenarios');
        if (!res.ok) throw new Error('Failed to load scenarios');
        scenarios = await res.json();
        renderScenarios();
    } catch (e) {
        console.error('Failed to load scenarios:', e);
        showToast('Ошибка загрузки сценариев', 'error');
    }
}

/* ── Render Scenario Cards ── */
function renderScenarios() {
    const container = document.getElementById('scenarioCards');
    const emptyState = document.getElementById('scenariosEmpty');
    if (!container || !emptyState) return;

    if (scenarios.length === 0) {
        container.innerHTML = '';
        emptyState.classList.remove('hidden');
        lucide.createIcons();
        return;
    }

    emptyState.classList.add('hidden');
    container.innerHTML = scenarios.map(s => `
        <div class="scenario-card">
            <div class="scenario-card-header">
                <div class="scenario-card-icon">
                    <i data-lucide="git-branch"></i>
                </div>
                <div>
                    <div class="scenario-card-title">${escapeHtml(s.name)}</div>
                    <span class="scenario-status ${s.enabled ? 'enabled' : 'disabled'}">
                        ${s.enabled ? 'Активен' : 'Отключён'}
                    </span>
                </div>
            </div>
            ${s.description ? `<div class="scenario-card-desc">${escapeHtml(s.description)}</div>` : ''}
            <div class="scenario-card-meta">
                <span><i data-lucide="scan-barcode" style="width:14px;height:14px"></i>
                    ${s.scannerNames && s.scannerNames.length > 0
                        ? s.scannerNames.join(', ')
                        : 'Все сканеры'}
                </span>
            </div>
            <div class="scenario-card-actions">
                <button class="btn btn-primary btn-sm" onclick="editScenario(${s.id})">
                    <i data-lucide="pencil"></i> Редактировать
                </button>
                <button class="btn btn-danger btn-sm" onclick="deleteScenario(${s.id}, '${escapeHtml(s.name)}')">
                    <i data-lucide="trash-2"></i> Удалить
                </button>
            </div>
        </div>
    `).join('');

    lucide.createIcons();
}

/* ── Create Scenario ── */
function showCreateScenario() {
    editingScenarioId = null;
    navigateTo('scenario-editor');
}

/* ── Edit Scenario ── */
let editingScenarioData = null;

async function editScenario(id) {
    editingScenarioId = id;
    location.hash = 'scenario-editor?id=' + id;
    Router.navigate('scenario-editor');
}

/* ── Delete Scenario ── */
async function deleteScenario(id, name) {
    if (!confirm(`Удалить сценарий «${name}»?`)) return;

    try {
        const res = await fetch(`/api/scenarios/${id}`, { method: 'DELETE' });
        if (!res.ok) throw new Error('Failed to delete');
        showToast(`Сценарий «${name}» удалён`, 'success');
        loadScenarios();
    } catch (e) {
        console.error('Failed to delete scenario:', e);
        showToast('Ошибка удаления сценария', 'error');
    }
}

/* ── Validate Scenario from list ── */
async function validateScenarioFromList(id) {
    try {
        const res = await fetch(`/api/scenarios/${id}/validate`, { method: 'POST' });
        if (!res.ok) throw new Error('Failed to validate');
        const result = await res.json();
        if (result.isValid) {
            showToast('Сценарий валиден', 'success');
        } else {
            showToast(`Ошибки: ${result.errors.join('; ')}`, 'error');
        }
    } catch (e) {
        showToast('Ошибка валидации', 'error');
    }
}
