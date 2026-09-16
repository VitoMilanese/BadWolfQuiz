(() => {
    "use strict";

    if (window.badWolfHostPanelClickRecoveryInitialized) {
        return;
    }
    window.badWolfHostPanelClickRecoveryInitialized = true;

    const panelSelector = [
        ".host-multiple-choice-panel",
        ".all-player-host-progress",
        ".peer-rated-host-sidebar",
        ".final-submission-list"
    ].join(",");
    const trackedButtonSelector = [
        ".host-multiple-choice-panel button:not([disabled])",
        ".all-player-host-progress button:not([disabled])",
        ".peer-rated-host-sidebar button:not([disabled])",
        ".final-submission-list button:not([disabled])"
    ].join(",");
    const maximumPointerDrift = 24;
    const replacementHitSlop = 12;

    let activePress = null;
    let recoveryHandle = 0;

    const normalizedText = value => (value ?? "")
        .replace(/\s+/g, " ")
        .trim();

    const closestButton = target => target instanceof Element
        ? target.closest("button")
        : null;

    const isTrackedButton = button =>
        button instanceof HTMLButtonElement &&
        !button.disabled &&
        button.closest(panelSelector) !== null;

    const fieldValue = (form, name) => {
        if (!(form instanceof HTMLFormElement)) {
            return "";
        }
        const field = form.elements.namedItem(name);
        return field instanceof HTMLInputElement ||
            field instanceof HTMLSelectElement ||
            field instanceof HTMLTextAreaElement
            ? field.value
            : "";
    };

    const handlerName = form => {
        if (!(form instanceof HTMLFormElement)) {
            return "";
        }
        try {
            return new URL(form.action, window.location.href)
                .searchParams.get("handler")
                ?.toLowerCase() ?? "";
        } catch {
            return "";
        }
    };

    const panelKind = button => {
        if (button.closest(".host-multiple-choice-panel")) {
            return "host-multiple-choice";
        }
        if (button.closest(".all-player-host-progress")) {
            return "all-player-progress";
        }
        if (button.closest(".peer-rated-host-sidebar")) {
            return "peer-rated";
        }
        if (button.closest(".final-submission-list")) {
            return "final-submission";
        }
        return "";
    };

    const rowIdentity = button => {
        const row = button.closest("li");
        if (!(row instanceof HTMLElement)) {
            return "";
        }

        const explicitId = row.dataset.playerId ||
            row.dataset.sidebarPlayerId ||
            row.dataset.buzzerRacePlayerId;
        if (explicitId) {
            return explicitId;
        }

        const name = row.querySelector(":scope > strong");
        return normalizedText(name?.textContent);
    };

    const actionSignature = button => {
        const form = button.form;
        return JSON.stringify({
            panel: panelKind(button),
            text: normalizedText(button.textContent),
            aria: normalizedText(button.getAttribute("aria-label")),
            title: normalizedText(button.getAttribute("title")),
            handler: handlerName(form),
            playerId: fieldValue(form, "playerId"),
            sourceQuestionId: fieldValue(form, "sourceQuestionId"),
            row: rowIdentity(button)
        });
    };

    const matchesPress = (button, press) =>
        isTrackedButton(button) &&
        actionSignature(button) === press.signature;

    const pointInside = (button, x, y) => {
        const rect = button.getBoundingClientRect();
        return x >= rect.left - replacementHitSlop &&
            x <= rect.right + replacementHitSlop &&
            y >= rect.top - replacementHitSlop &&
            y <= rect.bottom + replacementHitSlop;
    };

    const findReplacement = (press, event) => {
        const hit = closestButton(document.elementFromPoint(
            event.clientX,
            event.clientY));
        if (matchesPress(hit, press)) {
            return hit;
        }

        const matches = Array.from(document.querySelectorAll(
            trackedButtonSelector))
            .filter(button =>
                button instanceof HTMLButtonElement &&
                actionSignature(button) === press.signature);

        if (matches.length !== 1) {
            return null;
        }

        return pointInside(matches[0], event.clientX, event.clientY)
            ? matches[0]
            : null;
    };

    const clearActivePress = press => {
        if (press && activePress !== press) {
            return;
        }
        window.clearTimeout(recoveryHandle);
        recoveryHandle = 0;
        activePress = null;
    };

    window.addEventListener("pointerdown", event => {
        if (!event.isPrimary || event.button !== 0) {
            return;
        }

        const button = closestButton(event.target);
        if (!isTrackedButton(button)) {
            return;
        }

        window.clearTimeout(recoveryHandle);
        recoveryHandle = 0;
        activePress = {
            pointerId: event.pointerId,
            button,
            signature: actionSignature(button),
            startX: event.clientX,
            startY: event.clientY,
            clickDelivered: false
        };
    }, true);

    window.addEventListener("click", event => {
        const press = activePress;
        if (!press) {
            return;
        }

        const button = closestButton(event.target);
        if (button === press.button || matchesPress(button, press)) {
            press.clickDelivered = true;
        }
    }, true);

    window.addEventListener("pointerup", event => {
        const press = activePress;
        if (!press || press.pointerId !== event.pointerId) {
            return;
        }

        const drift = Math.hypot(
            event.clientX - press.startX,
            event.clientY - press.startY);
        if (drift > maximumPointerDrift) {
            clearActivePress(press);
            return;
        }

        // Native click is dispatched after pointerup. Wait one task before deciding
        // whether the original button disappeared between pointerdown and click.
        recoveryHandle = window.setTimeout(() => {
            recoveryHandle = 0;
            if (activePress !== press) {
                return;
            }
            if (press.clickDelivered || press.button.isConnected) {
                clearActivePress(press);
                return;
            }

            const replacement = findReplacement(press, event);
            clearActivePress(press);
            if (replacement instanceof HTMLButtonElement &&
                replacement.isConnected &&
                !replacement.disabled) {
                replacement.click();
            }
        }, 0);
    }, true);

    window.addEventListener("pointercancel", event => {
        if (activePress?.pointerId === event.pointerId) {
            clearActivePress(activePress);
        }
    }, true);

    window.addEventListener("blur", () => clearActivePress(activePress));
    document.addEventListener("visibilitychange", () => {
        if (document.hidden) {
            clearActivePress(activePress);
        }
    });
})();
