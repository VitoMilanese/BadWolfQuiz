(() => {
    const body = document.body;
    if (!body) {
        return;
    }

    const defaultFrameId = String(
        body.dataset.contributorFrameDefaultId ?? ""
    ).trim();
    const availableFrameIds = new Set(
        Array.from(document.querySelectorAll("[data-contributor-frame-option]"))
            .map(option => String(
                option.dataset.contributorFrameOption ?? ""
            ).trim())
            .filter(Boolean)
    );

    const normalizeFrameId = value => {
        const frameId = String(value ?? "").trim();
        if (!frameId) {
            return defaultFrameId;
        }
        if (availableFrameIds.size > 0 && !availableFrameIds.has(frameId)) {
            return defaultFrameId;
        }
        return frameId;
    };

    const frameNativeInsets = new Map(
        String(body.dataset.contributorFrameNativeInsets ?? "")
            .split(";")
            .map(entry => entry.split(":"))
            .filter(parts => parts.length === 2)
            .map(([id, inset]) => [id, Number.parseFloat(inset)])
            .filter(([, inset]) => Number.isFinite(inset) && inset >= 0)
    );

    const getFrameOptions = () =>
        Array.from(document.querySelectorAll("[data-contributor-frame-option]"))
            .map(option => ({
                id: String(option.dataset.contributorFrameOption ?? "").trim(),
                url: String(option.dataset.contributorFrameUrl ?? "").trim()
            }))
            .filter(option => option.id);

    const getFrameUrl = frameId => {
        const normalizedId = normalizeFrameId(frameId);
        if (!normalizedId) {
            return "";
        }

        const option = getFrameOptions().find(item => item.id === normalizedId);
        return option?.url || `/frames/${encodeURIComponent(normalizedId)}.png`;
    };

    const updateCombinedPreviewInset = (preview, overlay, frameId) => {
        if (overlay.hidden) {
            preview.style.setProperty("--host-avatar-frame-preview-inset", "0px");
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

            const nativeInset = frameNativeInsets.get(frameId) ?? 10;
            const scaledInset = nativeInset * renderedSize / naturalSize;
            preview.style.setProperty(
                "--host-avatar-frame-preview-inset",
                `${scaledInset}px`
            );
        };

        if (overlay.complete && overlay.naturalWidth > 0) {
            applyInset();
            return;
        }

        overlay.addEventListener("load", applyInset, { once: true });
    };

    const refreshCombinedPreview = panel => {
        const row = panel.closest("[data-host-avatar-frame-settings-row]");
        const preview = row?.querySelector("[data-host-avatar-frame-preview]");
        const avatar = preview?.querySelector("[data-host-avatar-preview]");
        const overlay = preview?.querySelector("[data-contributor-frame-preview]");
        const enabled = panel.querySelector('input[type="checkbox"]');
        const frameInput = panel.querySelector("[data-contributor-frame-id]");
        if (!preview || !avatar || !overlay || !enabled || !frameInput) {
            return;
        }

        const avatarSource = String(avatar.getAttribute("src") ?? "").trim();
        const hasAvatar = Boolean(avatarSource) && !avatar.hidden;
        preview.hidden = !hasAvatar;

        const frameId = normalizeFrameId(frameInput.value);
        const frameUrl = getFrameUrl(frameId);
        if (frameUrl && overlay.getAttribute("src") !== frameUrl) {
            overlay.src = frameUrl;
        }

        overlay.hidden = !hasAvatar || !enabled.checked || !frameId;
        updateCombinedPreviewInset(preview, overlay, frameId);
    };

    const cyclePanelFrame = (panel, direction) => {
        const frameInput = panel.querySelector("[data-contributor-frame-id]");
        if (!(frameInput instanceof HTMLInputElement)) {
            return;
        }

        const options = getFrameOptions();
        if (options.length === 0) {
            return;
        }

        const currentId = normalizeFrameId(frameInput.value);
        const currentIndex = options.findIndex(option => option.id === currentId);
        const startIndex = currentIndex >= 0
            ? currentIndex
            : direction > 0 ? -1 : 0;
        const nextIndex =
            (startIndex + Math.sign(direction) + options.length) % options.length;
        const next = options[nextIndex];

        frameInput.value = next.id;
        frameInput.dispatchEvent(new Event("change", { bubbles: true }));
    };

    const createCycleButton = (panel, opener, direction, icon) => {
        const button = document.createElement("button");
        const frameLabel = opener.textContent?.trim() || "Frame";
        button.type = "button";
        button.className =
            "button button-secondary icon-button contributor-frame-cycle-button";
        button.dataset.contributorFrameCycle = direction.toString();
        button.title = `${frameLabel} ${icon}`;
        button.setAttribute("aria-label", `${frameLabel} ${icon}`);
        const symbol = document.createElement("span");
        symbol.setAttribute("aria-hidden", "true");
        symbol.textContent = icon;
        button.append(symbol);
        button.addEventListener("click", () => cyclePanelFrame(panel, direction));
        return button;
    };

    const enhanceHostFramePanel = panel => {
        if (!(panel instanceof HTMLElement) ||
            panel.dataset.hostAvatarFrameSettingsEnhanced === "true") {
            return;
        }

        const grid = panel.closest(".settings-grid");
        const avatarField = grid?.querySelector(".host-avatar-field");
        const avatarInputRow = avatarField?.querySelector(".host-avatar-input-row");
        const avatarPreview = avatarField?.querySelector("[data-host-avatar-preview]");
        const framePreview = panel.querySelector("[data-contributor-frame-preview]");
        const opener = panel.querySelector("[data-open-contributor-frame-picker]");
        if (!grid || !avatarField || !avatarInputRow || !avatarPreview ||
            !framePreview || !opener) {
            return;
        }

        let row = avatarField.closest("[data-host-avatar-frame-settings-row]");
        if (!row) {
            row = document.createElement("div");
            row.className = "host-avatar-frame-settings-row";
            row.dataset.hostAvatarFrameSettingsRow = "true";
            avatarField.before(row);
            row.append(avatarField);
        }
        if (panel.parentElement !== row) {
            row.append(panel);
        }

        let combinedPreview = avatarField.querySelector(
            "[data-host-avatar-frame-preview]"
        );
        if (!combinedPreview) {
            combinedPreview = document.createElement("span");
            combinedPreview.className = "host-avatar-frame-preview";
            combinedPreview.dataset.hostAvatarFramePreview = "true";
            avatarInputRow.insertAdjacentElement("afterend", combinedPreview);
        }

        if (avatarPreview.parentElement !== combinedPreview) {
            combinedPreview.append(avatarPreview);
        }
        framePreview.classList.remove("contributor-frame-choice-preview");
        framePreview.classList.add("host-avatar-frame-preview-overlay");
        if (framePreview.parentElement !== combinedPreview) {
            combinedPreview.append(framePreview);
        }

        const actionRow = opener.closest(".contributor-frame-choice-row");
        if (actionRow &&
            !actionRow.querySelector("[data-contributor-frame-cycle]")) {
            opener.after(
                createCycleButton(panel, opener, -1, "◀"),
                createCycleButton(panel, opener, 1, "▶")
            );
        }

        panel.dataset.hostAvatarFrameSettingsEnhanced = "true";
        panel.addEventListener("change", () => refreshCombinedPreview(panel));
        avatarPreview.addEventListener("load", () => refreshCombinedPreview(panel));

        new MutationObserver(() => refreshCombinedPreview(panel)).observe(
            avatarPreview,
            {
                attributes: true,
                attributeFilter: ["src", "hidden"]
            }
        );

        if (typeof ResizeObserver === "function") {
            new ResizeObserver(() => refreshCombinedPreview(panel))
                .observe(combinedPreview);
        }

        refreshCombinedPreview(panel);
    };

    const enhanceHostFramePanels = () => {
        for (const panel of document.querySelectorAll(
            "[data-contributor-host-frame]"
        )) {
            enhanceHostFramePanel(panel);
        }
    };

    enhanceHostFramePanels();
    const initialHostFramePanels = Array.from(document.querySelectorAll(
        "[data-contributor-host-frame]"
    ));
    if (initialHostFramePanels.length === 0 ||
        initialHostFramePanels.some(panel =>
            panel.dataset.hostAvatarFrameSettingsEnhanced !== "true")) {
        const hostFramePanelObserver = new MutationObserver(() => {
            enhanceHostFramePanels();
            const panels = Array.from(document.querySelectorAll(
                "[data-contributor-host-frame]"
            ));
            if (panels.length > 0 &&
                panels.every(panel =>
                    panel.dataset.hostAvatarFrameSettingsEnhanced === "true")) {
                hostFramePanelObserver.disconnect();
            }
        });
        hostFramePanelObserver.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    const readFormState = form => {
        const panel = form?.querySelector("[data-contributor-host-frame]");
        if (!panel) {
            return null;
        }

        const enabled = panel.querySelector(
            'input[type="checkbox"][name="SettingsInput.HostAvatarFrameEnabled"]'
        );
        const frameId = panel.querySelector("[data-contributor-frame-id]");
        if (!(enabled instanceof HTMLInputElement) ||
            !(frameId instanceof HTMLInputElement)) {
            return null;
        }

        return {
            enabled: enabled.checked,
            frameId: normalizeFrameId(frameId.value)
        };
    };

    const resolveGameId = () => {
        const match = window.location.pathname.match(
            /\/Admin\/Games\/Lobby\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\/?$/i
        );
        return match?.[1] ?? "";
    };

    const syncHostFrame = async (state, sourceForm) => {
        if (!state || body.dataset.contributorHost !== "true") {
            return;
        }

        const gameId = resolveGameId();
        if (!gameId) {
            throw new Error("Unable to resolve the current game identifier.");
        }

        const token = sourceForm?.querySelector(
            'input[name="__RequestVerificationToken"]'
        )?.value;
        const formData = new FormData();
        formData.append("gameId", gameId);
        formData.append("enabled", state.enabled.toString());
        formData.append("frameId", state.frameId);
        if (token) {
            formData.append("__RequestVerificationToken", token);
        }

        const response = await fetch("/ContributorFrames?handler=HostFrame", {
            method: "POST",
            body: formData,
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            }
        });
        if (!response.ok) {
            throw new Error(response.statusText);
        }
    };

    const preloadedFrameUrls = new Set();
    const preloadFrame = state => {
        if (!state?.enabled || !state.frameId) {
            return;
        }

        const url = `/frames/${encodeURIComponent(state.frameId)}.png`;
        if (preloadedFrameUrls.has(url)) {
            return;
        }

        preloadedFrameUrls.add(url);
        const image = new Image();
        image.decoding = "async";
        image.src = url;
    };

    const startGameForm = document.getElementById("start-game-form");
    const redundantSaveActions = startGameForm?.querySelector(
        ":scope > .form-actions"
    );
    redundantSaveActions?.remove();

    const lobbyFramePanel = startGameForm?.querySelector(
        "[data-contributor-host-frame]"
    );
    lobbyFramePanel?.addEventListener("change", () => {
        preloadFrame(readFormState(startGameForm));
    });
    preloadFrame(readFormState(startGameForm));

    const dialog = document.getElementById("game-settings-dialog");
    const settingsForm = dialog?.querySelector("form");
    if (!dialog || !(settingsForm instanceof HTMLFormElement)) {
        return;
    }

    const findResponseError = markup => {
        if (!markup) {
            return null;
        }

        const parsed = new DOMParser().parseFromString(markup, "text/html");
        for (const message of parsed.querySelectorAll(".message-error")) {
            if (message.hasAttribute("hidden")) {
                continue;
            }
            const text = message.textContent?.trim();
            if (text) {
                return text;
            }
        }
        return null;
    };

    settingsForm.addEventListener("submit", async event => {
        event.preventDefault();

        const submitter = event.submitter instanceof HTMLButtonElement
            ? event.submitter
            : null;
        const nextFrameState = readFormState(settingsForm);
        submitter?.setAttribute("disabled", "disabled");

        try {
            const target = new URL(window.location.href);
            target.searchParams.set("handler", "UpdateSettings");
            target.hash = "";
            const response = await fetch(target.toString(), {
                method: "POST",
                body: new FormData(settingsForm),
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                redirect: "follow"
            });
            const markup = await response.text();
            if (!response.ok) {
                throw new Error(response.statusText);
            }

            const errorMessage = findResponseError(markup);
            if (errorMessage) {
                throw new Error(errorMessage);
            }

            preloadFrame(nextFrameState);
            await syncHostFrame(nextFrameState, settingsForm);
            dialog.close();

            if (window.BadWolfHostGameplay?.refresh) {
                await window.BadWolfHostGameplay.refresh();
            }
        } catch (error) {
            console.error("Failed to update game settings.", error);
            window.alert(
                error?.message ||
                body.dataset.contributorFrameSaveFailed ||
                ""
            );
        } finally {
            submitter?.removeAttribute("disabled");
        }
    }, true);
})();
