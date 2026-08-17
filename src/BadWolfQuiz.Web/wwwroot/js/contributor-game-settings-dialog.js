(() => {
    const dialogSelector = "#game-settings-dialog";
    let activeFramePanel = null;

    const frameIds = Array.from(new Set(
        String(document.body?.dataset.contributorFrameNativeInsets ?? "")
            .split(";")
            .map(entry => entry.split(":", 1)[0]?.trim())
            .filter(Boolean)
    ));

    const frameUrl = frameId =>
        frameId ? `/frames/${encodeURIComponent(frameId)}.png` : "";

    const setPanelFrame = (panel, frameId, url = "") => {
        const input = panel?.querySelector("[data-contributor-frame-id]");
        if (!(input instanceof HTMLInputElement) || !frameId) {
            return;
        }

        input.value = frameId;
        const row = panel.closest("[data-host-avatar-frame-settings-row]");
        const preview = row?.querySelector("[data-contributor-frame-preview]") ??
            panel.querySelector("[data-contributor-frame-preview]");
        if (preview instanceof HTMLImageElement) {
            preview.src = url || frameUrl(frameId);
        }
        input.dispatchEvent(new Event("change", { bubbles: true }));
    };

    const cyclePanelFrame = (panel, direction) => {
        if (frameIds.length === 0) {
            return;
        }

        const input = panel?.querySelector("[data-contributor-frame-id]");
        if (!(input instanceof HTMLInputElement)) {
            return;
        }

        const currentId = String(input.value ?? "").trim();
        const currentIndex = frameIds.indexOf(currentId);
        const startIndex = currentIndex >= 0
            ? currentIndex
            : direction > 0 ? -1 : 0;
        const nextIndex =
            (startIndex + Math.sign(direction) + frameIds.length) % frameIds.length;
        setPanelFrame(panel, frameIds[nextIndex]);
    };

    const loadPickerThumbnails = picker => {
        for (const image of picker.querySelectorAll(
            "img[data-contributor-frame-thumbnail-src]")) {
            if (!image.getAttribute("src")) {
                image.src = image.dataset.contributorFrameThumbnailSrc ?? "";
            }
        }
    };

    const openFramePicker = (panel, picker) => {
        activeFramePanel = panel;
        loadPickerThumbnails(picker);

        const selectedId = String(
            panel.querySelector("[data-contributor-frame-id]")?.value ?? ""
        ).trim();
        for (const option of picker.querySelectorAll(
            "[data-contributor-frame-option]")) {
            option.classList.toggle(
                "is-selected",
                option.dataset.contributorFrameOption === selectedId
            );
        }

        picker.addEventListener("close", () => {
            activeFramePanel = null;
        }, { once: true });

        if (!picker.open) {
            picker.showModal();
        }
    };

    document.addEventListener("click", event => {
        const target = event.target instanceof Element ? event.target : null;
        const dialog = target?.closest(dialogSelector);
        if (!dialog) {
            return;
        }

        const cycleButton = target.closest("[data-contributor-frame-cycle]");
        if (cycleButton) {
            const panel = cycleButton.closest("[data-contributor-host-frame]");
            const direction = Number.parseInt(
                cycleButton.dataset.contributorFrameCycle ?? "0",
                10
            );
            if (panel && direction !== 0) {
                event.preventDefault();
                event.stopImmediatePropagation();
                cyclePanelFrame(panel, direction);
            }
            return;
        }

        const opener = target.closest("[data-open-contributor-frame-picker]");
        if (!opener) {
            return;
        }

        const panel = opener.closest("[data-contributor-host-frame]");
        const picker = document.querySelector("[data-contributor-frame-picker]");
        if (!panel || !(picker instanceof HTMLDialogElement)) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        openFramePicker(panel, picker);
    }, true);

    document.addEventListener("click", event => {
        if (!activeFramePanel) {
            return;
        }

        const target = event.target instanceof Element ? event.target : null;
        const option = target?.closest("[data-contributor-frame-option]");
        const picker = option?.closest("[data-contributor-frame-picker]");
        if (!option || !(picker instanceof HTMLDialogElement) || !picker.open) {
            return;
        }

        const frameId = String(option.dataset.contributorFrameOption ?? "").trim();
        if (!frameId) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        const panel = activeFramePanel;
        activeFramePanel = null;
        setPanelFrame(
            panel,
            frameId,
            String(option.dataset.contributorFrameUrl ?? "").trim()
        );
        picker.close();
    }, true);
})();
