(() => {
    const eventName = "badwolf:buzzer-race";
    const transientSelector = "[data-host-gameplay-transient]";
    const collectingSelector = "[data-buzzer-race-collecting]";

    const createCollectingOverlay = () => {
        const overlay = document.createElement("aside");
        overlay.className = "buzzer-race-overlay buzzer-race-collecting-overlay";
        overlay.dataset.buzzerRaceCollecting = "";
        overlay.setAttribute("role", "status");
        overlay.setAttribute("aria-live", "polite");
        overlay.setAttribute("aria-busy", "true");

        const card = document.createElement("div");
        card.className = "buzzer-race-card buzzer-race-loading-card";

        const indicator = document.createElement("div");
        indicator.className = "buzzer-race-loading-indicator";
        indicator.setAttribute("aria-hidden", "true");
        indicator.innerHTML = `
            <span class="buzzer-race-loading-orbit buzzer-race-loading-orbit-outer"></span>
            <span class="buzzer-race-loading-orbit buzzer-race-loading-orbit-inner"></span>
            <span class="buzzer-race-loading-core"></span>`;

        card.append(indicator);
        overlay.append(card);
        return overlay;
    };

    const showCollectingOverlay = () => {
        const container = document.querySelector(transientSelector);
        if (!container) {
            return false;
        }

        if (container.querySelector(collectingSelector)) {
            return true;
        }

        container.replaceChildren(createCollectingOverlay());
        return true;
    };

    // Lobby dispatches this custom event after every buzzer-race payload.
    // Capture at window level so the collecting state can replace the normal
    // result renderer without changing the existing final result renderer.
    window.addEventListener(eventName, event => {
        if (!event.detail?.isCollecting) {
            return;
        }

        if (showCollectingOverlay()) {
            event.stopPropagation();
        }
    }, true);
})();
