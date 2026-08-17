(() => {
    const body = document.body;
    if (!body || body.dataset.contributorHost !== "true") {
        return;
    }

    const gameRoot = document.querySelector(
        ".host-game-board[data-game-code], .content-panel[data-game-code]"
    );
    const gameCode = String(gameRoot?.dataset.gameCode ?? "").trim();
    const pendingStateKey = gameCode
        ? `badwolfquiz:${gameCode}:pending-host-frame`
        : null;
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

    const normalizeState = value => ({
        enabled: value?.enabled === true,
        frameId: normalizeFrameId(value?.frameId)
    });

    const readBodyState = () => normalizeState({
        enabled: body.dataset.contributorHostFrameEnabled === "true",
        frameId: body.dataset.contributorHostFrameId
    });

    const writeBodyState = state => {
        const value = normalizeState(state);
        body.dataset.contributorHostFrameEnabled = value.enabled.toString();
        body.dataset.contributorHostFrameId = value.frameId;
        return value;
    };

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

        return normalizeState({
            enabled: enabled.checked,
            frameId: frameId.value
        });
    };

    const loadPendingState = () => {
        if (!pendingStateKey) {
            return null;
        }

        const serialized = sessionStorage.getItem(pendingStateKey);
        if (!serialized) {
            return null;
        }

        sessionStorage.removeItem(pendingStateKey);
        try {
            const value = JSON.parse(serialized);
            if (!value ||
                !Number.isFinite(value.savedAt) ||
                Date.now() - value.savedAt > 5 * 60 * 1000) {
                return null;
            }
            return normalizeState(value);
        } catch {
            return null;
        }
    };

    const savePendingState = state => {
        if (!pendingStateKey) {
            return;
        }

        const value = normalizeState(state);
        sessionStorage.setItem(
            pendingStateKey,
            JSON.stringify({
                ...value,
                savedAt: Date.now()
            })
        );
    };

    const bridge = window.BadWolfContributorGameFrameBridge ?? {
        handler: null,
        pendingUpdate: null
    };
    window.BadWolfContributorGameFrameBridge = bridge;

    bridge.publish = state => {
        const value = writeBodyState(state);
        if (typeof bridge.handler === "function") {
            bridge.handler(value);
        } else {
            bridge.pendingUpdate = value;
        }
    };

    const pendingState = loadPendingState();
    if (pendingState) {
        writeBodyState(pendingState);
        bridge.pendingUpdate = pendingState;
    }

    const installSignalRBridge = () => {
        const prototype = window.signalR?.HubConnectionBuilder?.prototype;
        if (!prototype || prototype.badWolfContributorFrameBridgePatched) {
            return false;
        }

        const originalBuild = prototype.build;
        prototype.build = function (...args) {
            const connection = originalBuild.apply(this, args);
            const originalOn = connection.on.bind(connection);

            connection.on = function (methodName, handler) {
                if (methodName === "HostContributorFrameChanged" &&
                    typeof handler === "function") {
                    bridge.handler = handler;
                    if (bridge.pendingUpdate) {
                        const update = bridge.pendingUpdate;
                        bridge.pendingUpdate = null;
                        queueMicrotask(() => {
                            if (bridge.handler === handler) {
                                handler(update);
                            }
                        });
                    }
                }
                return originalOn(methodName, handler);
            };

            return connection;
        };

        Object.defineProperty(
            prototype,
            "badWolfContributorFrameBridgePatched",
            {
                value: true,
                configurable: false,
                enumerable: false,
                writable: false
            }
        );
        return true;
    };

    if (!installSignalRBridge()) {
        window.addEventListener(
            "DOMContentLoaded",
            installSignalRBridge,
            { once: true }
        );
    }

    const handlerName = (form, submitter) => {
        try {
            const target = submitter?.formAction || form.action || window.location.href;
            return new URL(target, window.location.href)
                .searchParams
                .get("handler")
                ?.toLowerCase() ?? "";
        } catch {
            return "";
        }
    };

    document.addEventListener("submit", event => {
        const form = event.target instanceof HTMLFormElement
            ? event.target
            : null;
        if (!form || handlerName(form, event.submitter) !== "start") {
            return;
        }

        const state = readFormState(form);
        if (!state) {
            return;
        }

        savePendingState(state);
        bridge.publish(state);
    }, true);

    const dialog = document.getElementById("game-settings-dialog");
    const form = dialog?.querySelector("form");
    if (!dialog || !(form instanceof HTMLFormElement)) {
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

    form.addEventListener("submit", async event => {
        event.preventDefault();

        const submitter = event.submitter instanceof HTMLButtonElement
            ? event.submitter
            : null;
        const nextFrameState = readFormState(form) ?? readBodyState();
        submitter?.setAttribute("disabled", "disabled");

        try {
            const response = await fetch(
                submitter?.formAction || form.action || window.location.href,
                {
                    method: "POST",
                    body: new FormData(form),
                    headers: {
                        "X-Requested-With": "XMLHttpRequest"
                    },
                    redirect: "follow"
                }
            );
            const markup = await response.text();
            if (!response.ok) {
                throw new Error(response.statusText);
            }

            const errorMessage = findResponseError(markup);
            if (errorMessage) {
                throw new Error(errorMessage);
            }

            bridge.publish(nextFrameState);
            savePendingState(nextFrameState);
            dialog.close();

            if (window.BadWolfHostGameplay?.refresh) {
                await window.BadWolfHostGameplay.refresh();
            }
        } catch (error) {
            console.error("Failed to update game settings.", error);
            window.alert(error?.message || body.dataset.contributorFrameSaveFailed || "");
        } finally {
            submitter?.removeAttribute("disabled");
        }
    }, true);
})();
