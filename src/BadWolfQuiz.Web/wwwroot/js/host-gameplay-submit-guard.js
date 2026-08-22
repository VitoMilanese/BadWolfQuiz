(() => {
    if (window.badWolfHostGameplaySubmitGuardInitialized) {
        return;
    }

    window.badWolfHostGameplaySubmitGuardInitialized = true;

    const initialize = () => {
        const board = document.querySelector(".host-game-board[data-game-id]");
        const viewSelector = "[data-host-gameplay-view]";
        const replayDelayMilliseconds = 16;
        const lobbyUrl = new URL(window.location.href);
        const gameId = board?.dataset.gameId;
        const flowPaths = new Set(gameId
            ? [
                `/Admin/Games/RoundIntro/${encodeURIComponent(gameId)}`,
                `/Admin/Games/RunningRoundIntro/${encodeURIComponent(gameId)}`,
                `/Admin/Games/FinalQuestionTransition/${encodeURIComponent(gameId)}`
            ].map(path => path.toLowerCase())
            : []);
        let pendingSubmission = null;
        let replayHandle = 0;

        const isGameplayForm = form =>
            form instanceof HTMLFormElement &&
            !form.matches(".game-timer-pause, .game-timer-resume, .game-timer-adjust") &&
            !form.matches(".question-judge-actions") &&
            (form.matches(".question-selection-form") ||
             form.id === "remove-player-form" ||
             form.closest("#blocked-players-dialog") !== null ||
             form.closest(viewSelector) !== null);

        const canNavigate = url =>
            url.origin === lobbyUrl.origin &&
            (url.pathname.toLowerCase() === lobbyUrl.pathname.toLowerCase() ||
             flowPaths.has(url.pathname.toLowerCase()));

        const getAction = (form, submitter) => {
            const submitterHasFormAction =
                submitter?.hasAttribute("formaction") === true;
            return new URL(
                submitterHasFormAction ? submitter.formAction : form.action,
                window.location.href);
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

                submitter?.removeAttribute("disabled");
                submitter?.removeAttribute("aria-busy");
                if (submitter instanceof HTMLButtonElement ||
                    submitter instanceof HTMLInputElement) {
                    form.requestSubmit(submitter);
                } else {
                    form.requestSubmit();
                }
            }, replayDelayMilliseconds);
        };

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
            const key = submissionKey(form, submitter, action);

            event.preventDefault();
            event.stopImmediatePropagation();

            if (submitter?.hasAttribute("disabled")) {
                return;
            }

            if (pendingSubmission?.key === key) {
                scheduleReplay();
                return;
            }

            if (pendingSubmission === null) {
                pendingSubmission = { form, submitter, key };
                submitter?.setAttribute("disabled", "disabled");
                submitter?.setAttribute("aria-busy", "true");
            }
            scheduleReplay();
        }, true);
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        queueMicrotask(initialize);
    }
})();
