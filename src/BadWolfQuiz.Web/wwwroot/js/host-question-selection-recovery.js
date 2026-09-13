(() => {
    if (window.badWolfHostQuestionSelectionRecoveryInstalled) {
        return;
    }

    window.badWolfHostQuestionSelectionRecoveryInstalled = true;

    const nativeFetch = window.fetch.bind(window);
    const antiforgeryFieldName = "__RequestVerificationToken";
    const lobbyPathPrefix = "/Admin/Games/Lobby/";

    const getRequestUrl = input => {
        if (input instanceof Request) {
            return new URL(input.url, window.location.href);
        }

        return new URL(input, window.location.href);
    };

    const getRequestMethod = (input, init) =>
        (init?.method ?? (input instanceof Request ? input.method : "GET"))
            .toUpperCase();

    const isQuestionSelectionRequest = (input, init) => {
        if (getRequestMethod(input, init) !== "POST" ||
            !(init?.body instanceof FormData)) {
            return false;
        }

        const url = getRequestUrl(input);
        return url.origin === window.location.origin &&
            url.pathname.toLowerCase().startsWith(lobbyPathPrefix.toLowerCase()) &&
            url.searchParams.get("handler")?.toLowerCase() === "selectquestion";
    };

    const cloneFormDataWithToken = (formData, token) => {
        const clone = new FormData();
        for (const [name, value] of formData.entries()) {
            clone.append(name, value);
        }
        clone.set(antiforgeryFieldName, token);
        return clone;
    };

    const getFreshAntiforgeryToken = async () => {
        const board = document.querySelector(".host-game-board[data-game-id]");
        const gameId = board?.dataset.gameId;
        if (!gameId) {
            return null;
        }

        const lobbyUrl = new URL(
            `${lobbyPathPrefix}${encodeURIComponent(gameId)}`,
            window.location.origin);
        const response = await nativeFetch(lobbyUrl.href, {
            method: "GET",
            credentials: "same-origin",
            headers: {
                Accept: "text/html",
                "X-Requested-With": "XMLHttpRequest"
            },
            cache: "no-store"
        });
        if (!response.ok) {
            return null;
        }

        const responseUrl = new URL(response.url || lobbyUrl.href, lobbyUrl.href);
        if (responseUrl.origin !== lobbyUrl.origin ||
            responseUrl.pathname.toLowerCase() !== lobbyUrl.pathname.toLowerCase()) {
            return null;
        }

        const parsed = new DOMParser().parseFromString(
            await response.text(),
            "text/html");
        const freshBoard = parsed.querySelector(".host-game-board[data-game-id]");
        if (freshBoard?.dataset.gameId !== gameId) {
            return null;
        }

        const token = parsed.querySelector(
            `input[name="${antiforgeryFieldName}"]`)?.value;
        if (!token) {
            return null;
        }

        document.querySelectorAll(
            `input[name="${antiforgeryFieldName}"]`)
            .forEach(input => {
                input.value = token;
            });

        return token;
    };

    window.fetch = async (input, init) => {
        const response = await nativeFetch(input, init);
        if (!isQuestionSelectionRequest(input, init) ||
            response.status !== 400) {
            return response;
        }

        const contentType = response.headers.get("content-type") ?? "";
        if (contentType.toLowerCase().includes("application/json")) {
            return response;
        }

        try {
            const token = await getFreshAntiforgeryToken();
            if (!token) {
                return response;
            }

            const retryInit = {
                ...init,
                body: cloneFormDataWithToken(init.body, token)
            };
            console.warn(
                "Question selection returned a non-JSON 400 response; refreshed the antiforgery token and retried once.");
            return await nativeFetch(input, retryInit);
        } catch (error) {
            console.warn(
                "Question selection recovery after a non-JSON 400 response failed.",
                error);
            return response;
        }
    };
})();
