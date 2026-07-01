/* ── ScanBridge API Client ── */

class ApiError extends Error {
    constructor(status, body) {
        super(`API error ${status}: ${body}`);
        this.status = status;
        this.body = body;
    }
}

async function safeJson(res) {
    const text = await res.text();
    if (!text) return null;
    return JSON.parse(text);
}

const Api = {
    async get(url) {
        const res = await fetch(url);
        if (!res.ok) throw new ApiError(res.status, await res.text());
        return res.json();
    },

    async post(url, data) {
        const res = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
        if (!res.ok) throw new ApiError(res.status, await res.text());
        return safeJson(res);
    },

    async put(url, data) {
        const res = await fetch(url, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
        if (!res.ok) throw new ApiError(res.status, await res.text());
        return safeJson(res);
    },

    async delete(url) {
        const res = await fetch(url, { method: 'DELETE' });
        if (!res.ok) throw new ApiError(res.status, await res.text());
    }
};

function handleApiError(error, context) {
    console.error(`${context}:`, error);
    if (error instanceof ApiError && error.status >= 500) {
        showToast('Сервер недоступен. Попробуйте позже.', 'error');
    } else if (error instanceof ApiError) {
        showToast(`Ошибка: ${error.message}`, 'error');
    } else {
        showToast('Неожиданная ошибка', 'error');
    }
}
