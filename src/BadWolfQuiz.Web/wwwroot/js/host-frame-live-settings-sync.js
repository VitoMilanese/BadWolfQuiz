(() => {
    const body = document.body;
    if (!body) {
        return;
    }

    const hostCardSelector = "[data-host-card]";
    const hostFramePanelSelector = "[data-contributor-host-frame]";
    const hostCardMediaSelector = `${hostCardSelector} .host-card-media`;

    const synchronizeFrameControls = (allowDisabled = true) => {
        const hostCard = document.querySelector(hostCardSelector);
        if (!(hostCard instanceof HTMLElement)) {
            return;
        }

        const frameId = String(hostCard.dataset.avatarFrame ?? "").trim();
        const enabled = Boolean(frameId);
        if (!enabled && !allowDisabled) {
            return;
        }

        for (const panel of document.querySelectorAll(hostFramePanelSelector)) {
            const enabledInput = panel.querySelector(
                'input[type="checkbox"][name$=".HostAvatarFrameEnabled"]'
            );
            const frameInput = panel.querySelector("[data-contributor-frame-id]");
            if (!(enabledInput instanceof HTMLInputElement) ||
                !(frameInput instanceof HTMLInputElement)) {
                continue;
            }

            let changed = false;
            if (enabledInput.checked !== enabled) {
                enabledInput.checked = enabled;
                changed = true;
            }

            if (enabled && frameInput.value !== frameId) {
                frameInput.value = frameId;
                changed = true;
            }

            if (changed) {
                panel.dispatchEvent(new Event("change", { bubbles: true }));
            }
        }
    };

    let refreshQueued = false;
    let pendingAllowDisabled = false;
    const queueSynchronization = (allowDisabled = true) => {
        pendingAllowDisabled ||= allowDisabled;
        if (refreshQueued) {
            return;
        }

        refreshQueued = true;
        window.requestAnimationFrame(() => {
            const allowDisabledForRefresh = pendingAllowDisabled;
            pendingAllowDisabled = false;
            refreshQueued = false;
            synchronizeFrameControls(allowDisabledForRefresh);
        });
    };

    let hostFrameLayoutRefreshQueued = false;
    const queueHostFrameLayoutRefresh = () => {
        if (hostFrameLayoutRefreshQueued) {
            return;
        }

        hostFrameLayoutRefreshQueued = true;
        window.requestAnimationFrame(() => {
            window.requestAnimationFrame(() => {
                hostFrameLayoutRefreshQueued = false;
                if (!document.querySelector(hostCardSelector)) {
                    return;
                }

                // contributor-frames.js already owns frame positioning on resize.
                // Reuse that path after the persistent host card is relocated or
                // its live media becomes visible during restored-game rejoin flow.
                window.dispatchEvent(new Event("resize"));
            });
        });
    };

    const observer = new MutationObserver(records => {
        let shouldRefresh = false;
        let hostFrameStateChanged = false;
        let shouldRefreshHostFrameLayout = false;
        for (const record of records) {
            if (record.type === "attributes" &&
                record.target instanceof Element) {
                if (record.attributeName === "data-avatar-frame") {
                    shouldRefresh = true;
                    if (record.target.matches(hostCardSelector)) {
                        hostFrameStateChanged = true;
                    }
                }

                if (record.target.matches(hostCardMediaSelector) ||
                    record.target.matches(hostCardSelector)) {
                    shouldRefreshHostFrameLayout = true;
                }
            }

            if (record.type === "childList") {
                const changedNodes = [
                    ...record.addedNodes,
                    ...record.removedNodes
                ];
                if (changedNodes.some(node =>
                    node instanceof Element &&
                    (node.matches(".contributor-avatar-frame-overlay") ||
                     node.querySelector?.(".contributor-avatar-frame-overlay")))) {
                    shouldRefresh = true;
                    if (record.target instanceof Element &&
                        (record.target.matches(hostCardSelector) ||
                         record.target.closest(hostCardSelector))) {
                        hostFrameStateChanged = true;
                    }
                }

                const targetIsHostCard = record.target instanceof Element &&
                    (record.target.matches(hostCardSelector) ||
                     record.target.closest(hostCardSelector));
                const hostCardMoved = changedNodes.some(node =>
                    node instanceof Element &&
                    (node.matches(hostCardSelector) ||
                     node.querySelector?.(hostCardSelector)));
                const hostMediaChanged = changedNodes.some(node =>
                    node instanceof Element &&
                    (node.matches(".host-card-media") ||
                     node.querySelector?.(".host-card-media")));
                if (targetIsHostCard || hostCardMoved || hostMediaChanged) {
                    shouldRefreshHostFrameLayout = true;
                }
            }
        }

        if (shouldRefresh) {
            queueSynchronization(hostFrameStateChanged);
        }
        if (shouldRefreshHostFrameLayout) {
            queueHostFrameLayoutRefresh();
        }
    });

    observer.observe(body, {
        attributes: true,
        attributeFilter: ["data-avatar-frame", "hidden", "src"],
        childList: true,
        subtree: true
    });

    const synchronizeAfterHostGameplayChange = () => {
        queueSynchronization(false);
        queueHostFrameLayoutRefresh();
    };

    document.addEventListener(
        "badwolf:host-shell-mounted",
        synchronizeAfterHostGameplayChange);
    document.addEventListener(
        "badwolf:host-gameplay-updated",
        synchronizeAfterHostGameplayChange);

    // Do not overwrite the server-rendered disabled state on initial load. If a
    // frame is already active, however, make sure duplicated lobby/dialog forms
    // agree on the selected frame immediately. A second-frame layout pass also
    // covers restored games where the host card is relocated as players rejoin.
    queueSynchronization(false);
    queueHostFrameLayoutRefresh();
})();