(() => {
    "use strict";

    if (window.badWolfHostMultipleChoiceBootstrapInitialized) {
        return;
    }
    window.badWolfHostMultipleChoiceBootstrapInitialized = true;

    const bootstrapScript = document.currentScript ??
        document.querySelector('script[src*="host-multiple-choice-bootstrap.js"]');
    const isHostLobby = bootstrapScript?.dataset.hostLobby === "true";

    const initializeEditorAnswerPreview = () => {
        const presentationType = document.getElementById("Input_PresentationType");
        const answerSection = document.getElementById("answer-blocks");
        if (!(presentationType instanceof HTMLSelectElement) || !answerSection) {
            return;
        }

        document.addEventListener("click", event => {
            const target = event.target instanceof Element ? event.target : null;
            const previewButton = target?.closest(
                '[data-open-question-preview="answer"]');
            if (!previewButton || presentationType.value !== "4") {
                return;
            }

            const modal = document.getElementById("question-preview-modal");
            const title = document.getElementById("question-preview-title");
            const content = document.getElementById("question-preview-content");
            if (!modal || !title || !content) {
                return;
            }

            event.preventDefault();
            event.stopImmediatePropagation();

            const firstCard = answerSection.querySelector(
                ":scope > [data-content-block-list] > .content-block-card");
            const answer = firstCard
                ?.querySelector('textarea[name$=".TextContent"]')
                ?.value.trim() ?? "";
            const answerPreview = document.createElement("div");
            answerPreview.className = answer
                ? "question-preview-text game-content-block game-content-text"
                : "question-preview-empty";
            answerPreview.textContent = answer || "—";

            title.textContent = previewButton.textContent?.trim() || "Answer";
            content.classList.remove("four-clue-grid", "all-player-answer-grid");
            content.replaceChildren(answerPreview);
            modal.hidden = false;
            modal.setAttribute("aria-hidden", "false");
            document.body.classList.add("question-preview-open");
            modal.querySelector(".question-preview-close-button")?.focus();
        }, true);
    };

    const initializeDelayedHostGameplay = () => {
        if (!isHostLobby) {
            return;
        }

        const getBoard = () => document.querySelector(
            ".host-game-board[data-game-id]");
        if (getBoard()) {
            return;
        }

        const observer = new MutationObserver(() => {
            if (!getBoard()) {
                return;
            }

            observer.disconnect();
            document.getElementById("host-multiple-choice-styles")?.remove();
            window.badWolfHostMultipleChoiceInitialized = false;

            const mainScript = document.createElement("script");
            mainScript.src = "/js/host-multiple-choice.js?v=1.20.0-259.2";
            mainScript.dataset.savedQuestionType = "-1";
            document.body.appendChild(mainScript);
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    };

    initializeEditorAnswerPreview();
    initializeDelayedHostGameplay();
})();
