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

    const mediaSelectors = [
        ".player-avatar-current:not([hidden])",
        ".player-card-avatar:not([hidden])",
        ".player-list-avatar:not([hidden])",
        ".host-card-media video:not([hidden])",
        ".host-card-media iframe:not([hidden])",
        ".host-card-media img:not([hidden])",
        ".host-card-media:not([hidden])"
    ];

    const findVisibleMedia = owner => {
        for (const selector of mediaSelectors) {
            for (const media of owner.querySelectorAll(selector)) {
                if (getComputedStyle(media).display !== "none") {
                    return media;
                }
            }
        }
        return null;
    };

    const clearRuntimeMedia = owner => {
        for (const media of owner.querySelectorAll(
            ".contributor-frame-runtime-media"
        )) {
            media.classList.remove("contributor-frame-runtime-media");
            media.style.removeProperty("--contributor-frame-runtime-inset");
        }
    };

    const applyRuntimeMedia = owner => {
        if (!(owner instanceof HTMLElement)) return;

        const frameId = String(owner.dataset.avatarFrame ?? "").trim();
        const overlay = owner.querySelector(
            ":scope > .contributor-avatar-frame-overlay"
        );
        const media = findVisibleMedia(owner);
        if (!frameId || !(overlay instanceof HTMLImageElement) || !media) {
            clearRuntimeMedia(owner);
            return;
        }

        for (const staleMedia of owner.querySelectorAll(
            ".contributor-frame-runtime-media"
        )) {
            if (staleMedia !== media) {
                staleMedia.classList.remove("contributor-frame-runtime-media");
                staleMedia.style.removeProperty("--contributor-frame-runtime-inset");
            }
        }
        media.classList.add("contributor-frame-runtime-media");

        const naturalFrameSize = Math.min(
            overlay.naturalWidth || 0,
            overlay.naturalHeight || 0
        );
        const overlayRect = overlay.getBoundingClientRect();
        const renderedFrameSize = Math.min(
            overlayRect.width || 0,
            overlayRect.height || 0
        );
        if (naturalFrameSize <= 0 || renderedFrameSize <= 0) {
            return;
        }

        const nativeInsetPixels = frameInsets.get(frameId) ?? defaultInsetPixels;
        const scaledInsetPixels =
            nativeInsetPixels * renderedFrameSize / naturalFrameSize;
        const insetValue = `${scaledInsetPixels}px`;

        // Match the player frame-settings preview exactly: the suffix from the
        // PNG filename is measured at the frame's intrinsic size and scales
        // proportionally to the rendered frame size.
        media.style.setProperty(
            "--contributor-frame-runtime-inset",
            insetValue
        );
        owner.style.setProperty(
            "--contributor-frame-media-inset",
            insetValue
        );
    };

    const observedElements = new WeakSet();
    const resizeObserver = typeof ResizeObserver === "function"
        ? new ResizeObserver(entries => {
            const owners = new Set();
            for (const entry of entries) {
                const owner = entry.target.matches?.(".contributor-frame-owner")
                    ? entry.target
                    : entry.target.closest?.(".contributor-frame-owner");
                if (owner) owners.add(owner);
            }
            for (const owner of owners) {
                applyRuntimeMedia(owner);
            }
        })
        : null;

    const observeOwner = owner => {
        if (!resizeObserver) return;
        for (const element of [
            owner,
            owner.querySelector(":scope > .contributor-avatar-frame-overlay"),
            findVisibleMedia(owner)
        ]) {
            if (element && !observedElements.has(element)) {
                resizeObserver.observe(element);
                observedElements.add(element);
            }
        }
    };

    const applyAll = () => {
        for (const owner of document.querySelectorAll(
            ".contributor-frame-owner[data-avatar-frame]"
        )) {
            observeOwner(owner);
            applyRuntimeMedia(owner);
        }
    };

    let refreshQueued = false;
    const queueRefresh = () => {
        if (refreshQueued) return;
        refreshQueued = true;
        window.requestAnimationFrame(() => {
            refreshQueued = false;
            applyAll();
        });
    };

    new MutationObserver(queueRefresh).observe(body, {
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
        if (event.target instanceof Element &&
            (event.target.matches(".contributor-avatar-frame-overlay") ||
             mediaSelectors.some(selector => event.target.matches(selector)))) {
            queueRefresh();
        }
    }, true);
    document.addEventListener("badwolf:host-shell-mounted", queueRefresh);
    document.addEventListener("badwolf:host-gameplay-updated", queueRefresh);
    window.addEventListener("resize", queueRefresh);

    applyAll();
})();