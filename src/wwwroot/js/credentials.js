/* ── ScanBridge Credentials Module ── */

let credentials = [];

async function loadCredentials() {
    try {
        credentials = await Api.get('/api/credentials');
        renderCredentials();
    } catch (e) { handleApiError(e, 'Загрузка учётных данных'); }
}

function renderCredentials() {
    const tbody = document.getElementById('credentialsBody');
    const empty = document.getElementById('credentialsEmpty');
    if (!tbody || !empty) return;
    if (credentials.length === 0) { tbody.innerHTML = ''; empty.classList.remove('hidden'); lucide.createIcons(); return; }
    empty.classList.add('hidden');
    tbody.innerHTML = credentials.map(c => {
        const typeLabel = c.type === 'windows' ? 'Windows' : c.type === 'ftp' ? 'FTP' : 'SFTP';
        const typeBadge = c.type === 'windows' ? 'badge-blue' : c.type === 'ftp' ? 'badge-green' : 'badge-amber';
        const host = c.type === 'windows' ? (c.domain || '—') : (c.host || '—');
        return `<tr>
            <td><strong>${esc(c.name)}</strong></td>
            <td><span class="badge ${typeBadge}">${typeLabel}</span></td>
            <td>${esc(c.username || '—')}</td>
            <td>${esc(host)}</td>
            <td class="actions-cell">
                <button class="btn-icon" onclick="editCredential(${c.id})" title="Изменить"><i data-lucide="pencil"></i></button>
                <button class="btn-icon danger" onclick="removeCredential(${c.id}, '${esc(c.name)}')" title="Удалить"><i data-lucide="trash-2"></i></button>
            </td>
        </tr>`;
    }).join('');
    lucide.createIcons();
}

function showAddCredential() {
    document.getElementById('credentialModalTitle').textContent = 'Добавить учётные данные';
    document.getElementById('credentialEditId').value = -1;
    document.getElementById('fCredType').value = 'windows';
    document.getElementById('fCredName').value = '';
    document.getElementById('fCredDomain').value = '';
    document.getElementById('fCredUsername').value = '';
    document.getElementById('fCredPassword').value = '';
    document.getElementById('fCredHost').value = '';
    document.getElementById('fCredPort').value = '21';
    document.getElementById('fCredFtpUser').value = '';
    document.getElementById('fCredFtpPass').value = '';
    document.getElementById('fCredPassive').checked = true;
    onCredentialTypeChange();
    openModal('credentialModal');
}

function editCredential(id) {
    const c = credentials.find(x => x.id === id);
    if (!c) return;
    document.getElementById('credentialModalTitle').textContent = 'Редактировать учётные данные';
    document.getElementById('credentialEditId').value = id;
    document.getElementById('fCredType').value = c.type;
    document.getElementById('fCredName').value = c.name;
    document.getElementById('fCredDomain').value = c.domain || '';
    document.getElementById('fCredUsername').value = c.username || '';
    document.getElementById('fCredPassword').value = c.password || '';
    document.getElementById('fCredHost').value = c.host || '';
    document.getElementById('fCredPort').value = c.port || (c.type === 'sftp' ? 22 : 21);
    document.getElementById('fCredFtpUser').value = c.username || '';
    document.getElementById('fCredFtpPass').value = c.password || '';
    document.getElementById('fCredPassive').checked = c.passiveMode !== false;
    onCredentialTypeChange();
    openModal('credentialModal');
}

function onCredentialTypeChange() {
    const type = document.getElementById('fCredType').value;
    const windowsFields = document.getElementById('credFieldsWindows');
    const ftpFields = document.getElementById('credFieldsFtp');
    const passiveGroup = document.getElementById('credFtpPassiveGroup');

    windowsFields.classList.toggle('hidden', type !== 'windows');
    ftpFields.classList.toggle('hidden', type === 'windows');
    passiveGroup.classList.toggle('hidden', type !== 'ftp');

    if (type === 'ftp') document.getElementById('fCredPort').value = '21';
    if (type === 'sftp') document.getElementById('fCredPort').value = '22';
}

async function saveCredential() {
    const id = parseInt(document.getElementById('credentialEditId').value);
    const type = document.getElementById('fCredType').value;
    const data = {
        name: document.getElementById('fCredName').value.trim(),
        type: type,
        domain: document.getElementById('fCredDomain').value.trim(),
        username: type === 'windows'
            ? document.getElementById('fCredUsername').value.trim()
            : document.getElementById('fCredFtpUser').value.trim(),
        password: type === 'windows'
            ? document.getElementById('fCredPassword').value
            : document.getElementById('fCredFtpPass').value,
        host: document.getElementById('fCredHost').value.trim(),
        port: parseInt(document.getElementById('fCredPort').value) || (type === 'sftp' ? 22 : 21),
        passiveMode: document.getElementById('fCredPassive').checked
    };

    if (!data.name) { showToast('Введите имя', 'error'); return; }

    try {
        if (id === -1) {
            await Api.post('/api/credentials', data);
            showToast('Учётные данные добавлены');
        } else {
            await Api.put(`/api/credentials/${id}`, data);
            showToast('Учётные данные обновлены');
        }
        closeModal('credentialModal');
        loadCredentials();
    } catch (e) { handleApiError(e, 'Сохранение учётных данных'); }
}

async function removeCredential(id, name) {
    if (!confirm(`Удалить учётные данные «${name}»?`)) return;
    try {
        await Api.delete(`/api/credentials/${id}`);
        showToast('Учётные данные удалены');
        loadCredentials();
    } catch (e) { handleApiError(e, 'Удаление учётных данных'); }
}

/* ── Credential dropdown for scenario editor ── */
async function loadCredentialOptions(selectId, selectedId) {
    try {
        const data = await Api.get('/api/credentials');
        const sel = document.getElementById(selectId);
        if (!sel) return;
        const currentVal = selectedId || sel.value || '';
        sel.innerHTML = '<option value="">Не выбрано</option>';
        data.forEach(c => {
            const opt = document.createElement('option');
            opt.value = c.id;
            opt.textContent = `${c.name} (${c.type})`;
            if (String(c.id) === String(currentVal)) opt.selected = true;
            sel.appendChild(opt);
        });
    } catch (e) { console.error('loadCredentialOptions', e); }
}
