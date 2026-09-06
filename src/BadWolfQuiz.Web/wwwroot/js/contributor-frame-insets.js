(() => {
    const body = document.body;
    if (!body) return;

    const defaultInsetPixels = 10;
    const frameInsets = new Map(
        String(body.dataset.contributorFrameNativeInsets ?? "")
            .split(";")
            .map(entry => entry.split(":"))
            .filter(parts => parts.length === 2)
            .map(([id, inset]) => [id, Number.parseFloat(inset)])
            .filter(([, inset]) => Number.isFinite(inset) && inset >= 0)
    );
    const frameIds = Array.from(frameInsets.keys());
    const debugHostFrameInsets = new Map();
    let debugHostFrameId = null;

    const frameMediaSelectors = [
        ".player-avatar-current:not([hidden])",
        ".player-card-avatar:not([hidden])",
        ".player-list-avatar:not([hidden])",
        ".host-card-media video:not([hidden])",
        ".host-card-media iframe:not([hidden])",
        ".host-card-media img:not([hidden])",
        ".host-card-media:not([hidden])"
    ];

    const getBaseNativeInsetPixels = frameId =>
        frameInsets.get(frameId) ?? defaultInsetPixels;

    const getNativeInsetPixels = (owner, frameId) => {
        const baseInsetPixels = getBaseNativeInsetPixels(frameId);
        if (!owner.matches("[data-host-card]")) {
            return baseInsetPixels;
        }

        return debugHostFrameInsets.get(frameId) ?? baseInsetPixels;
    };

    const findFrameMedia = owner => {
        for (const selector of frameMediaSelectors) {
            for (const media of owner.querySelectorAll(selector)) {
                if (getComputedStyle(media).display !== "none") {
                    return media;
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
            return new URL(source, window.location.href)
                .pathname
                .startsWith("/avatars/");
        } catch {
            return false;
        }
    };

    const clearMediaClip = owner => {
        for (const media of owner.querySelectorAll(
            ".contributor-frame-clipped-media"
        )) {
            media.classList.remove("contributor-frame-clipped-media");
            media.style.removeProperty("--contributor-frame-clip-radius");
        }
    };

    const applyMediaClip = (
        owner,
        layoutFrameSize,
        scaledInsetPixels
    ) => {
        const media = findFrameMedia(owner);
        for (const staleMedia of owner.querySelectorAll(
            ".contributor-frame-clipped-media"
        )) {
            if (staleMedia !== media) {
                staleMedia.classList.remove("contributor-frame-clipped-media");
                staleMedia.style.removeProperty("--contributor-frame-clip-radius");
            }
        }

        if (!media || isBuiltInAvatar(media)) {
            if (media) {
                media.classList.remove("contributor-frame-clipped-media");
                media.style.removeProperty("--contributor-frame-clip-radius");
            }
            return;
        }

        // This is the same geometry as the frame-settings preview:
        // visible diameter = rendered frame size - 2 * scaled filename inset.
        // Using an explicit circle keeps rectangular images/webcams circular
        // instead of producing the oval created by border-radius/inset clipping.
        const radiusPixels = Math.max(
            0,
            layoutFrameSize / 2 - scaledInsetPixels
        );
        media.classList.add("contributor-frame-clipped-media");
        media.style.setProperty(
            "--contributor-frame-clip-radius",
            `${radiusPixels}px`
        );
    };

    const findHostFrameOwner = () => document.querySelector(
        "[data-host-card].contributor-frame-owner[data-avatar-frame]"
    );

    const applyDebugHostFrameSelection = () => {
        if (body.dataset.debugMode !== "true" || !debugHostFrameId) return;

        const owner = findHostFrameOwner();
        if (!owner) return;

        if (owner.dataset.avatarFrame !== debugHostFrameId) {
            owner.dataset.avatarFrame = debugHostFrameId;
        }

        const overlay = owner.querySelector(
            ":scope > .contributor-avatar-frame-overlay"
        );
        if (!overlay) return;

        const url = `/frames/${encodeURIComponent(debugHostFrameId)}.png`;
        if (overlay.getAttribute("src") !== url) {
            overlay.setAttribute("src", url);
        }
    };

    const applyInset = owner => {
        const frameId = String(owner.dataset.avatarFrame ?? "").trim();
        const overlay = owner.querySelector(
            ":scope > .contributor-avatar-frame-overlay"
        );
        if (!frameId || !overlay) {
            owner.style.removeProperty("--contributor-frame-media-inset");
            clearMediaClip(owner);
            return;
        }

        const naturalFrameSize = Math.min(
            overlay.naturalWidth || 0,
            overlay.naturalHeight || 0
        );
        const layoutFrameSize = Math.min(
            overlay.offsetWidth || 0,
            overlay.offsetHeight || 0
        );
        if (naturalFrameSize <= 0 || layoutFrameSize <= 0) return;

        const nativeInsetPixels = getNativeInsetPixels(owner, frameId);
        const scaledInsetPixels =
            nativeInsetPixels * layoutFrameSize / naturalFrameSize;
        const scaledInsetValue = `${scaledInsetPixels}px`;

        owner.style.setProperty(
            "--contributor-frame-media-inset",
            scaledInsetValue
        );

        const avatarMedia = owner.querySelector(
            ".contributor-frame-avatar-source"
        );
        avatarMedia?.style.setProperty(
            "--contributor-frame-scaled-avatar-inset",
            scaledInsetValue
        );

        applyMediaClip(owner, layoutFrameSize, scaledInsetPixels);
    };

    const observedOwners = new WeakSet();
    const observedOverlays = new WeakSet();
    const resizeObserver = typeof ResizeObserver === "function"
        ? new ResizeObserver(entries => {
            const owners = new Set();
            for (const entry of entries) {
                const owner = entry.target.matches?.(".contributor-frame-owner")
                    ? entry.target
                    : entry.target.closest?.(".contributor-frame-owner");
                if (owner) owners.add(owner);
            }
            for (const owner of owners) applyInset(owner);
        })
        : null;

    const observeAndApply = () => {
        applyDebugHostFrameSelection();

        for (const owner of document.querySelectorAll(".contributor-frame-owner")) {
            if (resizeObserver && !observedOwners.has(owner)) {
                resizeObserver.observe(owner);
                observedOwners.add(owner);
            }

            const overlay = owner.querySelector(
                ":scope > .contributor-avatar-frame-overlay"
            );
            if (resizeObserver && overlay && !observedOverlays.has(overlay)) {
                resizeObserver.observe(overlay);
                observedOverlays.add(overlay);
            }
            applyInset(owner);
        }
    };

    let refreshQueued = false;
    const queueRefresh = () => {
        if (refreshQueued) return;
        refreshQueued = true;
        window.requestAnimationFrame(() => {
            refreshQueued = false;
            observeAndApply();
        });
    };

    const getCurrentHostFrameId = () => {
        if (debugHostFrameId) return debugHostFrameId;
        return String(findHostFrameOwner()?.dataset.avatarFrame ?? "").trim();
    };

    const getCurrentHostFrameInset = () => {
        const frameId = getCurrentHostFrameId();
        if (!frameId) return null;

        return debugHostFrameInsets.get(frameId) ??
            getBaseNativeInsetPixels(frameId);
    };

    const updateDebugInsetValue = () => {
        if (body.dataset.debugMode !== "true") return;

        const value = document.querySelector(
            "[data-debug-host-frame-inset-value]"
        );
        if (!value) return;

        const insetPixels = getCurrentHostFrameInset();
        const text = Number.isFinite(insetPixels)
            ? `${insetPixels}px`
            : "—";
        if (value.textContent !== text) {
            value.textContent = text;
        }
    };

    const adjustHostFrameInset = delta => {
        if (!Number.isFinite(delta)) return;

        applyDebugHostFrameSelection();
        const owner = findHostFrameOwner();
        if (!owner) return;

        const frameId = getCurrentHostFrameId();
        if (!frameId) return;

        const currentInsetPixels =
            debugHostFrameInsets.get(frameId) ??
            getBaseNativeInsetPixels(frameId);
        debugHostFrameInsets.set(
            frameId,
            Math.max(0, currentInsetPixels + delta)
        );
        applyInset(owner);
        updateDebugInsetValue();
    };

    const cycleHostFrame = direction => {
        if (body.dataset.debugMode !== "true" ||
            !Number.isFinite(direction) ||
            direction === 0 ||
            frameIds.length === 0) {
            return;
        }

        const owner = findHostFrameOwner();
        if (!owner) return;

        const currentFrameId = getCurrentHostFrameId();
        const currentIndex = frameIds.indexOf(currentFrameId);
        const startIndex = currentIndex >= 0
            ? currentIndex
            : direction > 0 ? -1 : 0;
        const nextIndex =
            (startIndex + Math.sign(direction) + frameIds.length) % frameIds.length;

        debugHostFrameId = frameIds[nextIndex];
        applyDebugHostFrameSelection();
        applyInset(owner);
        updateDebugInsetValue();
        queueRefresh();
    };

    const resetHostCardScale = () => {
        const board = document.querySelector(
            ".host-game-board[data-game-code]"
        );
        const hostSlot = document.querySelector("[data-board-host-slot]");
        if (!board || !hostSlot) return;

        const gameCode = String(board.dataset.gameCode ?? "").trim();
        if (gameCode) {
            localStorage.removeItem(`badwolfquiz:${gameCode}:host-card-size`);
        }

        hostSlot.style.removeProperty("--board-host-width");
        hostSlot.style.removeProperty("--board-host-height");
        window.dispatchEvent(new Event("resize"));
    };

    const createDebugButton = (label, handler, icon = null) => {
        const button = document.createElement("button");
        button.className = "button button-secondary icon-button";
        button.type = "button";
        button.dataset.debugHostFrameTool = "true";
        button.title = label;
        button.setAttribute("aria-label", label);

        if (icon) {
            const span = document.createElement("span");
            span.setAttribute("aria-hidden", "true");
            span.textContent = icon;
            button.append(span);
        } else {
            button.textContent = label;
        }

        button.addEventListener("click", handler);
        return button;
    };

    const createDebugInsetValue = () => {
        const value = document.createElement("span");
        value.className = "button button-secondary";
        value.dataset.debugHostFrameInsetValue = "true";
        value.setAttribute("role", "status");
        value.setAttribute("aria-live", "polite");
        value.title = "Current avatar frame inset";
        value.textContent = "—";
        return value;
    };

    const installDebugControls = () => {
        if (body.dataset.debugMode !== "true") return;

        const board = document.querySelector(
            ".host-game-board[data-game-code]"
        );
        const header = document.querySelector(".game-header-context");
        if (!board || !header ||
            header.querySelector("[data-debug-host-frame-tool]")) {
            updateDebugInsetValue();
            return;
        }

        header.append(
            createDebugButton(
                "Reset host card to 100%",
                resetHostCardScale,
                "↻"
            ),
            createDebugButton("Previous frame", () => cycleHostFrame(-1), "▲"),
            createDebugButton("Next frame", () => cycleHostFrame(1), "▼"),
            createDebugButton("+5", () => adjustHostFrameInset(5)),
            createDebugButton("-5", () => adjustHostFrameInset(-5)),
            createDebugButton("+1", () => adjustHostFrameInset(1)),
            createDebugButton("-1", () => adjustHostFrameInset(-1)),
            createDebugInsetValue()
        );
        updateDebugInsetValue();
    };

    const mutationObserver = new MutationObserver(() => {
        applyDebugHostFrameSelection();
        queueRefresh();
        installDebugControls();
        updateDebugInsetValue();
    });
    mutationObserver.observe(document.body, {
        attributes: true,
        attributeFilter: [
            "class",
            "data-avatar-frame",
            "hidden",
            "src"
        ],
        childList: true,
        subtree: true
    });

    document.addEventListener("load", event => {
        if (!(event.target instanceof Element)) return;
        if (event.target.matches(".contributor-avatar-frame-overlay") ||
            frameMediaSelectors.some(selector => event.target.matches(selector))) {
            queueRefresh();
        }
    }, true);
    document.addEventListener("badwolf:host-shell-mounted", queueRefresh);
    document.addEventListener("badwolf:host-gameplay-updated", queueRefresh);
    window.addEventListener("resize", queueRefresh);

    observeAndApply();
    installDebugControls();
})();
