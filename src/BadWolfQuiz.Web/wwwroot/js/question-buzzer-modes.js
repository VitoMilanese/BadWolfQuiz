(() => {
    "use strict";

    const script = document.currentScript ??
        document.querySelector('script[src*="question-buzzer-modes.js"]');

    const initializeEditor = () => {
        const form = document.querySelector("[data-ajax-question-editor]");
        const modeSetting = document.getElementById("buzz-mode-setting");
        const modeSelect = document.getElementById("Input_BuzzModeOverride");
        const presentationType = document.getElementById("Input_PresentationType");
        const wager = document.getElementById("Input_IsSpecial");
        if (!form || !modeSetting ||
            !(modeSelect instanceof HTMLSelectElement) ||
            !(presentationType instanceof HTMLSelectElement)) {
            return;
        }

        let delaySetting = document.getElementById("buzz-delay-setting");
        let delayInput = document.getElementById("Input_BuzzDelaySeconds");
        if (!delaySetting) {
            delaySetting = document.createElement("div");
            delaySetting.id = "buzz-delay-setting";

            const label = document.createElement("label");
            label.htmlFor = "Input_BuzzDelaySeconds";
            label.textContent = script?.dataset.buzzDelayLabel || "Buzzer delay";

            delayInput = document.createElement("input");
            delayInput.id = "Input_BuzzDelaySeconds";
            delayInput.name = "Input.BuzzDelaySeconds";
            delayInput.type = "number";
            delayInput.min = "0";
            delayInput.step = "1";
            delayInput.value = script?.dataset.savedBuzzDelay || "0";

            delaySetting.append(label, delayInput);
            modeSetting.after(delaySetting);
        }

        const sync = () => {
            const isAllPlayer = presentationType.value === "2" ||
                presentationType.value === "3";
            const isWager = !isAllPlayer && (wager?.checked ?? false);
            const usesBuzzer = !isAllPlayer && !isWager;

            modeSetting.hidden = !usesBuzzer;
            modeSelect.disabled = !usesBuzzer;

            const showDelay = usesBuzzer && modeSelect.value === "4";
            delaySetting.hidden = !showDelay;
            if (delayInput instanceof HTMLInputElement) {
                delayInput.disabled = !showDelay;
            }
        };

        modeSelect.addEventListener("change", sync);
        presentationType.addEventListener("change", sync);
        wager?.addEventListener("change", sync);
        new MutationObserver(sync).observe(presentationType, {
            childList: true
        });
        window.setTimeout(sync, 0);
    };

    const initializeLobby = () => {
        const board = document.querySelector(
            ".host-game-board[data-game-id]");
        const view = document.querySelector("[data-host-gameplay-view]");
        if (!(board instanceof HTMLElement) || !view) {
            return;
        }

        const gameId = board.dataset.gameId;
        if (!gameId) {
            return;
        }

        const endpoint = `/Admin/Games/BuzzerActivation/${encodeURIComponent(gameId)}`;
        let policy = null;
        let policySourceQuestionId = null;
        let delayHandle = null;
        let syncHandle = null;

        const clearDelay = () => {
            if (delayHandle !== null) {
                window.clearTimeout(delayHandle);
                delayHandle = null;
            }
        };

        const currentSourceQuestionId = () => {
            const input = view.querySelector('input[name="sourceQuestionId"]');
            if (!(input instanceof HTMLInputElement)) {
                return null;
            }
            const value = Number.parseInt(input.value, 10);
            return Number.isFinite(value) ? value : null;
        };

        const setManualControlVisibility = currentPolicy => {
            view.querySelectorAll(
                'form[action*="handler=ActivateBuzzer"]')
                .forEach(form => {
                    form.hidden = currentPolicy?.mode !== "manual";
                });
        };

        const fetchPolicy = async sourceQuestionId => {
            const url = new URL(endpoint, window.location.origin);
            url.searchParams.set("handler", "Policy");
            url.searchParams.set("sourceQuestionId", sourceQuestionId);
            const response = await fetch(url, {
                headers: { "Accept": "application/json" },
                cache: "no-store"
            });
            if (!response.ok) {
                return null;
            }
            return response.json();
        };

        const activate = async trigger => {
            if (!policy?.active || policy.buzzerStatus !== "inactive") {
                return;
            }

            const token = document.querySelector(
                'input[name="__RequestVerificationToken"]');
            const body = new FormData();
            body.append("sourceQuestionId", String(policy.sourceQuestionId));
            body.append("trigger", trigger);
            if (token instanceof HTMLInputElement) {
                body.append("__RequestVerificationToken", token.value);
            }

            const url = new URL(endpoint, window.location.origin);
            url.searchParams.set("handler", "Activate");
            const response = await fetch(url, {
                method: "POST",
                body,
                headers: {
                    "Accept": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                }
            });
            if (!response.ok) {
                return;
            }

            const result = await response.json();
            if (result.success) {
                policy = { ...policy, buzzerStatus: "open" };
                clearDelay();
                setManualControlVisibility(policy);
                window.BadWolfHostGameplay?.refresh?.().catch?.(console.error);
            }
        };

        const applyPolicy = currentPolicy => {
            clearDelay();
            policy = currentPolicy;
            setManualControlVisibility(currentPolicy);

            if (!currentPolicy?.active ||
                currentPolicy.buzzerStatus !== "inactive") {
                return;
            }

            if (currentPolicy.mode === "afterdelay" &&
                currentPolicy.delayMilliseconds > 0) {
                delayHandle = window.setTimeout(
                    () => activate("delay").catch(console.error),
                    currentPolicy.delayMilliseconds);
            }
        };

        const sync = async () => {
            const sourceQuestionId = currentSourceQuestionId();
            if (sourceQuestionId === null) {
                policySourceQuestionId = null;
                policy = null;
                clearDelay();
                return;
            }

            if (sourceQuestionId === policySourceQuestionId && policy) {
                setManualControlVisibility(policy);
                return;
            }

            policySourceQuestionId = sourceQuestionId;
            try {
                applyPolicy(await fetchPolicy(sourceQuestionId));
            } catch (error) {
                console.error(error);
            }
        };

        const scheduleSync = () => {
            window.clearTimeout(syncHandle);
            syncHandle = window.setTimeout(() => {
                sync().catch(console.error);
            }, 40);
        };

        const gateMedia = () => {
            const presentation = view.querySelector(".question-presentation");
            if (!presentation) {
                return null;
            }
            return [...presentation.querySelectorAll(
                "audio.game-content-audio, " +
                "video.game-content-video, " +
                "iframe.youtube-auto-expand, " +
                "[data-youtube-placeholder]")]
                .find(element =>
                    !element.closest(".question-clue-hidden")) ?? null;
        };

        const completeMedia = event => {
            if (policy?.mode !== "aftermedia" ||
                policy.buzzerStatus !== "inactive" ||
                event.target !== gateMedia()) {
                return;
            }
            activate("media").catch(console.error);
        };

        document.addEventListener("ended", completeMedia, true);
        document.addEventListener("error", completeMedia, true);
        document.addEventListener("badwolf:youtube-ended", completeMedia);
        document.addEventListener("badwolf:youtube-error", completeMedia);

        new MutationObserver(scheduleSync).observe(view, {
            childList: true,
            subtree: true
        });
        scheduleSync();
    };

    window.setTimeout(() => {
        initializeEditor();
        initializeLobby();
    }, 0);
})();
