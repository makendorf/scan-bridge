/* ── ScanBridge Credential Field Module ── */
/* Переиспользуемый компонент для выбора учётных данных в настройках узлов */

const CredentialField = {
    /**
     * Генерирует HTML для dropdown учётных данных.
     * @param {string} key - Имя поля (data-key)
     * @param {string} label - Подпись
     * @param {string} showWhen - Правило показа (формат: "Field=val1|val2")
     * @param {string} selectedId - ID выбранного элемента
     * @returns {string} HTML
     */
    render(key, label, showWhen, selectedId) {
        return `<div class="form-group" data-showwhen="${showWhen || ''}">
            <label>${label}</label>
            <select class="set-field vs-setting" data-key="${key}" id="credSelect_${key}">
                <option value="">Не выбрано</option>
            </select>
        </div>`;
    },

    /**
     * Загружает опции в dropdown учётных данных.
     * @param {string} selectId - ID элемента select
     * @param {string|number} selectedId - ID выбранного элемента
     */
    async loadOptions(selectId, selectedId) {
        try {
            const data = await Api.get('/api/credentials');
            const sel = document.getElementById(selectId);
            if (!sel) return;
            const currentVal = selectedId || sel.value || '';
            sel.innerHTML = '<option value="">Не выбрано</option>';
            data.forEach(c => {
                const opt = document.createElement('option');
                opt.value = c.id;
                opt.textContent = `${c.name} (${c.type})`;
                if (String(c.id) === String(currentVal)) opt.selected = true;
                sel.appendChild(opt);
            });
        } catch (e) {
            console.error('CredentialField.loadOptions:', e);
        }
    },

    /**
     * Загружает credentials для всех select полей в контейнере.
     * @param {string} containerId - ID контейнера
     * @param {object} existing - Текущие настройки узла
     */
    loadAll(containerId, existing) {
        const container = document.getElementById(containerId);
        if (!container) return;
        container.querySelectorAll('select[data-key="CredentialId"]').forEach(sel => {
            const key = sel.dataset.key;
            const currentVal = existing[key] || '';
            this.loadOptions(sel.id, currentVal);
        });
    },

    /**
     * Перезагружает credentials для видимых select полей.
     * @param {string} containerId - ID контейнера
     */
    reloadVisible(containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;
        container.querySelectorAll('select[data-key="CredentialId"]').forEach(sel => {
            const group = sel.closest('.form-group');
            if (!group || group.style.display !== 'none') {
                const currentVal = sel.value || '';
                this.loadOptions(sel.id, currentVal);
            }
        });
    },

    /**
     * Обработчик смены настроек — перезагружает видимые credentials.
     * @param {string} containerId - ID контейнера
     */
    onSettingChange(containerId) {
        this.reloadVisible(containerId);
    }
};
