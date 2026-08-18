(() => {
    if (window.badWolfGameplayEscapeShortcutsInitialized) {
        return;
    }

    window.badWolfGameplayEscapeShortcutsInitialized = true;

    const bootstrapUrl = document.currentScript?.src
        ? new URL(document.currentScript.src)
        : null;
    const getSharedAssetUrl = path => {
        const assetUrl = new URL(path, window.location.origin);
        if (bootstrapUrl?.search) {
            assetUrl.search = bootstrapUrl.search;
        }
        return assetUrl;
    };
    const loadSharedScript = path => {
        const script = document.createElement("script");
        script.src = getSharedAssetUrl(path).href;
        script.async = false;
        document.head.appendChild(script);
    };
    const loadSharedStyle = path => {
        const stylesheet = document.createElement("link");
        stylesheet.rel = "stylesheet";
        stylesheet.href = getSharedAssetUrl(path).href;
        document.head.appendChild(stylesheet);
    };

    loadSharedStyle("/css/content-block-containers.css");
    loadSharedScript("/js/content-block-containers.js");

    const contentBlockEditorTarget = document.querySelector(
        ".content-block-section");
    if (contentBlockEditorTarget) {
        loadSharedStyle("/css/content-block-reorder-buttons.css");
        loadSharedScript("/js/content-block-reorder-buttons.js");
    }

    const editorResetTarget = document.querySelector(
        "#question-editor-back-link, #final-question-editor-back-link, #description-editor-back");
    if (editorResetTarget) {
        loadSharedStyle("/css/editor-reset-button.css");
        loadSharedScript("/js/editor-reset-button.js");
    }

    const quizCloneTarget = document.querySelector(
        ".quiz-list .quiz-action-menu");
    if (quizCloneTarget) {
        loadSharedScript("/js/quiz-clone-action.js");
    }

    const editorSaveOverlayTarget = document.querySelector(
        "form.quiz-board-form, form.question-editor");
    if (editorSaveOverlayTarget) {
        loadSharedScript("/js/editor-save-overlay.js");
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
