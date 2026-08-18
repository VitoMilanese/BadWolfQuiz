(() => {
    "use strict";

    if (window.badWolfEditorSaveOverlayInitialized) {
        return;
    }

    window.badWolfEditorSaveOverlayInitialized = true;

    const editor = document.querySelector(
        "form.quiz-board-form, form.question-editor");
    if (!editor) {
        return;
    }

    const editorSaveOverlayDurationMs = 1500;
    const editorSaveOverlayTransitionMs = 150;
    const editorSaveOverlayTimers = new WeakMap();

    const style = document.createElement("style");
    style.id = "editor-save-overlay-styles";
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
    padding: 0.95rem 1.2rem !important;
    box-sizing: border-box;
    transform: translateX(-50%);
    pointer-events: none;
    text-align: center;
    font-size: 1.08rem;
    font-weight: 600;
    line-height: 1.35;
    opacity: 1;
    box-shadow: 0 14px 40px rgba(0, 0, 0, 0.42);
    transition: opacity 150ms ease, transform 150ms ease;
}

body .editor-save-overlay.alert-success {
    border-color: rgba(63, 185, 80, 0.9) !important;
    background: rgba(63, 185, 80, 0.34) !important;
    background: color-mix(in srgb, rgb(63 185 80) 32%, var(--panel)) !important;
}

body .editor-save-overlay.alert-error {
    border-color: rgba(239, 35, 60, 0.92) !important;
    background: rgba(239, 35, 60, 0.34) !important;
    background: color-mix(in srgb, rgb(239 35 60) 32%, var(--panel)) !important;
}

body .editor-save-overlay.editor-save-overlay-hiding {
    opacity: 0;
    transform: translate(-50%, -8px) !important;
}

body .editor-save-overlay[hidden] {
    display: none !important;
}`;
    document.head.appendChild(style);

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

    document.querySelectorAll(
        "[data-editor-save-status], #success-message, [data-quiz-save-status], [data-question-save-status]")
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
