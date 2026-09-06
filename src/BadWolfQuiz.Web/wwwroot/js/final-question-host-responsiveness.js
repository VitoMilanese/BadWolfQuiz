(() => {
    if (window.badWolfFinalQuestionHostResponsivenessInitialized) {
        return;
    }

    window.badWolfFinalQuestionHostResponsivenessInitialized = true;

    const transitionCommitHandlers = new Set([
        "StartFinalQuestion",
        "ForceAdvanceToFinalQuestion"
    ]);
    const fastFinalHandlers = new Set([
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
            // Keep the established round-summary navigation path. Replacing the
            // returned leaderboard markup here made the podium animation replay
            // and raced the host navigation handler. Busy feedback is enough for
            // this transition until it can be optimized inside the shared router.
            startBusy();
            return;
        }

        if (transitionCommitHandlers.has(handler)) {
            // A round-summary Start/Force form is navigation to the dedicated
            // Final Question transition page. Do not post it directly or the
            // server enters FinalWagering while the host is still showing the
            // round summary.
            if (!form.matches("[data-final-question-transition-form]")) {
                return;
            }

            // The transition page intentionally requires a real host click. The
            // established transition guard blocks the old programmatic submit.
            if (submitter === null) {
                return;
            }

            // Keep the proven browser navigation path for the actual transition
            // commit. The Final Question refresh guard intentionally blocks host
            // gameplay refreshes while this screen is mounted, so handling this
            // POST through the generic AJAX fast path can advance players to
            // FinalWagering while leaving the host stuck on the transition page.
            // Busy feedback still makes the unavoidable wait visible.
            startBusy();
            return;
        }

        if (fastFinalHandlers.has(handler)) {
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