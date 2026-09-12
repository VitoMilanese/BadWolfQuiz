(() => {
    const body = document.body;
    if (!body || body.dataset.debugMode !== "true") return;

    const defaultTitle = "Unlock random achievement for first player";
    let titleResetTimer = null;

    const setTemporaryTitle = (button, value) => {
        button.title = value;
        button.setAttribute("aria-label", value);
        if (titleResetTimer !== null) {
            window.clearTimeout(titleResetTimer);
        }
        titleResetTimer = window.setTimeout(() => {
            button.title = defaultTitle;
            button.setAttribute("aria-label", defaultTitle);
            titleResetTimer = null;
        }, 3000);
    };

    const unlockRandomAchievement = async button => {
        const board = document.querySelector(".host-game-board[data-game-code]");
        const tokenInput = document.querySelector("[data-debug-achievement-antiforgery]");
        const gameCode = String(board?.dataset.gameCode ?? "").trim();
        const token = tokenInput?.value ?? "";
        if (!gameCode || !token) {
            setTemporaryTitle(button, "Game or antiforgery token is unavailable");
            return;
        }

        button.disabled = true;
        try {
            const form = new FormData();
            form.append("gameCode", gameCode);
            form.append("__RequestVerificationToken", token);

            const response = await fetch("/Admin/Games/DebugRandomAchievement", {
                method: "POST",
                credentials: "same-origin",
                body: form,
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                }
            });
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const result = await response.json();
            if (result?.success) {
                window.BadWolfAchievementUnlockNotifications?.handle?.(result.notification);
                setTemporaryTitle(
                    button,
                    `Unlocked ${result.achievementCode} for ${result.playerName}`
                );
                return;
            }

            if (result?.reason === "no-players") {
                setTemporaryTitle(button, "No players are available");
                return;
            }
            if (result?.reason === "all-unlocked") {
                setTemporaryTitle(
                    button,
                    "All built-in achievements are already unlocked for the first player"
                );
                return;
            }

            setTemporaryTitle(button, "Achievement was not unlocked");
        } catch (error) {
            console.error("Debug achievement unlock failed.", error);
            setTemporaryTitle(button, "Achievement unlock failed");
        } finally {
            button.disabled = false;
        }
    };

    const install = () => {
        const board = document.querySelector(".host-game-board[data-game-code]");
        const header = document.querySelector(".game-header-context");
        if (!board || !header || header.querySelector("[data-debug-achievement-unlock]")) {
            return;
        }

        const button = document.createElement("button");
        button.className = "button button-secondary icon-button";
        button.type = "button";
        button.dataset.debugAchievementUnlock = "true";
        button.title = defaultTitle;
        button.setAttribute("aria-label", defaultTitle);

        const icon = document.createElement("span");
        icon.setAttribute("aria-hidden", "true");
        icon.textContent = "🏆";
        button.append(icon);
        button.addEventListener("click", () => unlockRandomAchievement(button));
        header.append(button);
    };

    const observer = new MutationObserver(install);
    observer.observe(document.body, {
        childList: true,
        subtree: true
    });

    document.addEventListener("badwolf:host-shell-mounted", install);
    document.addEventListener("badwolf:host-gameplay-updated", install);
    install();
})();
