(() => {
    const timerSelector = ".host-game-timer";
    const quickAdjustmentSelector = ".game-timer-adjust";
    const engagedClass = "is-quick-actions-engaged";
    let engagedTimer = null;

    const clearEngagedTimer = () => {
        engagedTimer?.classList.remove(engagedClass);
        engagedTimer = null;
    };

    const style = document.createElement("style");
    style.textContent = `
        .host-game-timer.${engagedClass} .game-timer-quick-actions {
            opacity: 1;
            visibility: visible;
            pointer-events: auto;
        }
    `;
    document.head.append(style);

    document.addEventListener("pointerdown", event => {
        const target = event.target instanceof Element ? event.target : null;
        const adjustment = target?.closest(quickAdjustmentSelector);
        const timer = adjustment?.closest(timerSelector);
        if (!timer) {
            return;
        }

        if (engagedTimer !== timer) {
            clearEngagedTimer();
            engagedTimer = timer;
        }

        timer.classList.add(engagedClass);
    }, true);

    document.addEventListener("pointermove", event => {
        if (!engagedTimer) {
            return;
        }

        const pointerTarget = document.elementFromPoint(event.clientX, event.clientY);
        if (pointerTarget?.closest(timerSelector) === engagedTimer) {
            return;
        }

        clearEngagedTimer();
    }, { capture: true, passive: true });

    document.addEventListener("keydown", event => {
        if (event.key === "Escape") {
            clearEngagedTimer();
        }
    });
})();

(() => {
    if (window.badWolfGameplayRightOverlaySafeGapLoaderInstalled) {
        return;
    }

    window.badWolfGameplayRightOverlaySafeGapLoaderInstalled = true;
    if (document.querySelector("script[data-gameplay-right-overlay-safe-gap]")) {
        return;
    }

    const script = document.createElement("script");
    script.src = "/js/gameplay-right-overlay-safe-gap.js?v=8";
    script.async = false;
    script.dataset.gameplayRightOverlaySafeGap = "";
    document.head.appendChild(script);
})();

(() => {
    if (window.badWolfHostQuestionControlsLoaderInstalled) {
        return;
    }

    window.badWolfHostQuestionControlsLoaderInstalled = true;
    if (document.querySelector("script[data-host-question-controls]")) {
        return;
    }

    const script = document.createElement("script");
    script.src = "/js/host-question-controls.js?v=6";
    script.async = false;
    script.dataset.hostQuestionControls = "";
    document.head.appendChild(script);
})();

(() => {
    if (window.badWolfBoardPlayerScoreActionsLoaderInstalled) {
        return;
    }

    window.badWolfBoardPlayerScoreActionsLoaderInstalled = true;
    if (document.querySelector("script[data-board-player-score-actions]")) {
        return;
    }

    const script = document.createElement("script");
    script.src = "/js/board-player-score-actions.js?v=5";
    script.async = false;
    script.dataset.boardPlayerScoreActions = "";
    document.head.appendChild(script);
})();

(() => {
    if (window.badWolfFinalQuestionTransitionGuardLoaderInstalled) {
        return;
    }

    window.badWolfFinalQuestionTransitionGuardLoaderInstalled = true;
    if (document.querySelector("script[data-final-question-transition-guard]")) {
        return;
    }

    const script = document.createElement("script");
    script.src = "/js/final-question-transition-guard.js?v=1";
    script.async = false;
    script.dataset.finalQuestionTransitionGuard = "";
    document.head.appendChild(script);
})();

(() => {
    if (window.badWolfJoinCodeCopyTargetsInstalled) {
        return;
    }

    window.badWolfJoinCodeCopyTargetsInstalled = true;

    const copyText = async value => {
        if (navigator.clipboard?.writeText) {
            try {
                await navigator.clipboard.writeText(value);
                return;
            } catch {
                // Fall back to a temporary textarea below.
            }
        }

        const textarea = document.createElement("textarea");
        textarea.value = value;
        textarea.setAttribute("readonly", "readonly");
        textarea.style.position = "fixed";
        textarea.style.opacity = "0";
        document.body.appendChild(textarea);
        textarea.select();
        document.execCommand("copy");
        textarea.remove();
    };

    const bindCopyTarget = (target, valueFactory) => {
        if (!(target instanceof HTMLElement)) {
            return;
        }

        target.tabIndex = 0;
        target.setAttribute("role", "button");
        target.style.cursor = "copy";
        target.draggable = false;

        const copy = async () => {
            const value = valueFactory();
            if (!value) {
                return;
            }

            try {
                await copyText(value);
            } catch (error) {
                console.error("Unable to copy join information.", error);
            }
        };

        target.addEventListener("click", () => copy());
        target.addEventListener("keydown", event => {
            if (event.key !== "Enter" && event.key !== " ") {
                return;
            }

            event.preventDefault();
            copy();
        });
    };

    const panel = document.querySelector("[data-join-code-panel]");
    if (!panel) {
        return;
    }

    const gameCode = panel.dataset.gameCode?.trim() ?? "";
    const codeTarget = panel.querySelector(
        ".join-code-floating-content .join-code-value");
    const qrTarget = panel.querySelector(
        ".join-code-floating-content .join-qr-code");

    bindCopyTarget(codeTarget, () => gameCode);
    bindCopyTarget(qrTarget, () => {
        if (!gameCode) {
            return "";
        }

        return new URL(
            `/Join/${encodeURIComponent(gameCode)}/`,
            window.location.origin).href;
    });
})();