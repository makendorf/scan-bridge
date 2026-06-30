/* ── ScanBridge App (init + navigation) ── */

function updateStats() {
    const el = (id) => document.getElementById(id);
    if (el('statScanners')) el('statScanners').textContent = scanners.length;
    if (el('kpiScanners')) el('kpiScanners').textContent = scanners.length;
    if (el('kpiActive')) el('kpiActive').textContent = scanners.length;
    const totalActions = postScanGroups.reduce((sum, g) => sum + (g.actions ? g.actions.length : 0), 0);
    if (el('kpiActions')) el('kpiActions').textContent = totalActions;
    if (el('statActions')) el('statActions').textContent = postScanGroups.length;
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

/* ── Action Type Change Listener (setup after DOM ready) ── */
document.addEventListener('DOMContentLoaded', () => {
    const fActionType = document.getElementById('fActionType');
    if (fActionType) {
        fActionType.addEventListener('change', function() {
            updateActionSettings(this.value, {});
        });
    }
});

/* ── Init ── */
lucide.createIcons();
Router.init();
