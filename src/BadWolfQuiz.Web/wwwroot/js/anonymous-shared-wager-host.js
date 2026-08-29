(() => {
    if (window.BadWolfAnonymousSharedWagerHostStarted) return;
    window.BadWolfAnonymousSharedWagerHostStarted = true;

    const getRoot = () => document.querySelector('.host-game-board');
    if (!getRoot()) return;

    const match = location.pathname.match(/\/Admin\/Games\/Lobby\/([0-9a-f-]{36})/i);
    if (!match) return;
    const gameId = match[1];
    let panel = null;
    let lastKey = '';
    let lastPhase = null;
    let refreshingForActivation = false;

    const api = (handler, method = 'GET', data = null) => {
        const params = new URLSearchParams({ handler, gameId });
        if (data) Object.entries(data).forEach(([key, value]) => params.set(key, value));
        return fetch(`/AnonymousSharedWager?${params}`, {
            method,
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            cache: 'no-store'
        }).then(response => response.ok ? response.json() : Promise.reject(response));
    };

    const suppressNormalControls = suppress => {
        document.querySelectorAll('.question-judge-actions, .question-wager-form').forEach(element => {
            if (element.classList.contains('anonymous-shared-wager-judge-actions')) return;
            element.hidden = suppress;
            if (suppress) {
                element.style.setProperty('display', 'none', 'important');
            } else {
                element.style.removeProperty('display');
            }
        });
    };

    const ensurePanel = () => {
        if (!getRoot()) return null;
        if (panel?.isConnected) return panel;

        panel = document.querySelector(
            '[data-anonymous-shared-wager-host-panel]');
        return panel?.isConnected ? panel : null;
    };

    const removePanel = () => {
        document.querySelectorAll(
            '[data-anonymous-shared-wager-host-panel]').forEach(item => {
                item.replaceChildren();
                item.hidden = true;
            });
        panel = null;
    };

    const force = async playerId => {
        await api('Force', 'POST', { playerId });
        lastKey = '';
        await refresh();
    };

    const settle = async isCorrect => {
        await api('Settle', 'POST', { isCorrect });
        removePanel();
        suppressNormalControls(false);
        lastKey = '';
        lastPhase = null;
        await window.BadWolfHostGameplay?.refresh?.();
    };

    const render = status => {
        if (!status.active) {
            removePanel();
            suppressNormalControls(false);
            lastKey = '';
            lastPhase = null;
            return;
        }

        const target = ensurePanel();
        if (!target) return;
        target.hidden = false;
        const key = JSON.stringify({
            phase: status.phase,
            participants: status.participants,
            combinedWager: status.combinedWager,
            answeringPlayerId: status.answeringPlayerId
        });
        if (key === lastKey) return;
        lastKey = key;
        target.replaceChildren();
        suppressNormalControls(true);

        const title = document.createElement('h3');
        title.textContent = 'Анонімна спільна ставка';
        target.append(title);

        if (status.phase === 'collecting') {
            const note = document.createElement('p');
            note.textContent = `Ставку для ${status.answeringPlayerName} формують інші гравці. Суми та відсотки приховані.`;
            target.append(note);

            const list = document.createElement('ul');
            status.participants.forEach(participant => {
                const item = document.createElement('li');
                const label = document.createElement('span');
                label.textContent = `${participant.playerName}: ${participant.submitted ? 'готово' : 'очікується'}`;
                item.append(label);
                if (!participant.submitted) {
                    const button = document.createElement('button');
                    button.type = 'button';
                    button.className = 'button button-secondary';
                    button.textContent = 'AFK → 100%';
                    button.addEventListener('click', () => force(participant.playerId));
                    item.append(' ', button);
                }
                list.append(item);
            });
            target.append(list);
            return;
        }

        const summary = document.createElement('p');
        summary.textContent = `Ставка сформована: ${status.combinedWager}. Відповідає ${status.answeringPlayerName}.`;
        target.append(summary);

        const actions = document.createElement('div');
        actions.className = 'question-judge-actions anonymous-shared-wager-judge-actions';
        const correct = document.createElement('button');
        correct.type = 'button';
        correct.className = 'button judgment-correct-button';
        correct.textContent = 'Правильно';
        correct.addEventListener('click', () => settle(true));
        const incorrect = document.createElement('button');
        incorrect.type = 'button';
        incorrect.className = 'button judgment-incorrect-button';
        incorrect.textContent = 'Неправильно / немає відповіді';
        incorrect.addEventListener('click', () => settle(false));
        actions.append(correct, incorrect);
        target.append(actions);
    };

    const refresh = async () => {
        try {
            const status = await api('HostStatus');
            if (status.active &&
                status.phase === 'answering' &&
                lastPhase === 'collecting' &&
                !refreshingForActivation) {
                refreshingForActivation = true;
                await window.BadWolfHostGameplay?.refresh?.();
                removePanel();
                lastKey = '';
                refreshingForActivation = false;
            }
            lastPhase = status.active ? status.phase : null;
            render(status);
        } catch {
            // Keep the normal host surface usable if the private endpoint is unavailable.
        }
    };

    document.addEventListener('badwolf:host-gameplay-updated', () => {
        panel = null;
        lastKey = '';
        refresh();
    });
    setInterval(refresh, 1200);
    refresh();
})();
