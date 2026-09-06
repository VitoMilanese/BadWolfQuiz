(() => {
    const install = signalR => {
        if (window.badWolfHostPlayerVisualContractInstalled) {
            return true;
        }

        const builderPrototype = signalR?.HubConnectionBuilder?.prototype;
        if (!builderPrototype || typeof builderPrototype.build !== "function") {
            return false;
        }

        const originalBuild = builderPrototype.build;
        builderPrototype.build = function (...args) {
            const connection = originalBuild.apply(this, args);
            const originalOn = connection.on.bind(connection);

            connection.on = (methodName, handler) => {
                if (methodName !== "PlayersChanged" || typeof handler !== "function") {
                    return originalOn(methodName, handler);
                }

                return originalOn(methodName, (...eventArgs) => {
                    const update = eventArgs[0];
                    if (Array.isArray(update?.players)) {
                        for (const player of update.players) {
                            const imageDataUrl = typeof player?.imageDataUrl === "string"
                                ? player.imageDataUrl
                                : null;

                            // Lobby.cshtml still fingerprints these legacy names,
                            // while GameHub's current payload exposes imageDataUrl.
                            // Keep both views of the payload aligned so switching
                            // Avatar <-> Image triggers the existing render path.
                            player.usesUploadedImage = Boolean(imageDataUrl);
                            player.uploadedImageDataUrl = imageDataUrl;
                        }
                    }

                    return handler(...eventArgs);
                });
            };

            return connection;
        };

        window.badWolfHostPlayerVisualContractInstalled = true;
        return true;
    };

    if (install(window.signalR)) {
        return;
    }

    // This adapter is emitted inside the host page body, while SignalR itself
    // is loaded later by _Layout immediately before the page Scripts section.
    // DOMContentLoaded is therefore too late: Lobby.cshtml builds its hub
    // connection synchronously in that Scripts section. Intercept the late
    // window.signalR assignment so the builder is patched before that happens.
    const signalRDescriptor = Object.getOwnPropertyDescriptor(window, "signalR");
    if (!signalRDescriptor || signalRDescriptor.configurable) {
        let signalRValue = signalRDescriptor?.value;

        Object.defineProperty(window, "signalR", {
            configurable: true,
            enumerable: signalRDescriptor?.enumerable ?? true,
            get: () => signalRValue,
            set: value => {
                signalRValue = value;

                Object.defineProperty(window, "signalR", {
                    configurable: true,
                    enumerable: true,
                    writable: true,
                    value
                });

                install(value);
            }
        });

        if (signalRValue) {
            install(signalRValue);
        }
    }

    const installLateFallback = () => install(window.signalR);
    document.addEventListener(
        "DOMContentLoaded",
        installLateFallback,
        { once: true });
    window.addEventListener("load", installLateFallback, { once: true });
})();
