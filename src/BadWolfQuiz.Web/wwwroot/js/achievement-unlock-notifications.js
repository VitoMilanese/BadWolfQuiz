(() => {
    "use strict";

    if (window.badWolfAchievementUnlockNotificationsInitialized) {
        return;
    }
    window.badWolfAchievementUnlockNotificationsInitialized = true;

    const playerRoot = document.querySelector(
        ".player-lobby[data-game-code][data-player-id]");
    const hostRoot = document.querySelector(
        ".host-game-board[data-game-code], .content-panel[data-game-code]");
    const root = playerRoot || hostRoot;
    if (!(root instanceof HTMLElement) || !window.signalR) {
        return;
    }

    const gameCode = root.dataset.gameCode?.trim();
    if (!gameCode) {
        return;
    }

    const isPlayer = playerRoot instanceof HTMLElement;
    const ownPlayerId = isPlayer
        ? (playerRoot.dataset.playerId || "").toLowerCase()
        : "";
    const seenStorageKey = `badwolfquiz:achievement-unlocks:${gameCode}:${ownPlayerId || "host"}`;
    const seenIds = new Set();

    try {
        const persisted = JSON.parse(sessionStorage.getItem(seenStorageKey) || "[]");
        if (Array.isArray(persisted)) {
            persisted.slice(-100).forEach(value => {
                if (typeof value === "string" && value) {
                    seenIds.add(value);
                }
            });
        }
    } catch {
        // Session-storage failures must never break gameplay.
    }

    const remember = eventId => {
        if (!eventId || seenIds.has(eventId)) {
            return false;
        }

        seenIds.add(eventId);
        while (seenIds.size > 100) {
            seenIds.delete(seenIds.values().next().value);
        }

        try {
            sessionStorage.setItem(seenStorageKey, JSON.stringify([...seenIds]));
        } catch {
            // Keep in-memory de-duplication if storage is unavailable.
        }
        return true;
    };

    const wait = milliseconds => new Promise(resolve => {
        window.setTimeout(resolve, milliseconds);
    });

    const buildArtwork = notification => {
        const image = document.createElement("img");
        image.className = "achievement-unlock-artwork";
        image.alt = "";
        image.setAttribute("aria-hidden", "true");
        image.decoding = "async";
        image.src = notification.artworkUrl || "";
        return image;
    };

    const buildCopy = notification => {
        const copy = document.createElement("div");
        copy.className = "achievement-unlock-copy";

        const kicker = document.createElement("span");
        kicker.className = "achievement-unlock-kicker";
        kicker.textContent = "ACHIEVEMENT UNLOCKED";

        const title = document.createElement("strong");
        title.className = "achievement-unlock-title";
        title.textContent = notification.title || notification.achievementCode || "Achievement";

        const description = document.createElement("span");
        description.className = "achievement-unlock-description";
        description.textContent = notification.description || "";

        copy.append(kicker, title, description);
        return copy;
    };

    const playerQueue = [];
    let playerQueueRunning = false;

    const ensurePlayerStack = () => {
        let stack = document.querySelector("[data-achievement-unlock-player-stack]");
        if (stack instanceof HTMLElement) {
            return stack;
        }

        stack = document.createElement("div");
        stack.className = "achievement-unlock-player-stack";
        stack.dataset.achievementUnlockPlayerStack = "";
        stack.setAttribute("aria-live", "polite");
        stack.setAttribute("aria-atomic", "true");
        document.body.appendChild(stack);
        return stack;
    };

    const runPlayerQueue = async () => {
        if (playerQueueRunning) {
            return;
        }
        playerQueueRunning = true;

        try {
            while (playerQueue.length > 0) {
                const notification = playerQueue.shift();
                const toast = document.createElement("aside");
                toast.className = "achievement-unlock-player-toast";
                toast.dataset.achievementUnlockToast = "";
                toast.setAttribute("role", "status");
                toast.append(
                    buildArtwork(notification),
                    buildCopy(notification));

                const stack = ensurePlayerStack();
                stack.replaceChildren(toast);
                await wait(4300);
                toast.classList.add("is-leaving");
                await wait(360);
                toast.remove();
            }
        } finally {
            playerQueueRunning = false;
        }
    };

    const hostQueues = new Map();
    const hostQueueRunning = new Set();

    const findHostCard = playerId => {
        const normalized = String(playerId || "").toLowerCase();
        return Array.from(document.querySelectorAll(
            ".scoreboard-player[data-player-id]"))
            .find(card => card instanceof HTMLElement &&
                (card.dataset.playerId || "").toLowerCase() === normalized) || null;
    };

    const runHostQueue = async playerId => {
        if (hostQueueRunning.has(playerId)) {
            return;
        }
        hostQueueRunning.add(playerId);

        try {
            const queue = hostQueues.get(playerId) || [];
            while (queue.length > 0) {
                const notification = queue.shift();
                let card = findHostCard(playerId);

                // DOM refreshes can briefly replace the scoreboard card. Give the normal
                // PlayersChanged renderer a short chance to mount the replacement card.
                for (let retry = 0; !card && retry < 5; retry++) {
                    await wait(160);
                    card = findHostCard(playerId);
                }
                if (!(card instanceof HTMLElement)) {
                    continue;
                }

                const overlay = document.createElement("div");
                overlay.className = "achievement-unlock-host-overlay";
                overlay.dataset.achievementUnlockHostOverlay = "";
                overlay.setAttribute("role", "status");
                overlay.append(
                    buildArtwork(notification),
                    buildCopy(notification));

                card.classList.add("achievement-unlock-active");
                card.appendChild(overlay);
                await wait(3600);
                overlay.classList.add("is-leaving");
                await wait(320);
                overlay.remove();
                card.classList.remove("achievement-unlock-active");
            }
        } finally {
            hostQueueRunning.delete(playerId);
            if ((hostQueues.get(playerId) || []).length === 0) {
                hostQueues.delete(playerId);
            }
        }
    };

    const enqueueHost = notification => {
        const playerId = String(notification.playerId || "").toLowerCase();
        if (!playerId) {
            return;
        }

        const queue = hostQueues.get(playerId) || [];
        queue.push(notification);
        hostQueues.set(playerId, queue);
        void runHostQueue(playerId);
    };

    const handleUnlock = notification => {
        if (!notification || typeof notification !== "object") {
            return;
        }

        const playerId = String(notification.playerId || "").toLowerCase();
        if (isPlayer && playerId !== ownPlayerId) {
            return;
        }
        if (!remember(notification.eventId)) {
            return;
        }

        if (isPlayer) {
            playerQueue.push(notification);
            void runPlayerQueue();
            return;
        }

        enqueueHost(notification);
    };

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/game")
        .withAutomaticReconnect()
        .build();

    connection.on("AchievementUnlocked", handleUnlock);

    const join = async () => {
        if (connection.state !== signalR.HubConnectionState.Connected) {
            return;
        }
        try {
            await connection.invoke("JoinSession", gameCode);
        } catch (error) {
            console.warn("Unable to join achievement notification channel.", error);
        }
    };

    connection.onreconnected(() => {
        void join();
    });

    const start = async () => {
        try {
            await connection.start();
            await join();
        } catch (error) {
            console.warn("Achievement notification connection failed; retrying.", error);
            window.setTimeout(() => void start(), 1500);
        }
    };

    void start();
})();
