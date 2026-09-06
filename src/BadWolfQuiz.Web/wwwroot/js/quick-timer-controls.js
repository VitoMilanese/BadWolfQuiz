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
    "use strict";

    if (window.badWolfFinalQuestionTransitionGuardInstalled) {
        return;
    }

    window.badWolfFinalQuestionTransitionGuardInstalled = true;

    const finalTransitionSelector =
        "[data-host-gameplay-view] [data-final-question-transition]";
    const finalTransitionFormSelector =
        "[data-final-question-transition-form]";
    const guardedHandlers = new Set([
        "StartFinalQuestion",
        "ForceAdvanceToFinalQuestion"
    ]);
    let finalTransitionRequested = false;
    let finalTransitionSeen = false;

    const isFinalTransitionMounted = () =>
        document.querySelector(finalTransitionSelector) !== null;

    const isFinalTransitionLocked = () =>
        finalTransitionRequested || isFinalTransitionMounted();

    const getSubmitHandler = (form, submitter) => {
        const hasSubmitterAction =
            submitter?.hasAttribute("formaction") === true;
        const action = hasSubmitterAction
            ? submitter.formAction
            : form.action;

        if (!action) {
            return null;
        }

        return new URL(action, window.location.href)
            .searchParams.get("handler");
    };

    const installHostRefreshGuard = () => {
        const hostGameplay = window.BadWolfHostGameplay;
        if (!hostGameplay ||
            hostGameplay.finalQuestionTransitionRefreshGuardInstalled === true ||
            typeof hostGameplay.refresh !== "function") {
            return;
        }

        const refresh = hostGameplay.refresh.bind(hostGameplay);
        hostGameplay.refresh = (...args) => {
            if (isFinalTransitionLocked()) {
                return Promise.resolve(false);
            }

            return refresh(...args);
        };
        hostGameplay.finalQuestionTransitionRefreshGuardInstalled = true;
    };

    const lockFinalTransition = () => {
        finalTransitionRequested = true;
        window.BadWolfHostGameplay?.cancelPending?.();
        installHostRefreshGuard();
    };

    const syncFinalTransitionLock = () => {
        if (isFinalTransitionMounted()) {
            finalTransitionRequested = true;
            finalTransitionSeen = true;
            return;
        }

        if (finalTransitionSeen) {
            finalTransitionRequested = false;
            finalTransitionSeen = false;
        }
    };

    if (!window.badWolfFinalQuestionRequestSubmitGuardInstalled) {
        const requestSubmit = HTMLFormElement.prototype.requestSubmit;
        HTMLFormElement.prototype.requestSubmit = function (submitter) {
            if (this.matches(finalTransitionFormSelector) &&
                arguments.length === 0) {
                return;
            }

            return arguments.length === 0
                ? requestSubmit.call(this)
                : requestSubmit.call(this, submitter);
        };
        window.badWolfFinalQuestionRequestSubmitGuardInstalled = true;
    }

    installHostRefreshGuard();
    syncFinalTransitionLock();

    document.addEventListener(
        "badwolf:host-gameplay-updated",
        () => {
            installHostRefreshGuard();
            syncFinalTransitionLock();
        });

    document.addEventListener(
        "badwolf:host-shell-mounted",
        () => {
            installHostRefreshGuard();
            syncFinalTransitionLock();
        });

    window.addEventListener("submit", event => {
        const form = event.target instanceof HTMLFormElement
            ? event.target
            : null;
        if (!form) {
            return;
        }

        const submitter = event.submitter instanceof HTMLElement
            ? event.submitter
            : null;

        if (form.matches(finalTransitionFormSelector)) {
            lockFinalTransition();
            finalTransitionSeen = true;

            if (submitter) {
                return;
            }

            event.preventDefault();
            event.stopImmediatePropagation();
            return;
        }

        if (guardedHandlers.has(getSubmitHandler(form, submitter))) {
            lockFinalTransition();
        }
    }, true);
})();

(() => {
    if (window.badWolfJoinCodeCopyTargetsInstalled) {
        return;
    }

    window.badWolfJoinCodeCopyTargetsInstalled = true;

    const reducedMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches === true;
    const activeAnimations = new WeakMap();

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

    const showCopyFeedback = target => {
        activeAnimations.get(target)?.cancel();

        const animation = target.animate(
            reducedMotion
                ? [
                    { filter: "brightness(1)" },
                    { filter: "brightness(1.35)" },
                    { filter: "brightness(1)" }
                ]
                : [
                    { transform: "scale(1)", filter: "brightness(1)" },
                    { transform: "scale(0.96)", filter: "brightness(1.08)", offset: 0.25 },
                    { transform: "scale(1.045)", filter: "brightness(1.35) drop-shadow(0 0 12px #3fb950)", offset: 0.62 },
                    { transform: "scale(1)", filter: "brightness(1)" }
                ],
            {
                duration: reducedMotion ? 180 : 360,
                easing: "ease-out"
            });
        activeAnimations.set(target, animation);
        animation.addEventListener("finish", () => {
            if (activeAnimations.get(target) === animation) {
                activeAnimations.delete(target);
            }
        }, { once: true });

        const bounds = target.getBoundingClientRect();
        const feedback = document.createElement("span");
        feedback.textContent = "✓";
        feedback.setAttribute("aria-hidden", "true");
        Object.assign(feedback.style, {
            position: "fixed",
            zIndex: "1000",
            left: `${bounds.right - Math.min(28, bounds.width * 0.14)}px`,
            top: `${bounds.top + Math.min(28, bounds.height * 0.14)}px`,
            display: "grid",
            placeItems: "center",
            width: "28px",
            height: "28px",
            borderRadius: "999px",
            color: "#fff",
            background: "#238636",
            boxShadow: "0 6px 18px rgba(0, 0, 0, 0.35)",
            fontSize: "17px",
            fontWeight: "900",
            lineHeight: "1",
            pointerEvents: "none",
            transform: "translate(-50%, -50%)"
        });
        document.body.appendChild(feedback);

        const feedbackAnimation = feedback.animate(
            reducedMotion
                ? [
                    { opacity: 0 },
                    { opacity: 1, offset: 0.25 },
                    { opacity: 0 }
                ]
                : [
                    { opacity: 0, transform: "translate(-50%, -42%) scale(0.55)" },
                    { opacity: 1, transform: "translate(-50%, -50%) scale(1.12)", offset: 0.28 },
                    { opacity: 1, transform: "translate(-50%, -58%) scale(1)", offset: 0.68 },
                    { opacity: 0, transform: "translate(-50%, -82%) scale(0.9)" }
                ],
            {
                duration: reducedMotion ? 360 : 620,
                easing: "ease-out"
            });
        feedbackAnimation.addEventListener("finish", () => feedback.remove(), { once: true });
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
                showCopyFeedback(target);
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