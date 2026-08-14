(() => {
    const dialog = document.querySelector("[data-discord-settings-dialog]");
    const frame = dialog?.querySelector("[data-discord-settings-frame]");
    if (!dialog || !frame) {
        return;
    }

    const bindOpenButtons = () => {
        document.querySelectorAll("[data-open-discord-settings]").forEach(button => {
            if (button.dataset.discordSettingsDialogInitialized === "true") {
                return;
            }

            button.dataset.discordSettingsDialogInitialized = "true";
            button.addEventListener("click", () => {
                if (!frame.src) {
                    frame.src = frame.dataset.src;
                }
                dialog.showModal();
            });
        });
    };

    const publishVoiceReadiness = () => {
        const frameDocument = frame.contentDocument;
        if (!frameDocument) {
            return;
        }

        window.dispatchEvent(new CustomEvent("badwolfquiz:discord-voice-ready-changed", {
            detail: {
                ready: frameDocument.querySelector("[data-discord-test]") !== null
            }
        }));
    };

    bindOpenButtons();
    document.addEventListener("badwolf:host-shell-mounted", bindOpenButtons);
    frame.addEventListener("load", publishVoiceReadiness);

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

    window.addEventListener("message", event => {
        if (event.origin !== window.location.origin ||
            event.source !== frame.contentWindow ||
            event.data?.type !== "badwolfquiz:discord-auto-mute-changed") {
            return;
        }

        window.dispatchEvent(new CustomEvent("badwolfquiz:discord-auto-mute-changed", {
            detail: { enabled: event.data.enabled === true }
        }));
    });
})();
