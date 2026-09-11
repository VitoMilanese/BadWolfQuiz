(() => {
    const escapeHtml = value => String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");

    const cardHtml = item => {
        const target = Number(item.target) || 0;
        const current = Number(item.progress) || 0;
        const unlocked = item.isUnlocked === true;
        const percentage = target <= 0 ? 0 : Math.max(0, Math.min(100, Math.round(current * 100 / target)));
        const progress = unlocked
            ? ""
            : `<div class="player-achievement-progress" aria-label="${current} / ${target}"><span style="width:${percentage}%"></span></div><small>${current} / ${target}</small>`;
        return `<article class="player-achievement-card host-custom-achievement-card ${unlocked ? "is-unlocked" : "is-locked"}${item.isNewInCurrentGame ? " is-new" : ""}" data-achievement-code="${escapeHtml(item.code)}">
            <div class="player-achievement-card-top">
                <img class="player-achievement-image" src="${escapeHtml(item.artworkUrl)}" alt="" aria-hidden="true" loading="lazy" decoding="async">
                <span class="player-achievement-state" aria-hidden="true">${unlocked ? "✓" : "○"}</span>
            </div>
            <span class="custom-achievement-host-badge">HOST</span>
            <strong>${escapeHtml(item.name)}</strong>
            <p>${escapeHtml(item.description)}</p>
            ${progress}
        </article>`;
    };

    const appendCards = (grid, items) => {
        if (!grid || !Array.isArray(items) || items.length === 0) return;
        const existing = new Set([...grid.querySelectorAll("[data-achievement-code]")]
            .map(node => node.dataset.achievementCode));
        for (const item of items) {
            if (!item?.code || existing.has(item.code)) continue;
            grid.insertAdjacentHTML("beforeend", cardHtml(item));
            existing.add(item.code);
        }
        window.BadWolfTrimAchievementImages?.(grid);
    };

    const loadPlayerLobbyCards = async () => {
        const lobby = document.querySelector(".player-lobby[data-game-code][data-player-id]");
        if (!lobby) return;
        const url = new URL("/Player/CustomAchievements", window.location.origin);
        url.searchParams.set("code", lobby.dataset.gameCode || "");
        url.searchParams.set("playerId", lobby.dataset.playerId || "");
        try {
            const response = await fetch(url, { headers: { Accept: "application/json" }, credentials: "same-origin" });
            if (!response.ok) return;
            const data = await response.json();
            appendCards(document.querySelector("#player-achievements-dialog .player-achievements-grid"), data.achievements);
        } catch (error) {
            console.error("Failed to load host-defined achievements.", error);
        }
    };

    const loadHostDialogCards = async button => {
        const playerId = button.dataset.playerAchievementsPlayerId;
        if (!playerId) return;
        const segments = window.location.pathname.split("/").filter(Boolean);
        const gameId = segments.at(-1);
        if (!gameId) return;
        const url = new URL("/Admin/Games/CustomPlayerAchievements", window.location.origin);
        url.searchParams.set("id", gameId);
        url.searchParams.set("playerId", playerId);
        try {
            const response = await fetch(url, { headers: { Accept: "application/json" }, credentials: "same-origin" });
            if (!response.ok) return;
            const data = await response.json();
            const addWhenReady = () => {
                const grid = document.querySelector("[data-host-player-achievements-dialog] .player-achievements-grid");
                if (!grid) return false;
                appendCards(grid, data.achievements);
                return true;
            };
            if (addWhenReady()) return;
            const observer = new MutationObserver(() => {
                if (addWhenReady()) observer.disconnect();
            });
            observer.observe(document.body, { childList: true, subtree: true });
            window.setTimeout(() => observer.disconnect(), 5000);
        } catch (error) {
            console.error("Failed to load host-defined achievements for host view.", error);
        }
    };

    const enrichHistory = async () => {
        const entries = [...document.querySelectorAll('[data-achievement-code^="Custom:"]')];
        if (entries.length === 0) return;
        const codes = [...new Set(entries.map(entry => entry.dataset.achievementCode).filter(Boolean))];
        const url = new URL("/CustomAchievementMetadata", window.location.origin);
        url.searchParams.set("codes", codes.join(","));
        try {
            const response = await fetch(url, { headers: { Accept: "application/json" }, credentials: "same-origin" });
            if (!response.ok) return;
            const data = await response.json();
            const byCode = new Map((data.achievements || []).map(item => [item.code, item]));
            for (const entry of entries) {
                const item = byCode.get(entry.dataset.achievementCode);
                if (!item) continue;
                const copy = entry.querySelector(".player-achievement-history-copy");
                const title = copy?.querySelector("strong");
                const description = copy?.querySelector("p");
                if (title) title.textContent = item.name;
                if (description) description.textContent = item.description;
                const artwork = entry.querySelector(".player-achievement-history-artwork");
                if (artwork) artwork.innerHTML = `<img src="${escapeHtml(item.artworkUrl)}" alt="" loading="lazy" decoding="async">`;
                entry.classList.add("host-custom-achievement-history-entry");
                if (copy && !copy.querySelector(".custom-achievement-host-badge")) {
                    copy.insertAdjacentHTML("afterbegin", '<span class="custom-achievement-host-badge">HOST</span>');
                }
            }
        } catch (error) {
            console.error("Failed to enrich custom achievement history.", error);
        }
    };

    document.addEventListener("click", event => {
        const button = event.target instanceof Element
            ? event.target.closest("[data-player-achievements-player-id]")
            : null;
        if (button) void loadHostDialogCards(button);
    }, true);

    const initialize = () => {
        void loadPlayerLobbyCards();
        void enrichHistory();
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
