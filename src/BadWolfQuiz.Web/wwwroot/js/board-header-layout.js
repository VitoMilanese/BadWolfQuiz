(() => {
    if (window.badWolfBoardHeaderLayoutInitialized) {
        return;
    }

    window.badWolfBoardHeaderLayoutInitialized = true;

    const syncHeaderHeights = () => {
        const grid = document.querySelector(".host-board-grid");
        if (!(grid instanceof HTMLElement) ||
            grid.getClientRects().length === 0) {
            return false;
        }

        const headers = Array.from(grid.querySelectorAll(
            ".host-board-column > h3"));
        if (headers.length === 0) {
            return false;
        }

        headers.forEach(header => {
            header.style.height = "";
        });

        const maximumHeight = Math.max(
            ...headers.map(header => header.offsetHeight));
        if (maximumHeight <= 0) {
            return false;
        }

        headers.forEach(header => {
            header.style.height = `${maximumHeight}px`;
        });
        return true;
    };

    let followUpFrame = 0;
    const syncBeforePaint = () => {
        syncHeaderHeights();

        if (followUpFrame !== 0) {
            window.cancelAnimationFrame(followUpFrame);
        }
        followUpFrame = window.requestAnimationFrame(() => {
            followUpFrame = 0;
            syncHeaderHeights();
        });
    };

    document.addEventListener(
        "badwolf:host-gameplay-updated",
        syncBeforePaint);
    window.addEventListener("resize", syncBeforePaint);

    syncBeforePaint();
})();
