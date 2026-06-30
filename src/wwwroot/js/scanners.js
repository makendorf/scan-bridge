/* ── ScanBridge Scanners Module ── */

let scanners = [];

/* ── Reconnect Settings ── */
async function loadSettings() {
    const el = (id) => document.getElementById(id);
    if (!el('reconnectMode')) return;
    const [modeRes, configRes] = await Promise.all([
        fetch('/api/settings/reconnect'),
        fetch('/api/settings/reconnect/config')
    ]);
    const modeData = await modeRes.json();
    const configData = await configRes.json();
    el('reconnectMode').value = modeData.mode;
    el('reconnectDelay').value = configData.delayMs;
    el('reconnectMaxRetries').value = configData.maxRetries;
    el('reconnectContinuous').checked = configData.continuous;
    updateMaxRetriesState();
}

function updateMaxRetriesState() {
    const continuous = document.getElementById('reconnectContinuous').checked;
    document.getElementById('reconnectMaxRetries').disabled = continuous;
    document.getElementById('reconnectMaxRetries').style.opacity = continuous ? '0.4' : '1';
}

async function saveReconnectMode() {
    const mode = document.getElementById('reconnectMode').value;
    await fetch('/api/settings/reconnect', {
        method: 'PUT', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ mode })
    });
}

async function saveReconnectConfig() {
    updateMaxRetriesState();
    const delayMs = parseInt(document.getElementById('reconnectDelay').value) || 1000;
    const maxRetries = parseInt(document.getElementById('reconnectMaxRetries').value) || 10;
    const continuous = document.getElementById('reconnectContinuous').checked;
    const res = await fetch('/api/settings/reconnect/config', {
        method: 'PUT', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ delayMs, maxRetries, continuous })
    });
    if (res.ok) {
        const data = await res.json();
        document.getElementById('reconnectDelay').value = data.delayMs;
        document.getElementById('reconnectMaxRetries').value = data.maxRetries;
        document.getElementById('reconnectContinuous').checked = data.continuous;
        toast('Настройки переподключения сохранены, сканеры перезапущены');
    } else { toast('Ошибка сохранения настроек', true); }
}

/* ── Scanners CRUD ── */
async function load() {
    const res = await fetch('/api/scanners');
    scanners = await res.json();
    render();
    updateStats();
}

function render() {
    const tbody = document.getElementById('scannersBody');
    const empty = document.getElementById('emptyState');
    if (!tbody || !empty) return;
    if (scanners.length === 0) { tbody.innerHTML = ''; empty.classList.remove('hidden'); return; }
    empty.classList.add('hidden');
    tbody.innerHTML = scanners.map((s, i) => {
        const rc = s.reconnect || { delayMs: 1000, maxRetries: 10, continuous: false };
        const rcLabel = rc.continuous
            ? `<span class="badge badge-amber">∞ ${rc.delayMs}мс</span>`
            : `<span class="badge">${rc.maxRetries}× ${rc.delayMs}мс</span>`;
        return `<tr>
            <td><strong>${esc(s.name)}</strong></td>
            <td><span class="badge badge-green">${esc(s.portName)}</span></td>
            <td>${s.baudRate}</td>
            <td>${s.dataBits} / ${esc(s.parity)} / ${esc(s.stopBits)}</td>
            <td>${esc(s.handshake)}</td>
            <td>${rcLabel}</td>
            <td class="actions-cell">
                <button class="btn-icon" onclick="restart('${esc(s.name)}')" title="Перезапуск"><i data-lucide="refresh-cw"></i></button>
                <button class="btn-icon" onclick="editScanner(${i})" title="Изменить"><i data-lucide="pencil"></i></button>
                <button class="btn-icon danger" onclick="removeScanner(${i})" title="Удалить"><i data-lucide="trash-2"></i></button>
            </td>
        </tr>`;
    }).join('');
    lucide.createIcons();
}

async function loadPorts(selectedPort) {
    const sel = document.getElementById('fPort');
    try {
        const res = await fetch('/api/ports');
        const ports = await res.json();
        sel.innerHTML = '';
        if (ports.length === 0) { sel.innerHTML = '<option value="">Нет доступных портов</option>'; return; }
        ports.forEach(p => { const o = document.createElement('option'); o.value = p; o.textContent = p; sel.appendChild(o); });
        if (selectedPort) {
            if (!ports.includes(selectedPort)) { const o = document.createElement('option'); o.value = selectedPort; o.textContent = selectedPort + ' (не в системе)'; sel.appendChild(o); }
            sel.value = selectedPort;
        }
        document.getElementById('statPorts').textContent = ports.length;
    } catch { sel.innerHTML = '<option value="">Ошибка загрузки</option>'; }
}

function showAddScanner() {
    document.getElementById('scannerModalTitle').textContent = 'Добавить сканер';
    document.getElementById('editIndex').value = -1;
    document.getElementById('fName').value = '';
    document.getElementById('fBaud').value = '9600';
    document.getElementById('fDataBits').value = '8';
    document.getElementById('fParity').value = 'None';
    document.getElementById('fStopBits').value = 'One';
    document.getElementById('fHandshake').value = 'RequestToSend';
    document.getElementById('fReadTimeout').value = '5000';
    document.getElementById('fWriteTimeout').value = '5000';
    document.getElementById('fReconnectDelay').value = '1000';
    document.getElementById('fReconnectMaxRetries').value = '10';
    document.getElementById('fReconnectContinuous').value = 'false';
    document.getElementById('fControlCharMode').value = '0';
    loadPorts().then(() => openModal('scannerModal'));
}

async function editScanner(i) {
    const s = scanners[i];
    document.getElementById('scannerModalTitle').textContent = 'Редактировать сканер';
    document.getElementById('editIndex').value = i;
    document.getElementById('fName').value = s.name;
    document.getElementById('fBaud').value = s.baudRate;
    document.getElementById('fDataBits').value = s.dataBits;
    document.getElementById('fParity').value = s.parity;
    document.getElementById('fStopBits').value = s.stopBits;
    document.getElementById('fHandshake').value = s.handshake;
    document.getElementById('fReadTimeout').value = s.readTimeout;
    document.getElementById('fWriteTimeout').value = s.writeTimeout;
    if (s.reconnect) {
        document.getElementById('fReconnectDelay').value = s.reconnect.delayMs;
        document.getElementById('fReconnectMaxRetries').value = s.reconnect.maxRetries;
        document.getElementById('fReconnectContinuous').value = s.reconnect.continuous ? 'true' : 'false';
    }
    document.getElementById('fControlCharMode').value = String(s.controlCharMode ?? 0);
    await loadPorts(s.portName);
    openModal('scannerModal');
}

function hideScannerModal() { closeModal('scannerModal'); }

function getFormData() {
    return {
        name: document.getElementById('fName').value.trim(),
        portName: document.getElementById('fPort').value.trim(),
        baudRate: parseInt(document.getElementById('fBaud').value),
        dataBits: parseInt(document.getElementById('fDataBits').value),
        parity: document.getElementById('fParity').value,
        stopBits: document.getElementById('fStopBits').value,
        handshake: document.getElementById('fHandshake').value,
        readTimeout: parseInt(document.getElementById('fReadTimeout').value),
        writeTimeout: parseInt(document.getElementById('fWriteTimeout').value),
        reconnect: {
            delayMs: parseInt(document.getElementById('fReconnectDelay').value) || 1000,
            maxRetries: parseInt(document.getElementById('fReconnectMaxRetries').value) || 10,
            continuous: document.getElementById('fReconnectContinuous').value === 'true'
        },
        controlCharMode: parseInt(document.getElementById('fControlCharMode').value) || 0
    };
}

async function saveScanner() {
    const data = getFormData();
    if (!data.name || !data.portName) { toast('Имя и порт обязательны', true); return; }
    const idx = parseInt(document.getElementById('editIndex').value);
    const method = idx === -1 ? 'POST' : 'PUT';
    const url = idx === -1 ? '/api/scanners' : `/api/scanners/${idx}`;
    const res = await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
    if (res.ok) {
        const result = await res.json();
        scanners = result.scanners;
        render();
        hideScannerModal();
        if (result.conflict) toast(`Порт ${data.portName} был у сканера «${result.conflict}» — он остановлен`);
        else toast('Сканер сохранён, COM переподключён');
    } else { toast('Ошибка сохранения сканера', true); }
}

async function removeScanner(i) {
    if (!confirm(`Удалить сканер «${scanners[i].name}»?`)) return;
    const res = await fetch(`/api/scanners/${i}`, { method: 'DELETE' });
    if (res.ok) { await load(); toast('Сканер удалён'); }
    else { toast('Ошибка удаления сканера', true); }
}

async function restart(name) {
    const res = await fetch(`/api/scanners/${encodeURIComponent(name)}/restart`, { method: 'POST' });
    if (res.ok) { toast(`Сканер «${name}» перезапущен`); }
    else { toast('Ошибка перезапуска сканера', true); }
}
