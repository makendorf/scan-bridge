/* ── ScanBridge State Manager ── */

const AppState = {
    _state: {
        scanners: [],
        postScanGroups: [],
        scenarios: [],
        currentGroupId: null,
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
            case 'actions':
                this._state.currentGroupId = null;
                this._state.replacementRules = [];
                this._state.tagRules = [];
                break;
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
