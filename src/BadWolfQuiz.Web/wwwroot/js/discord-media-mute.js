(() => {
    const board = document.querySelector(".host-game-board[data-game-id]");
    if (!board) {
        return;
    }

    const moveGameControlsToHeader = () => {
        const controls = document.querySelector(".game-side-controls");
        const header = document.querySelector(".game-header-context");
        if (!controls || !header) {
            return;
        }

        const syncVisibility = () => {
            controls.style.display = board.classList.contains(
                "host-gameplay-presentation-mode")
                ? "none"
                : "flex";
        };

        controls.style.position = "static";
        controls.style.right = "auto";
        controls.style.bottom = "auto";
        controls.style.zIndex = "auto";
        controls.style.transform = "none";
        controls.dataset.headerGameControls = "";

        const discordSettings = header.querySelector("[data-open-discord-settings]");
        header.insertBefore(controls, discordSettings ?? null);
        syncVisibility();

        const visibilityObserver = new MutationObserver(syncVisibility);
        visibilityObserver.observe(board, {
            attributes: true,
            attributeFilter: ["class"]
        });
    };

    moveGameControlsToHeader();

    const gameId = board.dataset.gameId;
    const status = document.querySelector("[data-discord-operation-status]");
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    let automaticRequestActive = false;
    let requestInFlight = Promise.resolve();

    const post = async (handler, values, keepalive = false) => {
        const body = new FormData();
        body.append("id", gameId);
        Object.entries(values).forEach(([key, value]) => body.append(key, value));
        const response = await fetch(`?handler=${handler}`, {
            method: "POST",
            headers: token ? { "RequestVerificationToken": token } : {},
            body,
            keepalive
        });
        const result = await response.json();
        if (!response.ok) {
            throw new Error(result.error ?? "Discord voice operation failed.");
        }
        return result;
    };

    const reconcileAutomaticMute = shouldBeActive => {
        if (shouldBeActive && board.dataset.discordAutoMute !== "true") {
            return;
        }

        if (shouldBeActive === automaticRequestActive) {
            return;
        }

        automaticRequestActive = shouldBeActive;
        requestInFlight = requestInFlight
            .then(() => post("DiscordMedia", { active: shouldBeActive.toString() }))
            .catch(error => {
                automaticRequestActive = !shouldBeActive;
                if (status) {
                    status.textContent = error.message;
                }
            });
    };

    const mediaState = new BadWolfDiscordMediaState(reconcileAutomaticMute);
    const activate = media => mediaState.start(media);
    const deactivate = media => mediaState.stop(media);
    const nativeMediaSelector =
        "audio.game-content-audio, video.game-content-video";
    const youtubeSelector = "iframe.youtube-auto-expand";
    const boundNativeMedia = new WeakSet();
    const boundYouTubeFrames = new WeakSet();
    const youtubeKeys = new WeakMap();
    const youtubeWindows = new WeakMap();
    const youtubePlayers = new Map();
    let nextYouTubeKey = 0;

    const connectYouTubeFrame = iframe => {
        if (!board.contains(iframe)) {
            return;
        }

        const key = youtubeKeys.get(iframe);
        const contentWindow = iframe.contentWindow;
        if (!key || !contentWindow) {
            return;
        }

        const previousWindow = youtubeWindows.get(iframe);
        if (previousWindow && previousWindow !== contentWindow) {
            youtubePlayers.delete(previousWindow);
        }

        youtubeWindows.set(iframe, contentWindow);
        youtubePlayers.set(contentWindow, key);
        contentWindow.postMessage(JSON.stringify({
            event: "listening",
            id: key,
            channel: "badwolfquiz"
        }), "https://www.youtube.com");
        contentWindow.postMessage(JSON.stringify({
            event: "command",
            func: "addEventListener",
            args: ["onStateChange"]
        }), "https://www.youtube.com");
    };

    const bindNativeMedia = media => {
        if (boundNativeMedia.has(media)) {
            return;
        }

        boundNativeMedia.add(media);
        BadWolfDiscordMediaState.bindNativeMedia(media, mediaState, media);
    };

    const bindYouTubeFrame = iframe => {
        if (!youtubeKeys.has(iframe)) {
            youtubeKeys.set(iframe, `youtube-${nextYouTubeKey++}`);
        }

        if (!boundYouTubeFrames.has(iframe)) {
            boundYouTubeFrames.add(iframe);
            iframe.addEventListener("load", () => connectYouTubeFrame(iframe));
        }

        connectYouTubeFrame(iframe);
    };

    const forEachMedia = (root, selector, callback) => {
        if (!(root instanceof Element)) {
            return;
        }

        if (root.matches(selector)) {
            callback(root);
        }
        root.querySelectorAll(selector).forEach(callback);
    };

    const bindMediaTree = root => {
        forEachMedia(root, nativeMediaSelector, bindNativeMedia);
        forEachMedia(root, youtubeSelector, bindYouTubeFrame);
    };

    const unbindMediaTree = root => {
        forEachMedia(root, nativeMediaSelector, media => deactivate(media));
        forEachMedia(root, youtubeSelector, iframe => {
            const key = youtubeKeys.get(iframe);
            if (key) {
                deactivate(key);
            }

            const contentWindow = youtubeWindows.get(iframe);
            if (contentWindow) {
                youtubePlayers.delete(contentWindow);
                youtubeWindows.delete(iframe);
            }
        });
    };

    bindMediaTree(board);

    const mediaObserver = new MutationObserver(mutations => {
        for (const mutation of mutations) {
            mutation.removedNodes.forEach(unbindMediaTree);
            mutation.addedNodes.forEach(bindMediaTree);
        }
    });
    mediaObserver.observe(board, { childList: true, subtree: true });

    window.addEventListener("badwolfquiz:discord-auto-mute-changed", event => {
        const enabled = event.detail?.enabled === true;
        board.dataset.discordAutoMute = enabled.toString();
        reconcileAutomaticMute(enabled && mediaState.isActive);
    });

    window.setInterval(() => {
        if (!automaticRequestActive || !mediaState.isActive ||
            board.dataset.discordAutoMute !== "true") {
            return;
        }

        requestInFlight = requestInFlight
            .then(() => post("DiscordMedia", { active: "true" }))
            .catch(error => {
                if (status) {
                    status.textContent = error.message;
                }
            });
    }, 60_000);

    window.addEventListener("message", event => {
        if (event.origin !== "https://www.youtube.com" ||
            !youtubePlayers.has(event.source)) {
            return;
        }
        let message;
        try {
            message = typeof event.data === "string" ? JSON.parse(event.data) : event.data;
        } catch {
            return;
        }
        if (message?.event !== "onStateChange") {
            return;
        }
        const key = youtubePlayers.get(event.source);
        const playbackState = BadWolfDiscordMediaState.getYouTubePlaybackState(
            message.info);
        if (playbackState === true) {
            activate(key);
        } else if (playbackState === false) {
            deactivate(key);
        }
    });

    document.querySelectorAll("[data-discord-mute]").forEach(button => {
        button.addEventListener("click", async () => {
            const buttons = document.querySelectorAll("[data-discord-mute]");
            buttons.forEach(item => item.disabled = true);
            if (status) {
                status.textContent = "…";
            }
            try {
                const result = await post("DiscordMute", {
                    muted: button.dataset.discordMute
                });
                if (status) {
                    status.textContent = result.message;
                }
            } catch (error) {
                if (status) {
                    status.textContent = error.message;
                }
            } finally {
                buttons.forEach(item => item.disabled = false);
            }
        });
    });

    window.addEventListener("pagehide", () => {
        mediaObserver.disconnect();
        if (automaticRequestActive) {
            post("DiscordMedia", { active: "false" }, true).catch(() => {});
        }
    });
})();
