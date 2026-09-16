(() => {
    const root = document.querySelector('[data-word-rings-root][data-game-mode="cooperative"]');
    if (!(root instanceof HTMLElement)) return;

    const roomApiPath = (() => {
        try { return new URL(root.dataset.roomApiUrl || '/WordRingsRoomApi', window.location.href).pathname; }
        catch { return '/WordRingsRoomApi'; }
    })();
    const previousFetch = window.fetch.bind(window);

    const handlerFromUrl = input => {
        try {
            const url = typeof input === 'string' ? input : String(input?.url || '');
            const parsed = new URL(url, window.location.href);
            return parsed.pathname.endsWith(roomApiPath) ? parsed.searchParams.get('handler') || '' : '';
        } catch {
            return '';
        }
    };

    const jsonResponse = (payload, source) => new Response(JSON.stringify(payload), {
        status: source.status,
        statusText: source.statusText,
        headers: { 'Content-Type': 'application/json; charset=utf-8' }
    });

    window.fetch = async (input, init = {}) => {
        const handler = handlerFromUrl(input);
        const response = await previousFetch(input, init);
        if (handler !== 'SubmitRoomWord' || !root.classList.contains('has-pending-word')) return response;

        try {
            const payload = await response.clone().json();
            const result = payload?.result;
            const state = result?.state;
            if (payload?.success !== true || result?.isPending === true || state?.phase !== 'playing' ||
                !Array.isArray(state?.bankWords) || !Array.isArray(state?.queuedWords) || state.queuedWords.length === 0)
            {
                return response;
            }

            const stillPendingOnServer = Array.isArray(state.placements) && state.placements.some(placement =>
                placement?.isPending === true && placement?.playerId === state.playerId);
            if (stillPendingOnServer) return response;

            state.bankWords = [...state.bankWords, state.queuedWords[0]];
            state.queuedWords = state.queuedWords.slice(1);
            return jsonResponse(payload, response);
        } catch {
            return response;
        }
    };
})();