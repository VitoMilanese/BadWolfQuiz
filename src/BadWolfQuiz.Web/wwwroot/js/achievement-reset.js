(() => {
    const trashIcon = `
        <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="M4 7h16"></path>
            <path d="M9 7V4h6v3"></path>
            <path d="m7 7 1 13h8l1-13"></path>
            <path d="M10 11v5"></path>
            <path d="M14 11v5"></path>
        </svg>`;

    const getCardTitle = card =>
        card.querySelector(":scope > strong")?.textContent?.trim() ||
        card.dataset.achievementCode ||
        "";

    const setCardLocked = (card, result, dialog) => {
        card.classList.remove("is-unlocked", "is-new");
        card.classList.add("is-locked");
        card.querySelector("[data-achievement-reset-button]")?.remove();

        const state = card.querySelector(".player-achievement-state");
        if (state) {
            state.classList.remove("is-secret-unlocked");
            state.replaceChildren(document.createTextNode("○"));
        }

        const top = card.querySelector(".player-achievement-card-top");
        if (top && result.isSecret === true) {
            const artwork = top.querySelector(".player-achievement-image, .player-achievement-icon");
            const secret = document.createElement("span");
            secret.className = "player-achievement-icon";
            secret.setAttribute("aria-hidden", "true");
            secret.textContent = "❔";
            artwork?.replaceWith(secret);

            const title = card.querySelector(":scope > strong");
            const description = card.querySelector(":scope > p");
            if (title && dialog.dataset.secretTitle) {
                title.textContent = dialog.dataset.secretTitle;
            }
            if (description && dialog.dataset.secretDescription) {
                description.textContent = dialog.dataset.secretDescription;
            }
        }

        card.querySelector(":scope > .player-achievement-progress")?.remove();
        const currentSmall = card.querySelector(":scope > small");
        currentSmall?.remove();

        if (result.isSecret !== true) {
            const target = Math.max(0, Number(result.target) || 0);
            const progress = document.createElement("div");
            progress.className = "player-achievement-progress";
            progress.setAttribute("aria-label", `0 / ${target}`);
            const bar = document.createElement("span");
            bar.style.width = "0%";
            progress.append(bar);

            const small = document.createElement("small");
            small.textContent = `0 / ${target}`;
            card.append(progress, small);
        }
    };

    const initialize = () => {
        const dialog = document.querySelector("[data-achievement-reset-dialog]");
        if (!(dialog instanceof HTMLDialogElement)) {
            return;
        }

        const form = dialog.querySelector("[data-achievement-reset-form]");
        const codeInput = form?.querySelector('input[name="achievementCode"]');
        const message = dialog.querySelector("[data-achievement-reset-message]");
        const error = dialog.querySelector("[data-achievement-reset-error]");
        const submit = form?.querySelector('button[type="submit"]');
        let activeCard = null;
        let lastButton = null;

        const openForCard = (card, button) => {
            const code = card.dataset.achievementCode?.trim();
            if (!code || !codeInput) {
                return;
            }

            activeCard = card;
            lastButton = button;
            codeInput.value = code;
            if (message) {
                const template = dialog.dataset.confirmTemplate || "{0}";
                message.textContent = template.replace("{0}", getCardTitle(card));
            }
            if (error) {
                error.hidden = true;
                error.textContent = "";
            }
            if (!dialog.open) {
                dialog.showModal();
            }
        };

        const attachButton = card => {
            if (!(card instanceof HTMLElement) ||
                !card.matches(".player-achievement-card.is-unlocked[data-achievement-code]") ||
                card.querySelector("[data-achievement-reset-button]")) {
                return;
            }

            const button = document.createElement("button");
            button.type = "button";
            button.className = "player-achievement-reset-button";
            button.dataset.achievementResetButton = "";
            button.title = dialog.dataset.actionLabel || "";
            button.setAttribute(
                "aria-label",
                `${dialog.dataset.actionLabel || ""}: ${getCardTitle(card)}`.replace(/^:\s*/, ""));
            button.innerHTML = trashIcon;
            button.addEventListener("click", event => {
                event.preventDefault();
                event.stopPropagation();
                openForCard(card, button);
            });

            const top = card.querySelector(".player-achievement-card-top");
            top?.append(button);
        };

        const scan = root => {
            if (root instanceof HTMLElement && root.matches(".player-achievement-card")) {
                attachButton(root);
            }
            if (root instanceof Element || root instanceof Document) {
                for (const card of root.querySelectorAll(
                    ".player-achievement-card.is-unlocked[data-achievement-code]")) {
                    attachButton(card);
                }
            }
        };

        scan(document);
        const observer = new MutationObserver(mutations => {
            for (const mutation of mutations) {
                if (mutation.type === "attributes" && mutation.target instanceof HTMLElement) {
                    attachButton(mutation.target);
                }
                for (const node of mutation.addedNodes) {
                    if (node instanceof Element) {
                        scan(node);
                    }
                }
            }
        });
        observer.observe(document.body, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ["class"]
        });

        for (const cancel of dialog.querySelectorAll("[data-achievement-reset-cancel]")) {
            cancel.addEventListener("click", () => dialog.close());
        }
        dialog.addEventListener("click", event => {
            if (event.target === dialog) {
                dialog.close();
            }
        });
        dialog.addEventListener("close", () => {
            activeCard = null;
            lastButton?.focus();
            lastButton = null;
        });

        form?.addEventListener("submit", async event => {
            event.preventDefault();
            if (!activeCard || !form.action) {
                return;
            }

            if (error) {
                error.hidden = true;
                error.textContent = "";
            }
            if (submit instanceof HTMLButtonElement) {
                submit.disabled = true;
            }

            try {
                const response = await fetch(form.action, {
                    method: "POST",
                    body: new FormData(form),
                    credentials: "same-origin",
                    headers: {
                        Accept: "application/json",
                        "X-Requested-With": "XMLHttpRequest"
                    }
                });
                const result = await response.json().catch(() => null);
                if (!response.ok || result?.reset !== true) {
                    throw new Error(result?.message || dialog.dataset.errorLabel || "Reset failed");
                }

                setCardLocked(activeCard, result, dialog);
                dialog.close();
            } catch (exception) {
                if (error) {
                    error.textContent = exception instanceof Error && exception.message
                        ? exception.message
                        : dialog.dataset.errorLabel || "Reset failed";
                    error.hidden = false;
                }
            } finally {
                if (submit instanceof HTMLButtonElement) {
                    submit.disabled = false;
                }
            }
        });
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
