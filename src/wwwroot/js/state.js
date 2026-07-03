/* ── ScanBridge State Manager ── */

const AppState = {
    _state: {
        scanners: [],
        scenarios: [],
        editingScenarioId: null,
        editingScenarioData: null,
        selectedNodeId: null,
        editingScenarioConfig: null,
        logEntries: [],
        replacementRules: [],
        tagRules: [],
        dashActivityChart: null,
        dashFormatChart: null,
        dashPollTimer: null,
        dashCurrentPeriod: '24h',
        logTimer: null,
        lastRenderedLogCount: 0,
        logUserScrolledUp: false,
        _activeSettingsContainer: 'actionSettings',
    },

    get(key) { return this._state[key]; },
    set(key, value) { this._state[key] = value; },

    resetPageState(page) {
        switch (page) {
            case 'scenarios':
                this._state.editingScenarioId = null;
                this._state.editingScenarioData = null;
                break;
            case 'scenario-editor':
                this._state.selectedNodeId = null;
                this._state.editingScenarioConfig = null;
                break;
            case 'logs':
                this._state.logEntries = [];
                this._state.lastRenderedLogCount = 0;
                this._state.logUserScrolledUp = false;
                break;
        }
    }
};
