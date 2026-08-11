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

const configureGameRoundIntroRoutes = () => {
    const gameBoard = document.querySelector(".host-game-board[data-game-id]");
    const pathGameId = window.location.pathname.split("/").filter(Boolean).at(-1);
    const gameId = gameBoard?.dataset.gameId || pathGameId;

    if (!gameId || !window.location.pathname.includes("/Admin/Games/Lobby/")) {
        return;
    }

    const encodedGameId = encodeURIComponent(gameId);
    const startButton = document.querySelector('.lobby-start-button[form="start-game-form"]');
    if (startButton) {
        startButton.formAction = `/Admin/Games/RoundIntro/${encodedGameId}?handler=Prepare`;
    }

    const routeRoundForm = form => {
        if (!(form instanceof HTMLFormElement) || !form.action) {
            return;
        }

        const action = new URL(form.action, window.location.origin);
        const handler = action.searchParams.get("handler");

        if (form.id === "force-advance-round-form" || handler === "ForceAdvanceRound") {
            form.action = `/Admin/Games/RunningRoundIntro/${encodedGameId}?handler=ForceAdvance`;
            return;
        }

        if (handler === "AdvanceRound") {
            form.action = `/Admin/Games/RunningRoundIntro/${encodedGameId}?handler=Advance`;
        }
    };

    document.querySelectorAll("form").forEach(routeRoundForm);

    document.addEventListener("click", event => {
        const target = event.target instanceof Element ? event.target : null;
        if (target?.closest("[data-confirm-force-advance-round]")) {
            const form = document.getElementById("force-advance-round-form");
            if (!(form instanceof HTMLFormElement)) {
                return;
            }

            event.preventDefault();
            event.stopImmediatePropagation();
            routeRoundForm(form);
            document.getElementById("force-advance-round-dialog")?.close();
            form.requestSubmit();
            return;
        }

        const submitter = target?.closest("button, input[type='submit']");
        if (!submitter?.form) {
            return;
        }

        routeRoundForm(submitter.form);
        const action = new URL(submitter.form.action, window.location.origin);
        if (action.pathname.includes("/Admin/Games/RunningRoundIntro/")) {
            submitter.formAction = submitter.form.action;
        }
    }, true);

    document.addEventListener("submit", event => {
        routeRoundForm(event.target);
    }, true);

    const observer = new MutationObserver(() => {
        document.querySelectorAll("form").forEach(routeRoundForm);
    });
    observer.observe(document.body, { childList: true, subtree: true });
};

if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", configureGameRoundIntroRoutes, { once: true });
} else {
    configureGameRoundIntroRoutes();
}
