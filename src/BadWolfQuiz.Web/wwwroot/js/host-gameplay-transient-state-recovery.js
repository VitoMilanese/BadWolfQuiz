(() => {
    "use strict";

    if (window.badWolfHostGameplayTransientStateRecoveryInstalled) {
        return;
    }
    window.badWolfHostGameplayTransientStateRecoveryInstalled = true;

    const questionSelectionSelector = ".question-selection-form";
    const hostBoardSelector = ".host-game-board[data-game-id]";
    const persistentBoardSelector = "[data-host-gameplay-board]";
    const playerStateSelector =
        ".game-scoreboard .scoreboard-player.question-answering-player, " +
        ".game-scoreboard .scoreboard-player.question-attempted-player";

    const showQuestionSelectionError = message => {
        const errorTarget = document.getElementById("game-board-error");
        if (!(errorTarget instanceof HTMLElement)) {
            console.error(message);
            return;
        }

        errorTarget.textContent = message || "Request failed.";
        errorTarget.hidden = false;
        errorTarget.classList.remove("message-hidden");

        window.setTimeout(() => {
            if (!errorTarget.isConnected) {
                return;
            }
            errorTarget.classList.add("message-hidden");
            window.setTimeout(() => {
                if (!errorTarget.isConnected) {
                    return;
                }
                errorTarget.hidden = true;
                errorTarget.classList.remove("message-hidden");
            }, 300);
        }, 3000);
    };

    const submitReplacedQuestionSelection = async (form, submitter) => {
        if (!(form instanceof HTMLFormElement) ||
            form.dataset.transientRecoverySubmitting === "true") {
            return;
        }

        form.dataset.transientRecoverySubmitting = "true";
        const button = submitter instanceof HTMLButtonElement ||
            submitter instanceof HTMLInputElement
            ? submitter
            : form.querySelector("button[type='submit'], input[type='submit']");
        const wasDisabled = button?.disabled === true;
        if (button) {
            button.disabled = true;
        }

        try {
            const formData = new FormData(form);
            if (submitter?.name) {
                formData.append(submitter.name, submitter.value);
            }

            const response = await fetch(form.action, {
                method: "POST",
                credentials: "same-origin",
                body: formData,
                headers: {
                    Accept: "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                }
            });
            const contentType = response.headers.get("content-type") ?? "";
            let result = null;
            if (contentType.toLowerCase().includes("application/json")) {
                result = await response.json();
            }

            if (!response.ok || !result?.success) {
                throw new Error(result?.error || response.statusText || "Request failed.");
            }

            if (typeof window.BadWolfHostGameplay?.refresh === "function") {
                await window.BadWolfHostGameplay.refresh();
            } else {
                window.location.reload();
            }
        } catch (error) {
            console.error("Recovered question selection failed.", error);
            showQuestionSelectionError(
                error instanceof Error ? error.message : "Request failed.");
        } finally {
            delete form.dataset.transientRecoverySubmitting;
            if (button?.isConnected) {
                button.disabled = wasDisabled;
            }
        }
    };

    // The normal Lobby script binds directly to the question forms that exist on
    // the initial render. Host partial navigation can replace the persistent board
    // grid, so newly imported forms do not have that direct listener. This delegated
    // fallback is registered before the generic submit guard: an initial form is
    // already defaultPrevented by the normal handler, while a replaced form reaches
    // this fallback and is submitted once through the same AJAX contract.
    document.addEventListener("submit", event => {
        const form = event.target instanceof HTMLFormElement &&
            event.target.matches(questionSelectionSelector)
            ? event.target
            : null;
        if (!form || event.defaultPrevented) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        void submitReplacedQuestionSelection(form, event.submitter);
    });

    const repairPeerRatedTransientState = () => {
        const hostBoard = document.querySelector(hostBoardSelector);
        if (!(hostBoard instanceof HTMLElement)) {
            return;
        }

        const persistentBoard = hostBoard.querySelector(persistentBoardSelector);
        const boardVisible = persistentBoard instanceof HTMLElement &&
            persistentBoard.hidden === false;
        const peerRatedActive =
            hostBoard.classList.contains("peer-rated-all-player-active") ||
            hostBoard.classList.contains("peer-rated-question-shell-active");

        if (boardVisible &&
            hostBoard.classList.contains("peer-rated-returning-to-board")) {
            hostBoard.classList.remove("peer-rated-returning-to-board");
        }

        if (!boardVisible && !peerRatedActive) {
            return;
        }

        hostBoard.querySelectorAll(playerStateSelector).forEach(card => {
            card.classList.remove(
                "question-answering-player",
                "question-attempted-player");
        });
    };

    let repairFrame = 0;
    const scheduleRepair = () => {
        if (repairFrame !== 0) {
            return;
        }

        const repair = () => {
            repairFrame = 0;
            repairPeerRatedTransientState();
        };
        if (typeof window.requestAnimationFrame === "function") {
            repairFrame = window.requestAnimationFrame(repair);
        } else {
            repair();
        }
    };

    document.addEventListener("badwolf:host-gameplay-updated", scheduleRepair);
    document.addEventListener("badwolf:host-shell-mounted", scheduleRepair);
    window.addEventListener("pageshow", scheduleRepair);

    new MutationObserver(scheduleRepair).observe(document.documentElement, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ["class", "hidden"]
    });

    scheduleRepair();
})();
