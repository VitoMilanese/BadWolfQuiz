(() => {
    if (window.badWolfHostGameplaySubmitGuardInitialized) {
        return;
    }

    window.badWolfHostGameplaySubmitGuardInitialized = true;

    const selectionStyleId = "host-gameplay-selection-guard";
    const selectionBodyClass = "host-gameplay-no-select";

    const ensureSelectionStyle = () => {
        if (document.getElementById(selectionStyleId)) {
            return;
        }

        const style = document.createElement("style");
        style.id = selectionStyleId;
        style.textContent = `
body.${selectionBodyClass} {
    -webkit-user-select: none;
    user-select: none;
}

body.${selectionBodyClass} input:not([type="button"]):not([type="submit"]):not([type="reset"]),
body.${selectionBodyClass} textarea,
body.${selectionBodyClass} [contenteditable="true"] {
    -webkit-user-select: text;
    user-select: text;
}`;
        document.head.append(style);
    };

    const refreshSelectionMode = () => {
        const hasGameplayUi = document.querySelector(
            ".host-game-board, [data-host-gameplay-view], .game-intro-page") !== null;
        document.body?.classList.toggle(selectionBodyClass, hasGameplayUi);
    };

    const initialize = () => {
        ensureSelectionStyle();
        refreshSelectionMode();

        const viewSelector = "[data-host-gameplay-view]";
        const questionButtonSelector =
            "form.question-selection-form button[type='submit'], " +
            "form.question-selection-form input[type='submit']";
        const replayDelayMilliseconds = 16;
        const questionBusyDelayMilliseconds = 250;
        const questionBusySafetyMilliseconds = 15000;
        let pendingSubmission = null;
        let replayHandle = 0;
        let questionSelectionBusy = false;
        let questionBusyHandle = 0;
        let questionSafetyHandle = 0;
        let questionBusyOverlayOwned = false;
        let questionErrorObserver = null;
        let lockedQuestionButtons = [];

        const isGameplayForm = form =>
            form instanceof HTMLFormElement &&
            !form.matches(".game-timer-pause, .game-timer-resume, .game-timer-adjust") &&
            !form.matches(".question-judge-actions") &&
            (form.matches(".question-selection-form") ||
             form.id === "remove-player-form" ||
             form.closest("#blocked-players-dialog") !== null ||
             form.closest(viewSelector) !== null);

        const getAction = (form, submitter) => {
            const submitterHasFormAction =
                submitter?.hasAttribute("formaction") === true;
            return new URL(
                submitterHasFormAction ? submitter.formAction : form.action,
                window.location.href);
        };

        const canNavigate = url => {
            const board = document.querySelector(
                ".host-game-board[data-game-id]");
            const gameId = board?.dataset.gameId;
            if (!gameId || url.origin !== window.location.origin) {
                return false;
            }

            const encodedGameId = encodeURIComponent(gameId);
            const allowedPaths = new Set([
                `/Admin/Games/Lobby/${encodedGameId}`,
                `/Admin/Games/RoundIntro/${encodedGameId}`,
                `/Admin/Games/RunningRoundIntro/${encodedGameId}`,
                `/Admin/Games/FinalQuestionTransition/${encodedGameId}`
            ].map(path => path.toLowerCase()));
            return allowedPaths.has(url.pathname.toLowerCase());
        };

        const submissionKey = (form, submitter, action) => {
            const formData = new FormData(form);
            if (submitter?.name) {
                formData.append(submitter.name, submitter.value);
            }

            const fields = Array.from(formData.entries())
                .filter(([name]) => name !== "__RequestVerificationToken")
                .map(([name, value]) => [
                    name,
                    typeof value === "string" ? value : value.name
                ])
                .sort(([leftName, leftValue], [rightName, rightValue]) =>
                    leftName === rightName
                        ? leftValue.localeCompare(rightValue)
                        : leftName.localeCompare(rightName))
                .map(([name, value]) =>
                    `${encodeURIComponent(name)}=${encodeURIComponent(value)}`)
                .join("&");

            return `${form.method.toUpperCase()} ${action.href} ${fields}`;
        };

        const releaseQuestionSelectionBusy = () => {
            window.clearTimeout(questionBusyHandle);
            window.clearTimeout(questionSafetyHandle);
            questionBusyHandle = 0;
            questionSafetyHandle = 0;
            questionSelectionBusy = false;
            questionErrorObserver?.disconnect();
            questionErrorObserver = null;

            lockedQuestionButtons.forEach(({ button, wasDisabled }) => {
                if (button.isConnected) {
                    button.disabled = wasDisabled;
                }
            });
            lockedQuestionButtons = [];
            document.querySelector("[data-host-gameplay-board]")
                ?.removeAttribute("aria-busy");

            if (questionBusyOverlayOwned) {
                questionBusyOverlayOwned = false;
                window.BadWolfBusy?.hide?.();
            }
        };

        const rememberAndDisableQuestionButton = button => {
            if (!lockedQuestionButtons.some(state => state.button === button)) {
                lockedQuestionButtons.push({
                    button,
                    wasDisabled: button.disabled
                });
            }
            button.disabled = true;
        };

        const keepQuestionSelectionLocked = () => {
            if (!questionSelectionBusy) {
                return;
            }

            const board = document.querySelector("[data-host-gameplay-board]");
            if (!board) {
                return;
            }

            if (board.hidden) {
                releaseQuestionSelectionBusy();
                return;
            }

            board.querySelectorAll(questionButtonSelector)
                .forEach(rememberAndDisableQuestionButton);
            board.setAttribute("aria-busy", "true");
        };

        const lockOtherQuestionButtons = submitter => {
            if (questionSelectionBusy) {
                return false;
            }

            const board = document.querySelector("[data-host-gameplay-board]");
            if (!board) {
                return false;
            }

            questionSelectionBusy = true;
            lockedQuestionButtons = [];
            Array.from(board.querySelectorAll(questionButtonSelector))
                .filter(button => button !== submitter)
                .forEach(rememberAndDisableQuestionButton);
            board.setAttribute("aria-busy", "true");

            const errorTarget = document.getElementById("game-board-error");
            if (errorTarget) {
                questionErrorObserver = new MutationObserver(() => {
                    if (!errorTarget.hidden && errorTarget.textContent?.trim()) {
                        releaseQuestionSelectionBusy();
                    }
                });
                questionErrorObserver.observe(errorTarget, {
                    attributes: true,
                    childList: true,
                    subtree: true,
                    attributeFilter: ["hidden", "class"]
                });
            }

            questionBusyHandle = window.setTimeout(() => {
                questionBusyHandle = 0;
                questionBusyOverlayOwned =
                    window.BadWolfBusy?.show?.() === true;
            }, questionBusyDelayMilliseconds);
            questionSafetyHandle = window.setTimeout(
                releaseQuestionSelectionBusy,
                questionBusySafetyMilliseconds);
            return true;
        };

        document.addEventListener("click", event => {
            const submitter = event.target instanceof Element
                ? event.target.closest(questionButtonSelector)
                : null;
            if (!(submitter instanceof HTMLElement) || submitter.disabled) {
                return;
            }

            if (questionSelectionBusy) {
                event.preventDefault();
                event.stopImmediatePropagation();
                return;
            }

            lockOtherQuestionButtons(submitter);
        }, true);

        document.addEventListener("badwolf:host-shell-mounted", refreshSelectionMode);
        document.addEventListener("badwolf:host-gameplay-updated", () => {
            refreshSelectionMode();
            if (!questionSelectionBusy) {
                return;
            }

            const board = document.querySelector("[data-host-gameplay-board]");
            if (board?.hidden === true) {
                releaseQuestionSelectionBusy();
                return;
            }

            keepQuestionSelectionLocked();
        });

        const scheduleReplay = () => {
            if (replayHandle !== 0 || pendingSubmission === null) {
                return;
            }

            replayHandle = window.setTimeout(() => {
                replayHandle = 0;
                const pending = pendingSubmission;
                pendingSubmission = null;
                if (!pending) {
                    return;
                }

                const { form, submitter } = pending;
                if (!form.isConnected ||
                    (submitter && !submitter.isConnected)) {
                    return;
                }

                submitter?.removeAttribute("aria-busy");
                if (submitter instanceof HTMLButtonElement ||
                    submitter instanceof HTMLInputElement) {
                    form.requestSubmit(submitter);
                } else {
                    form.requestSubmit();
                }
            }, replayDelayMilliseconds);
        };

        // Bubble phase is intentional. The normal host partial-navigation handler
        // runs in capture phase and stops handled submits. Only a submit it leaves
        // unhandled while another command is finishing reaches this fallback.
        document.addEventListener("submit", event => {
            const form = event.target instanceof HTMLFormElement
                ? event.target
                : null;
            if (event.defaultPrevented || !isGameplayForm(form)) {
                return;
            }

            const submitter = event.submitter instanceof HTMLElement
                ? event.submitter
                : null;
            const action = getAction(form, submitter);
            if (!canNavigate(action)) {
                return;
            }

            event.preventDefault();
            event.stopImmediatePropagation();

            if (submitter?.hasAttribute("disabled")) {
                return;
            }

            const key = submissionKey(form, submitter, action);
            if (pendingSubmission?.key === key) {
                scheduleReplay();
                return;
            }

            if (pendingSubmission === null) {
                pendingSubmission = { form, submitter, key };
                submitter?.setAttribute("aria-busy", "true");
            }
            scheduleReplay();
        });
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        queueMicrotask(initialize);
    }
})();
