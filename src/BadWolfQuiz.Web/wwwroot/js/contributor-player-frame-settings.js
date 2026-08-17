(() => {
    const body = document.body;
    if (!body || body.dataset.contributorPlayer !== "true") {
        return;
    }

    const panelSelector = "[data-contributor-player-frame]";
    const sourceAvatarSelector =
        ".player-avatar-control [data-current-player-avatar]";
    const nativeInsets = new Map(
        String(body.dataset.contributorFrameNativeInsets ?? "")
            .split(";")
            .map(entry => entry.split(":"))
            .filter(parts => parts.length === 2)
            .map(([id, inset]) => [id, Number.parseFloat(inset)])
            .filter(([, inset]) => Number.isFinite(inset) && inset >= 0)
    );

    const getFrameIds = () =>
        Array.from(document.querySelectorAll("[data-contributor-frame-option]"))
            .map(option => String(
                option.dataset.contributorFrameOption ?? ""
            ).trim())
            .filter(Boolean);

    const frameUrl = frameId =>
        frameId ? `/frames/${encodeURIComponent(frameId)}.png` : "";

    const updateInset = (preview, overlay, frameId) => {
        if (overlay.hidden) {
            preview.style.setProperty(
                "--player-avatar-frame-preview-inset",
                "0px"
            );
            return;
        }

        const applyInset = () => {
            const naturalSize = Math.min(
                overlay.naturalWidth || 0,
                overlay.naturalHeight || 0
            );
            const renderedSize = Math.min(
                preview.clientWidth || 0,
                preview.clientHeight || 0
            );
            if (naturalSize <= 0 || renderedSize <= 0) {
                return;
            }

            const nativeInset = nativeInsets.get(frameId) ?? 10;
            preview.style.setProperty(
                "--player-avatar-frame-preview-inset",
                `${nativeInset * renderedSize / naturalSize}px`
            );
        };

        if (overlay.complete && overlay.naturalWidth > 0) {
            applyInset();
            return;
        }

        overlay.addEventListener("load", applyInset, { once: true });
    };

    const refreshPreview = panel => {
        const preview = panel.querySelector(
            "[data-player-avatar-frame-preview]"
        );
        const avatarPreview = preview?.querySelector(
            "[data-player-avatar-frame-preview-avatar]"
        );
        const framePreview = preview?.querySelector(
            "[data-contributor-frame-preview]"
        );
        const sourceAvatar = document.querySelector(sourceAvatarSelector);
        const enabled = panel.querySelector("[data-contributor-frame-enabled]");
        const frameInput = panel.querySelector("[data-contributor-frame-id]");
        if (!preview ||
            !(avatarPreview instanceof HTMLImageElement) ||
            !(framePreview instanceof HTMLImageElement) ||
            !(enabled instanceof HTMLInputElement) ||
            !(frameInput instanceof HTMLInputElement)) {
            return;
        }

        const avatarSource = String(
            sourceAvatar?.getAttribute("src") ?? ""
        ).trim();
        preview.hidden = !avatarSource;
        if (avatarSource && avatarPreview.getAttribute("src") !== avatarSource) {
            avatarPreview.src = avatarSource;
        }

        const frameId = String(frameInput.value ?? "").trim();
        const url = frameUrl(frameId);
        if (url && framePreview.getAttribute("src") !== url) {
            framePreview.src = url;
        }
        framePreview.hidden = !avatarSource || !enabled.checked || !frameId;
        updateInset(preview, framePreview, frameId);
    };

    const cycleFrame = (panel, direction) => {
        const frameInput = panel.querySelector("[data-contributor-frame-id]");
        if (!(frameInput instanceof HTMLInputElement)) {
            return;
        }

        const frameIds = getFrameIds();
        if (frameIds.length === 0) {
            return;
        }

        const currentId = String(frameInput.value ?? "").trim();
        const currentIndex = frameIds.indexOf(currentId);
        const startIndex = currentIndex >= 0
            ? currentIndex
            : direction > 0 ? -1 : 0;
        const nextIndex =
            (startIndex + Math.sign(direction) + frameIds.length) % frameIds.length;

        frameInput.value = frameIds[nextIndex];
        frameInput.dispatchEvent(new Event("change", { bubbles: true }));
    };

    const createCycleButton = (panel, opener, direction, icon) => {
        const button = document.createElement("button");
        const label = opener.textContent?.trim() || "Frame";
        button.type = "button";
        button.className =
            "button button-secondary icon-button contributor-frame-cycle-button";
        button.dataset.contributorPlayerFrameCycle = direction.toString();
        button.title = `${label} ${icon}`;
        button.setAttribute("aria-label", `${label} ${icon}`);

        const symbol = document.createElement("span");
        symbol.setAttribute("aria-hidden", "true");
        symbol.textContent = icon;
        button.append(symbol);
        button.addEventListener("click", () => cycleFrame(panel, direction));
        return button;
    };

    const enhancePanel = panel => {
        if (!(panel instanceof HTMLElement) ||
            panel.dataset.playerAvatarFramePreviewEnhanced === "true") {
            return;
        }

        const row = panel.querySelector(".contributor-frame-choice-row");
        const framePreview = panel.querySelector("[data-contributor-frame-preview]");
        const opener = panel.querySelector("[data-open-contributor-frame-picker]");
        if (!row || !(framePreview instanceof HTMLImageElement) || !opener) {
            return;
        }

        panel.classList.add("player-contributor-frame-settings");

        const preview = document.createElement("span");
        preview.className = "player-avatar-frame-preview";
        preview.dataset.playerAvatarFramePreview = "true";

        const avatarPreview = document.createElement("img");
        avatarPreview.className = "player-avatar-frame-preview-avatar";
        avatarPreview.dataset.playerAvatarFramePreviewAvatar = "true";
        avatarPreview.alt = "";
        preview.append(avatarPreview);

        framePreview.classList.remove("contributor-frame-choice-preview");
        framePreview.classList.add("player-avatar-frame-preview-overlay");
        preview.append(framePreview);
        row.prepend(preview);

        if (!row.querySelector("[data-contributor-player-frame-cycle]")) {
            opener.after(
                createCycleButton(panel, opener, -1, "\u25c0"),
                createCycleButton(panel, opener, 1, "\u25b6")
            );
        }

        panel.dataset.playerAvatarFramePreviewEnhanced = "true";
        panel.addEventListener("change", () => refreshPreview(panel));

        const sourceAvatar = document.querySelector(sourceAvatarSelector);
        if (sourceAvatar) {
            new MutationObserver(() => refreshPreview(panel)).observe(
                sourceAvatar,
                {
                    attributes: true,
                    attributeFilter: ["src"]
                }
            );
        }

        if (typeof ResizeObserver === "function") {
            new ResizeObserver(() => refreshPreview(panel)).observe(preview);
        }

        refreshPreview(panel);
    };

    const enhancePanels = () => {
        for (const panel of document.querySelectorAll(panelSelector)) {
            enhancePanel(panel);
        }
    };

    enhancePanels();
    new MutationObserver(enhancePanels).observe(body, {
        childList: true,
        subtree: true
    });
})();
