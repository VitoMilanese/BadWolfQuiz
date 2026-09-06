(() => {
    const install = () => {
        if (window.badWolfHostPlayerVisualContractInstalled) {
            return true;
        }

        const builderPrototype = window.signalR?.HubConnectionBuilder?.prototype;
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
                            // Avatar <-> Image triggers the existing renderPlayers path.
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

    if (!install()) {
        document.addEventListener("DOMContentLoaded", install, { once: true });
        window.addEventListener("load", install, { once: true });
    }
})();
