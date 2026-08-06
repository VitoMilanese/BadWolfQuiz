(() => {
    const dialog = document.querySelector("[data-discord-settings-dialog]");
    const frame = dialog?.querySelector("[data-discord-settings-frame]");
    if (!dialog || !frame) {
        return;
    }

    document.querySelectorAll("[data-open-discord-settings]").forEach(button => {
        button.addEventListener("click", () => {
            if (!frame.src) {
                frame.src = frame.dataset.src;
            }
            dialog.showModal();
        });
    });

    dialog.querySelector("[data-close-discord-settings]")
        ?.addEventListener("click", () => dialog.close());
    dialog.addEventListener("click", event => {
        if (event.target === dialog) {
            dialog.close();
        }
    });
    dialog.addEventListener("close", () => {
        if (frame.src) {
            frame.src = frame.src;
        }
    });
})();
