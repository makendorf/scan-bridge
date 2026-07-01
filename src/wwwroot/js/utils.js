/* ── ScanBridge Utils ── */

function esc(s) { const d = document.createElement('div'); d.textContent = s; return d.innerHTML; }
const escapeHtml = esc;

/* ── Modal Helpers ── */
function openModal(id) {
    document.getElementById(id).classList.add('open');
    lucide.createIcons();
}
function closeModal(id) {
    document.getElementById(id).classList.remove('open');
}
document.querySelectorAll('.modal-overlay').forEach(el => {
    el.addEventListener('click', function(e) { if (e.target === this) this.classList.remove('open'); });
});

/* ── Toast ── */
function toast(msg, type) {
    const t = document.createElement('div');
    t.className = 'toast ' + (type === 'error' ? 'toast-error' : 'toast-success');
    t.textContent = msg;
    document.getElementById('toastContainer').appendChild(t);
    setTimeout(() => t.remove(), 3000);
}
const showToast = toast;

/* ── Theme Toggle ── */
function getTheme() { return localStorage.getItem('scanbridge-theme') || 'dark'; }
function applyTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('scanbridge-theme', theme);
}
function toggleTheme() {
    const current = getTheme();
    applyTheme(current === 'dark' ? 'light' : 'dark');
}
applyTheme(getTheme());
