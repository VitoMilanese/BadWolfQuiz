(() => {
    if (window.badWolfGameplayEscapeShortcutsInitialized) {
        return;
    }

    window.badWolfGameplayEscapeShortcutsInitialized = true;

    const editorSaveOverlayTarget = document.querySelector(
        "form.quiz-board-form, form.question-editor");
    if (editorSaveOverlayTarget) {
        const bootstrapUrl = document.currentScript?.src
            ? new URL(document.currentScript.src)
            : null;
        const overlayUrl = new URL(
            "/js/editor-save-overlay.js",
            window.location.origin);
        if (bootstrapUrl?.search) {
            overlayUrl.search = bootstrapUrl.search;
        }

        const overlayScript = document.createElement("script");
        overlayScript.src = overlayUrl.href;
        overlayScript.async = false;
        document.head.appendChild(overlayScript);
    }

    const hasBlockingUi = () =>
        document.querySelector(
            "dialog[open], details.action-menu[open], .language-menu.open, [role='menu']:not([hidden])") !== null;

    const findQuestionReviewReturnLink = () => {
        const links = document.querySelectorAll(
            ".question-review-preview .question-review-actions a[href]");

        for (const link of links) {
            const targetUrl = new URL(link.href, window.location.href);
            if (!targetUrl.searchParams.has("previewQuestionId")) {
                return link;
            }
        }

        return null;
    };

    document.addEventListener("keydown", event => {
        if (event.key !== "Escape" ||
            event.defaultPrevented ||
            event.isComposing ||
            event.repeat ||
            event.altKey ||
            event.ctrlKey ||
            event.metaKey ||
            event.shiftKey ||
            hasBlockingUi()) {
            return;
        }

        const initialIntroForm = document.querySelector(
            "form[data-game-intro-start]");
        if (initialIntroForm instanceof HTMLFormElement) {
            event.preventDefault();
            const submitter = initialIntroForm.querySelector(
                "button[type='submit'], input[type='submit']");
            if (submitter instanceof HTMLButtonElement ||
                submitter instanceof HTMLInputElement) {
                initialIntroForm.requestSubmit(submitter);
            } else {
                initialIntroForm.requestSubmit();
            }
            return;
        }

        const runningIntroFinish = document.querySelector(
            "[data-game-intro-page] .game-intro-actions a[href]:not([data-game-intro-next])");
        if (runningIntroFinish instanceof HTMLAnchorElement) {
            event.preventDefault();
            runningIntroFinish.click();
            return;
        }

        const questionReviewReturn = findQuestionReviewReturnLink();
        if (questionReviewReturn instanceof HTMLAnchorElement) {
            event.preventDefault();
            questionReviewReturn.click();
        }
    }, true);
})();
