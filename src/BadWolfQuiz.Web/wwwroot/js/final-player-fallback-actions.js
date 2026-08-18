(() => {
    if (window.badWolfFinalPlayerFallbackActionsInitialized) {
        return;
    }

    window.badWolfFinalPlayerFallbackActionsInitialized = true;

    const handlers = new Map([
        ["SubmitMinimumFinalWager", "wager"],
        ["SubmitEmptyFinalAnswer", "answer"]
    ]);
    const lockHandlers = {
        wager: "LockFinalWagers",
        answer: "LockFinalAnswers"
    };
    let fallbackQueue = Promise.resolve();

    const getFallbackKind = form => {
        if (!(form instanceof HTMLFormElement)) {
            return null;
        }

        const action = new URL(form.action, window.location.href);
        return handlers.get(action.searchParams.get("handler")) ?? null;
    };

    const showError = message => {
        let target = document.getElementById("game-board-error");
        if (!target) {
            target = document.createElement("div");
            target.id = "game-board-error";
            target.className = "message error";
            document.querySelector(".host-game-board")?.prepend(target);
        }
        if (!target) {
            console.error(message);
            return;
        }

        target.textContent = message;
        target.hidden = false;
        target.classList.remove("message-hidden");
    };

    const applySuccess = (form, kind, result) => {
        const row = form.closest("li");
        const status = row?.querySelector(":scope > strong + span");
        if (status && result.submittedLabel) {
            status.textContent = result.submittedLabel;
        }

        form.remove();

        if (!result.allSubmitted) {
            return;
        }

        const lockHandler = lockHandlers[kind];
        const lockButton = document.querySelector(
            `form[action*="handler=${lockHandler}"] button[type="submit"]`);
        lockButton?.removeAttribute("disabled");
    };

    const submitFallback = async (form, button, kind) => {
        const gameId = document.querySelector(
            ".host-game-board[data-game-id]")?.dataset.gameId;
        if (!gameId) {
            delete form.dataset.finalFallbackSubmitting;
            button.disabled = false;
            button.removeAttribute("aria-busy");
            return;
        }

        try {
            const formData = new FormData(form);
            formData.set("gameId", gameId);
            formData.set("kind", kind);

            const response = await fetch("/Admin/Games/FinalFallback", {
                method: "POST",
                body: formData,
                credentials: "same-origin",
                headers: {
                    Accept: "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                }
            });
            const contentType = response.headers.get("content-type") ?? "";
            const result = contentType.includes("application/json")
                ? await response.json()
                : null;

            if (!response.ok || !result?.success) {
                throw new Error(result?.error ?? response.statusText);
            }

            applySuccess(form, kind, result);
        } catch (error) {
            delete form.dataset.finalFallbackSubmitting;
            button.disabled = false;
            button.removeAttribute("aria-busy");
            showError(error instanceof Error ? error.message : String(error));
        }
    };

    const enqueueFallback = (form, button, kind) => {
        if (form.dataset.finalFallbackSubmitting === "true") {
            return;
        }

        form.dataset.finalFallbackSubmitting = "true";
        button.disabled = true;
        button.setAttribute("aria-busy", "true");

        fallbackQueue = fallbackQueue.then(() =>
            submitFallback(form, button, kind));
    };

    window.addEventListener("click", event => {
        const button = event.target instanceof Element
            ? event.target.closest("button[type='submit']")
            : null;
        const form = button?.form;
        const kind = getFallbackKind(form);
        if (!(button instanceof HTMLButtonElement) ||
            !(form instanceof HTMLFormElement) ||
            !kind ||
            button.disabled) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        enqueueFallback(form, button, kind);
    }, true);
})();
