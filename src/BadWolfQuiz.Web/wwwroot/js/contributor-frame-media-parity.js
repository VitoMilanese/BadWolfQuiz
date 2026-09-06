(() => {
    const body = document.body;
    if (!body) return;

    const nativeInsets = new Map(
        String(body.dataset.contributorFrameNativeInsets ?? "")
            .split(";")
            .map(entry => entry.split(":"))
            .filter(parts => parts.length === 2)
            .map(([id, inset]) => [id, Number.parseFloat(inset)])
            .filter(([, inset]) => Number.isFinite(inset) && inset >= 0)
    );

    const mediaSelectors = [
        ".player-avatar-current:not([hidden])",
        ".player-card-avatar:not([hidden])",
        ".player-list-avatar:not([hidden])",
        "img.host-card-media:not([hidden])",
        "video.host-card-media:not([hidden])",
        "iframe.host-card-media:not([hidden])",
        ".host-card-media img:not([hidden])",
        ".host-card-media video:not([hidden])",
        ".host-card-media iframe:not([hidden])"
    ];

    const hostPlayerCardMediaSelector =
        ".game-scoreboard .scoreboard-player:not(.host-card) > .player-card-avatar";

    const findMedia = owner => {
        for (const selector of mediaSelectors) {
            for (const media of owner.querySelectorAll(selector)) {
                if (getComputedStyle(media).display !== "none") {
                    return media;
                }
            }
        }
        return null;
    };

    const clearMedia = media => {
        media.classList.remove("contributor-frame-preview-parity-media");
        media.style.removeProperty("--contributor-frame-preview-parity-inset");
        media.style.removeProperty("--contributor-frame-preview-parity-size");
    };

    let refreshQueued = false;
    const queueRefresh = () => {
        if (refreshQueued) return;
        refreshQueued = true;
        window.requestAnimationFrame(() => {
            refreshQueued = false;
            refreshAll();
            window.setTimeout(refreshAll, 60);
            window.setTimeout(refreshAll, 220);
        });
    };

    const applyOwner = owner => {
        const overlay = owner.querySelector(
            ":scope > .contributor-avatar-frame-overlay"
        );
        const media = findMedia(owner);

        for (const stale of owner.querySelectorAll(
            ".contributor-frame-preview-parity-media"
        )) {
            if (stale !== media) {
                clearMedia(stale);
            }
        }

        if (!(overlay instanceof HTMLImageElement) || !media) {
            return;
        }

        const applyGeometry = () => {
            if (!owner.isConnected || !media.isConnected || !overlay.isConnected) {
                return;
            }

            const frameId = String(owner.dataset.avatarFrame ?? "").trim();
            if (!frameId) {
                clearMedia(media);
                return;
            }

            const naturalSize = Math.min(
                overlay.naturalWidth || 0,
                overlay.naturalHeight || 0
            );
            const renderedSize = Math.min(
                overlay.offsetWidth || 0,
                overlay.offsetHeight || 0
            );
            if (naturalSize <= 0 || renderedSize <= 0) {
                return;
            }

            const nativeInset = nativeInsets.get(frameId) ?? 10;
            const scaledInset = nativeInset * renderedSize / naturalSize;

            media.classList.add("contributor-frame-preview-parity-media");
            media.style.setProperty(
                "--contributor-frame-preview-parity-inset",
                `${scaledInset}px`
            );

            if (media.matches(hostPlayerCardMediaSelector)) {
                media.style.setProperty(
                    "--contributor-frame-preview-parity-size",
                    `${renderedSize}px`
                );
            } else {
                media.style.removeProperty(
                    "--contributor-frame-preview-parity-size"
                );
            }
        };

        if (overlay.complete && overlay.naturalWidth > 0) {
            applyGeometry();
        } else {
            overlay.addEventListener("load", queueRefresh, { once: true });
        }
    };

    function refreshAll() {
        for (const media of document.querySelectorAll(
            ".contributor-frame-preview-parity-media"
        )) {
            if (!media.closest(
                ".contributor-frame-owner[data-avatar-frame]"
            )) {
                clearMedia(media);
            }
        }

        for (const owner of document.querySelectorAll(
            ".contributor-frame-owner[data-avatar-frame]"
        )) {
            applyOwner(owner);
        }
    }

    const resizeObserver = typeof ResizeObserver === "function"
        ? new ResizeObserver(queueRefresh)
        : null;
    const observed = new WeakSet();

    const observeRuntimeNodes = () => {
        if (!resizeObserver) return;
        for (const owner of document.querySelectorAll(
            ".contributor-frame-owner[data-avatar-frame]"
        )) {
            const overlay = owner.querySelector(
                ":scope > .contributor-avatar-frame-overlay"
            );
            const media = findMedia(owner);
            for (const node of [owner, overlay, media]) {
                if (node instanceof Element && !observed.has(node)) {
                    resizeObserver.observe(node);
                    observed.add(node);
                }
            }
        }
    };

    const mutationObserver = new MutationObserver(() => {
        observeRuntimeNodes();
        queueRefresh();
    });
    mutationObserver.observe(body, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: [
            "class",
            "data-avatar-frame",
            "hidden",
            "src"
        ]
    });

    document.addEventListener("load", event => {
        if (!(event.target instanceof Element)) return;
        if (event.target.matches(".contributor-avatar-frame-overlay") ||
            mediaSelectors.some(selector => event.target.matches(selector))) {
            queueRefresh();
        }
    }, true);
    document.addEventListener("badwolf:host-shell-mounted", queueRefresh);
    document.addEventListener("badwolf:host-gameplay-updated", queueRefresh);
    window.addEventListener("resize", queueRefresh);

    observeRuntimeNodes();
    refreshAll();
    window.setTimeout(queueRefresh, 0);
})();
