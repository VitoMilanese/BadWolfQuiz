(() => {
    "use strict";

    if (window.badWolfPeerRatedReturnNoReloadInitialized) {
        return;
    }
    window.badWolfPeerRatedReturnNoReloadInitialized = true;

    const peerApiPath = "/api/peer-rated-all-player-question";
    const originalFetch = window.fetch.bind(window);

    // Prevent the older polish layer from installing its own ReturnToBoard
    // wrapper. That wrapper forced window.location.reload() after success,
    // while the peer-rated runtime already refreshes the host shell via AJAX.
    window.badWolfPeerRatedReturnFetchWrapped = true;

    const isReturnToBoardRequest = input => {
        const rawUrl = input instanceof Request ? input.url : String(input ?? "");
        try {
            const url = new URL(rawUrl, window.location.origin);
            return url.pathname === peerApiPath &&
                url.searchParams.get("handler") === "ReturnToBoard";
        } catch {
            return rawUrl.includes(peerApiPath) &&
                rawUrl.includes("handler=ReturnToBoard");
        }
    };

    const setReturningToBoard = active => {
        const board = document.querySelector(".host-game-board[data-game-code]");
        if (board instanceof HTMLElement) {
            board.classList.toggle("peer-rated-returning-to-board", active);
        }
    };

    window.fetch = async (...args) => {
        const returnToBoard = isReturnToBoardRequest(args[0]);
        if (returnToBoard) {
            setReturningToBoard(true);
        }

        try {
            const response = await originalFetch(...args);
            if (returnToBoard && !response.ok) {
                setReturningToBoard(false);
            }
            return response;
        } catch (error) {
            if (returnToBoard) {
                setReturningToBoard(false);
            }
            throw error;
        }
    };
})();
