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
