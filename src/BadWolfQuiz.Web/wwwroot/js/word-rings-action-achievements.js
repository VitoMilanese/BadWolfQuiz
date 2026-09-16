(() => {
    const root = document.querySelector('[data-word-rings-root][data-game-mode="cooperative"]');
    if (!(root instanceof HTMLElement)) return;

    const roomCode = String(root.dataset.roomCode || '').trim().toUpperCase();
    const roomApiUrl = root.dataset.roomApiUrl || '/WordRingsRoomApi';
    const patchApiUrl = '/minigames/word-rings-action-cards-patch-api';
    const storageKey = `badwolf.wordrings.room.${roomCode}`;
    const antiForgery = root.querySelector('[data-word-rings-antiforgery] input[name="__RequestVerificationToken"]');
    const nextFetch = window.fetch.bind(window);

    const token = () => {
        try { return String(JSON.parse(localStorage.getItem(storageKey) || 'null')?.token || ''); }
        catch { return ''; }
    };

    const handlerFromUrl = url => {
        try {
            const parsed = new URL(url, window.location.href);
            const expected = new URL(roomApiUrl, window.location.href);
            if (parsed.pathname !== expected.pathname) return '';
            return parsed.searchParams.get('handler') || '';
        } catch {
            return '';
        }
    };

    const formValue = (init, key) => init?.body instanceof FormData ? init.body.get(key) : null;

    const patchPost = async (handler, fields = {}) => {
        const data = new FormData();
        if (antiForgery instanceof HTMLInputElement) data.set(antiForgery.name, antiForgery.value);
        Object.entries(fields).forEach(([key, value]) => {
            if (value !== null && value !== undefined && value !== '') data.set(key, String(value));
        });
        const response = await nextFetch(`${patchApiUrl}?handler=${encodeURIComponent(handler)}`, {
            method: 'POST', body: data, headers: { Accept: 'application/json' }
        });
        if (!response.ok) return null;
        return response.json();
    };

    window.fetch = async (input, init = {}) => {
        const url = typeof input === 'string' ? input : String(input?.url || '');
        const handler = handlerFromUrl(url);
        if (handler !== 'SubmitRoomWord' && handler !== 'ResolveRoomPlacement') {
            return nextFetch(input, init);
        }

        const playerToken = String(formValue(init, 'playerToken') || token());
        const placementId = handler === 'ResolveRoomPlacement'
            ? Number.parseInt(String(formValue(init, 'placementId') || ''), 10)
            : null;
        const capture = await patchPost('CapturePlacementAchievement', {
            roomCode: formValue(init, 'roomCode') || roomCode,
            playerToken,
            placementId: Number.isFinite(placementId) ? placementId : '',
            word: handler === 'SubmitRoomWord' ? formValue(init, 'word') : ''
        }).catch(() => null);

        const response = await nextFetch(input, init);
        if (!capture?.success || !capture.captureId) return response;

        try {
            const payload = await response.clone().json();
            if (!payload?.success) return response;
            let resolvedPlacementId = Number.isFinite(placementId) ? placementId : null;
            if (handler === 'SubmitRoomWord') {
                const result = payload.result;
                const placements = result?.state?.placements || [];
                const placement = [...placements].reverse().find(item =>
                    item.playerId === result?.state?.playerId &&
                    String(item.word || '').toLocaleLowerCase() === String(result?.word || '').toLocaleLowerCase());
                resolvedPlacementId = placement?.id ?? null;
            }
            await patchPost('FinalizePlacementAchievement', {
                roomCode: formValue(init, 'roomCode') || roomCode,
                playerToken,
                captureId: capture.captureId,
                placementId: resolvedPlacementId ?? ''
            });
        } catch (error) {
            console.debug('Could not finalize a Word Rings action achievement.', error);
        }
        return response;
    };
})();
