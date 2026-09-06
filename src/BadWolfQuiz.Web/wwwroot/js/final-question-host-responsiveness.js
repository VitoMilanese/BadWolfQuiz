(() => {
    if (window.badWolfFinalQuestionHostResponsivenessInitialized) {
        return;
    }

    window.badWolfFinalQuestionHostResponsivenessInitialized = true;

    const viewSelector = "[data-host-gameplay-view]";
    const boardSelector = "[data-host-gameplay-board]";
    const transientSelector = "[data-host-gameplay-transient]";
    const fastFinalHandlers = new Set([
        "StartFinalQuestion",
        "ForceAdvanceToFinalQuestion",
        "SubmitMinimumFinalWager",
        "LockFinalWagers",
        "SubmitEmptyFinalAnswer",
        "LockFinalAnswers"
    ]);
    const feedbackOnlyHandlers = new Set([
        "JudgeFinalAnswer",
        "CompleteFinalQuestion"
    ]);
    const busyDelayMilliseconds = 180;
    const busySafetyMilliseconds = 15000;

    let busyDelayHandle = 0;
    let busySafetyHandle = 0;
    let busyOwned = false;

    const getAction = (form, submitter) => {
        const hasFormAction = submitter?.hasAttribute("formaction") === true;
        return new URL(
            hasFormAction ? submitter.formAction : form.action,
            window.location.href);
    };

    const getHandler = action => action.searchParams.get("handler") ?? "";

    const stopBusy = () => {
        window.clearTimeout(busyDelayHandle);
        window.clearTimeout(busySafetyHandle);
        busyDelayHandle = 0;
        busySafetyHandle = 0;

        if (busyOwned) {
            busyOwned = false;
            window.BadWolfBusy?.hide?.();
        }
    };

    const startBusy = () => {
        stopBusy();
        busyDelayHandle = window.setTimeout(() => {
            busyDelayHandle = 0;
            busyOwned = window.BadWolfBusy?.show?.() === true;
        }, busyDelayMilliseconds);
        busySafetyHandle = window.setTimeout(stopBusy, busySafetyMilliseconds);
    };

    const showError = message => {
        const errorTarget = document.getElementById("game-board-error");
        if (errorTarget) {
            errorTarget.textContent = message;
            errorTarget.hidden = false;
            errorTarget.classList.remove("message-hidden");
            return;
        }

        window.alert(message);
    };

    const copyHostBoardState = (currentView, nextView) => {
        const currentHostBoard = currentView.closest(
            ".host-game-board[data-game-id]");
        const nextHostBoard = nextView.closest(
            ".host-game-board[data-game-id]");
        if (!currentHostBoard || !nextHostBoard) {
            return;
        }

        for (const className of [
            "final-question-host",
            "all-player-question-wagering",
            "anonymous-shared-wager-active"
        ]) {
            currentHostBoard.classList.toggle(
                className,
                nextHostBoard.classList.contains(className));
        }

        if (nextHostBoard.dataset.gameStatus) {
            currentHostBoard.dataset.gameStatus = nextHostBoard.dataset.gameStatus;
        }
    };

    const applyReturnedLobbyMarkup = (markup, responseUrl) => {
        const parsed = new DOMParser().parseFromString(markup, "text/html");
        const nextView = parsed.querySelector(viewSelector);
        const nextBoard = parsed.querySelector(boardSelector);
        const currentView = document.querySelector(viewSelector);
        const currentBoard = document.querySelector(boardSelector);

        if (!nextView || !nextBoard || !currentView || !currentBoard) {
            return false;
        }

        window.BadWolfHostGameplay?.cancelPending?.();
        copyHostBoardState(currentView, nextView);

        currentView.replaceChildren(
            ...Array.from(nextView.childNodes, node =>
                document.importNode(node, true)));
        currentBoard.hidden = nextBoard.hidden;

        const currentTransient = document.querySelector(transientSelector);
        const nextTransient = parsed.querySelector(transientSelector);
        if (currentTransient) {
            currentTransient.replaceChildren(
                ...Array.from(nextTransient?.childNodes ?? [], node =>
                    document.importNode(node, true)));
        }

        const currentRoundHeading = document.querySelector(
            ".game-header-context h2");
        const nextRoundHeading = parsed.querySelector(
            ".game-header-context h2");
        if (currentRoundHeading && nextRoundHeading) {
            currentRoundHeading.textContent = nextRoundHeading.textContent;
            currentRoundHeading.title = nextRoundHeading.title;
        }

        const boardMenu = document.querySelector(".board-action-menu");
        if (boardMenu) {
            boardMenu.hidden =
                nextView.querySelector(".round-summary") !== null ||
                nextView.querySelector(
                    "[data-game-intro-page], [data-final-question-transition]") !== null;
        }

        const targetUrl = new URL(responseUrl, window.location.href);
        if (targetUrl.origin === window.location.origin &&
            targetUrl.pathname === window.location.pathname) {
            history.replaceState({ hostGameplay: true }, "", targetUrl.href);
        }

        document.dispatchEvent(new CustomEvent(
            "badwolf:host-gameplay-updated"));
        return true;
    };

    const submitPreparedFinalAdvance = async (form, submitter, action) => {
        const button = submitter ?? form.querySelector("button[type='submit']");
        button?.setAttribute("disabled", "disabled");
        button?.setAttribute("aria-busy", "true");
        form.closest("dialog")?.close();
        startBusy();

        try {
            const formData = new FormData(form);
            if (submitter?.name) {
                formData.append(submitter.name, submitter.value);
            }

            const response = await fetch(action.href, {
                method: "POST",
                body: formData,
                credentials: "same-origin",
                headers: { Accept: "text/html" }
            });
            if (!response.ok) {
                throw new Error(response.statusText);
            }

            const markup = await response.text();
            const responseUrl = response.url || window.location.href;
            if (!applyReturnedLobbyMarkup(markup, responseUrl)) {
                if (window.BadWolfHostFlowNavigation?.applyMarkup) {
                    await window.BadWolfHostFlowNavigation.applyMarkup(
                        markup,
                        responseUrl);
                } else {
                    window.location.assign(responseUrl);
                    return;
                }
            }
        } catch (error) {
            console.error("Final Question preparation failed.", error);
            showError(error.message || "Final Question preparation failed.");
        } finally {
            if (button?.isConnected) {
                button.removeAttribute("disabled");
                button.removeAttribute("aria-busy");
            }
            stopBusy();
        }
    };

    const submitFastFinalCommand = async (form, submitter, action) => {
        const button = submitter ?? form.querySelector("button[type='submit']");
        button?.setAttribute("disabled", "disabled");
        button?.setAttribute("aria-busy", "true");
        startBusy();

        let gameplayUpdated = false;
        const onGameplayUpdated = () => {
            gameplayUpdated = true;
            stopBusy();
        };
        document.addEventListener(
            "badwolf:host-gameplay-updated",
            onGameplayUpdated);

        try {
            const formData = new FormData(form);
            if (submitter?.name) {
                formData.append(submitter.name, submitter.value);
            }

            const response = await fetch(action.href, {
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

            // Final Question commands broadcast their new state before optional
            // Discord cleanup finishes. Let that SignalR-driven refresh win when
            // it has already arrived; otherwise perform one explicit refresh.
            if (!gameplayUpdated) {
                if (window.BadWolfHostGameplay?.refresh) {
                    await window.BadWolfHostGameplay.refresh();
                } else {
                    window.location.reload();
                    return;
                }
            }
        } catch (error) {
            console.error("Final Question command failed.", error);
            showError(error.message || "Final Question command failed.");
        } finally {
            document.removeEventListener(
                "badwolf:host-gameplay-updated",
                onGameplayUpdated);
            if (button?.isConnected) {
                button.removeAttribute("disabled");
                button.removeAttribute("aria-busy");
            }
            stopBusy();
        }
    };

    document.addEventListener("badwolf:host-gameplay-updated", stopBusy);

    document.addEventListener("submit", event => {
        const form = event.target instanceof HTMLFormElement
            ? event.target
            : null;
        if (!form || event.defaultPrevented) {
            return;
        }

        const submitter = event.submitter instanceof HTMLElement
            ? event.submitter
            : null;
        const action = getAction(form, submitter);
        if (action.origin !== window.location.origin) {
            return;
        }

        const handler = getHandler(action);
        if (handler === "PrepareFinalQuestionLeaderboard") {
            event.preventDefault();
            event.stopImmediatePropagation();
            void submitPreparedFinalAdvance(form, submitter, action);
            return;
        }

        if (fastFinalHandlers.has(handler)) {
            // The transition page intentionally requires a real host click.
            // Leave its old programmatic three-second requestSubmit blocked by
            // the existing navigation guard rather than treating it as consent.
            if (form.matches("[data-final-question-transition-form]") &&
                submitter === null) {
                return;
            }

            event.preventDefault();
            event.stopImmediatePropagation();
            void submitFastFinalCommand(form, submitter, action);
            return;
        }

        if (feedbackOnlyHandlers.has(handler)) {
            // Judging keeps the existing answer-feedback sound path. Give the
            // host immediate busy feedback while that established handler runs.
            startBusy();
        }
    }, true);
})();
