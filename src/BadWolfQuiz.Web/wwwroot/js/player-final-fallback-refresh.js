(() => {
    "use strict";

    if (window.badWolfPlayerFinalFallbackRefreshBootstrapInstalled) {
        return;
    }
    window.badWolfPlayerFinalFallbackRefreshBootstrapInstalled = true;

    const fallbackEventName = "FinalQuestionPlayerFallbackChanged";

    const install = signalR => {
        const prototype = signalR?.HubConnection?.prototype;
        if (!prototype || prototype.badWolfPlayerFinalFallbackRefreshInstalled) {
            return;
        }

        const registerHandler = prototype.on;
        if (typeof registerHandler !== "function") {
            return;
        }

        prototype.badWolfPlayerFinalFallbackRefreshInstalled = true;
        prototype.on = function(methodName, handler) {
            if (typeof methodName === "string" &&
                methodName.toLowerCase() === "finalquestionstatechanged" &&
                typeof handler === "function") {
                const lobby = document.querySelector(
                    ".player-lobby[data-player-id]");
                const playerId = lobby?.dataset.playerId?.toLowerCase();

                if (playerId) {
                    registerHandler.call(this, fallbackEventName, update => {
                        const targetPlayerId = String(
                            update?.playerId ?? "").toLowerCase();
                        if (targetPlayerId === playerId) {
                            handler(update);
                        }
                    });
                }
            }

            return registerHandler.apply(this, arguments);
        };
    };

    if (window.signalR) {
        install(window.signalR);
        return;
    }

    try {
        Object.defineProperty(window, "signalR", {
            configurable: true,
            enumerable: true,
            get() {
                return undefined;
            },
            set(value) {
                Object.defineProperty(window, "signalR", {
                    configurable: true,
                    enumerable: true,
                    writable: true,
                    value
                });
                install(value);
            }
        });
    } catch (error) {
        console.error("Player final fallback refresh bootstrap failed.", error);
    }
})();
