(() => {
    const initializeDialog = dialog => {
        const lobby = dialog.closest(".player-lobby");
        if (!lobby) {
            return;
        }

        const label = lobby.dataset.playerAchievementsLabel?.trim();
        const playerName = lobby.dataset.playerName?.trim();
        const heading = Array.from(lobby.children)
            .find(element => element.tagName === "H1");
        const playerLine = heading?.nextElementSibling;

        if (label &&
            playerName &&
            playerLine instanceof HTMLParagraphElement) {
            const opener = document.createElement("button");
            opener.type = "button";
            opener.className = "player-achievements-open";
            opener.dataset.playerAchievementsOpen = "";
            opener.setAttribute("aria-haspopup", "dialog");
            opener.setAttribute("aria-controls", dialog.id);

            const labelElement = document.createElement("strong");
            labelElement.textContent = `${label}:`;
            opener.append(labelElement, document.createTextNode(` ${playerName}`));
            playerLine.replaceChildren(opener);
        }

        let lastOpener = null;
        for (const opener of lobby.querySelectorAll("[data-player-achievements-open]")) {
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
