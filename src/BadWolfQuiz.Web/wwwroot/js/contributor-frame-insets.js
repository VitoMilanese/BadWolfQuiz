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

        const nativeInsetPixels = frameInsets.get(frameId) ?? defaultInsetPixels;
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

    const mutationObserver = new MutationObserver(queueRefresh);
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
})();
