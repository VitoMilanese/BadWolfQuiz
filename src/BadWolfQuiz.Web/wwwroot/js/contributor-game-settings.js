(() => {
    const body = document.body;
    if (!body) {
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
        if (!pendingStateKey || !state) {
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

    const bridge = window.BadWolfContributorGameFrameBridge ?? {};
    const handlers = bridge.handlers instanceof Set
        ? bridge.handlers
        : new Set();
    if (typeof bridge.handler === "function") {
        handlers.add(bridge.handler);
    }
    bridge.handlers = handlers;
    bridge.pendingUpdate = bridge.pendingUpdate ?? null;
    window.BadWolfContributorGameFrameBridge = bridge;

    const flushPendingUpdate = () => {
        if (!bridge.pendingUpdate || bridge.handlers.size === 0) {
            return;
        }

        const update = bridge.pendingUpdate;
        bridge.pendingUpdate = null;
        for (const handler of bridge.handlers) {
            try {
                handler(update);
            } catch (error) {
                console.error("Failed to apply host frame state.", error);
            }
        }
    };

    bridge.registerHandler = handler => {
        if (typeof handler !== "function") {
            return;
        }
        bridge.handlers.add(handler);
        flushPendingUpdate();
    };

    bridge.publish = state => {
        const value = writeBodyState(state);
        bridge.pendingUpdate = value;
        flushPendingUpdate();
        return value;
    };

    const wrapConnectionOn = connection => {
        if (!connection ||
            typeof connection.on !== "function" ||
            connection.badWolfContributorFrameBridgePatched) {
            return;
        }

        const originalOn = connection.on.bind(connection);
        connection.on = (methodName, handler) => {
            if (methodName === "HostContributorFrameChanged") {
                bridge.registerHandler(handler);
            }
            return originalOn(methodName, handler);
        };
        Object.defineProperty(
            connection,
            "badWolfContributorFrameBridgePatched",
            { value: true }
        );
    };

    const installSignalRBridge = () => {
        let installed = false;
        const connectionPrototype = window.signalR?.HubConnection?.prototype;
        if (connectionPrototype &&
            typeof connectionPrototype.on === "function" &&
            !connectionPrototype.badWolfContributorFrameBridgePatched) {
            const originalOn = connectionPrototype.on;
            connectionPrototype.on = function (methodName, handler) {
                if (methodName === "HostContributorFrameChanged") {
                    bridge.registerHandler(handler);
                }
                return originalOn.call(this, methodName, handler);
            };
            Object.defineProperty(
                connectionPrototype,
                "badWolfContributorFrameBridgePatched",
                { value: true }
            );
            installed = true;
        }

        const builderPrototype = window.signalR?.HubConnectionBuilder?.prototype;
        if (builderPrototype &&
            typeof builderPrototype.build === "function" &&
            !builderPrototype.badWolfContributorFrameBridgePatched) {
            const originalBuild = builderPrototype.build;
            builderPrototype.build = function (...args) {
                const connection = originalBuild.apply(this, args);
                wrapConnectionOn(connection);
                return connection;
            };
            Object.defineProperty(
                builderPrototype,
                "badWolfContributorFrameBridgePatched",
                { value: true }
            );
            installed = true;
        }

        return installed;
    };

    installSignalRBridge();

    const pendingState = loadPendingState();
    if (pendingState) {
        bridge.publish(pendingState);
    }

    const startGameForm = document.getElementById("start-game-form");
    const publishStartFrameState = () => {
        if (!(startGameForm instanceof HTMLFormElement)) {
            return;
        }

        const state = readFormState(startGameForm);
        if (!state) {
            return;
        }

        savePendingState(state);
        bridge.publish(state);
    };

    startGameForm?.addEventListener(
        "submit",
        publishStartFrameState,
        true
    );
    document.querySelector(
        '.lobby-start-button[form="start-game-form"]'
    )?.addEventListener(
        "click",
        publishStartFrameState,
        true
    );

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

            if (nextFrameState) {
                savePendingState(nextFrameState);
                bridge.publish(nextFrameState);
            }
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