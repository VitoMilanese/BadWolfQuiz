(() => {
    const root = document.querySelector('[data-word-rings-root][data-game-mode="solo"]');
    if (!root) return;

    const dialog = root.querySelector('[data-create-room-dialog]');
    const openButton = root.querySelector('[data-open-coop-room]');
    const closeButton = root.querySelector('[data-close-create-room]');
    const createForm = root.querySelector('[data-create-room-form]');
    const createName = root.querySelector('[data-create-room-name]');
    const createTarget = root.querySelector('[data-create-room-target]');
    const createPartial = root.querySelector('[data-create-room-partial]');
    const createError = root.querySelector('[data-create-room-error]');
    const joinForm = root.querySelector('[data-room-join-shortcut-form]');
    const joinCode = root.querySelector('[data-room-join-code]');
    const joinName = root.querySelector('[data-room-join-name]');
    const antiForgery = root.querySelector('[data-word-rings-antiforgery] input[name="__RequestVerificationToken"]');
    const apiUrl = root.dataset.roomApiUrl;
    const nicknameKey = 'badwolf.wordrings.nickname';
    const hostRoomKey = 'badwolf.wordrings.host-room';

    if (!(dialog instanceof HTMLDialogElement) || !apiUrl) return;

    const normalizeCode = value => String(value || '').trim().toUpperCase();
    const sessionKey = code => `badwolf.wordrings.room.${normalizeCode(code)}`;

    const loadPreviousHostRoom = () => {
        try {
            const parsed = JSON.parse(localStorage.getItem(hostRoomKey) || 'null');
            const code = normalizeCode(parsed?.code);
            const token = String(parsed?.token || '');
            return code.length === 6 && token ? { code, token } : null;
        } catch {
            localStorage.removeItem(hostRoomKey);
            return null;
        }
    };

    const savedNickname = localStorage.getItem(nicknameKey) || '';
    if (createName instanceof HTMLInputElement) createName.value = savedNickname;
    if (joinName instanceof HTMLInputElement) joinName.value = savedNickname;

    const setError = (element, message) => {
        if (element) element.textContent = message || '';
    };

    const post = async (handler, fields) => {
        const data = new FormData();
        if (antiForgery instanceof HTMLInputElement) {
            data.set('__RequestVerificationToken', antiForgery.value);
        }
        Object.entries(fields).forEach(([key, value]) => data.set(key, String(value)));
        const response = await fetch(`${apiUrl}?handler=${encodeURIComponent(handler)}`, {
            method: 'POST',
            body: data,
            headers: { 'Accept': 'application/json' }
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json();
    };

    const saveConnection = (connection, name) => {
        const code = normalizeCode(connection?.roomCode);
        const token = connection?.playerToken;
        if (!code || !token) return false;
        const isHost = connection?.state?.isHost === true;
        localStorage.setItem(nicknameKey, name);
        localStorage.setItem(sessionKey(code), JSON.stringify({ token, name, isHost }));
        if (isHost) {
            localStorage.setItem(hostRoomKey, JSON.stringify({ code, token }));
        }
        return true;
    };

    const goToRoom = code => {
        const url = new URL(window.location.href);
        url.search = '';
        url.searchParams.set('room', normalizeCode(code));
        window.location.assign(url.toString());
    };

    const friendlyError = code => {
        if (code === 'InvalidPlayerName') return root.dataset.roomInvalidName || root.dataset.roomError;
        return root.dataset.roomError || 'Room error';
    };

    openButton?.addEventListener('click', () => {
        setError(createError, '');
        if (!dialog.open) dialog.showModal();
        createName?.focus();
    });

    closeButton?.addEventListener('click', () => dialog.close());
    dialog.addEventListener('cancel', event => {
        event.preventDefault();
        dialog.close();
    });

    createForm?.addEventListener('submit', async event => {
        event.preventDefault();
        const name = createName?.value?.trim() || '';
        const targetScore = Number.parseInt(createTarget?.value || '10', 10);
        if (!name) {
            setError(createError, root.dataset.roomInvalidName);
            createName?.focus();
            return;
        }

        const submit = createForm.querySelector('[type="submit"]');
        if (submit instanceof HTMLButtonElement) submit.disabled = true;
        setError(createError, '');
        try {
            const previousHostRoom = loadPreviousHostRoom();
            const payload = await post('CreateRoom', {
                playerName: name,
                targetScore,
                partialScoreEnabled: createPartial?.checked === true,
                previousRoomCode: previousHostRoom?.code || '',
                previousPlayerToken: previousHostRoom?.token || ''
            });
            if (!payload.success || !saveConnection(payload.connection, name)) {
                setError(createError, friendlyError(payload.error));
                return;
            }
            const nextCode = normalizeCode(payload.connection.roomCode);
            if (previousHostRoom && previousHostRoom.code !== nextCode) {
                localStorage.removeItem(sessionKey(previousHostRoom.code));
            }
            goToRoom(nextCode);
        } catch (error) {
            console.error('Could not create Word Rings room.', error);
            setError(createError, root.dataset.roomError);
        } finally {
            if (submit instanceof HTMLButtonElement) submit.disabled = false;
        }
    });

    joinForm?.addEventListener('submit', async event => {
        event.preventDefault();
        const code = normalizeCode(joinCode?.value);
        const name = joinName?.value?.trim() || '';
        if (code.length !== 6 || !name) {
            setError(createError, !name ? root.dataset.roomInvalidName : root.dataset.roomError);
            return;
        }

        const submit = joinForm.querySelector('[type="submit"]');
        if (submit instanceof HTMLButtonElement) submit.disabled = true;
        setError(createError, '');
        try {
            const payload = await post('JoinRoom', { roomCode: code, playerName: name });
            if (!payload.success || !saveConnection(payload.connection, name)) {
                setError(createError, friendlyError(payload.error));
                return;
            }
            goToRoom(code);
        } catch (error) {
            console.error('Could not join Word Rings room.', error);
            setError(createError, root.dataset.roomError);
        } finally {
            if (submit instanceof HTMLButtonElement) submit.disabled = false;
        }
    });
})();
