(() => {
    const body = document.body;
    if (!body) return;

    const validFrameIds = new Set(["gold-fang", "moonlight", "ember"]);
    const normalizeFrameId = value => validFrameIds.has(value) ? value : "gold-fang";
    const setFrame = (element, enabled, frameId) => {
        if (!element) return;
        if (enabled) {
            element.dataset.avatarFrame = normalizeFrameId(frameId);
        } else {
            delete element.dataset.avatarFrame;
        }
    };

    const hostTemplate = document.getElementById("contributor-host-frame-template");
    if (hostTemplate && body.dataset.contributorHost === "true") {
        const hostSettingsGrid = document.querySelector("form.form-card .settings-grid");
        if (hostSettingsGrid && !hostSettingsGrid.querySelector("[data-contributor-host-frame]")) {
            hostSettingsGrid.append(hostTemplate.content.cloneNode(true));
        }
    }

    const hostFrameState = {
        enabled: body.dataset.contributorHostFrameEnabled === "true",
        frameId: normalizeFrameId(body.dataset.contributorHostFrameId)
    };

    const applyHostFrame = () => {
        for (const hostCard of document.querySelectorAll(".scoreboard-player.host-card")) {
            setFrame(hostCard, hostFrameState.enabled, hostFrameState.frameId);
        }
    };
    applyHostFrame();

    const playerFrameMap = new Map();
    const applyPlayerFrames = root => {
        const scope = root instanceof Element ? root : document;
        for (const element of scope.querySelectorAll?.("[data-player-id]") ?? []) {
            const state = playerFrameMap.get(element.dataset.playerId);
            setFrame(element, state?.enabled === true, state?.frameId);
        }
        if (root instanceof Element && root.matches("[data-player-id]")) {
            const state = playerFrameMap.get(root.dataset.playerId);
            setFrame(root, state?.enabled === true, state?.frameId);
        }
    };

    const hostGameRoot = document.querySelector(
        ".host-game-board[data-game-code], .content-panel[data-game-code]"
    );
    if (hostGameRoot && typeof signalR !== "undefined") {
        const gameCode = hostGameRoot.dataset.gameCode;
        const observer = new MutationObserver(records => {
            for (const record of records) {
                for (const node of record.addedNodes) {
                    if (node instanceof Element) applyPlayerFrames(node);
                }
            }
            applyHostFrame();
        });
        observer.observe(document.body, { childList: true, subtree: true });

        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/game")
            .withAutomaticReconnect()
            .build();

        const refreshPlayerFrames = async () => {
            const response = await fetch(
                `/ContributorFrames?code=${encodeURIComponent(gameCode)}`,
                { headers: { Accept: "application/json" } }
            );
            if (!response.ok) return;
            const update = await response.json();
            playerFrameMap.clear();
            for (const player of update?.players ?? []) {
                playerFrameMap.set(String(player.id), {
                    enabled: player.enabled === true,
                    frameId: normalizeFrameId(player.frameId)
                });
            }
            applyPlayerFrames(document);
        };

        connection.on("PlayersChanged", () => {
            refreshPlayerFrames().catch(console.error);
        });

        connection.on("HostContributorFrameChanged", update => {
            hostFrameState.enabled = update?.enabled === true;
            hostFrameState.frameId = normalizeFrameId(update?.frameId);
            applyHostFrame();
        });

        const join = async () => {
            await connection.invoke("JoinSession", gameCode);
            await refreshPlayerFrames();
        };
        connection.onreconnected(() => join().catch(console.error));
        connection.start().then(join).catch(console.error);
    }

    const playerTemplate = document.getElementById("contributor-player-frame-template");
    const playerLobby = document.querySelector(".player-lobby");
    if (!playerTemplate || !playerLobby || body.dataset.contributorPlayer !== "true") {
        return;
    }

    const mediaSettings = document.querySelector(
        ".player-media-settings:not(.player-menu-settings) .player-media-settings-content"
    );
    const avatarControl = document.querySelector(".player-avatar-control");
    if (!mediaSettings || !avatarControl) return;

    mediaSettings.append(playerTemplate.content.cloneNode(true));
    const framePanel = mediaSettings.querySelector("[data-contributor-player-frame]");
    const enabledInput = framePanel?.querySelector("[data-contributor-frame-enabled]");
    const frameSelect = framePanel?.querySelector("[data-contributor-frame-id]");
    const status = framePanel?.querySelector("[data-contributor-frame-status]");
    const antiforgery = framePanel?.querySelector("[data-contributor-antiforgery]")?.value;
    if (!enabledInput || !frameSelect) return;

    const normalizedPlayerName = playerLobby.dataset.playerName.trim().toLocaleLowerCase();
    const enabledKey = `badwolfquiz:contributor-frame-enabled:${normalizedPlayerName}`;
    const frameKey = `badwolfquiz:contributor-frame:${normalizedPlayerName}`;
    const storedEnabled = localStorage.getItem(enabledKey);
    let frameEnabled = storedEnabled === null
        ? body.dataset.contributorPlayerFrameEnabled === "true"
        : storedEnabled === "true";
    let frameId = normalizeFrameId(
        localStorage.getItem(frameKey) ||
        body.dataset.contributorPlayerFrameId ||
        frameSelect.value
    );

    const applyPlayerFrame = () => {
        enabledInput.checked = frameEnabled;
        frameSelect.value = frameId;
        setFrame(avatarControl, frameEnabled, frameId);
    };

    const savePreference = () => {
        localStorage.setItem(enabledKey, frameEnabled.toString());
        localStorage.setItem(frameKey, frameId);
        applyPlayerFrame();
    };

    const syncFrame = async () => {
        const gameCode = playerLobby.dataset.gameCode;
        const playerId = playerLobby.dataset.playerId;
        const accessToken = localStorage.getItem(`badwolfquiz:${gameCode}:player:${playerId}`) ||
            playerLobby.dataset.accessToken;
        if (!accessToken) return false;

        const form = new FormData();
        form.append("accessToken", accessToken);
        form.append("enabled", frameEnabled.toString());
        form.append("frameId", frameId);
        if (antiforgery) form.append("__RequestVerificationToken", antiforgery);

        const response = await fetch(`${window.location.pathname}?handler=AvatarFrame`, {
            method: "POST",
            body: form,
            headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        if (!response.ok) {
            if (status) status.textContent = body.dataset.contributorFrameSaveFailed || "";
            return false;
        }

        if (status) status.textContent = "";
        return true;
    };

    enabledInput.addEventListener("change", () => {
        frameEnabled = enabledInput.checked;
        savePreference();
        syncFrame().catch(console.error);
    });
    frameSelect.addEventListener("change", () => {
        frameId = normalizeFrameId(frameSelect.value);
        savePreference();
        syncFrame().catch(console.error);
    });

    savePreference();
    window.setTimeout(() => {
        syncFrame().then(saved => {
            if (!saved) {
                window.setTimeout(() => syncFrame().catch(console.error), 1500);
            }
        }).catch(console.error);
    }, 750);
})();
