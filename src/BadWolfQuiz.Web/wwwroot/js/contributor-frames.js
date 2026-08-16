(() => {
    const body = document.body;
    if (!body) return;

    const validFrameIds = new Set(
        Array.from({ length: 24 }, (_, index) => String(index + 1))
    );
    const normalizeFrameId = value => {
        const id = String(value ?? "").trim();
        return validFrameIds.has(id) ? id : "1";
    };
    const frameUrl = frameId => `/frames/${normalizeFrameId(frameId)}.png`;

    const findFrameMedia = owner => {
        const selectors = [
            ".player-avatar-current:not([hidden])",
            ".player-card-avatar:not([hidden])",
            ".player-list-avatar:not([hidden])",
            ".host-card-media:not([hidden])"
        ];
        for (const selector of selectors) {
            const element = owner.querySelector(selector);
            if (element) return element;
        }
        return null;
    };

    const positionFrameOverlay = owner => {
        const overlay = owner.querySelector(":scope > .contributor-avatar-frame-overlay");
        if (!overlay) return;
        const media = findFrameMedia(owner);
        if (!media) {
            overlay.hidden = true;
            return;
        }

        const ownerRect = owner.getBoundingClientRect();
        const mediaRect = media.getBoundingClientRect();
        if (mediaRect.width <= 0 || mediaRect.height <= 0) {
            overlay.hidden = true;
            return;
        }

        overlay.hidden = false;
        overlay.style.left = `${mediaRect.left - ownerRect.left}px`;
        overlay.style.top = `${mediaRect.top - ownerRect.top}px`;
        overlay.style.width = `${mediaRect.width}px`;
        overlay.style.height = `${mediaRect.height}px`;
    };

    const removeFrame = owner => {
        if (!owner) return;
        delete owner.dataset.avatarFrame;
        owner.querySelector(":scope > .contributor-avatar-frame-overlay")?.remove();
        owner.classList.remove("contributor-frame-owner");
    };

    const setFrame = (owner, enabled, frameId) => {
        if (!owner) return;
        if (!enabled) {
            removeFrame(owner);
            return;
        }

        const normalizedId = normalizeFrameId(frameId);
        owner.dataset.avatarFrame = normalizedId;
        owner.classList.add("contributor-frame-owner");

        let overlay = owner.querySelector(":scope > .contributor-avatar-frame-overlay");
        if (!overlay) {
            overlay = document.createElement("img");
            overlay.className = "contributor-avatar-frame-overlay";
            overlay.alt = "";
            overlay.setAttribute("aria-hidden", "true");
            owner.append(overlay);
        }
        const url = frameUrl(normalizedId);
        if (overlay.getAttribute("src") !== url) {
            overlay.src = url;
        }
        window.requestAnimationFrame(() => positionFrameOverlay(owner));
    };

    const repositionAllFrames = () => {
        for (const owner of document.querySelectorAll(".contributor-frame-owner")) {
            positionFrameOverlay(owner);
        }
    };
    window.addEventListener("resize", repositionAllFrames);
    document.addEventListener("load", event => {
        if (event.target instanceof Element &&
            event.target.matches(".player-avatar-current, .player-card-avatar, .player-list-avatar, .host-card-media")) {
            repositionAllFrames();
        }
    }, true);

    const framePicker = document.querySelector("[data-contributor-frame-picker]");
    let activeFramePanel = null;
    if (framePicker) {
        const refreshSelectedOption = () => {
            const selectedId = normalizeFrameId(
                activeFramePanel?.querySelector("[data-contributor-frame-id]")?.value
            );
            for (const option of framePicker.querySelectorAll("[data-contributor-frame-option]")) {
                option.classList.toggle(
                    "is-selected",
                    option.dataset.contributorFrameOption === selectedId
                );
            }
        };

        document.addEventListener("click", event => {
            const opener = event.target.closest?.("[data-open-contributor-frame-picker]");
            if (!opener) return;
            activeFramePanel = opener.closest(
                "[data-contributor-host-frame], [data-contributor-player-frame]"
            );
            if (!activeFramePanel) return;
            refreshSelectedOption();
            framePicker.showModal();
        });

        framePicker.querySelector("[data-close-contributor-frame-picker]")
            ?.addEventListener("click", () => framePicker.close());
        framePicker.addEventListener("click", event => {
            if (event.target === framePicker) framePicker.close();
        });

        for (const option of framePicker.querySelectorAll("[data-contributor-frame-option]")) {
            option.addEventListener("click", () => {
                if (!activeFramePanel) return;
                const input = activeFramePanel.querySelector("[data-contributor-frame-id]");
                const preview = activeFramePanel.querySelector("[data-contributor-frame-preview]");
                const id = normalizeFrameId(option.dataset.contributorFrameOption);
                const url = option.dataset.contributorFrameUrl || frameUrl(id);
                if (input) {
                    input.value = id;
                }
                if (preview) {
                    preview.src = url;
                }
                input?.dispatchEvent(new Event("change", { bubbles: true }));
                framePicker.close();
            });
        }
    }

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
            repositionAllFrames();
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
    const frameInput = framePanel?.querySelector("[data-contributor-frame-id]");
    const preview = framePanel?.querySelector("[data-contributor-frame-preview]");
    const status = framePanel?.querySelector("[data-contributor-frame-status]");
    const antiforgery = framePanel?.querySelector("[data-contributor-antiforgery]")?.value;
    if (!enabledInput || !frameInput) return;

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
        frameInput.value
    );

    const applyPlayerFrame = () => {
        enabledInput.checked = frameEnabled;
        frameInput.value = frameId;
        if (preview) preview.src = frameUrl(frameId);
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
    frameInput.addEventListener("change", () => {
        frameId = normalizeFrameId(frameInput.value);
        savePreference();
        syncFrame().catch(console.error);
    });

    const mediaObserver = new MutationObserver(() => applyPlayerFrame());
    mediaObserver.observe(avatarControl, {
        attributes: true,
        subtree: true,
        attributeFilter: ["hidden"]
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
