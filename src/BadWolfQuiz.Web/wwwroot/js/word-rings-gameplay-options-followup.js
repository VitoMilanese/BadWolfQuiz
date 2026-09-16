(() => {
    const root = document.querySelector('[data-word-rings-root]');
    if (!(root instanceof HTMLElement)) return;

    const isSolo = (root.dataset.gameMode || 'solo').toLowerCase() === 'solo';
    const roomCode = String(root.dataset.roomCode || '').trim().toUpperCase();
    const roomApiUrl = root.dataset.roomApiUrl || '/WordRingsRoomApi';
    const gameplayApiUrl = '/minigames/word-rings-gameplay-options-api';
    const antiForgery = root.querySelector('[data-word-rings-antiforgery] input[name="__RequestVerificationToken"]');
    const baseFetch = window.fetch.bind(window);
    const soloStorageKey = 'badwolf.wordrings.gameplay-options';
    const roomStorageKey = `badwolf.wordrings.room.${roomCode}`;
    let soloSettingsDirty = false;

    const currentToken = () => {
        if (!roomCode) return '';
        try { return String(JSON.parse(localStorage.getItem(roomStorageKey) || 'null')?.token || ''); }
        catch { return ''; }
    };

    const postGameplay = async (handler, fields = {}) => {
        const data = new FormData();
        if (antiForgery instanceof HTMLInputElement) data.set(antiForgery.name, antiForgery.value);
        Object.entries(fields).forEach(([key, value]) => {
            if (value !== null && value !== undefined && value !== '') data.set(key, String(value));
        });
        const response = await baseFetch(`${gameplayApiUrl}?handler=${encodeURIComponent(handler)}`, {
            method: 'POST',
            body: data,
            headers: { Accept: 'application/json' }
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json();
    };

    const syntheticJsonResponse = (payload, source) => new Response(JSON.stringify(payload), {
        status: source?.status || 200,
        statusText: source?.statusText || 'OK',
        headers: { 'Content-Type': 'application/json; charset=utf-8' }
    });

    const handlerFromUrl = url => {
        try {
            const parsed = new URL(url, window.location.href);
            if (!parsed.pathname.endsWith(new URL(roomApiUrl, window.location.href).pathname)) return '';
            return parsed.searchParams.get('handler') || '';
        } catch {
            return '';
        }
    };

    const clampTarget = value => Math.max(5, Math.min(20, Number.parseInt(value, 10) || 5));
    const clampHand = (value, target) => Math.max(5, Math.min(10, target, Number.parseInt(value, 10) || 5));

    const syncCreateHandMaximum = ({ normalize = false } = {}) => {
        const target = root.querySelector('[data-create-room-target]');
        const hand = root.querySelector('[data-create-room-hand-size]');
        if (!(target instanceof HTMLInputElement) || !(hand instanceof HTMLInputElement)) return;

        const rawTarget = Number.parseInt(target.value, 10);
        const effectiveTarget = Number.isFinite(rawTarget) ? Math.max(5, Math.min(20, rawTarget)) : 5;
        hand.max = String(Math.min(10, effectiveTarget));
        const rawHand = Number.parseInt(hand.value, 10);
        if (Number.isFinite(rawHand) && rawHand > Math.min(10, effectiveTarget)) {
            hand.value = String(Math.min(10, effectiveTarget));
        }
        if (!normalize) return;
        target.value = String(clampTarget(target.value));
        hand.max = String(Math.min(10, Number(target.value)));
        hand.value = String(clampHand(hand.value, Number(target.value)));
    };

    const soloInputs = () => {
        const panel = root.querySelector('[data-solo-gameplay-options]');
        if (!(panel instanceof HTMLElement)) return null;
        const inputs = [...panel.querySelectorAll('input[type="number"]')];
        return inputs.length >= 2 && inputs[0] instanceof HTMLInputElement && inputs[1] instanceof HTMLInputElement
            ? { panel, target: inputs[0], hand: inputs[1] }
            : null;
    };

    const syncSoloHandMaximum = ({ normalize = false, save = false } = {}) => {
        const inputs = soloInputs();
        if (!inputs) return;
        const { target, hand } = inputs;
        const rawTarget = Number.parseInt(target.value, 10);
        const effectiveTarget = Number.isFinite(rawTarget) ? Math.max(5, Math.min(20, rawTarget)) : 5;
        hand.max = String(Math.min(10, effectiveTarget));
        const rawHand = Number.parseInt(hand.value, 10);
        if (Number.isFinite(rawHand) && rawHand > Math.min(10, effectiveTarget)) {
            hand.value = String(Math.min(10, effectiveTarget));
        }
        if (normalize) {
            target.value = String(clampTarget(target.value));
            hand.max = String(Math.min(10, Number(target.value)));
            hand.value = String(clampHand(hand.value, Number(target.value)));
        }
        if (save) {
            try {
                localStorage.setItem(soloStorageKey, JSON.stringify({
                    targetScore: clampTarget(target.value),
                    handSize: clampHand(hand.value, clampTarget(target.value))
                }));
                soloSettingsDirty = true;
            } catch { }
        }
    };

    root.addEventListener('input', event => {
        const target = event.target;
        if (!(target instanceof HTMLInputElement)) return;
        if (target.matches('[data-create-room-target], [data-create-room-hand-size]')) {
            event.stopImmediatePropagation();
            syncCreateHandMaximum();
            return;
        }
        if (target.closest('[data-solo-gameplay-options]')) {
            event.stopImmediatePropagation();
            syncSoloHandMaximum();
        }
    }, true);

    root.addEventListener('change', event => {
        const target = event.target;
        if (!(target instanceof HTMLInputElement)) return;
        if (target.matches('[data-create-room-target], [data-create-room-hand-size]')) {
            syncCreateHandMaximum({ normalize: true });
            return;
        }
        if (target.closest('[data-solo-gameplay-options]')) {
            syncSoloHandMaximum({ normalize: true, save: true });
        }
    }, true);

    const attachSoloCloseReload = () => {
        if (!isSolo) return;
        const inputs = soloInputs();
        const dialog = inputs?.panel.closest('dialog');
        if (!(dialog instanceof HTMLDialogElement) || dialog.dataset.gameplayCloseReload === 'true') return;
        dialog.dataset.gameplayCloseReload = 'true';
        dialog.addEventListener('close', () => {
            if (soloSettingsDirty) window.location.reload();
        });
    };

    const observer = new MutationObserver(() => {
        syncCreateHandMaximum();
        syncSoloHandMaximum();
        attachSoloCloseReload();
    });
    observer.observe(root, { childList: true, subtree: true });
    syncCreateHandMaximum();
    syncSoloHandMaximum();
    attachSoloCloseReload();

    const findExhaustedPlayer = state => {
        if (!state || !Array.isArray(state.players)) return null;
        const phaseAllowsOverride = state.phase === 'playing' ||
            (state.phase === 'finished' && state.outcome === 'lost');
        if (!phaseAllowsOverride) return null;
        return state.players.find(player =>
            !(state.dedicatedHostMode === true && player.isHost === true) &&
            Number(player.remainingWords) === 0) || null;
    };

    window.fetch = async (input, init = {}) => {
        const url = typeof input === 'string' ? input : String(input?.url || '');
        const handler = handlerFromUrl(url);
        const response = await baseFetch(input, init);
        if (handler !== 'RoomState') return response;

        try {
            const payload = await response.clone().json();
            const exhausted = findExhaustedPlayer(payload?.state);
            if (!payload?.success || !exhausted) return response;
            const token = String(init?.body instanceof FormData
                ? init.body.get('playerToken') || currentToken()
                : currentToken());
            const code = String(init?.body instanceof FormData
                ? init.body.get('roomCode') || roomCode
                : roomCode);
            if (!token || !code) return response;

            const finalized = await postGameplay('FinalizePlayerExhaustion', {
                roomCode: code,
                playerToken: token,
                playerId: exhausted.id
            });
            if (finalized?.success && finalized.state) {
                payload.state = finalized.state;
                return syntheticJsonResponse(payload, response);
            }
        } catch (error) {
            console.debug('Could not finalize the exhausted Word Rings player.', error);
        }
        return response;
    };
})();
