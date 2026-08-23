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
    script.src = "/js/gameplay-right-overlay-safe-gap.js?v=5";
    script.async = false;
    script.dataset.gameplayRightOverlaySafeGap = "";
    document.head.appendChild(script);
})();
