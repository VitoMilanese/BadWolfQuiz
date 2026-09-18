(() => {
    const initializeDialog = dialog => {
        const lobby = dialog.closest(".player-lobby");
        if (!lobby) {
            return;
        }

        let lastOpener = null;
        for (const opener of lobby.querySelectorAll("[data-player-achievements-open]")) {
            opener.hidden = false;

            // The player lobby action drawer owns transitions to its target
            // dialogs so it can animate closed before the modal opens.
            if (opener.hasAttribute("data-player-dialog-target")) {
                continue;
            }

            opener.addEventListener("click", () => {
                lastOpener = opener;
                if (!dialog.open) {
                    dialog.showModal();
                }
            });
        }

        dialog.querySelector("[data-player-achievements-close]")
            ?.addEventListener("click", () => dialog.close());

        dialog.addEventListener("click", event => {
            if (event.target === dialog) {
                dialog.close();
            }
        });

        dialog.addEventListener("close", () => {
            lastOpener?.focus();
        });
    };

    const initialize = () => {
        for (const dialog of document.querySelectorAll("[data-player-achievements-dialog]")) {
            initializeDialog(dialog);
        }
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
