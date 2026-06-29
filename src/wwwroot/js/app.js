/* ── ScanBridge App (init + navigation) ── */

function updateStats() {
    document.getElementById('statScanners').textContent = scanners.length;
    document.getElementById('kpiScanners').textContent = scanners.length;
    document.getElementById('kpiActive').textContent = scanners.length;
    const totalActions = postScanGroups.reduce((sum, g) => sum + (g.actions ? g.actions.length : 0), 0);
    document.getElementById('kpiActions').textContent = totalActions;
    document.getElementById('statActions').textContent = postScanGroups.length;
}

/* ── Navigation ── */
function switchPanel(name) {
    document.querySelectorAll('.sidebar-link').forEach(l => l.classList.remove('active'));
    document.querySelector(`.sidebar-link[data-panel="${name}"]`).classList.add('active');
    document.querySelectorAll('.panel').forEach(p => p.classList.remove('active'));
    document.getElementById('panel-' + name).classList.add('active');
    if (name === 'logs') { loadLogs(); startLogPolling(); stopDashPolling(); }
    else { stopLogPolling(); }
    if (name === 'actions') { loadGroups(); stopDashPolling(); }
    if (name === 'scanners') { load(); loadSettings(); stopDashPolling(); }
    if (name === 'dashboard') { loadDashboard(); startDashPolling(); }
    closeSidebar();
}

function toggleSidebar() {
    document.getElementById('sidebar').classList.toggle('open');
    document.getElementById('sidebarBackdrop').classList.toggle('open');
}
function closeSidebar() {
    document.getElementById('sidebar').classList.remove('open');
    document.getElementById('sidebarBackdrop').classList.remove('open');
}

/* ── Action Type Change Listener ── */
document.getElementById('fActionType').addEventListener('change', function() {
    updateActionSettings(this.value, {});
});

/* ── Init ── */
lucide.createIcons();
load();
loadSettings();
loadGroups();
