/* ── ScanBridge Router — page loading system ── */

const Router = {
    currentPage: null,
    contentEl: null,

    init() {
        this.contentEl = document.getElementById('page-content');
        const hash = location.hash.slice(1) || 'scanners';
        this.navigate(hash);
    },

    async navigate(page) {
        if (this.currentPage === page) return;

        // Stop polling for previous page
        if (typeof stopLogPolling === 'function') stopLogPolling();
        if (typeof stopDashPolling === 'function') stopDashPolling();

        try {
            const res = await fetch(`/pages/${page}.html`);
            if (!res.ok) throw new Error(`Page ${page} not found`);
            const html = await res.text();

            this.contentEl.innerHTML = html;
            this.currentPage = page;
            location.hash = page;

            // Update sidebar active state
            document.querySelectorAll('.sidebar-link').forEach(l => l.classList.remove('active'));
            const activeLink = document.querySelector(`.sidebar-link[data-page="${page}"]`);
            if (activeLink) activeLink.classList.add('active');

            // Init lucide icons
            if (typeof lucide !== 'undefined') lucide.createIcons();

            // Run page-specific init
            this.onPageLoad(page);

        } catch (e) {
            console.error('Router error:', e);
            this.contentEl.innerHTML = `<div class="empty-state"><p>Страница не найдена: ${page}</p></div>`;
        }
    },

    onPageLoad(page) {
        switch (page) {
            case 'dashboard':
                if (typeof loadDashboard === 'function') loadDashboard();
                if (typeof startDashPolling === 'function') startDashPolling();
                break;
            case 'scanners':
                if (typeof load === 'function') load();
                if (typeof loadSettings === 'function') loadSettings();
                break;
            case 'actions':
                if (typeof loadGroups === 'function') loadGroups();
                break;
            case 'scenarios':
                if (typeof loadScenarios === 'function') loadScenarios();
                break;
            case 'scenario-editor':
                if (typeof initScenarioEditor === 'function') {
                    const scenarioData = (typeof editingScenarioData !== 'undefined' && editingScenarioData) ? editingScenarioData : null;
                    editingScenarioData = null;
                    initScenarioEditor(scenarioData);
                }
                break;
            case 'logs':
                if (typeof initLogsPage === 'function') initLogsPage();
                if (typeof loadLogs === 'function') loadLogs();
                if (typeof startLogPolling === 'function') startLogPolling();
                break;
        }
    }
};

/* ── Global navigation function ── */
function navigateTo(page) {
    Router.navigate(page);
    closeSidebar();
}

/* ── Hash change support ── */
window.addEventListener('hashchange', () => {
    const page = location.hash.slice(1) || 'scanners';
    Router.navigate(page);
});
