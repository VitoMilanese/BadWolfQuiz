(() => {
    const body = document.body;
    if (!body) {
        return;
    }

    const hostCardSelector = "[data-host-card]";
    const hostFramePanelSelector = "[data-contributor-host-frame]";

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

    const observer = new MutationObserver(records => {
        let shouldRefresh = false;
        let hostFrameStateChanged = false;
        for (const record of records) {
            if (record.type === "attributes" &&
                record.attributeName === "data-avatar-frame" &&
                record.target instanceof Element) {
                shouldRefresh = true;
                if (record.target.matches(hostCardSelector)) {
                    hostFrameStateChanged = true;
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
            }
        }

        if (shouldRefresh) {
            queueSynchronization(hostFrameStateChanged);
        }
    });

    observer.observe(body, {
        attributes: true,
        attributeFilter: ["data-avatar-frame"],
        childList: true,
        subtree: true
    });

    document.addEventListener(
        "badwolf:host-shell-mounted",
        () => queueSynchronization(false));
    document.addEventListener(
        "badwolf:host-gameplay-updated",
        () => queueSynchronization(false));

    // Do not overwrite the server-rendered disabled state on initial load. If a
    // frame is already active, however, make sure duplicated lobby/dialog forms
    // agree on the selected frame immediately.
    queueSynchronization(false);
})();