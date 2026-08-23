(() => {
    "use strict";

    if (window.badWolfGameplayRightOverlaySafeGapInitialized) {
        return;
    }

    window.badWolfGameplayRightOverlaySafeGapInitialized = true;

    const propertyName = "--gameplay-right-overlay-safe-gap";
    const finalAnsweringRightGapProperty = "--final-answering-drawer-right-gap";
    const finalAnsweringContentBasePaddingProperty =
        "--final-answering-content-base-padding-right";
    const finalAnsweringContentReserveProperty =
        "--final-answering-content-right-reserve";
    const finalAnsweringContentReserveClass =
        "final-answering-drawer-content-reserved";
    const breathingSpace = 8;
    const overlayScrollbarReserve = 16;
    const scrollbarOverflowTolerance = 1;
    const styleId = "gameplay-right-overlay-safe-gap-styles";
    const finalAnsweringListSelector =
        ".final-question-host[data-game-status=\"finalanswering\"] " +
        ".final-question-panel > .final-submission-list";
    const finalAnsweringDrawerSelector =
        ".final-question-host[data-game-status=\"finalanswering\"] " +
        ".final-question-panel > .final-submission-drawer";
    const finalAnsweringContentSelector =
        ".question-presentation .game-content-blocks";
    const finalAnsweringImageSelector =
        ".question-presentation .game-content-blocks img.game-content-image";
    const finalAnsweringDrawerClass = "final-submission-drawer";
    const observedFinalAnsweringImages = new WeakSet();
    let animationFrameHandle = 0;
    let finalAnsweringImageResizeObserver = null;

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
        right: var(--final-answering-drawer-right-gap, 0px);
        bottom: clamp(4rem, 8vh, 5.25rem);
        box-sizing: border-box;
        width: 2.75rem;
        max-width: min(24rem, calc(100% - 1rem));
        margin: 0;
        padding: 0;
        overflow: hidden;
        border: 1px solid var(--line);
        border-right: 0;
        border-radius: 0.9rem 0 0 0.9rem;
        background: var(--panel-glass);
        box-shadow: inset 18px 0 38px -30px rgb(0 0 0 / 28%);
        transform: none;
        transition: width 180ms ease, box-shadow 180ms ease;
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
        width: min(24rem, calc(100% - 1rem));
        box-shadow: inset 20px 0 44px -34px rgb(0 0 0 / 36%);
    }

    .final-question-host[data-game-status="finalanswering"]
        .question-presentation
        .game-content-blocks.${finalAnsweringContentReserveClass} {
        box-sizing: border-box;
        padding-right: calc(
            var(${finalAnsweringContentBasePaddingProperty}, 0px) +
            var(${finalAnsweringContentReserveProperty}, 0px));
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
        scrollbar-gutter: auto;
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

    const clearFinalAnsweringContentReservations = () => {
        document.querySelectorAll(`.${finalAnsweringContentReserveClass}`)
            .forEach(content => {
                if (!(content instanceof HTMLElement)) {
                    return;
                }

                const host = content.closest(
                    ".final-question-host[data-game-status=\"finalanswering\"]");
                if (host) {
                    return;
                }

                content.classList.remove(finalAnsweringContentReserveClass);
                content.style.removeProperty(
                    finalAnsweringContentBasePaddingProperty);
                content.style.removeProperty(finalAnsweringContentReserveProperty);
            });
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

    const observeFinalAnsweringImages = () => {
        if (!finalAnsweringImageResizeObserver) {
            return;
        }

        document.querySelectorAll(
            `.final-question-host[data-game-status="finalanswering"] ${finalAnsweringImageSelector}`)
            .forEach(image => {
                if (!(image instanceof HTMLImageElement) ||
                    observedFinalAnsweringImages.has(image)) {
                    return;
                }

                observedFinalAnsweringImages.add(image);
                finalAnsweringImageResizeObserver.observe(image);
            });
    };

    const hasVisibleVerticalScrollbar = element => {
        const styles = window.getComputedStyle(element);
        if (styles.overflowY !== "auto" && styles.overflowY !== "scroll") {
            return false;
        }

        return styles.overflowY === "scroll" ||
            element.scrollHeight >
                element.clientHeight + scrollbarOverflowTolerance;
    };

    const findFinalAnsweringScrollbarOwner = (content, panel) => {
        const host = panel.closest(".final-question-host");
        const panelRect = panel.getBoundingClientRect();

        for (let candidate = content;
            candidate instanceof HTMLElement;
            candidate = candidate.parentElement) {
            if (hasVisibleVerticalScrollbar(candidate)) {
                const candidateRect = candidate.getBoundingClientRect();
                if (candidateRect.right >= panelRect.right - 64 &&
                    candidateRect.left < panelRect.right) {
                    return candidate;
                }
            }

            if (candidate === host) {
                break;
            }
        }

        return null;
    };

    const reserveFinalAnsweringHandleLane = (
        content,
        drawer,
        panelRect,
        contentRect,
        rightGap) => {
        if (!content.classList.contains(finalAnsweringContentReserveClass)) {
            content.style.setProperty(
                finalAnsweringContentBasePaddingProperty,
                window.getComputedStyle(content).paddingRight);
            content.classList.add(finalAnsweringContentReserveClass);
        }

        const basePaddingRight = Number.parseFloat(
            content.style.getPropertyValue(
                finalAnsweringContentBasePaddingProperty)) || 0;
        const handleWidth = Number.parseFloat(
            window.getComputedStyle(drawer, "::before").width) || 44;
        const drawerLeftBoundary =
            panelRect.right - rightGap - handleWidth;
        const totalRightReserve = Math.max(
            0,
            Math.ceil(contentRect.right - drawerLeftBoundary));
        const additionalRightReserve = Math.max(
            0,
            totalRightReserve - basePaddingRight);

        content.style.setProperty(
            finalAnsweringContentReserveProperty,
            `${additionalRightReserve}px`);
    };

    const applyFinalAnsweringDrawerRightGap = (
        scrollbarWidth,
        safeGap) => {
        clearFinalAnsweringContentReservations();

        document.querySelectorAll(finalAnsweringDrawerSelector).forEach(drawer => {
            if (!(drawer instanceof HTMLElement)) {
                return;
            }

            const panel = drawer.closest(".final-question-panel");
            const content = panel?.querySelector(finalAnsweringContentSelector);
            if (!(panel instanceof HTMLElement) || !(content instanceof HTMLElement)) {
                drawer.style.removeProperty(finalAnsweringRightGapProperty);
                return;
            }

            const panelRect = panel.getBoundingClientRect();
            const contentRect = content.getBoundingClientRect();
            const pageRightGap = scrollbarWidth > 0 ? safeGap : 0;
            const scrollbarOwner = findFinalAnsweringScrollbarOwner(
                content,
                panel);
            let rightGap = pageRightGap;

            if (scrollbarOwner) {
                const scrollbarOwnerRect =
                    scrollbarOwner.getBoundingClientRect();
                const classicScrollbarWidth = Math.max(
                    0,
                    scrollbarOwner.offsetWidth - scrollbarOwner.clientWidth);
                const scrollbarReserve = Math.max(
                    overlayScrollbarReserve,
                    classicScrollbarWidth);
                const drawerRightBoundary =
                    scrollbarOwnerRect.right -
                    scrollbarReserve -
                    breathingSpace;
                rightGap = Math.max(
                    rightGap,
                    Math.ceil(panelRect.right - drawerRightBoundary));
            }

            rightGap = Math.max(0, rightGap);
            drawer.style.setProperty(
                finalAnsweringRightGapProperty,
                `${rightGap}px`);
            reserveFinalAnsweringHandleLane(
                content,
                drawer,
                panelRect,
                contentRect,
                rightGap);
        });
    };

    const applySafeGap = () => {
        const scrollbarWidth = Math.max(
            0,
            window.innerWidth - document.documentElement.clientWidth);
        const safeGap = scrollbarWidth + breathingSpace;
        document.documentElement.style.setProperty(
            propertyName,
            `${safeGap}px`);
        applyFinalAnsweringDrawerRightGap(scrollbarWidth, safeGap);
    };

    const refreshLayout = () => {
        ensureFinalAnsweringDrawer();
        observeFinalAnsweringImages();
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
            observeFinalAnsweringImages();
            applySafeGap();
        });
    };

    if (typeof ResizeObserver === "function") {
        finalAnsweringImageResizeObserver = new ResizeObserver(() => {
            refreshLayout();
        });
    }

    installStyles();
    refreshLayout();

    const mutationObserver = new MutationObserver(() => {
        if (document.querySelector(finalAnsweringListSelector) ||
            document.querySelector(finalAnsweringDrawerSelector) ||
            document.querySelector(`.${finalAnsweringContentReserveClass}`)) {
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
