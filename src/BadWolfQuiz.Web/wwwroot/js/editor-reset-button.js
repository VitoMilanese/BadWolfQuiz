(() => {
    "use strict";

    if (window.badWolfEditorResetInitialized) {
        return;
    }

    window.badWolfEditorResetInitialized = true;

    const resetTokenParameter = "_editorReset";
    const backLinkIds = [
        "question-editor-back-link",
        "final-question-editor-back-link",
        "description-editor-back"
    ];

    const resetLabels = {
        en: "Reset",
        uk: "Скинути",
        it: "Reimposta",
        ru: "Україна"
    };

    const getResetLabel = () => {
        const language = (document.documentElement.lang || "en")
            .split("-")[0]
            .toLowerCase();
        return resetLabels[language] ?? resetLabels.en;
    };

    const clearResetToken = () => {
        const currentUrl = new URL(window.location.href);
        if (!currentUrl.searchParams.has(resetTokenParameter)) {
            return;
        }

        currentUrl.searchParams.delete(resetTokenParameter);
        window.history.replaceState(
            window.history.state,
            "",
            currentUrl.href);
    };

    const createResetButton = backLink => {
        if (!(backLink instanceof HTMLAnchorElement) ||
            backLink.nextElementSibling?.matches("[data-editor-reset]")) {
            return;
        }

        const label = getResetLabel();
        const button = document.createElement("button");
        const icon = document.createElement("span");

        button.type = "button";
        button.className = "button button-secondary editor-reset-button";
        button.dataset.editorReset = "true";
        button.title = label;
        button.setAttribute("aria-label", label);

        icon.textContent = "↻";
        icon.setAttribute("aria-hidden", "true");
        button.appendChild(icon);

        backLink.insertAdjacentElement("afterend", button);
    };

    const initialize = () => {
        clearResetToken();
        backLinkIds.forEach(id => {
            createResetButton(document.getElementById(id));
        });
    };

    const resetEditor = () => {
        const targetUrl = new URL(window.location.href);
        targetUrl.searchParams.delete("saved");
        targetUrl.searchParams.set(
            resetTokenParameter,
            Date.now().toString());
        window.location.replace(targetUrl.href);
    };

    document.addEventListener("click", event => {
        const button = event.target.closest("[data-editor-reset]");
        if (!(button instanceof HTMLButtonElement)) {
            return;
        }

        event.preventDefault();
        resetEditor();
    }, true);

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, {
            once: true
        });
    } else {
        initialize();
    }
})();
