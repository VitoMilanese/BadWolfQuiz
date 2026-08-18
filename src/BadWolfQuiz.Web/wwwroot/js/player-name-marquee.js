(() => {
    if (window.badWolfPlayerNameMarqueeInitialized) {
        return;
    }

    window.badWolfPlayerNameMarqueeInitialized = true;

    const playerNameSelector = [
        ".final-submission-list li > strong",
        ".game-scoreboard .scoreboard-player[data-player-id] > strong",
        ".board-player-name"
    ].join(", ");

    const resizeObserver = typeof ResizeObserver === "function"
        ? new ResizeObserver(entries => {
            for (const entry of entries) {
                refreshName(entry.target);
            }
        })
        : null;

    let refreshFrame = 0;

    const prepareName = name => {
        if (!(name instanceof HTMLElement)) {
            return null;
        }

        const existingTrack = name.querySelector(
            ":scope > .player-name-marquee-track");
        if (existingTrack instanceof HTMLElement) {
            name.classList.add("player-name-marquee");
            return existingTrack;
        }

        const text = name.textContent ?? "";
        const track = document.createElement("span");
        track.className = "player-name-marquee-track";
        track.textContent = text;

        name.replaceChildren(track);
        name.classList.add("player-name-marquee");
        if (!name.hasAttribute("title")) {
            name.title = text.trim();
        }

        resizeObserver?.observe(name);
        return track;
    };

    function refreshName(name) {
        if (!(name instanceof HTMLElement) || !name.matches(playerNameSelector)) {
            return;
        }

        const track = prepareName(name);
        if (!(track instanceof HTMLElement)) {
            return;
        }

        const trackWidth = track.getBoundingClientRect().width;
        const overflowDistance = Math.max(
            0,
            trackWidth - name.clientWidth);
        const isOverflowing = overflowDistance > 1;

        name.classList.toggle("is-overflowing", isOverflowing);
        if (isOverflowing) {
            name.style.setProperty(
                "--player-name-marquee-end",
                `${-overflowDistance}px`);
        } else {
            name.style.removeProperty("--player-name-marquee-end");
        }
    }

    const refreshAll = () => {
        refreshFrame = 0;
        document.querySelectorAll(playerNameSelector).forEach(refreshName);
    };

    const scheduleRefresh = () => {
        if (refreshFrame !== 0) {
            return;
        }

        refreshFrame = window.requestAnimationFrame(refreshAll);
    };

    new MutationObserver(scheduleRefresh).observe(document.documentElement, {
        childList: true,
        subtree: true
    });

    document.addEventListener("badwolf:host-gameplay-updated", scheduleRefresh);
    window.addEventListener("resize", scheduleRefresh);
    if (document.fonts?.ready) {
        document.fonts.ready
            .then(scheduleRefresh)
            .catch(() => {});
    }

    scheduleRefresh();
})();
