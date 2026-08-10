(() => {
    const form = document.querySelector(".player-join-lock-form");
    const trigger = form?.querySelector(".player-join-lock");

    if (!form || !trigger) {
        return;
    }

    const hostBoard = form.closest(".host-game-board");
    const gameId = hostBoard?.dataset.gameId;
    if (!gameId) {
        return;
    }

    const menu = document.createElement("div");
    menu.className = "player-admission-menu";

    const popover = document.createElement("div");
    popover.className = "player-admission-menu-popover";
    popover.hidden = true;
    popover.setAttribute("role", "menu");

    const waitingAction = document.createElement("button");
    waitingAction.type = "button";
    waitingAction.className = "button button-secondary player-admission-menu-action";
    waitingAction.dataset.playerAdmissionAcceptAll = "";
    waitingAction.hidden = true;
    waitingAction.setAttribute("role", "menuitem");

    const autoAction = document.createElement("button");
    autoAction.type = "button";
    autoAction.className = "button button-secondary player-admission-menu-action";
    autoAction.dataset.playerAdmissionAuto = "";
    autoAction.setAttribute("role", "menuitem");

    const autoActionLabel = document.createElement("span");
    const autoActionState = document.createElement("span");
    autoActionState.className = "player-admission-menu-action-state";
    autoAction.append(autoActionLabel, autoActionState);

    const joinAction = document.createElement("button");
    joinAction.type = "button";
    joinAction.className = "button button-secondary player-admission-menu-action";
    joinAction.dataset.playerAdmissionJoining = "";
    joinAction.setAttribute("role", "menuitem");

    popover.append(waitingAction, autoAction, joinAction);
    form.parentNode.insertBefore(menu, form);
    menu.append(form, popover);

    trigger.type = "button";
    trigger.setAttribute("aria-haspopup", "menu");
    trigger.setAttribute("aria-expanded", "false");

    const endpoint = `/Admin/Games/PlayerAdmission?id=${encodeURIComponent(gameId)}`;
    const verificationToken = form.querySelector('input[name="__RequestVerificationToken"]')?.value;
    let refreshTimer = null;

    function closeMenu() {
        popover.hidden = true;
        trigger.setAttribute("aria-expanded", "false");
        if (refreshTimer !== null) {
            window.clearInterval(refreshTimer);
            refreshTimer = null;
        }
    }

    async function readState() {
        const response = await fetch(endpoint, {
            headers: { Accept: "application/json" },
            credentials: "same-origin"
        });

        if (!response.ok) {
            throw new Error(`Player admission state request failed with ${response.status}.`);
        }

        const state = await response.json();
        waitingAction.hidden = state.waitingCount === 0;
        waitingAction.textContent = state.labels.acceptAllWaiting;
        autoActionLabel.textContent = `${state.labels.automaticAcceptance}:`;
        autoActionState.textContent = state.automaticallyAcceptNewPlayers
            ? state.labels.enabled
            : state.labels.disabled;
        joinAction.textContent = state.allowsNewPlayers
            ? state.labels.denyNewConnections
            : state.labels.allowNewConnections;

        trigger.classList.toggle("is-open", state.allowsNewPlayers);
        trigger.classList.toggle("is-closed", !state.allowsNewPlayers);
        trigger.querySelector("span").textContent = state.allowsNewPlayers ? "🔓" : "🔒";
        trigger.title = state.allowsNewPlayers
            ? state.labels.denyNewConnections
            : state.labels.allowNewConnections;
        trigger.setAttribute("aria-label", trigger.title);
    }

    async function post(url) {
        const body = new URLSearchParams();
        if (verificationToken) {
            body.set("__RequestVerificationToken", verificationToken);
        }

        const response = await fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
                "X-Requested-With": "XMLHttpRequest"
            },
            body
        });

        if (!response.ok) {
            throw new Error(`Player admission action failed with ${response.status}.`);
        }
    }

    async function runAction(action) {
        try {
            await action();
            await readState();
            closeMenu();
        } catch (error) {
            console.error(error);
        }
    }

    trigger.addEventListener("click", async event => {
        event.preventDefault();

        if (!popover.hidden) {
            closeMenu();
            return;
        }

        try {
            await readState();
            popover.hidden = false;
            trigger.setAttribute("aria-expanded", "true");
            refreshTimer = window.setInterval(() => {
                readState().catch(console.error);
            }, 1500);
        } catch (error) {
            console.error(error);
        }
    });

    waitingAction.addEventListener("click", () => runAction(() =>
        post(`/Admin/Games/PlayerAdmission?id=${encodeURIComponent(gameId)}&handler=AcceptAllWaiting`)));

    autoAction.addEventListener("click", event => {
        event.preventDefault();
        event.stopPropagation();
        runAction(() =>
            post(`/Admin/Games/PlayerAdmission?id=${encodeURIComponent(gameId)}&handler=ToggleAutomaticAcceptance`));
    });

    joinAction.addEventListener("click", () => runAction(async () => {
        const action = form.getAttribute("action");
        if (!action) {
            return;
        }

        await post(action);
    }));

    document.addEventListener("click", event => {
        if (!menu.contains(event.target)) {
            closeMenu();
        }
    });

    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && !popover.hidden) {
            closeMenu();
            trigger.focus();
        }
    });
})();
