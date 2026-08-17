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

    const startGameForm = document.getElementById("start-game-form");
    const redundantSaveActions = startGameForm?.querySelector(
        ":scope > .form-actions"
    );
    redundantSaveActions?.remove();

    const lobbyFramePanel = startGameForm?.querySelector(
        "[data-contributor-host-frame]"
    );
    let lobbyFrameChanged = false;
    lobbyFramePanel?.addEventListener("change", () => {
        lobbyFrameChanged = true;
    });

    if (lobbyFramePanel) {
        const runningViewObserver = new MutationObserver(() => {
            if (!lobbyFrameChanged ||
                !document.querySelector(".host-game-board[data-game-code]")) {
                return;
            }

            lobbyFrameChanged = false;
            runningViewObserver.disconnect();
            window.location.reload();
        });
        runningViewObserver.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

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
