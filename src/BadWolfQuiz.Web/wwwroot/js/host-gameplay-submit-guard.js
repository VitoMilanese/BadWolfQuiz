(() => {
    if (window.badWolfHostGameplaySubmitGuardInitialized) {
        return;
    }

    window.badWolfHostGameplaySubmitGuardInitialized = true;

    const initialize = () => {
        const viewSelector = "[data-host-gameplay-view]";
        const replayDelayMilliseconds = 16;
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

        const submissionKey = (form, submitter) => {
            const submitterHasFormAction =
                submitter?.hasAttribute("formaction") === true;
            const action = new URL(
                submitterHasFormAction ? submitter.formAction : form.action,
                window.location.href);
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
            const key = submissionKey(form, submitter);

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
