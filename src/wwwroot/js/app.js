/* ── ScanBridge App (init + navigation) ── */

function updateStats() {
    const el = (id) => document.getElementById(id);
    if (el('statScanners')) el('statScanners').textContent = scanners.length;
    if (el('kpiScanners')) el('kpiScanners').textContent = scanners.length;
    if (el('kpiActive')) el('kpiActive').textContent = scanners.length;
}

/* ── Sidebar Toggle ── */
function toggleSidebar() {
    document.getElementById('sidebar').classList.toggle('open');
    document.getElementById('sidebarBackdrop').classList.toggle('open');
}
function closeSidebar() {
    document.getElementById('sidebar').classList.remove('open');
    document.getElementById('sidebarBackdrop').classList.remove('open');
}

/* ── Init ── */
lucide.createIcons();
Router.init();
