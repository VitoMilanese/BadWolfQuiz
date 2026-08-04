(function () {
    "use strict";

    const extensions = {
        "image/avif": "avif",
        "image/gif": "gif",
        "image/jpeg": "jpg",
        "image/png": "png",
        "image/svg+xml": "svg",
        "image/webp": "webp"
    };

    function showError(button, message) {
        const editor = button.closest(".image-block-editor");
        const error = editor?.querySelector("[data-image-clipboard-error]");
        if (error) {
            error.textContent = message;
            error.hidden = false;
        }
    }

    function clearError(button) {
        const editor = button.closest(".image-block-editor");
        const error = editor?.querySelector("[data-image-clipboard-error]");
        if (error) {
            error.textContent = "";
            error.hidden = true;
        }
    }

    async function findImage() {
        if (!navigator.clipboard?.read) {
            throw new Error("Clipboard API is unavailable.");
        }

        const items = await navigator.clipboard.read();
        for (const item of items) {
            const imageType = item.types.find(type => type.startsWith("image/"));
            if (imageType) {
                return item.getType(imageType);
            }
        }

        return null;
    }

    document.addEventListener("click", async event => {
        const button = event.target.closest("[data-image-clipboard-button]");
        if (!button) {
            return;
        }

        clearError(button);
        button.disabled = true;
        try {
            const blob = await findImage();
            if (!blob) {
                showError(button, button.dataset.clipboardEmpty);
                return;
            }

            const editor = button.closest(".image-block-editor");
            const input = editor?.querySelector(".uploaded-file-input");
            if (!input) {
                return;
            }

            const extension = extensions[blob.type] || "png";
            const file = blob instanceof File && blob.name
                ? blob
                : new File([blob], `clipboard-image.${extension}`, { type: blob.type });
            const transfer = new DataTransfer();
            transfer.items.add(file);
            input.files = transfer.files;
            input.dispatchEvent(new Event("change", { bubbles: true }));
        } catch {
            showError(button, button.dataset.clipboardError);
        } finally {
            button.disabled = false;
        }
    });
}());
