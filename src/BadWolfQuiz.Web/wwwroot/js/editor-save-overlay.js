(() => {
    "use strict";

    if (window.badWolfQuizEditorSaveOverlayInitialized) {
        return;
    }

    window.badWolfQuizEditorSaveOverlayInitialized = true;

    const editorSaveOverlayDurationMs = 1000;
    const editorSaveOverlayTransitionMs = 150;
    const editorSaveOverlayTimers = new WeakMap();

    const style = document.createElement("style");
    style.id = "editor-save-overlay-top-styles";
    style.textContent = `
body .editor-save-overlay {
    position: fixed !important;
    z-index: 1200;
    left: 50%;
    top: calc(var(--topbar-height, 60px) + 16px + env(safe-area-inset-top)) !important;
    bottom: auto !important;
    width: max-content;
    max-width: min(720px, calc(100vw - 32px));
    margin: 0 !important;
    box-sizing: border-box;
    transform: translateX(-50%);
    pointer-events: none;
    text-align: center;
    box-shadow: 0 12px 36px rgba(0, 0, 0, 0.32);
    transition: opacity 150ms ease, transform 150ms ease;
}

body .editor-save-overlay.editor-save-overlay-hiding {
    opacity: 0;
    transform: translate(-50%, -8px) !important;
}

body .editor-save-overlay[hidden] {
    display: none !important;
}`;
    document.head.appendChild(style);

    const editor = document.querySelector("form.quiz-board-form");
    if (!editor) {
        return;
    }

    const moveToOverlay = status => {
        status.classList.add("editor-save-overlay");
        if (status.parentElement !== document.body) {
            document.body.appendChild(status);
        }
    };

    const scheduleHide = status => {
        const previousTimer = editorSaveOverlayTimers.get(status);
        if (previousTimer) {
            window.clearTimeout(previousTimer);
        }

        const hideTimer = window.setTimeout(() => {
            status.classList.add("editor-save-overlay-hiding");
            const transitionTimer = window.setTimeout(() => {
                status.hidden = true;
                status.classList.remove("editor-save-overlay-hiding");
            }, editorSaveOverlayTransitionMs);
            editorSaveOverlayTimers.set(status, transitionTimer);
        }, editorSaveOverlayDurationMs);

        editorSaveOverlayTimers.set(status, hideTimer);
    };

    const showStatus = status => {
        moveToOverlay(status);
        status.classList.remove("editor-save-overlay-hiding", "message-hidden");
        scheduleHide(status);
    };

    const watchStatus = status => {
        moveToOverlay(status);

        const sync = () => {
            if (status.hidden || !status.textContent?.trim()) {
                return;
            }

            showStatus(status);
        };

        const observer = new MutationObserver(sync);
        observer.observe(status, {
            attributes: true,
            attributeFilter: ["hidden"],
            childList: true,
            characterData: true,
            subtree: true
        });
        sync();
    };

    document.querySelectorAll("#success-message, [data-quiz-save-status]")
        .forEach(watchStatus);

    const validationCandidates = editor.querySelectorAll(
        ".validation-summary li, .field-validation-error, .text-danger");
    const validationMessage = [...validationCandidates]
        .map(element => element.textContent?.trim())
        .find(Boolean);

    if (validationMessage) {
        const status = document.createElement("div");
        status.className = "alert alert-error editor-save-overlay";
        status.setAttribute("role", "status");
        status.textContent = validationMessage;
        document.body.appendChild(status);
        showStatus(status);
    }
})();
