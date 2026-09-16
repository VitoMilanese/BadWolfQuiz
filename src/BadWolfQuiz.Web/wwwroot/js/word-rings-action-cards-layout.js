(() => {
    const root = document.querySelector('[data-word-rings-root]');
    if (!(root instanceof HTMLElement)) return;

    const layout = root.querySelector('.word-rings-layout');
    const players = root.querySelector('[data-word-rings-players]');
    const actionShell = root.querySelector('[data-action-cards-shell]');
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

    const observer = new MutationObserver(syncVisibility);
    observer.observe(players, { attributes: true, attributeFilter: ['hidden'] });
    observer.observe(actionShell, { attributes: true, attributeFilter: ['hidden'] });
    syncVisibility();
})();
