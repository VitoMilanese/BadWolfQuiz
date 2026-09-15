(() => {
    const root = document.querySelector('[data-word-rings-root][data-game-mode="solo"]');
    if (!root) return;

    const form = root.querySelector('[data-create-room-form]');
    const nameInput = root.querySelector('[data-create-room-name]');
    const targetInput = root.querySelector('[data-create-room-target]');
    const partialInput = root.querySelector('[data-create-room-partial]');
    const errorElement = root.querySelector('[data-create-room-error]');
    const antiForgery = root.querySelector('[data-word-rings-antiforgery] input[name="__RequestVerificationToken"]');
    const apiUrl = root.dataset.roomApiUrl;
    const nicknameKey = 'badwolf.wordrings.nickname';
    const hostRoomKey = 'badwolf.wordrings.host-room';

    if (!(form instanceof HTMLFormElement) || !apiUrl) return;

    const normalizeCode = value => String(value || '').trim().toUpperCase();
    const sessionKey = code => `badwolf.wordrings.room.${normalizeCode(code)}`;

    const selectedRole = () =>
        form.querySelector('[data-create-room-role]:checked')?.value || 'player';

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

    const post = async fields => {
        const data = new FormData();
        if (antiForgery instanceof HTMLInputElement) {
            data.set('__RequestVerificationToken', antiForgery.value);
        }
        Object.entries(fields).forEach(([key, value]) => data.set(key, String(value)));
        const response = await fetch(`${apiUrl}?handler=CreateRoom`, {
            method: 'POST',
            body: data,
            headers: { Accept: 'application/json' }
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json();
    };

    const saveConnection = (connection, name) => {
        const code = normalizeCode(connection?.roomCode);
        const token = connection?.playerToken;
        if (!code || !token) return false;
        localStorage.setItem(nicknameKey, name);
        localStorage.setItem(sessionKey(code), JSON.stringify({ token, name, isHost: true }));
        localStorage.setItem(hostRoomKey, JSON.stringify({ code, token }));
        return true;
    };

    const goToRoom = code => {
        const url = new URL(window.location.href);
        url.search = '';
        url.searchParams.set('room', normalizeCode(code));
        window.location.assign(url.toString());
    };

    form.addEventListener('submit', async event => {
        if (selectedRole() !== 'host') return;

        event.preventDefault();
        event.stopImmediatePropagation();

        const name = nameInput?.value?.trim() || '';
        const targetScore = Number.parseInt(targetInput?.value || '10', 10);
        if (!name) {
            if (errorElement) errorElement.textContent = root.dataset.roomInvalidName || '';
            nameInput?.focus();
            return;
        }

        const submit = form.querySelector('[type="submit"]');
        if (submit instanceof HTMLButtonElement) submit.disabled = true;
        if (errorElement) errorElement.textContent = '';

        try {
            const previousHostRoom = loadPreviousHostRoom();
            const payload = await post({
                playerName: name,
                targetScore,
                partialScoreEnabled: partialInput?.checked === true,
                hostChoosesRules: true,
                previousRoomCode: previousHostRoom?.code || '',
                previousPlayerToken: previousHostRoom?.token || ''
            });

            if (!payload.success || !saveConnection(payload.connection, name)) {
                if (errorElement) errorElement.textContent = root.dataset.roomError || '';
                return;
            }

            const nextCode = normalizeCode(payload.connection.roomCode);
            if (previousHostRoom && previousHostRoom.code !== nextCode) {
                localStorage.removeItem(sessionKey(previousHostRoom.code));
            }
            goToRoom(nextCode);
        } catch (error) {
            console.error('Could not create hosted Word Rings room.', error);
            if (errorElement) errorElement.textContent = root.dataset.roomError || '';
        } finally {
            if (submit instanceof HTMLButtonElement) submit.disabled = false;
        }
    }, true);
})();
