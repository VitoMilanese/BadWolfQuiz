(() => {
    const root = document.querySelector('[data-word-rings-root]');
    if (!(root instanceof HTMLElement)) return;

    const layout = root.querySelector('.word-rings-layout');
    const players = root.querySelector('[data-word-rings-players]');
    const actionShell = root.querySelector('[data-action-cards-shell]');
    const theftPreview = root.querySelector('[data-action-theft-preview]');
    if (!(layout instanceof HTMLElement) ||
        !(players instanceof HTMLElement) ||
        !(actionShell instanceof HTMLElement)) return;

    let leftColumn = layout.querySelector('.word-rings-action-left-column');
    if (!(leftColumn instanceof HTMLElement)) {
        leftColumn = document.createElement('aside');
        leftColumn.className = 'word-rings-action-left-column';
        layout.insertBefore(leftColumn, layout.firstElementChild);
    }

    if (players.parentElement !== leftColumn) leftColumn.append(players);
    if (actionShell.parentElement !== leftColumn) leftColumn.append(actionShell);

    const syncVisibility = () => {
        const hasPlayers = !players.hidden;
        const hasActionCards = !actionShell.hidden;
        leftColumn.hidden = !hasPlayers && !hasActionCards;
        layout.classList.toggle('has-action-left-column', !leftColumn.hidden);
    };

    const syncTheftPlaceholder = () => {
        if (!(theftPreview instanceof HTMLElement)) return;
        const current = theftPreview.querySelector('.word-rings-action-theft-placeholder');
        const revealedCard = theftPreview.querySelector('.word-rings-action-card.is-theft-preview');
        if (theftPreview.hidden || revealedCard) {
            current?.remove();
            return;
        }
        if (current instanceof HTMLElement) return;

        const placeholder = document.createElement('div');
        placeholder.className = 'word-rings-action-theft-placeholder';
        placeholder.setAttribute('aria-hidden', 'true');
        const status = theftPreview.querySelector('.word-rings-action-theft-status');
        theftPreview.insertBefore(placeholder, status);
    };

    const syncLayout = () => {
        syncVisibility();
        syncTheftPlaceholder();
    };

    const observer = new MutationObserver(syncLayout);
    observer.observe(players, { attributes: true, attributeFilter: ['hidden'] });
    observer.observe(actionShell, { attributes: true, attributeFilter: ['hidden'] });
    if (theftPreview instanceof HTMLElement) {
        observer.observe(theftPreview, { childList: true, attributes: true, attributeFilter: ['hidden'] });
    }
    syncLayout();
})();
