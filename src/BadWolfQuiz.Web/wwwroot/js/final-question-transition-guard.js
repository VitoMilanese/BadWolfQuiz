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

            // Legacy auto-submit used requestSubmit() without a visible button.
            // Only an explicit host action may leave the transition stage.
            if (submitter) {
                return;
            }

            event.preventDefault();
            event.stopImmediatePropagation();
            return;
        }

        if (guardedHandlers.has(getSubmitHandler(form, submitter))) {
            // Lock before the soft-navigation GET starts. This closes the race
            // where a lobby refresh can begin while the transition is loading.
            lockFinalTransition();
        }
    }, true);
})();
