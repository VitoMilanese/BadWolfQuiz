(() => {
    const root = document.querySelector('[data-word-rings-root][data-game-mode="cooperative"]');
    if (!root) return;

    const apiUrl = root.dataset.roomApiUrl;
    const roomCode = String(root.dataset.roomCode || '').trim().toUpperCase();
    const antiForgery = root.querySelector('[data-word-rings-antiforgery] input[name="__RequestVerificationToken"]');
    const playersList = root.querySelector('[data-word-rings-player-list]');
    const wordList = root.querySelector('[data-word-list]');
    const startButton = root.querySelector('[data-start-room]');
    const chooseRulesButton = root.querySelector('[data-open-room-rule-picker]');
    const revealButton = root.querySelector('[data-reveal-rules]');
    const checkButton = root.querySelector('[data-check]');
    const judgePanel = root.querySelector('[data-room-host-judge]');
    const judgeButton = root.querySelector('[data-room-host-resolve]');
    const seedSetupPanel = root.querySelector('[data-room-host-seed-setup]');
    const seedConfirmButton = root.querySelector('[data-room-host-seed-confirm]');
    const lockButton = root.querySelector('[data-toggle-room-lock]');
    const ruleDialog = root.querySelector('[data-room-rule-picker-dialog]');
    const confirmRuleDialog = root.querySelector('[data-confirm-room-rule-picker]');
    const resultDialog = root.querySelector('[data-word-rings-result-dialog]');
    const storageKey = `badwolf.wordrings.room.${roomCode}`;
    if (!apiUrl || !roomCode) return;

    let hostState = null;
    let busy = false;
    let lastAutoOpenedVersion = null;

    const loadToken = () => {
        try { return String(JSON.parse(localStorage.getItem(storageKey) || 'null')?.token || ''); }
        catch { return ''; }
    };

    const post = async (handler, fields = {}) => {
        const data = new FormData();
        if (antiForgery instanceof HTMLInputElement) data.set('__RequestVerificationToken', antiForgery.value);
        Object.entries(fields).forEach(([key, value]) => data.set(key, String(value)));
        const response = await fetch(`${apiUrl}?handler=${encodeURIComponent(handler)}`, {
            method: 'POST', body: data, headers: { Accept: 'application/json' }
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json();
    };

    const action = async (handler, fields = {}, { renderBusy = true } = {}) => {
        const token = loadToken();
        if (!token || busy) return;
        busy = true;
        if (renderBusy) render();
        try {
            const payload = await post(handler, { roomCode, playerToken: token, ...fields });
            if (payload.success && payload.state) hostState = payload.state;
        } catch (error) {
            console.error(`Word Rings host action ${handler} failed.`, error);
        } finally {
            busy = false;
            render();
        }
    };

    const fetchState = async () => {
        const token = loadToken();
        if (!token || busy) return;
        try {
            const payload = await post('RoomHostState', { roomCode, playerToken: token });
            if (payload.success && payload.state) {
                hostState = payload.state;
                render();
            }
        } catch { }
    };

    const selection = ring => hostState?.ruleSelections?.find(item => item.ring === ring) || null;
    const rulesComplete = () => ['A', 'B', 'C'].every(ring => selection(ring)?.selectedRuleId);
    const isHostController = () => hostState?.isHost === true && hostState?.hostChoosesRules === true;
    const canChooseRules = () => isHostController() && hostState?.phase !== 'playing';

    const renderRulePicker = () => {
        if (!(ruleDialog instanceof HTMLDialogElement) || !hostState) return;
        if (!canChooseRules()) {
            if (ruleDialog.open) ruleDialog.close();
            return;
        }
        for (const ring of ['A', 'B', 'C']) {
            const group = ruleDialog.querySelector(`[data-room-rule-group="${ring}"]`);
            const options = group?.querySelector('[data-room-rule-options]');
            const refresh = group?.querySelector('[data-refresh-room-rules]');
            const current = selection(ring);
            if (!(options instanceof HTMLElement)) continue;

            const ruleOptions = current?.options || [];
            const existingButtons = [...options.querySelectorAll('.word-rings-rule-option[data-rule-id]')];
            const needsRebuild = existingButtons.length !== ruleOptions.length ||
                ruleOptions.some((option, index) => existingButtons[index]?.dataset.ruleId !== String(option.id));

            if (needsRebuild) {
                options.replaceChildren();
                for (const option of ruleOptions) {
                    const button = document.createElement('button');
                    button.type = 'button';
                    button.className = 'word-rings-rule-option';
                    button.dataset.ruleId = String(option.id);
                    button.addEventListener('click', () =>
                        void action('SelectRoomRule', { ring, ruleId: option.id }, { renderBusy: false }));
                    options.append(button);
                }
            }

            const buttonsById = new Map(
                [...options.querySelectorAll('.word-rings-rule-option[data-rule-id]')]
                    .map(button => [button.dataset.ruleId, button]));
            for (const option of ruleOptions) {
                const button = buttonsById.get(String(option.id));
                if (!(button instanceof HTMLButtonElement)) continue;
                if (button.textContent !== option.text) button.textContent = option.text;
                button.classList.toggle('is-selected', option.isSelected === true);
                button.disabled = busy || hostState.phase === 'playing';
                button.setAttribute('aria-pressed', option.isSelected === true ? 'true' : 'false');
            }

            if (refresh instanceof HTMLButtonElement) {
                refresh.dataset.ring = ring;
                refresh.disabled = busy || current?.canRefresh !== true || hostState.phase === 'playing';
            }
        }

        if (confirmRuleDialog instanceof HTMLButtonElement) {
            confirmRuleDialog.disabled = busy || hostState.phase === 'playing' || !rulesComplete();
        }
    };

    const renderPlayerActions = () => {
        if (!playersList || !hostState) return;
        const cards = [...playersList.querySelectorAll(':scope > .word-rings-player-card')];
        cards.forEach((card, index) => {
            card.querySelector('.word-rings-player-actions')?.remove();
            const player = hostState.players?.[index];
            if (!player || hostState.isHost !== true || player.id === hostState.playerId) return;

            const actions = document.createElement('div');
            actions.className = 'word-rings-player-actions';
            if (hostState.phase === 'playing' && hostState.seedSetupPending !== true && player.isPlayingParticipant && Number(player.remainingWords) > 0 && player.id !== hostState.currentPlayerId) {
                const pass = document.createElement('button');
                pass.type = 'button';
                pass.className = 'word-rings-player-action';
                pass.textContent = '➜';
                pass.title = root.dataset.roomPassTurn || '';
                pass.setAttribute('aria-label', pass.title);
                pass.disabled = busy || Boolean(hostState.pendingPlacement);
                pass.addEventListener('click', () => void action('SetRoomTurn', { playerId: player.id }));
                actions.append(pass);
            }
            if (hostState.phase !== 'finished') {
                const kick = document.createElement('button');
                kick.type = 'button';
                kick.className = 'word-rings-player-action is-danger';
                kick.textContent = '×';
                kick.title = root.dataset.roomKickPlayer || '';
                kick.setAttribute('aria-label', kick.title);
                kick.disabled = busy || Boolean(hostState.pendingPlacement);
                kick.addEventListener('click', () => void action('KickRoomPlayer', { playerId: player.id }));
                actions.append(kick);
            }
            if (actions.childElementCount) card.append(actions);
        });
    };

    const applyHandLimit = () => {
        if (!wordList || !hostState) return;
        const limit = Math.max(5, Math.min(10, Number(hostState.handLimit || hostState.targetScore || 10)));
        const available = [...wordList.querySelectorAll(':scope > .word-rings-word[data-word]')]
            .filter(token => !token.classList.contains('is-placed') && (!token.hidden || token.dataset.roomHostHidden === 'true'));
        available.forEach((token, index) => {
            const shouldHide = index >= limit;
            if (shouldHide && token.dataset.roomHostHidden !== 'true') {
                token.hidden = true;
                token.dataset.roomHostHidden = 'true';
            } else if (!shouldHide && token.dataset.roomHostHidden === 'true') {
                token.hidden = false;
                delete token.dataset.roomHostHidden;
            }
        });
    };

    const renderHostJudgement = () => {
        const pending = isHostController() && hostState?.phase === 'playing'
            ? hostState.pendingPlacement
            : null;
        if (judgePanel instanceof HTMLElement) judgePanel.hidden = !pending;
        if (judgeButton instanceof HTMLButtonElement) {
            judgeButton.hidden = !pending;
            judgeButton.disabled = busy || !pending;
            judgeButton.textContent = pending?.wasMoved
                ? root.dataset.roomHostMove || ''
                : root.dataset.roomHostCorrect || '';
            judgeButton.dataset.placementId = pending ? String(pending.id) : '';
        }
        if (seedSetupPanel instanceof HTMLElement) {
            seedSetupPanel.hidden = !(isHostController() && hostState?.seedSetupPending === true);
        }
        if (seedConfirmButton instanceof HTMLButtonElement) {
            seedConfirmButton.hidden = !(isHostController() && hostState?.seedSetupPending === true);
            seedConfirmButton.disabled = busy || Number(hostState?.seedWordsRemaining || 0) > 0;
        }
        if (checkButton instanceof HTMLButtonElement) checkButton.hidden = isHostController();
    };

    const renderToolbar = () => {
        if (!hostState) return;
        if (startButton instanceof HTMLButtonElement) {
            startButton.hidden = hostState.isHost !== true || hostState.phase === 'playing';
            startButton.disabled = busy || hostState.canStart !== true;
        }
        if (chooseRulesButton instanceof HTMLButtonElement) {
            const canOpenRules = canChooseRules();
            chooseRulesButton.hidden = !canOpenRules;
            chooseRulesButton.classList.toggle('is-hidden', !canOpenRules);
            chooseRulesButton.disabled = busy || !canOpenRules;
        }
        if (revealButton instanceof HTMLButtonElement) {
            const canRevealRules = isHostController() && hostState.phase !== 'finished';
            revealButton.hidden = !canRevealRules;
        }
        if (lockButton instanceof HTMLButtonElement) {
            lockButton.hidden = !(hostState.isHost === true && hostState.phase === 'waiting');
            lockButton.disabled = busy;
            const locked = hostState.joinLocked === true;
            const label = locked ? root.dataset.roomUnlockJoining : root.dataset.roomLockJoining;
            lockButton.textContent = locked ? '🔒' : '🔓';
            lockButton.title = label || '';
            lockButton.setAttribute('aria-label', label || '');
            lockButton.setAttribute('aria-pressed', locked ? 'true' : 'false');
        }
        root.classList.toggle('is-host-controller', hostState.isHost === true && hostState.hostChoosesRules === true);
        if (root.classList.contains('is-host-controller') && resultDialog instanceof HTMLDialogElement && resultDialog.open) resultDialog.close();
    };

    const render = () => {
        renderToolbar();
        renderRulePicker();
        renderPlayerActions();
        renderHostJudgement();
        applyHandLimit();
        if (canChooseRules() && !rulesComplete()) {
            if (lastAutoOpenedVersion !== hostState.version && ruleDialog instanceof HTMLDialogElement && !ruleDialog.open) {
                lastAutoOpenedVersion = hostState.version;
                ruleDialog.showModal();
            }
        }
    };

    startButton?.addEventListener('click', event => {
        if (hostState?.isHost !== true) {
            event.preventDefault();
            event.stopImmediatePropagation();
            return;
        }
        if (hostState.hostChoosesRules === true && hostState.canStart !== true) {
            event.preventDefault();
            event.stopImmediatePropagation();
            if (!rulesComplete() && ruleDialog instanceof HTMLDialogElement && !ruleDialog.open) {
                renderRulePicker();
                ruleDialog.showModal();
            }
            return;
        }
        if (hostState.canStart === true && startButton instanceof HTMLButtonElement) {
            startButton.disabled = true;
        }
    }, true);

    root.addEventListener('wordrings:game-ended', event => {
        if (!isHostController()) return;
        event.stopImmediatePropagation();
        if (resultDialog instanceof HTMLDialogElement && resultDialog.open) resultDialog.close();
    }, true);

    chooseRulesButton?.addEventListener('click', () => {
        if (!canChooseRules()) {
            if (ruleDialog instanceof HTMLDialogElement && ruleDialog.open) ruleDialog.close();
            return;
        }
        renderRulePicker();
        if (ruleDialog instanceof HTMLDialogElement && !ruleDialog.open) ruleDialog.showModal();
    });
    confirmRuleDialog?.addEventListener('click', () => {
        if (rulesComplete() && ruleDialog instanceof HTMLDialogElement) ruleDialog.close();
    });
    ruleDialog?.addEventListener('cancel', event => { event.preventDefault(); ruleDialog.close(); });
    ruleDialog?.querySelectorAll('[data-refresh-room-rules]').forEach(button => {
        button.addEventListener('click', () => {
            const ring = button.dataset.ring || button.closest('[data-room-rule-group]')?.dataset.roomRuleGroup;
            if (ring) void action('RefreshRoomRules', { ring }, { renderBusy: false });
        });
    });
    seedConfirmButton?.addEventListener('click', () => void action('ConfirmRoomSeeds'));
    judgeButton?.addEventListener('click', () => {
        const placementId = Number.parseInt(judgeButton.dataset.placementId || '', 10);
        if (Number.isFinite(placementId)) void action('ResolveRoomPlacement', { placementId });
    });
    root.addEventListener('wordrings:host-pending-moved', () => void fetchState());
    lockButton?.addEventListener('click', () => void action('SetRoomJoinLock', { locked: hostState?.joinLocked !== true }));

    if (playersList) new MutationObserver(renderPlayerActions).observe(playersList, { childList: true });
    if (wordList) new MutationObserver(applyHandLimit).observe(wordList, { childList: true, attributes: true, attributeFilter: ['hidden', 'class'] });
    if (resultDialog instanceof HTMLDialogElement) new MutationObserver(renderToolbar).observe(resultDialog, { attributes: true, attributeFilter: ['open'] });

    void fetchState();
    window.setInterval(fetchState, 1000);
})();
