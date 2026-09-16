(() => {
    const install = signalR => {
        if (window.badWolfPlayerMobileRecoveryInstalled) {
            return true;
        }

        const builderPrototype = signalR?.HubConnectionBuilder?.prototype;
        if (!builderPrototype || typeof builderPrototype.build !== "function") {
            return false;
        }

        const originalBuild = builderPrototype.build;
        builderPrototype.build = function (...args) {
            const connection = originalBuild.apply(this, args);
            const lobby = document.querySelector(".player-lobby[data-game-code][data-player-id]");
            if (!(lobby instanceof HTMLElement)) {
                return connection;
            }

            const originalStart = connection.start.bind(connection);
            let startPromise = null;
            let rejoinApprovalRequired = false;

            connection.on("RejoinApprovalRequired", () => {
                rejoinApprovalRequired = true;
            });
            connection.on("RejoinApproved", () => {
                rejoinApprovalRequired = false;
                document.dispatchEvent(new Event("badwolf:player-session-ready"));
            });

            const delay = milliseconds => new Promise(resolve => {
                window.setTimeout(resolve, milliseconds);
            });

            const startWithRetry = async () => {
                if (connection.state !== signalR.HubConnectionState.Disconnected) {
                    return;
                }

                if (startPromise) {
                    return startPromise;
                }

                startPromise = (async () => {
                    let retryDelay = 1000;
                    while (connection.state === signalR.HubConnectionState.Disconnected) {
                        try {
                            await originalStart();
                            return;
                        } catch (error) {
                            console.warn("Player SignalR start failed; retrying.", error);
                            await delay(retryDelay);
                            retryDelay = Math.min(retryDelay * 2, 10000);
                        }
                    }
                })();

                try {
                    await startPromise;
                } finally {
                    startPromise = null;
                }
            };

            const getAccessToken = () => {
                const bootstrapToken = lobby.dataset.accessToken;
                if (bootstrapToken) {
                    return bootstrapToken;
                }

                const gameCode = lobby.dataset.gameCode;
                const playerId = lobby.dataset.playerId;
                return localStorage.getItem(`badwolfquiz:${gameCode}:player:${playerId}`);
            };

            const restorePlayerSession = async () => {
                if (connection.state !== signalR.HubConnectionState.Connected) {
                    return false;
                }

                const accessToken = getAccessToken();
                if (!accessToken) {
                    return false;
                }

                rejoinApprovalRequired = false;
                await connection.invoke(
                    "JoinPlayerSession",
                    lobby.dataset.gameCode,
                    accessToken,
                    document.visibilityState === "visible",
                    null);
                return !rejoinApprovalRequired;
            };

            const refreshVisiblePlayerState = async () => {
                if (document.visibilityState !== "visible") {
                    return;
                }

                if (connection.state === signalR.HubConnectionState.Disconnected) {
                    await startWithRetry();
                    if (await restorePlayerSession()) {
                        document.dispatchEvent(new Event("badwolf:player-session-ready"));
                    }
                    return;
                }

                if (connection.state === signalR.HubConnectionState.Connected) {
                    await connection.invoke("SetPlayerVisibility", true);
                    document.dispatchEvent(new Event("badwolf:player-session-ready"));
                }
            };

            connection.start = startWithRetry;
            connection.onclose(() => {
                void (async () => {
                    await startWithRetry();
                    if (await restorePlayerSession()) {
                        document.dispatchEvent(new Event("badwolf:player-session-ready"));
                    }
                })().catch(console.error);
            });

            const recoverWhenVisible = () => {
                if (document.visibilityState === "visible") {
                    window.setTimeout(() => {
                        void refreshVisiblePlayerState().catch(console.error);
                    }, 0);
                }
            };

            document.addEventListener("visibilitychange", recoverWhenVisible);
            window.addEventListener("pageshow", recoverWhenVisible);
            window.addEventListener("online", recoverWhenVisible);
            window.addEventListener("focus", recoverWhenVisible);
            window.addEventListener("blur", () => {
                window.setTimeout(recoverWhenVisible, 0);
            });

            return connection;
        };

        window.badWolfPlayerMobileRecoveryInstalled = true;
        return true;
    };

    if (install(window.signalR)) {
        return;
    }

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
    document.addEventListener("DOMContentLoaded", installLateFallback, { once: true });
    window.addEventListener("load", installLateFallback, { once: true });
})();
