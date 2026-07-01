/* ── ScanBridge Actions Module ── */

let postScanGroups = [];
let currentGroupId = null;
let replacementRules = [];
let tagRules = [];

/* ── Groups CRUD ── */
async function loadGroups() {
    try {
        const data = await Api.get('/api/postscan/groups');
        postScanGroups = data.groups || [];
        renderGroupCards();
        updateStats();
    } catch (e) { handleApiError(e, 'Загрузка групп'); }
}

function renderGroupCards() {
    const container = document.getElementById('groupCards');
    const empty = document.getElementById('groupsEmpty');
    if (!container || !empty) return;
    if (postScanGroups.length === 0) { container.innerHTML = ''; empty.classList.remove('hidden'); return; }
    empty.classList.add('hidden');
    container.innerHTML = postScanGroups.map((g, i) => {
        const enabledBadge = g.enabled
            ? '<span class="badge badge-green">Вкл</span>'
            : '<span class="badge">Выкл</span>';
        const scannerBadges = g.scannerNames && g.scannerNames.length > 0
            ? g.scannerNames.map(s => `<span class="badge badge-green">${esc(s)}</span>`).join(' ')
            : '<span class="badge badge-blue">Все сканеры</span>';
        const actionCount = g.actions ? g.actions.length : 0;
        const actionLabel = actionCount === 1 ? 'действие' : (actionCount < 5 ? 'действия' : 'действий');
        const upBtn = i > 0 ? `<button class="btn-icon" onclick="moveGroup(${i},-1)" title="Вверх"><i data-lucide="chevron-up"></i></button>` : '';
        const downBtn = i < postScanGroups.length - 1 ? `<button class="btn-icon" onclick="moveGroup(${i},1)" title="Вниз"><i data-lucide="chevron-down"></i></button>` : '';
        return `<div class="group-card ${g.enabled ? '' : 'group-card-disabled'}" onclick="openGroup(${i})">
            <div class="group-card-header">
                <h3>${esc(g.name)}</h3>
                <div class="group-card-actions" onclick="event.stopPropagation()">
                    ${upBtn}${downBtn}
                    <button class="btn-icon" onclick="editGroupSettings(${i})" title="Настройки группы"><i data-lucide="settings"></i></button>
                    <button class="btn-icon danger" onclick="removeGroup(${i})" title="Удалить группу"><i data-lucide="trash-2"></i></button>
                </div>
            </div>
            <div class="group-card-scanners">${scannerBadges}</div>
            <div class="group-card-footer">
                <span class="group-card-count">${actionCount} ${actionLabel}</span>
                ${enabledBadge}
            </div>
        </div>`;
    }).join('');
    lucide.createIcons();
}

function openGroup(index) {
    currentGroupId = index;
    const group = postScanGroups[index];
    document.getElementById('groupModalTitle').textContent = group.name;
    document.getElementById('groupModalScanners').innerHTML =
        group.scannerNames && group.scannerNames.length > 0
            ? group.scannerNames.map(s => `<span class="badge badge-green">${esc(s)}</span>`).join(' ')
            : '<span class="badge badge-blue">Все сканеры</span>';
    renderGroupActions();
    openModal('groupModal');
}

function renderGroupActions() {
    const tbody = document.getElementById('groupActionsBody');
    const empty = document.getElementById('groupActionsEmpty');
    const group = postScanGroups[currentGroupId];
    if (!group || !group.actions || group.actions.length === 0) {
        tbody.innerHTML = '';
        empty.classList.remove('hidden');
        return;
    }
    empty.classList.add('hidden');
    tbody.innerHTML = group.actions.map((a, i) => {
        const typeName = ACTION_TYPES[a.type]?.name || a.type;
        const enabledBadge = a.enabled
            ? '<span class="badge badge-green">Вкл</span>'
            : '<span class="badge">Выкл</span>';
        const settingsStr = a.settings && Object.keys(a.settings).length > 0
            ? Object.entries(a.settings).filter(([k]) => k !== 'Replacements').map(([k, v]) => `${k}=${v}`).join(', ')
            : '—';
        const upBtn = i > 0 ? `<button class="btn-icon" onclick="moveGroupAction(${i},-1)" title="Вверх"><i data-lucide="chevron-up"></i></button>` : '';
        const downBtn = i < group.actions.length - 1 ? `<button class="btn-icon" onclick="moveGroupAction(${i},1)" title="Вниз"><i data-lucide="chevron-down"></i></button>` : '';
        return `<tr>
            <td><strong>${esc(typeName)}</strong></td>
            <td>${enabledBadge}</td>
            <td class="settings-cell">${esc(settingsStr)}</td>
            <td class="actions-cell">
                ${upBtn}${downBtn}
                <button class="btn-icon" onclick="editGroupAction(${i})" title="Изменить"><i data-lucide="pencil"></i></button>
                <button class="btn-icon danger" onclick="removeGroupAction(${i})" title="Удалить"><i data-lucide="trash-2"></i></button>
            </td>
        </tr>`;
    }).join('');
    lucide.createIcons();
}

function hideGroupModal() { closeModal('groupModal'); currentGroupId = null; }

/* ── Group Settings Modal ── */
function editGroupSettings(index) {
    const group = postScanGroups[index];
    document.getElementById('fGroupName').value = group.name;
    document.getElementById('fGroupEnabled').value = group.enabled ? 'true' : 'false';
    const checkboxes = document.getElementById('groupScannerCheckboxes');
    checkboxes.innerHTML = scanners.map(s => {
        const checked = group.scannerNames && group.scannerNames.includes(s.name) ? 'checked' : '';
        return `<label class="checkbox-label"><input type="checkbox" value="${esc(s.name)}" ${checked}> ${esc(s.name)}</label>`;
    }).join('');
    document.getElementById('editGroupIndex').value = index;
    openModal('groupSettingsModal');
}

function saveGroupSettings() {
    const idx = parseInt(document.getElementById('editGroupIndex').value);
    const name = document.getElementById('fGroupName').value.trim();
    const enabled = document.getElementById('fGroupEnabled').value === 'true';
    const scannerNames = Array.from(document.querySelectorAll('#groupScannerCheckboxes input:checked')).map(cb => cb.value);
    if (!name) { toast('Имя группы обязательно', true); return; }
    if (idx === -1) {
        postScanGroups.push({ id: 0, name, enabled, scannerNames, actions: [] });
    } else {
        postScanGroups[idx].name = name;
        postScanGroups[idx].enabled = enabled;
        postScanGroups[idx].scannerNames = scannerNames;
    }
    saveGroups();
    closeModal('groupSettingsModal');
}

function hideGroupSettingsModal() { closeModal('groupSettingsModal'); }

function addGroup() {
    document.getElementById('fGroupName').value = '';
    document.getElementById('fGroupEnabled').value = 'true';
    const checkboxes = document.getElementById('groupScannerCheckboxes');
    checkboxes.innerHTML = scanners.map(s =>
        `<label class="checkbox-label"><input type="checkbox" value="${esc(s.name)}"> ${esc(s.name)}</label>`
    ).join('');
    document.getElementById('editGroupIndex').value = -1;
    openModal('groupSettingsModal');
}

function removeGroup(index) {
    if (!confirm(`Удалить группу «${postScanGroups[index].name}»?`)) return;
    postScanGroups.splice(index, 1);
    saveGroups();
}

function moveGroup(i, dir) {
    const j = i + dir;
    if (j < 0 || j >= postScanGroups.length) return;
    [postScanGroups[i], postScanGroups[j]] = [postScanGroups[j], postScanGroups[i]];
    saveGroups();
}

/* ── Actions within group ── */
function showAddGroupAction() {
    document.getElementById('actionModalTitle').textContent = 'Добавить действие';
    document.getElementById('editActionIndex').value = -1;
    document.getElementById('fActionType').value = 'ClipboardPaste';
    document.getElementById('fActionEnabled').value = 'true';
    updateActionSettings('ClipboardPaste', {});
    openModal('actionModal');
}

function editGroupAction(i) {
    const group = postScanGroups[currentGroupId];
    const a = group.actions[i];
    document.getElementById('actionModalTitle').textContent = 'Редактировать действие';
    document.getElementById('editActionIndex').value = i;
    document.getElementById('fActionType').value = a.type;
    document.getElementById('fActionEnabled').value = a.enabled ? 'true' : 'false';
    updateActionSettings(a.type, a.settings || {});
    openModal('actionModal');
}

function hideActionModal() { closeModal('actionModal'); }

function saveAction() {
    const type = document.getElementById('fActionType').value;
    const enabled = document.getElementById('fActionEnabled').value === 'true';
    const settings = getActionSettings(type);
    const idx = parseInt(document.getElementById('editActionIndex').value);
    const action = { type, enabled, settings };
    const group = postScanGroups[currentGroupId];
    if (idx === -1) group.actions.push(action);
    else group.actions[idx] = action;
    saveGroups();
    hideActionModal();
    renderGroupActions();
}

function removeGroupAction(i) {
    if (!confirm('Удалить действие?')) return;
    postScanGroups[currentGroupId].actions.splice(i, 1);
    saveGroups();
    renderGroupActions();
}

function moveGroupAction(i, dir) {
    const j = i + dir;
    const actions = postScanGroups[currentGroupId].actions;
    if (j < 0 || j >= actions.length) return;
    [actions[i], actions[j]] = [actions[j], actions[i]];
    saveGroups();
    renderGroupActions();
}

async function saveGroups() {
    try {
        const data = await Api.put('/api/postscan/groups', postScanGroups);
        postScanGroups = data.groups || [];
        renderGroupCards();
        updateStats();
        toast('Сохранено');
    } catch (e) { handleApiError(e, 'Сохранение групп'); }
}

/* ── Action Settings Editor (shared) ── */
let _activeSettingsContainer = 'actionSettings';

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
    lucide.createIcons();
}

function onSettingChange() { applyShowWhen(); }

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
    lucide.createIcons();
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
    lucide.createIcons();
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
