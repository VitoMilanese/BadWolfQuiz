(() => {
    let refreshFrame = 0;

    const refreshAfterGameplayUpdate = () => {
        if (refreshFrame !== 0) {
            return;
        }

        refreshFrame = window.requestAnimationFrame(() => {
            refreshFrame = 0;
            window.BadWolfStarfield?.refresh?.();
        });
    };

    document.addEventListener(
        'badwolf:host-gameplay-updated',
        refreshAfterGameplayUpdate);
})();
