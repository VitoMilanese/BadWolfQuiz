(() => {
    const root = document.querySelector('[data-word-rings-root]');
    const dialog = root?.querySelector('[data-word-rings-result-dialog]');
    if (!root || !(dialog instanceof HTMLDialogElement)) return;

    const card = dialog.querySelector('[data-word-rings-result-card]');
    const icon = dialog.querySelector('[data-result-icon]');
    const title = dialog.querySelector('[data-result-title]');
    const message = dialog.querySelector('[data-result-message]');
    const closeButton = dialog.querySelector('[data-close-result]');
    const reopenButton = root.querySelector('[data-reopen-result]');
    const burst = dialog.querySelector('.word-rings-result-burst');
    let shown = false;
    let lastResult = null;

    const format = (template, ...values) => values.reduce(
        (result, value, index) => result.replace(`{${index}}`, String(value)),
        template || '');

    const addParticles = won => {
        if (!(burst instanceof HTMLElement)) return;
        burst.replaceChildren();
        const count = won ? 22 : 12;
        for (let index = 0; index < count; index++) {
            const particle = document.createElement('span');
            particle.className = 'word-rings-result-particle';
            particle.style.setProperty('--particle-index', String(index));
            particle.style.setProperty('--particle-count', String(count));
            particle.style.setProperty('--particle-delay', `${(index % 6) * 38}ms`);
            burst.append(particle);
        }
    };

    const syncReopenButton = () => {
        if (!(reopenButton instanceof HTMLButtonElement)) return;
        reopenButton.hidden = !(lastResult && shown && !dialog.open && root.classList.contains('is-game-over'));
    };

    const clearResult = () => {
        shown = false;
        lastResult = null;
        if (dialog.open) dialog.close();
        dialog.classList.remove('is-win', 'is-loss');
        if (card instanceof HTMLElement) card.classList.remove('is-animating');
        if (burst instanceof HTMLElement) burst.replaceChildren();
        syncReopenButton();
    };

    const show = ({ won, titleText, messageText }, { repeat = false } = {}) => {
        if (shown && !repeat) return;
        shown = true;
        lastResult = { won, titleText, messageText };
        dialog.classList.toggle('is-win', won);
        dialog.classList.toggle('is-loss', !won);
        if (card instanceof HTMLElement) {
            card.classList.remove('is-animating');
            void card.offsetWidth;
            card.classList.add('is-animating');
        }
        if (icon) icon.textContent = won ? '★' : '×';
        if (title) title.textContent = titleText || (won ? root.dataset.winTitle : root.dataset.loseTitle) || '';
        if (message) message.textContent = messageText || '';
        addParticles(won);
        if (!dialog.open) dialog.showModal();
        syncReopenButton();
    };

    root.addEventListener('wordrings:game-ended', event => {
        const detail = event.detail || {};
        show({
            won: detail.won === true,
            titleText: detail.title,
            messageText: detail.message
        });
    });

    root.addEventListener('wordrings:game-reset', clearResult);

    const maybeShowSoloResult = () => {
        if (shown || root.dataset.gameMode !== 'solo' || !root.classList.contains('is-game-over')) return;
        const progress = root.querySelector('[data-progress]')?.textContent || '';
        const values = progress.match(/\d+(?:[.,]\d+)?/g)?.map(value => Number(value.replace(',', '.'))) || [];
        if (values.length < 2 || values[1] <= 0) return;
        const correct = values[0];
        const target = values[1];
        const won = correct >= target;
        const template = won ? root.dataset.soloWinTemplate : root.dataset.soloLoseTemplate;
        show({
            won,
            titleText: won ? root.dataset.winTitle : root.dataset.loseTitle,
            messageText: format(template, correct, target)
        });
    };

    const handleRootState = () => {
        if (root.dataset.gameMode === 'cooperative' && lastResult && !root.classList.contains('is-game-over')) {
            clearResult();
            return;
        }
        maybeShowSoloResult();
        syncReopenButton();
    };

    new MutationObserver(handleRootState).observe(root, {
        attributes: true,
        attributeFilter: ['class']
    });
    maybeShowSoloResult();

    reopenButton?.addEventListener('click', () => {
        if (lastResult) show(lastResult, { repeat: true });
    });
    closeButton?.addEventListener('click', () => dialog.close());
    dialog.addEventListener('close', syncReopenButton);
    dialog.addEventListener('cancel', event => {
        event.preventDefault();
        dialog.close();
    });
})();
