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
        // Allow re-entry for scenario-editor, skip for others
        if (this.currentPage === page && page !== 'scenario-editor') return;

        // Prevent re-entrant calls
        if (this._navigating) return;
        this._navigating = true;

        // Stop polling for previous page
        if (typeof stopLogPolling === 'function') stopLogPolling();
        if (typeof stopDashPolling === 'function') stopDashPolling();

        // Destroy drawflow before leaving editor
        if (this.currentPage === 'scenario-editor' && typeof drawflowEditor !== 'undefined' && drawflowEditor !== null) {
            try { drawflowEditor.clear(); } catch(e) {}
            drawflowEditor = null;
        }

        try {
            const cacheBust = Date.now();
            const res = await fetch(`/pages/${page}.html?t=${cacheBust}`, { cache: 'no-store' });
            if (!res.ok) throw new Error(`Page ${page} not found`);
            const html = await res.text();

            this.contentEl.innerHTML = html;
            this.currentPage = page;
            // Preserve query params in hash for scenario-editor
            const existingHash = location.hash;
            if (page === 'scenario-editor' && existingHash.includes('?')) {
                // keep hash as-is
            } else {
                location.hash = page;
            }

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
            this.currentPage = null;
        } finally {
            this._navigating = false;
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
                    const hashParts = location.hash.split('?');
                    const params = new URLSearchParams(hashParts[1] || '');
                    const scenarioId = params.get('id');
                    if (scenarioId) {
                        initScenarioEditor(null, parseInt(scenarioId));
                    } else {
                        initScenarioEditor(null);
                    }
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
