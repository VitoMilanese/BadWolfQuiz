(() => {
    if (window.badWolfGameplayPolishInitialized) {
        return;
    }

    window.badWolfGameplayPolishInitialized = true;

    const introStartFormSelector = "form[data-game-intro-start]";
    let introBusyDelayHandle = 0;
    let introBusySafetyHandle = 0;
    let introBusyOwned = false;

    const clearIntroBusy = () => {
        window.clearTimeout(introBusyDelayHandle);
        window.clearTimeout(introBusySafetyHandle);
        introBusyDelayHandle = 0;
        introBusySafetyHandle = 0;

        if (introBusyOwned) {
            introBusyOwned = false;
            window.BadWolfBusy?.hide?.();
        }
    };

    const beginIntroStart = page => {
        if (!(page instanceof HTMLElement)) {
            return;
        }

        page.classList.add("is-starting-game");
        clearIntroBusy();
        introBusyDelayHandle = window.setTimeout(() => {
            introBusyDelayHandle = 0;
            introBusyOwned = window.BadWolfBusy?.show?.() === true;
        }, 180);
        introBusySafetyHandle = window.setTimeout(() => {
            page.classList.remove("is-starting-game");
            clearIntroBusy();
        }, 15000);
    };

    document.addEventListener("submit", event => {
        const form = event.target instanceof HTMLFormElement
            ? event.target
            : null;
        if (!form?.matches(introStartFormSelector)) {
            return;
        }

        beginIntroStart(form.closest("[data-game-intro-page]"));
    }, true);

    document.addEventListener("badwolf:host-shell-mounted", clearIntroBusy);

    /* The running-game join panel can be inserted after first-round intro soft
       navigation. quick-timer-controls.js may already have completed its one-time
       setup before that panel existed, so use delegation and read the current
       panel/game code at click time instead of capturing stale DOM. */
    const joinCodeTargetSelector =
        "[data-join-code-panel] .join-code-floating-content .join-code-value";
    const joinQrTargetSelector =
        "[data-join-code-panel] .join-code-floating-content .join-qr-code";
    const joinCopyTargetSelector =
        `${joinCodeTargetSelector}, ${joinQrTargetSelector}`;

    const copyText = async value => {
        if (navigator.clipboard?.writeText) {
            try {
                await navigator.clipboard.writeText(value);
                return;
            } catch {
                // In local HTTP testing Clipboard API can be unavailable.
            }
        }

        const textarea = document.createElement("textarea");
        textarea.value = value;
        textarea.setAttribute("readonly", "readonly");
        textarea.style.position = "fixed";
        textarea.style.opacity = "0";
        document.body.appendChild(textarea);
        textarea.select();
        document.execCommand("copy");
        textarea.remove();
    };

    const showJoinCopyFeedback = target => {
        try {
            target.animate([
                { transform: "scale(1)", filter: "brightness(1)" },
                { transform: "scale(0.97)", filter: "brightness(1.08)", offset: 0.28 },
                { transform: "scale(1.035)", filter: "brightness(1.32)", offset: 0.64 },
                { transform: "scale(1)", filter: "brightness(1)" }
            ], {
                duration: 320,
                easing: "ease-out"
            });
        } catch {
            // Visual feedback is optional; copying must still succeed.
        }

        const bounds = target.getBoundingClientRect();
        const feedback = document.createElement("span");
        feedback.textContent = "✓";
        feedback.setAttribute("aria-hidden", "true");
        Object.assign(feedback.style, {
            position: "fixed",
            zIndex: "1600",
            left: `${bounds.right - Math.min(26, bounds.width * 0.12)}px`,
            top: `${bounds.top + Math.min(26, bounds.height * 0.12)}px`,
            display: "grid",
            placeItems: "center",
            width: "26px",
            height: "26px",
            borderRadius: "999px",
            color: "#fff",
            background: "#238636",
            boxShadow: "0 6px 18px rgb(0 0 0 / 35%)",
            fontSize: "16px",
            fontWeight: "900",
            lineHeight: "1",
            pointerEvents: "none",
            transform: "translate(-50%, -50%)"
        });
        document.body.appendChild(feedback);
        window.setTimeout(() => feedback.remove(), 620);
    };

    const prepareJoinCopyTargets = () => {
        document.querySelectorAll(joinCopyTargetSelector).forEach(target => {
            if (!(target instanceof HTMLElement)) {
                return;
            }

            target.tabIndex = 0;
            target.setAttribute("role", "button");
            target.style.cursor = "copy";
            target.draggable = false;
        });
    };

    const copyJoinTarget = async target => {
        const panel = target.closest("[data-join-code-panel]");
        const gameCode = panel?.dataset.gameCode?.trim() ?? "";
        if (!gameCode) {
            return;
        }

        const value = target.matches(joinQrTargetSelector)
            ? new URL(
                `/Join/${encodeURIComponent(gameCode)}/`,
                window.location.origin).href
            : gameCode;

        await copyText(value);
        showJoinCopyFeedback(target);
    };

    const resolveJoinCopyTarget = value =>
        value instanceof Element
            ? value.closest(joinCopyTargetSelector)
            : null;

    document.addEventListener("click", event => {
        const target = resolveJoinCopyTarget(event.target);
        if (!(target instanceof HTMLElement)) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        void copyJoinTarget(target).catch(error =>
            console.error("Unable to copy join information.", error));
    }, true);

    document.addEventListener("keydown", event => {
        if (event.key !== "Enter" && event.key !== " ") {
            return;
        }

        const target = resolveJoinCopyTarget(event.target);
        if (!(target instanceof HTMLElement)) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        void copyJoinTarget(target).catch(error =>
            console.error("Unable to copy join information.", error));
    }, true);

    document.addEventListener("badwolf:host-shell-mounted", prepareJoinCopyTargets);
    document.addEventListener("badwolf:host-gameplay-updated", prepareJoinCopyTargets);
    prepareJoinCopyTargets();

    const playerLobby = document.querySelector(
        ".player-lobby[data-player-id][data-final-status]");
    if (!(playerLobby instanceof HTMLElement) ||
        playerLobby.dataset.finalStatus !== "lobby") {
        return;
    }

    const playerBuzzer = document.getElementById("player-buzzer");
    const playerTimer = document.getElementById("game-timer");

    const markPlayerRuntimeStarted = () => {
        if (playerLobby.dataset.finalStatus !== "lobby") {
            return true;
        }

        const runtimeStarted =
            playerLobby.classList.contains("is-page-buzzer-active") ||
            playerBuzzer?.classList.contains("player-buzzer-open") === true ||
            (playerTimer instanceof HTMLElement && !playerTimer.hidden);
        if (!runtimeStarted) {
            return false;
        }

        // The Player Lobby is intentionally long-lived during regular gameplay.
        // Its server-rendered data-final-status can therefore still say "lobby"
        // after the host starts the game. Once a runtime signal is visible, move
        // the page out of the pre-game waiting-room CSS contract permanently.
        playerLobby.dataset.finalStatus = "running";
        playerLobby.classList.add("player-runtime-layout");
        return true;
    };

    const observer = new MutationObserver(() => {
        if (markPlayerRuntimeStarted()) {
            observer.disconnect();
        }
    });

    observer.observe(playerLobby, {
        attributes: true,
        attributeFilter: ["class"]
    });
    if (playerBuzzer instanceof HTMLElement) {
        observer.observe(playerBuzzer, {
            attributes: true,
            attributeFilter: ["class", "disabled"]
        });
    }
    if (playerTimer instanceof HTMLElement) {
        observer.observe(playerTimer, {
            attributes: true,
            attributeFilter: ["hidden"]
        });
    }

    if (markPlayerRuntimeStarted()) {
        observer.disconnect();
    }
})();
