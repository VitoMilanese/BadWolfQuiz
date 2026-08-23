(() => {
    "use strict";

    if (window.badWolfGameplayRightOverlaySafeGapInitialized) {
        return;
    }

    window.badWolfGameplayRightOverlaySafeGapInitialized = true;

    const propertyName = "--gameplay-right-overlay-safe-gap";
    const breathingSpace = 8;
    const styleId = "gameplay-right-overlay-safe-gap-styles";
    const finalAnsweringListSelector =
        ".final-question-host[data-game-status=\"finalanswering\"] " +
        ".final-question-panel > .final-submission-list";
    const finalAnsweringDrawerClass = "final-submission-drawer";
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
        visibility: hidden;
    }

    .final-question-host[data-game-status="finalanswering"]
        .final-question-panel > .final-submission-drawer {
        position: absolute;
        z-index: 40;
        top: clamp(3.25rem, 8vh, 5rem);
        right: var(--gameplay-right-overlay-safe-gap, 8px);
        bottom: clamp(4rem, 8vh, 5.25rem);
        width: min(24rem, calc(100% - 1rem));
        max-width: none;
        margin: 0;
        padding: 0;
        overflow: hidden;
        border: 1px solid var(--line);
        border-right: 0;
        border-radius: 0.9rem 0 0 0.9rem;
        background: var(--panel-glass);
        box-shadow: -18px 0 38px rgb(0 0 0 / 28%);
        transform: translateX(calc(100% - 2.75rem));
        transition: transform 180ms ease, box-shadow 180ms ease;
    }

    .final-question-host[data-game-status="finalanswering"]
        .final-question-panel > .final-submission-drawer::before {
        content: "👥";
        position: absolute;
        z-index: 2;
        top: 0;
        bottom: 0;
        left: 0;
        width: 2.75rem;
        display: grid;
        place-items: center;
        border-right: 1px solid var(--line);
        background: var(--panel-2);
        color: var(--text);
        font-size: 1.15rem;
        line-height: 1;
        pointer-events: none;
    }

    .final-question-host[data-game-status="finalanswering"]
        .final-question-panel > .final-submission-drawer:hover,
    .final-question-host[data-game-status="finalanswering"]
        .final-question-panel > .final-submission-drawer:focus-within {
        transform: translateX(0);
        box-shadow: -20px 0 44px rgb(0 0 0 / 36%);
    }

    .final-question-host[data-game-status="finalanswering"]
        .final-question-panel > .final-submission-drawer > .final-submission-list {
        position: absolute;
        top: 0.55rem;
        right: 0.75rem;
        bottom: 0.55rem;
        left: 3.25rem;
        width: auto;
        max-width: none;
        max-height: none;
        grid-template-columns: 1fr;
        align-content: start;
        gap: 0.4rem;
        margin: 0;
        padding: 0 1rem 0 0;
        overflow-x: hidden;
        overflow-y: auto;
        overscroll-behavior: contain;
        scrollbar-gutter: stable;
        border: 0;
        border-radius: 0;
        background: transparent;
        box-shadow: none;
        transform: none;
        transition: none;
        visibility: visible;
    }

    .final-question-host[data-game-status="finalanswering"]
        .final-question-panel > .final-submission-drawer > .final-submission-list > li {
        box-sizing: border-box;
        width: 100%;
        max-width: 100%;
        grid-template-columns: minmax(0, 1fr) auto auto;
    }
}

@media (prefers-reduced-motion: reduce) {
    .final-question-host[data-game-status="finalanswering"]
        .final-question-panel > .final-submission-drawer {
        transition: none;
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

    const ensureFinalAnsweringDrawer = () => {
        document.querySelectorAll(finalAnsweringListSelector).forEach(list => {
            if (!(list instanceof HTMLElement)) {
                return;
            }

            if (list.parentElement?.classList.contains(finalAnsweringDrawerClass)) {
                return;
            }

            const drawer = document.createElement("div");
            drawer.className = finalAnsweringDrawerClass;
            list.before(drawer);
            drawer.appendChild(list);
        });
    };

    const applySafeGap = () => {
        const scrollbarWidth = Math.max(
            0,
            window.innerWidth - document.documentElement.clientWidth);
        document.documentElement.style.setProperty(
            propertyName,
            `${scrollbarWidth + breathingSpace}px`);
    };

    const refreshLayout = () => {
        ensureFinalAnsweringDrawer();
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
            ensureFinalAnsweringDrawer();
            applySafeGap();
        });
    };

    installStyles();
    refreshLayout();

    const mutationObserver = new MutationObserver(() => {
        if (document.querySelector(finalAnsweringListSelector)) {
            refreshLayout();
        }
    });
    mutationObserver.observe(document.documentElement, {
        childList: true,
        subtree: true
    });

    window.addEventListener("resize", refreshLayout, { passive: true });
    window.addEventListener("pageshow", refreshLayout);
    window.visualViewport?.addEventListener(
        "resize",
        refreshLayout,
        { passive: true });
    document.addEventListener(
        "badwolf:host-shell-mounted",
        refreshLayout);
    document.addEventListener(
        "badwolf:host-gameplay-updated",
        refreshLayout);
})();
