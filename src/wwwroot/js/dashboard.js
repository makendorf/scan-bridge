/* ── ScanBridge Dashboard ── */

let dashActivityChart = null;
let dashFormatChart = null;
let dashPollTimer = null;
let dashCurrentPeriod = '24h';
const DASH_POLL_MS = 10000;

async function loadDashboard() {
    if (!document.getElementById('dashLastUpdate')) return;
    await Promise.all([
        loadDashStats(),
        loadDashActivity(dashCurrentPeriod),
        loadDashFormats(),
        loadDashScans(),
        loadDashPerScanner(),
        loadDashReconnects()
    ]);
    document.getElementById('dashLastUpdate').textContent =
        'Обновлено: ' + new Date().toLocaleTimeString('ru-RU');
    lucide.createIcons();
}

async function loadDashStats() {
    try {
        const r = await fetch('/api/dashboard/stats');
        const d = await r.json();
        document.getElementById('dashTotalScanners').textContent = d.totalScanners;
        document.getElementById('dashActiveScanners').textContent = d.activeScanners;
        document.getElementById('dashTotalScans').textContent = d.totalScans.toLocaleString('ru-RU');
        document.getElementById('dashScansToday').textContent = d.scansToday.toLocaleString('ru-RU');
        document.getElementById('dashSuccessRate').textContent = d.successRate.toFixed(1) + '%';
        document.getElementById('dashDbSize').textContent = d.dbSizeMb.toFixed(1) + ' МБ';
    } catch (e) { console.error('dash stats', e); }
}

async function loadDashActivity(period) {
    dashCurrentPeriod = period;
    document.querySelectorAll('.dashboard-period-btns .btn').forEach(b => {
        b.classList.toggle('active', b.dataset.period === period);
    });
    if (!document.getElementById('activityChart')) return;
    try {
        const r = await fetch('/api/dashboard/activity?period=' + period);
        const d = await r.json();
        renderActivityChart(d.labels, d.data, period);
    } catch (e) { console.error('dash activity', e); }
}

function renderActivityChart(labels, data, period) {
    const ctx = document.getElementById('activityChart');
    if (!ctx) return;

    const isDark = document.documentElement.getAttribute('data-theme') !== 'light';
    const gridColor = isDark ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.06)';
    const textColor = isDark ? '#8892A4' : '#475569';

    if (dashActivityChart) {
        dashActivityChart.data.labels = labels;
        dashActivityChart.data.datasets[0].data = data;
        dashActivityChart.update();
        return;
    }

    dashActivityChart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Сканов',
                data: data,
                backgroundColor: 'rgba(59, 130, 246, 0.5)',
                borderColor: 'rgba(59, 130, 246, 0.8)',
                borderWidth: 1,
                borderRadius: 4
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                x: { ticks: { color: textColor, maxRotation: 45 }, grid: { color: gridColor } },
                y: { beginAtZero: true, ticks: { color: textColor }, grid: { color: gridColor } }
            }
        }
    });
}

async function loadDashFormats() {
    try {
        const r = await fetch('/api/dashboard/formats');
        const d = await r.json();
        renderFormatChart(d.labels, d.data, d.colors);
    } catch (e) { console.error('dash formats', e); }
}

function renderFormatChart(labels, data, colors) {
    const ctx = document.getElementById('formatChart');
    if (!ctx) return;

    if (dashFormatChart) {
        dashFormatChart.data.labels = labels;
        dashFormatChart.data.datasets[0].data = data;
        dashFormatChart.data.datasets[0].backgroundColor = colors;
        dashFormatChart.update();
        return;
    }

    dashFormatChart = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: labels,
            datasets: [{
                data: data,
                backgroundColor: colors,
                borderWidth: 0,
                hoverOffset: 8
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: '60%',
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: {
                        color: document.documentElement.getAttribute('data-theme') !== 'light'
                            ? '#8892A4' : '#475569',
                        padding: 16,
                        usePointStyle: true,
                        pointStyleWidth: 10
                    }
                }
            }
        }
    });
}

async function loadDashScans() {
    try {
        const r = await fetch('/api/dashboard/scans?limit=20');
        const scans = await r.json();
        const tbody = document.getElementById('dashScansBody');
        const empty = document.getElementById('dashScansEmpty');
        if (!tbody || !empty) return;
        if (!scans.length) { tbody.innerHTML = ''; empty.classList.remove('hidden'); return; }
        empty.classList.add('hidden');
        tbody.innerHTML = scans.map(s => `<tr>
            <td>${new Date(s.timestamp).toLocaleString('ru-RU')}</td>
            <td><span class="badge badge-blue">${esc(s.scannerName)}</span></td>
            <td><span class="badge">${esc(s.format)}</span></td>
            <td class="settings-cell">${esc(s.parsedData)}</td>
            <td>${s.isValid
                ? '<span class="badge badge-green">OK</span>'
                : '<span class="badge badge-amber">ERR</span>'}</td>
        </tr>`).join('');
    } catch (e) { console.error('dash scans', e); }
}

async function loadDashPerScanner() {
    try {
        const r = await fetch('/api/dashboard/per-scanner');
        const scanners = await r.json();
        const container = document.getElementById('dashScannerCards');
        if (!container) return;
        if (!scanners.length) {
            container.innerHTML = '<div class="empty-state"><i data-lucide="scan-barcode"></i><p>Нет сканеров</p></div>';
            return;
        }
        container.innerHTML = scanners.map(s => {
            const rate = s.total > 0 ? ((s.valid / s.total) * 100).toFixed(0) : 0;
            const statusClass = s.isActive ? 'badge-green' : 'badge-amber';
            const statusText = s.isActive ? 'Онлайн' : 'Оффлайн';
            return `<div class="dashboard-scanner-card">
                <div class="dashboard-scanner-header">
                    <div class="dashboard-scanner-name">${esc(s.name)}</div>
                    <span class="badge ${statusClass}">${statusText}</span>
                </div>
                <div class="dashboard-scanner-stats">
                    <div class="dashboard-scanner-stat">
                        <span class="dashboard-scanner-stat-label">Сканов</span>
                        <span class="dashboard-scanner-stat-value">${s.total.toLocaleString('ru-RU')}</span>
                    </div>
                    <div class="dashboard-scanner-stat">
                        <span class="dashboard-scanner-stat-label">Скорость</span>
                        <span class="dashboard-scanner-stat-value">${s.avgScansPerHour} ск/ч</span>
                    </div>
                    <div class="dashboard-scanner-stat">
                        <span class="dashboard-scanner-stat-label">Uptime</span>
                        <span class="dashboard-scanner-stat-value">${s.uptime}</span>
                    </div>
                </div>
                <div class="dashboard-scanner-last">
                    Последний: ${new Date(s.lastScan).toLocaleString('ru-RU')}
                </div>
            </div>`;
        }).join('');
    } catch (e) { console.error('dash per-scanner', e); }
}

async function loadDashReconnects() {
    try {
        const r = await fetch('/api/dashboard/reconnects?hours=24');
        const errors = await r.json();
        const container = document.getElementById('dashReconnects');
        const empty = document.getElementById('dashReconnectsEmpty');
        if (!container || !empty) return;
        if (!errors.length) { container.innerHTML = ''; empty.classList.remove('hidden'); return; }
        empty.classList.add('hidden');
        container.innerHTML = errors.slice(0, 20).map(e => `<div class="dashboard-reconnect-item">
            <div class="dashboard-reconnect-header">
                <span class="badge badge-amber">${esc(e.scannerName)}</span>
                <span class="dashboard-reconnect-time">${new Date(e.timestamp).toLocaleString('ru-RU')}</span>
            </div>
            <div class="dashboard-reconnect-msg">${esc(e.errorMessage)}</div>
            <div class="dashboard-reconnect-attempt">Попытка #${e.attemptNumber}</div>
        </div>`).join('');
    } catch (e) { console.error('dash reconnects', e); }
}

function startDashPolling() {
    stopDashPolling();
    dashPollTimer = setInterval(loadDashboard, DASH_POLL_MS);
}

function stopDashPolling() {
    if (dashPollTimer) { clearInterval(dashPollTimer); dashPollTimer = null; }
}
