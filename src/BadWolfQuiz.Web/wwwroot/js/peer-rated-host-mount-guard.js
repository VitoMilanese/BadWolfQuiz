(() => {
    "use strict";

    if (window.badWolfPeerRatedHostMountGuardInitialized) {
        return;
    }
    window.badWolfPeerRatedHostMountGuardInitialized = true;

    const enforceHostMount = () => {
        const board = document.querySelector(".host-game-board[data-game-code]");
        if (!(board instanceof HTMLElement)) {
            return;
        }

        const summary = board.querySelector(".current-question-summary");
        for (const ui of board.querySelectorAll(".peer-rated-host-ui")) {
            if (!(ui instanceof HTMLElement)) {
                continue;
            }

            if (!(summary instanceof HTMLElement) || !summary.contains(ui)) {
                ui.remove();
            }
        }
    };

    document.addEventListener("badwolf:host-gameplay-updated", enforceHostMount);
    document.addEventListener("badwolf:host-shell-mounted", enforceHostMount);
    window.addEventListener("pageshow", enforceHostMount);

    enforceHostMount();
    new MutationObserver(enforceHostMount)
        .observe(document.documentElement, {
            childList: true,
            subtree: true
        });
})();
