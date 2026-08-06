(() => {
    const board = document.querySelector(".host-game-board[data-game-id]");
    if (!board) {
        return;
    }

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
        if (board.dataset.discordAutoMute !== "true") {
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

    board.querySelectorAll("audio.game-content-audio, video.game-content-video")
        .forEach(media => {
            media.addEventListener("play", () => activate(media));
            ["pause", "ended", "error", "abort", "emptied"]
                .forEach(name => media.addEventListener(name, () => deactivate(media)));
        });

    const youtubePlayers = new Map();
    board.querySelectorAll("iframe.youtube-auto-expand").forEach((iframe, index) => {
        const key = `youtube-${index}`;
        youtubePlayers.set(iframe.contentWindow, key);
        iframe.addEventListener("load", () => {
            iframe.contentWindow?.postMessage(JSON.stringify({
                event: "listening",
                id: key,
                channel: "badwolfquiz"
            }), "https://www.youtube.com");
            iframe.contentWindow?.postMessage(JSON.stringify({
                event: "command",
                func: "addEventListener",
                args: ["onStateChange"]
            }), "https://www.youtube.com");
        });
    });
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
        if (message.info === 1) {
            activate(key);
        } else if (message.info === 0 || message.info === 2 || message.info === 5) {
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
        if (automaticRequestActive) {
            post("DiscordMedia", { active: "false" }, true).catch(() => {});
        }
    });
})();
