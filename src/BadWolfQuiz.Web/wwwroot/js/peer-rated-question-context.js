(() => {
    "use strict";

    if (window.badWolfPeerRatedQuestionContextInitialized) {
        return;
    }
    window.badWolfPeerRatedQuestionContextInitialized = true;

    const style = document.createElement("style");
    style.id = "peer-rated-question-context-styles";
    style.textContent = `
.host-game-board.peer-rated-reviewing
    .current-question-summary > .question-presentation > [data-question-heading] {
    visibility: hidden !important;
}
.host-game-board.peer-rated-reviewing .peer-rated-question-context {
    position: absolute;
    top: clamp(3.5rem, 7vh, 5rem);
    left: .75rem;
    right: var(--peer-rated-question-context-right-gap, 24rem);
    z-index: 37;
    box-sizing: border-box;
    display: grid !important;
    grid-template-rows: auto minmax(0, auto) !important;
    gap: clamp(.45rem, 1vh, .8rem);
    width: auto !important;
    max-width: none !important;
    min-height: 0 !important;
    height: auto !important;
    max-height: var(--peer-rated-question-context-max-height, 42%);
    margin: 0;
    padding: clamp(.55rem, 1.3vw, .9rem);
    overflow: auto;
    overscroll-behavior: contain;
    border: 1px solid var(--line);
    border-radius: .9rem;
    background: var(--panel-glass);
    box-shadow: 0 .35rem 1rem rgb(0 0 0 / 14%);
    pointer-events: auto;
}
.host-game-board.peer-rated-reviewing .peer-rated-question-context > .eyebrow {
    margin: 0;
}
.host-game-board.peer-rated-reviewing
    .peer-rated-question-context > .game-content-blocks {
    min-height: 0;
    max-height: none;
    gap: clamp(.45rem, 1vh, .8rem);
}
.host-game-board.peer-rated-reviewing
    .peer-rated-question-context :is(.game-content-image, .game-content-video) {
    max-width: 100%;
    max-height: clamp(7rem, 22vh, 16rem);
    object-fit: contain;
}
.host-game-board.peer-rated-reviewing
    .peer-rated-question-context .game-content-text {
    font-size: clamp(1rem, 1.8vw, 2rem);
    line-height: 1.25;
}
`;
    document.head.appendChild(style);

    let layoutFrame = 0;

    const getBoard = () => document.querySelector(
        ".host-game-board[data-game-code]");

    const getQuestionPresentation = summary => Array.from(summary.children)
        .find(element =>
            element instanceof HTMLElement &&
            element.classList.contains("question-presentation")) ?? null;

    const getSourceQuestionId = presentation => presentation
        ?.querySelector(".game-content-blocks")
        ?.dataset.sourceQuestionId ?? "";

    const prepareQuestionBlocks = sourceBlocks => {
        const clone = sourceBlocks.cloneNode(true);
        if (!(clone instanceof HTMLElement)) {
            return null;
        }

        clone.classList.remove("peer-rated-host-content-reserved");
        clone.classList.add("peer-rated-question-context-blocks");
        clone.style.removeProperty("--peer-rated-content-base-padding-right");
        clone.style.removeProperty("--peer-rated-content-right-reserve");
        clone.removeAttribute("data-question-clues");

        clone.querySelectorAll("[id]").forEach(element =>
            element.removeAttribute("id"));
        clone.querySelectorAll("[autoplay]").forEach(element =>
            element.removeAttribute("autoplay"));
        clone.querySelectorAll("[data-autoplay-media]").forEach(element =>
            element.setAttribute("data-autoplay-media", "false"));
        clone.querySelectorAll("[data-youtube-autoplay]").forEach(element =>
            element.setAttribute("data-youtube-autoplay", "false"));

        return clone;
    };

    const createQuestionContext = (summary, presentation) => {
        const sourceBlocks = presentation.querySelector(".game-content-blocks");
        if (!(sourceBlocks instanceof HTMLElement)) {
            return null;
        }

        const context = document.createElement("section");
        context.className = "game-content-presentation peer-rated-question-context";
        context.dataset.sourceQuestionId = getSourceQuestionId(presentation);

        const sourceHeading = presentation.querySelector("[data-question-heading]");
        if (sourceHeading instanceof HTMLElement) {
            const heading = sourceHeading.cloneNode(true);
            if (heading instanceof HTMLElement) {
                heading.removeAttribute("id");
                heading.removeAttribute("data-question-heading");
                context.appendChild(heading);
            }
        }

        const blocks = prepareQuestionBlocks(sourceBlocks);
        if (!blocks) {
            return null;
        }
        context.appendChild(blocks);
        summary.appendChild(context);

        context.querySelectorAll("img, iframe, video").forEach(media =>
            media.addEventListener("load", scheduleLayout, { once: true }));
        context.querySelectorAll("audio, video").forEach(media =>
            media.addEventListener("loadedmetadata", scheduleLayout, { once: true }));

        return context;
    };

    const removeQuestionContext = summary => {
        summary?.querySelectorAll(":scope > .peer-rated-question-context")
            .forEach(element => element.remove());
        summary?.querySelectorAll(".peer-rated-host-stage")
            .forEach(stage => stage.style.removeProperty("top"));
    };

    const setPixelValue = (element, property, value) => {
        const next = `${Math.max(0, Math.ceil(value))}px`;
        if (element.style.getPropertyValue(property) !== next) {
            element.style.setProperty(property, next);
        }
    };

    const syncLayout = () => {
        layoutFrame = 0;
        const board = getBoard();
        const summary = board?.querySelector(".current-question-summary");
        if (!(board instanceof HTMLElement) ||
            !(summary instanceof HTMLElement) ||
            !board.classList.contains("peer-rated-reviewing")) {
            removeQuestionContext(summary);
            return;
        }

        const presentation = getQuestionPresentation(summary);
        if (!(presentation instanceof HTMLElement)) {
            removeQuestionContext(summary);
            return;
        }

        const sourceQuestionId = getSourceQuestionId(presentation);
        let context = summary.querySelector(":scope > .peer-rated-question-context");
        if (context instanceof HTMLElement &&
            context.dataset.sourceQuestionId !== sourceQuestionId) {
            context.remove();
            context = null;
        }
        if (!(context instanceof HTMLElement)) {
            context = createQuestionContext(summary, presentation);
        }
        if (!(context instanceof HTMLElement)) {
            return;
        }

        const ui = summary.querySelector(":scope > .peer-rated-host-ui");
        const stage = ui?.querySelector(".peer-rated-host-stage");
        const sidebar = ui?.querySelector(".peer-rated-host-sidebar");
        if (!(ui instanceof HTMLElement) || !(stage instanceof HTMLElement)) {
            return;
        }

        const summaryRect = summary.getBoundingClientRect();
        const uiRect = ui.getBoundingClientRect();
        const computedTop = Number.parseFloat(window.getComputedStyle(context).top) || 56;
        const effectiveBottom = Math.max(
            computedTop + 120,
            uiRect.bottom - summaryRect.top);
        const minimumAnswerHeight = Math.min(190, Math.max(120, effectiveBottom * .3));
        const maxContextHeight = Math.max(
            96,
            Math.min(
                effectiveBottom * .48,
                effectiveBottom - computedTop - minimumAnswerHeight - 20));
        setPixelValue(
            context,
            "--peer-rated-question-context-max-height",
            maxContextHeight);

        if (sidebar instanceof HTMLElement) {
            const sidebarRect = sidebar.getBoundingClientRect();
            const rightGap = Math.max(
                12,
                summaryRect.right - sidebarRect.left + 12);
            setPixelValue(
                context,
                "--peer-rated-question-context-right-gap",
                rightGap);
        } else {
            context.style.removeProperty("--peer-rated-question-context-right-gap");
        }

        const contextRect = context.getBoundingClientRect();
        const stageTop = Math.min(
            Math.max(0, effectiveBottom - minimumAnswerHeight),
            Math.max(computedTop, contextRect.bottom - summaryRect.top + 12));
        setPixelValue(stage, "top", stageTop);
    };

    function scheduleLayout() {
        if (layoutFrame && typeof window.cancelAnimationFrame === "function") {
            window.cancelAnimationFrame(layoutFrame);
        }
        if (typeof window.requestAnimationFrame === "function") {
            layoutFrame = window.requestAnimationFrame(syncLayout);
        } else {
            syncLayout();
        }
    }

    document.addEventListener("badwolf:host-gameplay-updated", scheduleLayout);
    document.addEventListener("badwolf:host-shell-mounted", scheduleLayout);
    window.addEventListener("resize", scheduleLayout, { passive: true });
    window.visualViewport?.addEventListener("resize", scheduleLayout, { passive: true });
    window.addEventListener("pageshow", scheduleLayout);

    new MutationObserver(scheduleLayout).observe(document.documentElement, {
        childList: true,
        subtree: true
    });

    scheduleLayout();
})();
