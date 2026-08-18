(function () {
    "use strict";

    const editorSaveOverlayDurationMs = 1000;
    const editorSaveOverlayTransitionMs = 150;
    const editorSaveOverlayTimers = new WeakMap();

    function ensureEditorSaveOverlayStyles() {
        if (document.getElementById("editor-save-overlay-styles")) {
            return;
        }

        const style = document.createElement("style");
        style.id = "editor-save-overlay-styles";
        style.textContent = `
.editor-save-overlay {
    position: fixed !important;
    z-index: 1200;
    left: 50%;
    bottom: max(24px, env(safe-area-inset-bottom));
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

.editor-save-overlay.editor-save-overlay-hiding {
    opacity: 0;
    transform: translate(-50%, 8px);
}

.editor-save-overlay[hidden] {
    display: none !important;
}`;
        document.head.appendChild(style);
    }

    function moveEditorSaveStatusToOverlay(status) {
        ensureEditorSaveOverlayStyles();
        status.classList.add("editor-save-overlay");
        if (status.parentElement !== document.body) {
            document.body.appendChild(status);
        }
    }

    function scheduleEditorSaveOverlayHide(status) {
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
    }

    function showEditorSaveStatus(status) {
        moveEditorSaveStatusToOverlay(status);
        status.classList.remove("editor-save-overlay-hiding", "message-hidden");
        scheduleEditorSaveOverlayHide(status);
    }

    function watchEditorSaveStatus(status) {
        moveEditorSaveStatusToOverlay(status);

        const sync = () => {
            if (status.hidden || !status.textContent?.trim()) {
                return;
            }

            showEditorSaveStatus(status);
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
    }

    function getEditorValidationMessage(editor) {
        const candidates = editor.querySelectorAll(
            ".validation-summary li, .field-validation-error, .text-danger");

        for (const candidate of candidates) {
            const message = candidate.textContent?.trim();
            if (message) {
                return message;
            }
        }

        return "";
    }

    function initializeEditorSaveOverlay() {
        const editor = document.querySelector("form.question-editor");
        if (!editor) {
            return;
        }

        document.querySelectorAll(
            "#success-message, [data-question-save-status]")
            .forEach(watchEditorSaveStatus);

        const validationMessage = getEditorValidationMessage(editor);
        if (!validationMessage) {
            return;
        }

        const status = document.createElement("div");
        status.className = "alert alert-error editor-save-overlay";
        status.setAttribute("role", "status");
        status.textContent = validationMessage;
        document.body.appendChild(status);
        showEditorSaveStatus(status);
    }

    let filePickerPending = false;
    let suppressEscapeUntil = 0;

    document.addEventListener("click", event => {
        if (event.target.closest?.('input[type="file"]')) {
            filePickerPending = true;
        }
    }, true);

    window.addEventListener("focus", () => {
        if (!filePickerPending) {
            return;
        }

        filePickerPending = false;
        suppressEscapeUntil = Date.now() + 1000;
    }, true);

    window.addEventListener("keyup", event => {
        if (event.key !== "Escape" ||
            (!filePickerPending && Date.now() > suppressEscapeUntil)) {
            return;
        }

        filePickerPending = false;
        suppressEscapeUntil = 0;
        event.stopImmediatePropagation();
    }, true);

    const extensionsByMimeType = {
        "audio/aac": "aac",
        "audio/flac": "flac",
        "audio/m4a": "m4a",
        "audio/mp4": "m4a",
        "audio/mpeg": "mp3",
        "audio/ogg": "ogg",
        "audio/wav": "wav",
        "audio/webm": "webm",
        "audio/x-m4a": "m4a",
        "audio/x-wav": "wav",
        "image/avif": "avif",
        "image/gif": "gif",
        "image/jpeg": "jpg",
        "image/png": "png",
        "image/svg+xml": "svg",
        "image/webp": "webp"
    };

    const extensionsByKind = {
        audio: new Set(["aac", "flac", "m4a", "mp3", "mp4", "oga", "ogg", "wav", "webm"]),
        image: new Set(["avif", "gif", "jpeg", "jpg", "png", "svg", "webp"])
    };

    function getEditorKind(editor) {
        return editor?.classList.contains("audio-block-editor")
            ? "audio"
            : "image";
    }

    function getExtension(fileName) {
        const separator = fileName.lastIndexOf(".");
        return separator >= 0
            ? fileName.slice(separator + 1).toLowerCase()
            : "";
    }

    function isExpectedFile(file, kind) {
        if (file.type) {
            return file.type.toLowerCase().startsWith(`${kind}/`);
        }

        return extensionsByKind[kind].has(getExtension(file.name));
    }

    function showError(button, message) {
        const editor = button.closest(".image-block-editor, .audio-block-editor");
        const error = editor?.querySelector("[data-media-clipboard-error]");
        if (error) {
            error.textContent = message;
            error.hidden = false;
        }
    }

    function clearError(button) {
        const editor = button.closest(".image-block-editor, .audio-block-editor");
        const error = editor?.querySelector("[data-media-clipboard-error]");
        if (error) {
            error.textContent = "";
            error.hidden = true;
        }
    }

    function setUploadedFile(editor, file) {
        const input = editor?.querySelector(".uploaded-file-input");
        if (!input) {
            return false;
        }

        const transfer = new DataTransfer();
        transfer.items.add(file);
        input.files = transfer.files;
        input.dispatchEvent(new Event("change", { bubbles: true }));
        return true;
    }

    async function findMediaBlob(kind) {
        if (!navigator.clipboard?.read) {
            throw new Error("Clipboard API is unavailable.");
        }

        const items = await navigator.clipboard.read();
        for (const item of items) {
            const mediaType = item.types.find(type =>
                type.toLowerCase().startsWith(`${kind}/`));
            if (mediaType) {
                return item.getType(mediaType);
            }
        }

        return null;
    }

    function createClipboardFile(blob, kind) {
        if (blob instanceof File && blob.name) {
            return blob;
        }

        const extension = extensionsByMimeType[blob.type.toLowerCase()] ||
            (kind === "audio" ? "mp3" : "png");
        return new File(
            [blob],
            `clipboard-${kind}.${extension}`,
            { type: blob.type });
    }

    function initializeQuestionEditorTabs() {
        const form = document.querySelector(".question-editor");
        const questionSection = document.getElementById("question-blocks");
        const answerSection = document.getElementById("answer-blocks");

        if (!form || !questionSection || !answerSection) {
            return;
        }

        const questionHeading = questionSection.previousElementSibling;
        const questionValidation = questionSection.nextElementSibling;
        const answerHeading = answerSection.previousElementSibling;
        const answerValidation = answerSection.nextElementSibling;
        const questionTypeSetting = form.querySelector(".question-type-setting");

        if (questionHeading?.tagName !== "H2" ||
            answerHeading?.tagName !== "H2") {
            return;
        }

        const questionTitle = questionHeading.textContent.trim();
        const answerTitle = answerHeading.textContent.trim();

        const tabs = document.createElement("div");
        tabs.className = "editor-actions question-answer-tabs";
        tabs.setAttribute("role", "tablist");

        const createTab = (name, title, active) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = active
                ? "button button-primary"
                : "button button-secondary";
            button.textContent = title;
            button.dataset.questionEditorTab = name;
            button.setAttribute("role", "tab");
            button.setAttribute("aria-selected", active ? "true" : "false");
            return button;
        };

        const questionTab = createTab("question", questionTitle, true);
        const answerTab = createTab("answer", answerTitle, false);
        tabs.append(questionTab, answerTab);

        const tabsAnchor = questionTypeSetting || questionHeading;
        tabsAnchor.before(tabs);

        questionHeading.hidden = true;
        answerHeading.hidden = true;

        const groups = {
            question: [questionTypeSetting, questionSection, questionValidation],
            answer: [answerSection, answerValidation]
        };

        const selectTab = name => {
            const questionActive = name === "question";

            questionTab.classList.toggle("button-primary", questionActive);
            questionTab.classList.toggle("button-secondary", !questionActive);
            questionTab.setAttribute(
                "aria-selected",
                questionActive ? "true" : "false");

            answerTab.classList.toggle("button-primary", !questionActive);
            answerTab.classList.toggle("button-secondary", questionActive);
            answerTab.setAttribute(
                "aria-selected",
                questionActive ? "false" : "true");

            for (const element of groups.question) {
                if (element) {
                    element.hidden = !questionActive;
                }
            }

            for (const element of groups.answer) {
                if (element) {
                    element.hidden = questionActive;
                }
            }
        };

        questionTab.addEventListener("click", () => selectTab("question"));
        answerTab.addEventListener("click", () => selectTab("answer"));

        selectTab("question");
    }

    initializeEditorSaveOverlay();
    initializeQuestionEditorTabs();

    document.addEventListener("click", async event => {
        const button = event.target.closest("[data-media-clipboard-button]");
        if (!button) {
            return;
        }

        const editor = button.closest(".image-block-editor, .audio-block-editor");
        const kind = button.dataset.mediaKind || getEditorKind(editor);
        clearError(button);
        button.disabled = true;
        try {
            const blob = await findMediaBlob(kind);
            if (!blob) {
                showError(button, button.dataset.clipboardEmpty);
                return;
            }

            setUploadedFile(editor, createClipboardFile(blob, kind));
        } catch {
            showError(button, button.dataset.clipboardError);
        } finally {
            button.disabled = false;
        }
    });

    document.addEventListener("paste", event => {
        const editor = event.target.closest?.(
            ".image-block-editor, .audio-block-editor");
        if (!editor || !event.clipboardData?.files?.length) {
            return;
        }

        const button = editor.querySelector("[data-media-clipboard-button]");
        const kind = button?.dataset.mediaKind || getEditorKind(editor);
        const file = [...event.clipboardData.files]
            .find(item => isExpectedFile(item, kind));

        if (!file) {
            if (button) {
                showError(button, button.dataset.clipboardEmpty);
            }
            return;
        }

        event.preventDefault();
        if (button) {
            clearError(button);
        }
        setUploadedFile(editor, file);
    });
}());
