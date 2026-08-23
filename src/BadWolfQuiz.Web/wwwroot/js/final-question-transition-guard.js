(() => {
    "use strict";

    if (window.badWolfFinalQuestionTransitionGuardInstalled) {
        return;
    }

    window.badWolfFinalQuestionTransitionGuardInstalled = true;

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
