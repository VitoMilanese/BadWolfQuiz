for (const message of document.querySelectorAll("[data-auto-dismiss]")) {
    window.setTimeout(() => {
        message.classList.add("message-hidden");
        message.addEventListener("transitionend", () => message.remove(), { once: true });
    }, 4000);
}

document.querySelectorAll("details.action-menu").forEach(menu => {
    menu.addEventListener("toggle", () => {
        if (!menu.open) {
            return;
        }

        document.querySelectorAll("details.action-menu[open]").forEach(other => {
            if (other !== menu) {
                other.removeAttribute("open");
            }
        });
    });
});

document.addEventListener("click", event => {
    const selectedItem = event.target.closest?.(".action-menu-item");
    selectedItem?.closest("details.action-menu")?.removeAttribute("open");

    document.querySelectorAll("details.action-menu[open]").forEach(menu => {
        if (!menu.contains(event.target)) {
            menu.removeAttribute("open");
        }
    });
});

document.addEventListener("keydown", event => {
    if (event.key === "Escape") {
        document.querySelectorAll("details.action-menu[open]").forEach(menu => {
            menu.removeAttribute("open");
        });
    }
});

document.querySelectorAll("[data-auto-rating-form]").forEach(form => {
    form.addEventListener("submit", event => event.preventDefault());
    const inputs = form.querySelectorAll('input[name="score"]');
    const state = form.querySelector("[data-rating-save-state]");

    const saveRating = async score => {
            const formData = new FormData(form);
            formData.set("score", score);
            inputs.forEach(item => item.disabled = true);
            if (state) {
                state.textContent = "";
            }

            try {
                const response = await fetch(form.action, {
                    method: "POST",
                    body: formData,
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                if (!response.ok) {
                    throw new Error("Rating was not saved.");
                }
                form.classList.add("is-saved");
            } catch {
                if (state) {
                    state.textContent = form.dataset.ratingErrorLabel;
                }
            } finally {
                inputs.forEach(item => item.disabled = false);
            }
    };

    inputs.forEach(input => {
        input.addEventListener("change", () => saveRating(input.value));
        const label = form.querySelector(`label[for="${input.id}"]`);
        label?.addEventListener("click", event => {
            if (!input.checked) {
                return;
            }

            event.preventDefault();
            input.checked = false;
            saveRating("0");
        });
    });
});

const languageButton = document.getElementById("languageButton");
const languageMenu = document.getElementById("languageMenu");

languageButton?.addEventListener("click", event => {
    event.stopPropagation();
    document.querySelectorAll("details.action-menu[open]").forEach(menu => {
        menu.removeAttribute("open");
    });
    const isOpen = languageMenu.classList.toggle("open");
    languageButton.setAttribute("aria-expanded", isOpen.toString());
});

languageMenu?.addEventListener("click", event => event.stopPropagation());

document.addEventListener("click", () => {
    languageMenu?.classList.remove("open");
    languageButton?.setAttribute("aria-expanded", "false");
});

document.addEventListener("keydown", event => {
    if (event.key === "Escape") {
        languageMenu?.classList.remove("open");
        languageButton?.setAttribute("aria-expanded", "false");
    }
});

if (window.location.pathname.includes("/Admin/Games/Lobby/")) {
    const gameId = window.location.pathname.split("/").filter(Boolean).at(-1);
    const startButton = document.querySelector('.lobby-start-button[form="start-game-form"]');

    if (gameId && startButton) {
        startButton.formAction = `/Admin/Games/RoundIntro/${encodeURIComponent(gameId)}?handler=Prepare`;
    }

    if (gameId) {
        document.querySelectorAll("form").forEach(form => {
            if (!form.action) {
                return;
            }

            const action = new URL(form.action, window.location.origin);
            if (action.searchParams.get("handler") !== "AdvanceRound") {
                return;
            }

            form.addEventListener("submit", async event => {
                event.preventDefault();

                const submitter = event.submitter;
                if (submitter) {
                    submitter.disabled = true;
                }

                try {
                    const response = await fetch(form.action, {
                        method: "POST",
                        body: new FormData(form),
                        credentials: "same-origin",
                        headers: { "X-Requested-With": "XMLHttpRequest" }
                    });

                    if (!response.ok) {
                        throw new Error("Round advance failed.");
                    }

                    window.location.assign(
                        `/Admin/Games/RunningRoundIntro/${encodeURIComponent(gameId)}`);
                } catch {
                    form.submit();
                }
            });
        });
    }
}
