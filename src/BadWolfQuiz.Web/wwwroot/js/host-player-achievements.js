(() => {
    const label = document.head?.dataset.hostPlayerAchievementsLabel || "Achievements";
    let activeButton = null;
    let dialog = null;

    const escapeHtml = value => String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");

    const achievementSortGroup = item => item.isUnlocked ? 0 : item.isSecret ? 2 : 1;

    const secretUnlockedStateIcon = `
        <svg class="player-achievement-state-icon player-achievement-secret-revealed" viewBox="0 0 24 24" focusable="false" aria-hidden="true">
            <path class="player-achievement-secret-eye" d="M2.4 12s3.5-5.2 9.6-5.2 9.6 5.2 9.6 5.2-3.5 5.2-9.6 5.2S2.4 12 2.4 12Z"></path>
            <circle class="player-achievement-secret-pupil" cx="12" cy="12" r="4"></circle>
            <path class="player-achievement-secret-check" d="m10.15 12.05 1.35 1.35 2.75-3.05"></path>
        </svg>`;

    const ensureDialog = () => {
        if (dialog) return dialog;

        dialog = document.createElement("dialog");
        dialog.className = "player-achievements-dialog host-player-achievements-dialog";
        dialog.dataset.hostPlayerAchievementsDialog = "";
        document.body.append(dialog);

        dialog.addEventListener("click", event => {
            if (event.target === dialog) dialog.close();
        });
        dialog.addEventListener("close", () => activeButton?.focus());
        return dialog;
    };

    const renderDialog = data => {
        const achievements = Array.isArray(data.achievements)
            ? [...data.achievements].sort((left, right) =>
                achievementSortGroup(left) - achievementSortGroup(right))
            : [];
        const cards = achievements.map(item => {
            const lockedSecret = item.isSecret && !item.isUnlocked;
            const classes = [
                "player-achievement-card",
                item.isUnlocked ? "is-unlocked" : "is-locked",
                item.isNewInCurrentGame ? "is-new" : ""
            ].filter(Boolean).join(" ");
            const name = lockedSecret ? data.secretTitle : item.name;
            const description = lockedSecret ? data.secretDescription : item.description;
            let progress = "";

            if (!lockedSecret && !item.isUnlocked) {
                const target = Number(item.target) || 0;
                const current = Number(item.progress) || 0;
                const percentage = target <= 0
                    ? 0
                    : Math.max(0, Math.min(100, Math.round(current * 100 / target)));
                progress = `<div class="player-achievement-progress" aria-label="${current} / ${target}"><span style="width:${percentage}%"></span></div><small>${current} / ${target}</small>`;
            }

            const icon = lockedSecret
                ? '<span class="player-achievement-icon" aria-hidden="true">❔</span>'
                : `<img class="player-achievement-image" src="/images/achievements/${encodeURIComponent(item.code)}.png" alt="" aria-hidden="true" loading="lazy" decoding="async">`;
            const stateClass = item.isUnlocked && item.isSecret
                ? "player-achievement-state is-secret-unlocked"
                : "player-achievement-state";
            const stateIcon = item.isUnlocked
                ? (item.isSecret ? secretUnlockedStateIcon : "✓")
                : "○";

            return `<article class="${classes}">
                <div class="player-achievement-card-top">
                    ${icon}
                    <span class="${stateClass}" aria-hidden="true">${stateIcon}</span>
                </div>
                <strong>${escapeHtml(name)}</strong>
                <p>${escapeHtml(description)}</p>
                ${progress}
            </article>`;
        }).join("");

        const targetDialog = ensureDialog();
        targetDialog.setAttribute("aria-labelledby", "host-player-achievements-title");
        targetDialog.innerHTML = `<div class="player-achievements-dialog-card">
            <header class="dialog-heading player-achievements-dialog-heading">
                <h2 id="host-player-achievements-title">${escapeHtml(data.title)} — ${escapeHtml(data.playerName)}</h2>
                <button class="dialog-close" type="button" data-host-player-achievements-close aria-label="${escapeHtml(data.closeLabel)}">×</button>
            </header>
            <div class="player-achievements-dialog-body">
                <section class="player-achievements-panel">
                    <div class="player-achievements-heading"><div>
                        <span class="player-achievements-kicker">BAD WOLF / MILESTONES</span>
                        <p>${escapeHtml(data.subtitle)}</p>
                    </div><strong class="player-achievements-count">${escapeHtml(data.unlockedCountText)}</strong></div>
                    <div class="player-achievements-grid">${cards}</div>
                </section>
            </div>
        </div>`;
        targetDialog.querySelector("[data-host-player-achievements-close]")
            ?.addEventListener("click", () => targetDialog.close());
    };

    const openFor = async button => {
        const playerId = button.dataset.playerAchievementsPlayerId;
        if (!playerId) return;

        activeButton = button;
        button.disabled = true;
        try {
            const url = new URL("/Admin/Games/PlayerAchievements", window.location.origin);
            const gameId = window.location.pathname.split("/").filter(Boolean).at(-1);
            url.searchParams.set("id", gameId);
            url.searchParams.set("playerId", playerId);

            const response = await fetch(url, {
                headers: { "Accept": "application/json" },
                credentials: "same-origin"
            });
            if (!response.ok) throw new Error(`HTTP ${response.status}`);

            renderDialog(await response.json());
            if (!dialog.open) dialog.showModal();
        } catch (error) {
            console.error("Failed to load player achievements.", error);
        } finally {
            button.disabled = false;
        }
    };

    const enhancePlayerList = () => {
        const list = document.querySelector("#player-list.player-list");
        if (!list) return;

        for (const item of list.querySelectorAll("li[data-player-id]")) {
            const actions = item.querySelector(".player-card-actions");
            if (!actions || actions.querySelector("[data-player-achievements-player-id]")) continue;

            const button = document.createElement("button");
            button.type = "button";
            button.className = "button button-secondary icon-button player-action-button player-achievements-host-button";
            button.dataset.playerAchievementsPlayerId = item.dataset.playerId || "";
            button.title = label;
            button.setAttribute("aria-label", label);
            button.setAttribute("aria-haspopup", "dialog");
            button.innerHTML = '<span aria-hidden="true">🏆</span>';

            const remove = actions.querySelector("[data-remove-player]");
            actions.insertBefore(button, remove || actions.firstChild);
        }
    };

    document.addEventListener("click", event => {
        const button = event.target instanceof Element
            ? event.target.closest("[data-player-achievements-player-id]")
            : null;
        if (button) openFor(button);
    });

    const initialize = () => {
        enhancePlayerList();

        new MutationObserver(() => {
            enhancePlayerList();
        }).observe(document.body, { childList: true, subtree: true });
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
