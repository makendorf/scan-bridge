/* ── ScanBridge Logs Module ── */

let logEntries = [];
let logTimer = null;
let lastRenderedLogCount = 0;
let logUserScrolledUp = false;

function startLogPolling() { stopLogPolling(); logTimer = setInterval(loadLogs, 1000); }
function stopLogPolling() { if (logTimer) { clearInterval(logTimer); logTimer = null; } }

async function loadLogs() {
    try {
        logEntries = await Api.get('/api/logs');
        renderLogs();
    } catch (e) { handleApiError(e, 'Загрузка логов'); }
}

function renderLogLine(e) {
    const time = new Date(e.timestamp).toLocaleTimeString('ru-RU');
    const exc = e.exception ? `<div class="log-exc">${esc(e.exception)}</div>` : '';
    return `<div class="log-line"><span class="log-time">${time}</span><span class="log-level log-level-${e.level}">${e.level}</span><span class="log-msg">${esc(e.message)}${exc}</span></div>`;
}

function getFilteredLogs() {
    const showINF = document.getElementById('showINF').checked;
    const showWRN = document.getElementById('showWRN').checked;
    const showERR = document.getElementById('showERR').checked;
    const showDBG = document.getElementById('showDBG').checked;
    return logEntries.filter(e => {
        if (e.level === 'INF' && !showINF) return false;
        if (e.level === 'WRN' && !showWRN) return false;
        if (e.level === 'ERR' && !showERR) return false;
        if (e.level === 'DBG' && !showDBG) return false;
        return true;
    }).reverse();
}

function renderLogs() {
    const panel = document.getElementById('logPanel');
    const filtered = getFilteredLogs();
    const atBottom = panel.scrollHeight - panel.scrollTop - panel.clientHeight < 30;

    if (filtered.length < lastRenderedLogCount) {
        panel.innerHTML = '';
        lastRenderedLogCount = 0;
    }

    if (lastRenderedLogCount === 0) {
        panel.innerHTML = filtered.map(renderLogLine).join('');
        lastRenderedLogCount = filtered.length;
    } else if (filtered.length > lastRenderedLogCount) {
        const prevHeight = panel.scrollHeight;
        const newEntries = filtered.slice(0, filtered.length - lastRenderedLogCount);
        panel.insertAdjacentHTML('afterbegin', newEntries.map(renderLogLine).join(''));
        lastRenderedLogCount = filtered.length;
        if (!atBottom && !logUserScrolledUp) {
            panel.scrollTop += panel.scrollHeight - prevHeight;
        }
    }

    if (atBottom || (document.getElementById('autoScroll').checked && !logUserScrolledUp))
        panel.scrollTop = panel.scrollHeight;
}

function initLogsPage() {
    const panel = document.getElementById('logPanel');
    if (panel) {
        panel.addEventListener('scroll', function() {
            const atBottom = panel.scrollHeight - panel.scrollTop - panel.clientHeight < 30;
            if (atBottom) logUserScrolledUp = false;
            else logUserScrolledUp = true;
        });
    }
    document.querySelectorAll('.log-toolbar input').forEach(cb => cb.addEventListener('change', () => {
        lastRenderedLogCount = 0;
        renderLogs();
    }));
}

async function clearLogs() {
    try {
        await Api.delete('/api/logs');
        logEntries = [];
        lastRenderedLogCount = 0;
        document.getElementById('logPanel').innerHTML = '';
    } catch (e) { handleApiError(e, 'Очистка логов'); }
}
