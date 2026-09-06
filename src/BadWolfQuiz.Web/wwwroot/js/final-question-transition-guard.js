(() => {
    "use strict";

    if (window.badWolfFinalQuestionTransitionGuardInstalled) {
        return;
    }

    window.badWolfFinalQuestionTransitionGuardInstalled = true;

    const finalTransitionSelector =
        "[data-host-gameplay-view] [data-final-question-transition]";

    const isFinalTransitionActive = () =>
        document.querySelector(finalTransitionSelector) !== null;

    const installHostRefreshGuard = () => {
        const hostGameplay = window.BadWolfHostGameplay;
        if (!hostGameplay ||
            hostGameplay.finalQuestionTransitionRefreshGuardInstalled === true ||
            typeof hostGameplay.refresh !== "function") {
            return;
        }

        const refresh = hostGameplay.refresh.bind(hostGameplay);
        hostGameplay.refresh = (...args) => {
            if (isFinalTransitionActive()) {
                return Promise.resolve(false);
            }

            return refresh(...args);
        };
        hostGameplay.finalQuestionTransitionRefreshGuardInstalled = true;
    };

    installHostRefreshGuard();
    document.addEventListener(
        "badwolf:host-gameplay-updated",
        installHostRefreshGuard);

    window.addEventListener("submit", event => {
        const form = event.target instanceof HTMLFormElement
            ? event.target
            : null;

        if (!form?.matches("[data-final-question-transition-form]")) {
            return;
        }

        // The host must explicitly use the visible action button. Legacy
        // requestSubmit() calls have no submitter and must not advance the game.
        if (event.submitter instanceof HTMLElement) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
    }, true);
})();
