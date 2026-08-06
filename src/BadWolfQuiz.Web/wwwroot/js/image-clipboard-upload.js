(function () {
    "use strict";

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
