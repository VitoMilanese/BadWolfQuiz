(() => {
    const root = document.querySelector('.player-lobby[data-game-code][data-player-id]');
    if (!root) return;

    const code = root.dataset.gameCode;
    const playerId = root.dataset.playerId;
    const accessToken = root.dataset.accessToken || '';
    let panel = null;
    let lastKey = '';

    const api = (handler, method = 'GET', data = null) => {
        const params = new URLSearchParams({ handler, code, playerId, accessToken });
        if (data) Object.entries(data).forEach(([key, value]) => params.set(key, value));
        return fetch(`/AnonymousSharedWager?${params}`, {
            method,
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            cache: 'no-store'
        }).then(response => response.ok ? response.json() : Promise.reject(response));
    };

    const setBuzzerSuppressed = suppressed => {
        const buzzer = document.getElementById('player-buzzer');
        const buzzerPanel = buzzer?.closest('.player-buzzer-panel');
        if (buzzerPanel) buzzerPanel.hidden = suppressed;
        if (buzzer) {
            buzzer.disabled = suppressed || buzzer.disabled;
            buzzer.dataset.anonymousSharedWagerSuppressed = suppressed ? 'true' : 'false';
        }
    };

    const ensurePanel = () => {
        if (panel?.isConnected) return panel;
        panel = document.createElement('section');
        panel.className = 'anonymous-shared-wager-player-panel';
        panel.setAttribute('aria-live', 'polite');
        const buzzerPanel = document.querySelector('.player-buzzer-panel');
        (buzzerPanel?.parentElement || root).insertBefore(panel, buzzerPanel || null);
        return panel;
    };

    const render = status => {
        if (!status.active || status.phase !== 'collecting') {
            setBuzzerSuppressed(false);
            panel?.remove();
            panel = null;
            lastKey = '';
            return;
        }

        setBuzzerSuppressed(true);
        const target = ensurePanel();
        const key = `${status.role}:${status.submitted}:${status.maximumShare}`;
        if (key === lastKey) return;
        lastKey = key;
        target.replaceChildren();

        const title = document.createElement('h2');
        title.textContent = 'Анонімна спільна ставка';
        target.append(title);

        if (status.role === 'answering') {
            const text = document.createElement('p');
            text.textContent = 'Інші гравці анонімно формують вашу ставку. Очікуйте завершення.';
            target.append(text);
            return;
        }

        if (status.role !== 'funding') {
            const text = document.createElement('p');
            text.textContent = 'Очікуйте завершення спільної ставки.';
            target.append(text);
            return;
        }

        if (status.submitted) {
            const text = document.createElement('p');
            text.textContent = 'Ваш внесок прийнято. Очікуйте інших гравців.';
            target.append(text);
            return;
        }

        const hint = document.createElement('p');
        hint.textContent = `Ваша частка: до ${status.maximumShare} очок. Це ваші власні очки під ризиком.`;
        target.append(hint);

        const choices = document.createElement('div');
        choices.className = 'anonymous-shared-wager-choices';
        let selected = null;
        [0, 25, 50, 75, 100].forEach(value => {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'button button-secondary';
            button.textContent = `${value}%`;
            button.addEventListener('click', () => {
                selected = value;
                choices.querySelectorAll('button').forEach(item => item.classList.remove('is-selected'));
                button.classList.add('is-selected');
                confirm.disabled = false;
            });
            choices.append(button);
        });
        target.append(choices);

        const confirm = document.createElement('button');
        confirm.type = 'button';
        confirm.className = 'button button-primary';
        confirm.textContent = 'Підтвердити внесок';
        confirm.disabled = true;
        confirm.addEventListener('click', async () => {
            if (selected === null) return;
            confirm.disabled = true;
            await api('Submit', 'POST', { percentage: selected });
            lastKey = '';
            await refresh();
        });
        target.append(confirm);
    };

    const refresh = async () => {
        try {
            render(await api('PlayerStatus'));
        } catch {
            // Existing realtime UI remains authoritative if the private status call fails.
        }
    };

    document.addEventListener('badwolf:player-gameplay-updated', refresh);
    setInterval(refresh, 1500);
    refresh();
})();
