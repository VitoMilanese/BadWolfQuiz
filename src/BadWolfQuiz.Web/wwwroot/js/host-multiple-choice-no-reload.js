(() => {
    "use strict";

    if (window.badWolfHostMultipleChoiceNoReloadInitialized) {
        return;
    }
    window.badWolfHostMultipleChoiceNoReloadInitialized = true;

    let submitInProgress = false;

    const isNoAnswerForm = form => {
        if (!(form instanceof HTMLFormElement) ||
            !form.closest(".host-multiple-choice-panel")) {
            return false;
        }

        try {
            const url = new URL(form.action, window.location.origin);
            return url.pathname.startsWith("/Admin/Games/Lobby/") &&
                url.searchParams.get("handler") === "ResolveQuestion";
        } catch {
            return form.action.includes("/Admin/Games/Lobby/") &&
                form.action.includes("handler=ResolveQuestion");
        }
    };

    document.addEventListener("submit", async event => {
        const form = event.target;
        if (!isNoAnswerForm(form) ||
            typeof window.BadWolfHostGameplay?.refresh !== "function") {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();

        if (submitInProgress) {
            return;
        }
        submitInProgress = true;

        const submitter = event.submitter instanceof HTMLButtonElement
            ? event.submitter
            : form.querySelector('button[type="submit"]');
        if (submitter instanceof HTMLButtonElement) {
            submitter.disabled = true;
        }

        try {
            const response = await fetch(form.action, {
                method: "POST",
                credentials: "same-origin",
                body: new FormData(form),
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            form.closest(".host-multiple-choice-panel")?.remove();
            await window.BadWolfHostGameplay.refresh();
        } catch (error) {
            console.error("Host multiple-choice no-answer action failed.", error);
            if (submitter instanceof HTMLButtonElement) {
                submitter.disabled = false;
            }
        } finally {
            submitInProgress = false;
        }
    }, true);
})();
