(() => {
    "use strict";

    if (window.badWolfGameplayRightOverlaySafeGapInitialized) {
        return;
    }

    window.badWolfGameplayRightOverlaySafeGapInitialized = true;

    const propertyName = "--gameplay-right-overlay-safe-gap";
    const breathingSpace = 8;
    const styleId = "gameplay-right-overlay-safe-gap-styles";
    let animationFrameHandle = 0;

    const installStyles = () => {
        if (document.getElementById(styleId)) {
            return;
        }

        const style = document.createElement("style");
        style.id = styleId;
        style.textContent = `
@media (hover: hover) and (pointer: fine) and (min-width: 801px) {
    .host-game-board.all-player-question-answering
        .current-question-summary .all-player-host-progress {
        right: var(--gameplay-right-overlay-safe-gap, 8px);
    }

    .final-question-host[data-game-status="finalanswering"]
        .final-question-panel > .final-submission-list {
        right: var(--gameplay-right-overlay-safe-gap, 8px);
        padding-right: calc(0.6rem + 1rem);
    }

    .final-question-host[data-game-status="finalanswering"]
        .final-question-panel > .final-submission-list > li {
        box-sizing: border-box;
        width: auto;
        max-width: 100%;
    }
}

@media (min-width: 801px) {
    .final-question-host[data-game-status="finalwagering"]
        .final-question-panel > .final-submission-list {
        right: calc(0.75rem + var(--gameplay-right-overlay-safe-gap, 8px));
        scrollbar-gutter: stable;
    }
}
`;
        document.head.appendChild(style);
    };

    const applySafeGap = () => {
        const scrollbarWidth = Math.max(
            0,
            window.innerWidth - document.documentElement.clientWidth);
        document.documentElement.style.setProperty(
            propertyName,
            `${scrollbarWidth + breathingSpace}px`);
    };

    const scheduleSafeGapUpdate = () => {
        applySafeGap();

        if (animationFrameHandle !== 0 &&
            typeof window.cancelAnimationFrame === "function") {
            window.cancelAnimationFrame(animationFrameHandle);
            animationFrameHandle = 0;
        }

        if (typeof window.requestAnimationFrame !== "function") {
            return;
        }

        animationFrameHandle = window.requestAnimationFrame(() => {
            animationFrameHandle = 0;
            applySafeGap();
        });
    };

    installStyles();
    scheduleSafeGapUpdate();

    window.addEventListener("resize", scheduleSafeGapUpdate, { passive: true });
    window.addEventListener("pageshow", scheduleSafeGapUpdate);
    window.visualViewport?.addEventListener(
        "resize",
        scheduleSafeGapUpdate,
        { passive: true });
    document.addEventListener(
        "badwolf:host-shell-mounted",
        scheduleSafeGapUpdate);
    document.addEventListener(
        "badwolf:host-gameplay-updated",
        scheduleSafeGapUpdate);
})();
