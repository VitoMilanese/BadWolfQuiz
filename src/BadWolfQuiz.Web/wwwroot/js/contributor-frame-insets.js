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
    const debugHostFrameInsets = new Map();

    const getBaseNativeInsetPixels = frameId =>
        frameInsets.get(frameId) ?? defaultInsetPixels;

    const getNativeInsetPixels = (owner, frameId) => {
        const baseInsetPixels = getBaseNativeInsetPixels(frameId);
        if (!owner.matches("[data-host-card]")) {
            return baseInsetPixels;
        }

        return debugHostFrameInsets.get(frameId) ?? baseInsetPixels;
    };

    const applyInset = owner => {
        const frameId = String(owner.dataset.avatarFrame ?? "").trim();
        const media = owner.querySelector(".contributor-frame-avatar-source");
        const overlay = owner.querySelector(
            ":scope > .contributor-avatar-frame-overlay"
        );
        if (!frameId || !media || !overlay) return;

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
        media.style.setProperty(
            "--contributor-frame-scaled-avatar-inset",
            `${scaledInsetPixels}px`
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

    const findHostFrameOwner = () => document.querySelector(
        "[data-host-card].contributor-frame-owner[data-avatar-frame]"
    );

    const getCurrentHostFrameInset = () => {
        const owner = findHostFrameOwner();
        if (!owner) return null;

        const frameId = String(owner.dataset.avatarFrame ?? "").trim();
        if (!frameId) return null;

        return getNativeInsetPixels(owner, frameId);
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

        const owner = findHostFrameOwner();
        if (!owner) return;

        const frameId = String(owner.dataset.avatarFrame ?? "").trim();
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

    const createDebugButton = (label, handler, icon = false) => {
        const button = document.createElement("button");
        button.className = "button button-secondary icon-button";
        button.type = "button";
        button.dataset.debugHostFrameTool = "true";
        button.title = label;
        button.setAttribute("aria-label", label);

        if (icon) {
            const span = document.createElement("span");
            span.setAttribute("aria-hidden", "true");
            span.textContent = "↻";
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
            createDebugButton("100%", resetHostCardScale, true),
            createDebugButton("+5", () => adjustHostFrameInset(5)),
            createDebugButton("-5", () => adjustHostFrameInset(-5)),
            createDebugButton("+1", () => adjustHostFrameInset(1)),
            createDebugButton("-1", () => adjustHostFrameInset(-1)),
            createDebugInsetValue()
        );
        updateDebugInsetValue();
    };

    const mutationObserver = new MutationObserver(() => {
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
