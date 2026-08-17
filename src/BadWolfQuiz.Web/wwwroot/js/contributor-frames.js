(() => {
    const body = document.body;
    if (!body) return;

    const defaultFrameId = String(
        body.dataset.contributorFrameDefaultId ?? ""
    ).trim();
    const availableFrameIds = new Set(
        Array.from(document.querySelectorAll("[data-contributor-frame-option]"))
            .map(option => String(option.dataset.contributorFrameOption ?? "").trim())
            .filter(Boolean)
    );
    const normalizeFrameId = value => {
        const id = String(value ?? "").trim();
        if (!id) return defaultFrameId;
        if (availableFrameIds.size > 0 && !availableFrameIds.has(id)) {
            return defaultFrameId;
        }
        return id;
    };
    const frameUrl = frameId => {
        const id = normalizeFrameId(frameId);
        return id ? `/frames/${encodeURIComponent(id)}.png` : "";
    };

    const defaultAvatarInsetRatio = 0.16;
    const minimumAvatarInsetRatio = 0.08;
    const maximumAvatarInsetRatio = 0.30;
    const frameAlphaThreshold = 72;
    const frameInsetMarginRatio = 0.018;
    const frameInsetRatios = new Map();
    const frameInsetRequests = new Map();

    const measureFrameInsetRatio = image => {
        const naturalWidth = image.naturalWidth;
        const naturalHeight = image.naturalHeight;
        if (naturalWidth <= 0 || naturalHeight <= 0) {
            return defaultAvatarInsetRatio;
        }

        const maximumDimension = 512;
        const scale = Math.min(
            1,
            maximumDimension / Math.max(naturalWidth, naturalHeight)
        );
        const width = Math.max(1, Math.round(naturalWidth * scale));
        const height = Math.max(1, Math.round(naturalHeight * scale));
        const canvas = document.createElement("canvas");
        canvas.width = width;
        canvas.height = height;
        const context = canvas.getContext("2d", { willReadFrequently: true });
        if (!context) return defaultAvatarInsetRatio;

        context.clearRect(0, 0, width, height);
        context.drawImage(image, 0, 0, width, height);

        let pixels;
        try {
            pixels = context.getImageData(0, 0, width, height).data;
        } catch {
            return defaultAvatarInsetRatio;
        }

        const centerX = (width - 1) / 2;
        const centerY = (height - 1) / 2;
        const minimumSide = Math.min(width, height);
        const maximumRadius = Math.floor(minimumSide / 2);
        if (maximumRadius <= 1) return defaultAvatarInsetRatio;

        let safeRadius = maximumRadius;
        const rayCount = 720;
        for (let ray = 0; ray < rayCount; ray += 1) {
            const angle = ray * Math.PI * 2 / rayCount;
            const cosine = Math.cos(angle);
            const sine = Math.sin(angle);
            for (let radius = 1; radius <= maximumRadius; radius += 1) {
                const x = Math.round(centerX + cosine * radius);
                const y = Math.round(centerY + sine * radius);
                if (x < 0 || x >= width || y < 0 || y >= height) break;
                const alpha = pixels[(y * width + x) * 4 + 3];
                if (alpha >= frameAlphaThreshold) {
                    safeRadius = Math.min(safeRadius, radius - 1);
                    break;
                }
            }
        }

        const measuredInset =
            0.5 - safeRadius / minimumSide + frameInsetMarginRatio;
        if (!Number.isFinite(measuredInset)) {
            return defaultAvatarInsetRatio;
        }

        return Math.min(
            maximumAvatarInsetRatio,
            Math.max(minimumAvatarInsetRatio, measuredInset)
        );
    };

    const ensureFrameInsetRatio = frameId => {
        const id = normalizeFrameId(frameId);
        if (!id) return Promise.resolve(defaultAvatarInsetRatio);
        if (frameInsetRatios.has(id)) {
            return Promise.resolve(frameInsetRatios.get(id));
        }
        if (frameInsetRequests.has(id)) {
            return frameInsetRequests.get(id);
        }

        const request = new Promise(resolve => {
            const image = new Image();
            image.decoding = "async";
            image.addEventListener("load", () => {
                resolve(measureFrameInsetRatio(image));
            }, { once: true });
            image.addEventListener("error", () => {
                resolve(defaultAvatarInsetRatio);
            }, { once: true });
            image.src = frameUrl(id);
        }).then(ratio => {
            frameInsetRatios.set(id, ratio);
            frameInsetRequests.delete(id);
            return ratio;
        });

        frameInsetRequests.set(id, request);
        return request;
    };

    const findFrameMedia = owner => {
        const selectors = [
            ".player-avatar-current:not([hidden])",
            ".player-card-avatar:not([hidden])",
            ".player-list-avatar:not([hidden])",
            ".host-card-media video:not([hidden])",
            ".host-card-media iframe:not([hidden])",
            ".host-card-media img:not([hidden])",
            ".host-card-media:not([hidden])"
        ];
        for (const selector of selectors) {
            for (const element of owner.querySelectorAll(selector)) {
                if (getComputedStyle(element).display !== "none") {
                    return element;
                }
            }
        }
        return null;
    };

    const isBuiltInAvatar = media => {
        if (!(media instanceof HTMLImageElement)) return false;
        const source = media.currentSrc || media.getAttribute("src");
        if (!source) return false;
        try {
            return new URL(source, window.location.href).pathname.startsWith("/avatars/");
        } catch {
            return false;
        }
    };

    const clearAvatarInset = owner => {
        for (const media of owner.querySelectorAll(".contributor-frame-avatar-source")) {
            media.classList.remove("contributor-frame-avatar-source");
            media.style.removeProperty("--contributor-frame-avatar-inset");
        }
    };

    const updateAvatarInset = (owner, media, frameSize) => {
        const builtInAvatar = isBuiltInAvatar(media);
        media.classList.toggle(
            "contributor-frame-avatar-source",
            builtInAvatar
        );
        if (!builtInAvatar) {
            media.style.removeProperty("--contributor-frame-avatar-inset");
            return;
        }

        const frameId = normalizeFrameId(owner.dataset.avatarFrame);
        const insetRatio = frameInsetRatios.get(frameId) ?? defaultAvatarInsetRatio;
        media.style.setProperty(
            "--contributor-frame-avatar-inset",
            `${Math.max(2, frameSize * insetRatio)}px`
        );
    };

    const positionFrameOverlay = owner => {
        const overlay = owner.querySelector(":scope > .contributor-avatar-frame-overlay");
        if (!overlay) return;
        const media = findFrameMedia(owner);
        if (!media) {
            clearAvatarInset(owner);
            overlay.hidden = true;
            return;
        }

        for (const insetMedia of owner.querySelectorAll(
            ".contributor-frame-avatar-source"
        )) {
            if (insetMedia !== media) {
                insetMedia.classList.remove("contributor-frame-avatar-source");
                insetMedia.style.removeProperty("--contributor-frame-avatar-inset");
            }
        }

        const ownerRect = owner.getBoundingClientRect();
        const mediaRect = media.getBoundingClientRect();
        if (mediaRect.width <= 0 || mediaRect.height <= 0) {
            overlay.hidden = true;
            return;
        }

        const scaleX = owner.offsetWidth > 0
            ? ownerRect.width / owner.offsetWidth
            : 1;
        const scaleY = owner.offsetHeight > 0
            ? ownerRect.height / owner.offsetHeight
            : 1;
        const mediaLeft = (mediaRect.left - ownerRect.left) / scaleX - owner.clientLeft;
        const mediaTop = (mediaRect.top - ownerRect.top) / scaleY - owner.clientTop;
        const mediaWidth = mediaRect.width / scaleX;
        const mediaHeight = mediaRect.height / scaleY;
        const frameSize = Math.min(mediaWidth, mediaHeight);

        updateAvatarInset(owner, media, frameSize);
        overlay.hidden = false;
        overlay.style.left = `${mediaLeft + (mediaWidth - frameSize) / 2}px`;
        overlay.style.top = `${mediaTop + (mediaHeight - frameSize) / 2}px`;
        overlay.style.width = `${frameSize}px`;
        overlay.style.height = `${frameSize}px`;
    };

    const observedFrameMedia = new WeakMap();
    const frameResizeObserver = typeof ResizeObserver === "function"
        ? new ResizeObserver(entries => {
            const owners = new Set();
            for (const entry of entries) {
                const owner = entry.target.closest?.(".contributor-frame-owner");
                if (owner) owners.add(owner);
            }
            for (const owner of owners) {
                positionFrameOverlay(owner);
            }
        })
        : null;

    const observeFrameLayout = owner => {
        if (!frameResizeObserver) return;
        frameResizeObserver.observe(owner);
        const media = findFrameMedia(owner);
        const previousMedia = observedFrameMedia.get(owner);
        if (previousMedia && previousMedia !== media) {
            frameResizeObserver.unobserve(previousMedia);
        }
        if (media) {
            frameResizeObserver.observe(media);
            observedFrameMedia.set(owner, media);
        } else {
            observedFrameMedia.delete(owner);
        }
    };

    const unobserveFrameLayout = owner => {
        if (!frameResizeObserver) return;
        frameResizeObserver.unobserve(owner);
        const media = observedFrameMedia.get(owner);
        if (media) frameResizeObserver.unobserve(media);
        observedFrameMedia.delete(owner);
    };

    const removeFrame = owner => {
        if (!owner) return;
        unobserveFrameLayout(owner);
        clearAvatarInset(owner);
        delete owner.dataset.avatarFrame;
        owner.querySelector(":scope > .contributor-avatar-frame-overlay")?.remove();
        owner.classList.remove("contributor-frame-owner");
    };

    const setFrame = (owner, enabled, frameId) => {
        if (!owner) return;
        const normalizedId = normalizeFrameId(frameId);
        if (!enabled || !normalizedId) {
            removeFrame(owner);
            return;
        }

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
        observeFrameLayout(owner);
        window.requestAnimationFrame(() => positionFrameOverlay(owner));
        ensureFrameInsetRatio(normalizedId).then(() => {
            if (owner.isConnected && owner.dataset.avatarFrame === normalizedId) {
                positionFrameOverlay(owner);
            }
        }).catch(console.error);
    };

    const repositionAllFrames = () => {
        for (const owner of document.querySelectorAll(".contributor-frame-owner")) {
            observeFrameLayout(owner);
            positionFrameOverlay(owner);
        }
    };
    window.addEventListener("resize", repositionAllFrames);
    document.addEventListener("load", event => {
        if (event.target instanceof Element &&
            event.target.matches(
                ".player-avatar-current, .player-card-avatar, .player-list-avatar, " +
                ".host-card-media, .host-card-media img, .host-card-media video, " +
                ".host-card-media iframe"
            )) {
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

    let hostGameConnection = null;
    let hostGameStarting = false;

    const findHostGameRoot = () => document.querySelector(
        ".host-game-board[data-game-code], .content-panel[data-game-code]"
    );

    const refreshPlayerFrames = async gameCode => {
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

    const startHostGameIntegration = () => {
        if (hostGameConnection || hostGameStarting || typeof signalR === "undefined") {
            return;
        }

        const hostGameRoot = findHostGameRoot();
        const gameCode = hostGameRoot?.dataset.gameCode;
        if (!gameCode) return;

        hostGameStarting = true;
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/game")
            .withAutomaticReconnect()
            .build();

        connection.on("PlayersChanged", () => {
            refreshPlayerFrames(gameCode).catch(console.error);
        });

        connection.on("HostContributorFrameChanged", update => {
            hostFrameState.enabled = update?.enabled === true;
            hostFrameState.frameId = normalizeFrameId(update?.frameId);
            applyHostFrame();
        });

        const join = async () => {
            await connection.invoke("JoinSession", gameCode);
            await refreshPlayerFrames(gameCode);
            applyHostFrame();
            repositionAllFrames();
        };

        connection.onreconnected(() => join().catch(console.error));
        connection.start()
            .then(async () => {
                hostGameConnection = connection;
                hostGameStarting = false;
                await join();
            })
            .catch(error => {
                hostGameStarting = false;
                console.error(error);
            });
    };

    const liveFrameObserver = new MutationObserver(records => {
        let hasAddedElements = false;
        for (const record of records) {
            for (const node of record.addedNodes) {
                if (!(node instanceof Element)) continue;
                hasAddedElements = true;
                if (hostGameConnection) {
                    applyPlayerFrames(node);
                }
            }
        }

        if (hasAddedElements) {
            applyHostFrame();
            repositionAllFrames();
        }
        startHostGameIntegration();
    });
    liveFrameObserver.observe(document.body, { childList: true, subtree: true });
    startHostGameIntegration();

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
