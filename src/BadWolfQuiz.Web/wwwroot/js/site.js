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

// Toggle the floating join-code panel from any join-code trigger, including the header QR button.
document.addEventListener("click", event => {
    const trigger = event.target.closest?.("[data-open-join-code]");
    if (!trigger) {
        return;
    }

    const panel = document.querySelector("[data-join-code-panel]");
    if (!panel) {
        return;
    }

    event.preventDefault();
    event.stopImmediatePropagation();
    panel.hidden = !panel.hidden;
}, true);

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
    const runningIntroBase = `/Admin/Games/RunningRoundIntro/${encodedGameId}`;
    const finalTransitionBase = `/Admin/Games/FinalQuestionTransition/${encodedGameId}`;
    const startButton = document.querySelector('.lobby-start-button[form="start-game-form"]');
    if (startButton) {
        startButton.formAction = `/Admin/Games/RoundIntro/${encodedGameId}?handler=Prepare`;
    }

    const getFormHandler = form => {
        if (!(form instanceof HTMLFormElement) || !form.action) {
            return null;
        }

        return new URL(form.action, window.location.origin).searchParams.get("handler");
    };

    const openFinalTransition = force => {
        window.location.assign(force
            ? `${finalTransitionBase}?force=true`
            : finalTransitionBase);
    };

    const routeRoundForm = form => {
        if (!(form instanceof HTMLFormElement) || !form.action) {
            return;
        }

        const action = new URL(form.action, window.location.origin);
        const handler = action.searchParams.get("handler");

        if (form.id === "force-advance-round-form" || handler === "ForceAdvanceRound") {
            form.action = `${runningIntroBase}?handler=ForceAdvance`;
            return;
        }

        if (handler === "PreviousRound") {
            form.action = `${runningIntroBase}?handler=Previous`;
            return;
        }

        if (handler === "ReturnToUnfinishedRound") {
            form.action = `${runningIntroBase}?handler=ReturnToUnfinished`;
            return;
        }

        if (handler === "AdvanceRound") {
            form.action = `${runningIntroBase}?handler=Advance`;
        }
    };

    const submitRoutedForm = form => {
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        routeRoundForm(form);
        HTMLFormElement.prototype.submit.call(form);
    };

    const advanceEmptyRoundSummary = () => {
        const summary = document.querySelector(".host-game-board .round-summary");
        if (!summary || summary.querySelector(".round-podium-player")) {
            return false;
        }

        const form = summary.querySelector("form");
        if (!(form instanceof HTMLFormElement)) {
            return false;
        }

        const action = new URL(form.action, window.location.origin);
        if (action.searchParams.get("handler") !== "AdvanceRound" &&
            !action.pathname.includes("/RunningRoundIntro/")) {
            return false;
        }

        if (summary.dataset.autoAdvanceStarted === "true") {
            return true;
        }

        summary.dataset.autoAdvanceStarted = "true";
        submitRoutedForm(form);
        return true;
    };

    document.querySelectorAll("form").forEach(routeRoundForm);

    document.addEventListener("click", event => {
        const target = event.target instanceof Element ? event.target : null;

        const categoryPreview = target?.closest("[data-category-preview-url]");
        if (categoryPreview) {
            event.preventDefault();
            event.stopImmediatePropagation();
            window.location.assign(categoryPreview.dataset.categoryPreviewUrl);
            return;
        }

        if (target?.closest("[data-open-natural-final-warning]")) {
            event.preventDefault();
            event.stopImmediatePropagation();
            const dialog = document.getElementById("natural-final-warning-dialog");
            if (dialog instanceof HTMLDialogElement && !dialog.open) {
                dialog.showModal();
            }
            return;
        }

        if (target?.closest("[data-confirm-force-advance-round]")) {
            const form = document.getElementById("force-advance-round-form");
            if (!(form instanceof HTMLFormElement)) {
                return;
            }

            const hasPlayers = document.querySelector(
                ".scoreboard-player[data-player-id]:not([data-host-card])") !== null;

            if (!hasPlayers) {
                event.preventDefault();
                event.stopImmediatePropagation();
                document.getElementById("force-advance-round-dialog")?.close();

                fetch(`${runningIntroBase}?handler=ForceAdvance`, {
                    method: "POST",
                    body: new FormData(form),
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                })
                    .then(response => {
                        if (!response.ok) {
                            throw new Error(response.statusText);
                        }
                        window.location.assign(response.url || `${runningIntroBase}?returning=true`);
                    })
                    .catch(error => {
                        console.error(error);
                        window.location.reload();
                    });
                return;
            }
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

    document.addEventListener("keydown", event => {
        if (event.key !== "Enter" && event.key !== " ") {
            return;
        }

        const categoryPreview = event.target instanceof Element
            ? event.target.closest("[data-category-preview-url]")
            : null;
        if (!categoryPreview) {
            return;
        }

        event.preventDefault();
        window.location.assign(categoryPreview.dataset.categoryPreviewUrl);
    });

    document.addEventListener("submit", event => {
        const form = event.target;
        const handler = getFormHandler(form);

        if (handler === "StartFinalQuestion" || handler === "ForceAdvanceToFinalQuestion") {
            event.preventDefault();
            event.stopImmediatePropagation();
            openFinalTransition(handler === "ForceAdvanceToFinalQuestion");
            return;
        }

        if (handler === "Previous" ||
            handler === "PreviousRound" ||
            handler === "ReturnToUnfinished" ||
            handler === "ReturnToUnfinishedRound") {
            event.preventDefault();
            event.stopImmediatePropagation();
            routeRoundForm(form);

            const hostBoard = document.querySelector(".host-game-board");
            hostBoard?.classList.remove("host-game-board");

            fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: { "X-Requested-With": "XMLHttpRequest" }
            })
                .then(response => {
                    if (!response.ok) {
                        throw new Error(response.statusText);
                    }

                    window.location.assign(response.url || `${runningIntroBase}?returning=true`);
                })
                .catch(error => {
                    console.error(error);
                    hostBoard?.classList.add("host-game-board");
                    window.location.reload();
                });
            return;
        }

        routeRoundForm(form);
    }, true);

    if (advanceEmptyRoundSummary()) {
        return;
    }

    const observer = new MutationObserver(() => {
        document.querySelectorAll("form").forEach(routeRoundForm);
        advanceEmptyRoundSummary();
    });
    observer.observe(document.body, { childList: true, subtree: true });
};

if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", configureGameRoundIntroRoutes, { once: true });
} else {
    configureGameRoundIntroRoutes();
}
