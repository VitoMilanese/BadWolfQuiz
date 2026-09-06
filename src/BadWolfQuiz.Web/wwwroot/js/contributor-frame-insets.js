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

    const getBaseNativeInsetPixels = frameId =>
        frameInsets.get(frameId) ?? defaultInsetPixels;

    const getNativeInsetPixels = (owner, frameId) => {
        const baseInsetPixels = getBaseNativeInsetPixels(frameId);
        if (!owner.matches("[data-host-card]")) {
            return baseInsetPixels;
        }

        return debugHostFrameInsets.get(frameId) ?? baseInsetPixels;
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

        // Keep non-avatar media and built-in avatars on the exact same opening.
        // The native inset comes from the frame filename (for example 3-96.png).
        owner.style.setProperty(
            "--contributor-frame-media-inset",
            scaledInsetValue
        );

        const media = owner.querySelector(".contributor-frame-avatar-source");
        media?.style.setProperty(
            "--contributor-frame-scaled-avatar-inset",
            scaledInsetValue
        );
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
        attributeFilter: ["class", "data-avatar-frame", "hidden"],
        childList: true,
        subtree: true
    });

    document.addEventListener("load", event => {
        if (event.target instanceof Element &&
            event.target.matches(".contributor-avatar-frame-overlay")) {
            queueRefresh();
        }
    }, true);
    window.addEventListener("resize", queueRefresh);

    observeAndApply();
    installDebugControls();
})();