(() => {
    const root = document.querySelector('[data-minigames-root]');
    const button = root?.querySelector('[data-copy-room-link]');
    const roomCodeLabel = root?.querySelector('[data-room-code]');
    const status = root?.querySelector('[data-copy-room-status]');
    if (!root || !button || !roomCodeLabel) return;

    const storagePrefix = 'badwolf-minigame-player:';
    const normalizeRoomCode = () => roomCodeLabel.textContent.trim().toUpperCase();

    const updateButton = () => {
        button.disabled = normalizeRoomCode().length !== 6;
    };

    const buildRoomUrl = roomCode => {
        const url = new URL(window.location.href);
        url.searchParams.set('room', roomCode);
        url.hash = '';
        return url.toString();
    };

    const copyWithFallback = async value => {
        if (navigator.clipboard?.writeText && window.isSecureContext) {
            await navigator.clipboard.writeText(value);
            return;
        }

        const input = document.createElement('textarea');
        input.value = value;
        input.setAttribute('readonly', '');
        input.style.position = 'fixed';
        input.style.opacity = '0';
        document.body.append(input);
        input.select();
        const copied = document.execCommand('copy');
        input.remove();
        if (!copied) throw new Error('Clipboard copy failed.');
    };

    const touchRoom = async roomCode => {
        const token = window.localStorage.getItem(`${storagePrefix}${roomCode}`);
        const hubUrl = root.dataset.hubUrl;
        if (!token || !hubUrl || !window.signalR) return;

        const connection = new window.signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .build();
        try {
            await connection.start();
            await connection.invoke('TouchRoom', roomCode, token);
        } catch {
            // Copying the room link should still succeed if the TTL touch cannot be sent.
        } finally {
            try {
                await connection.stop();
            } catch {
                // Ignore shutdown failures for this short-lived activity connection.
            }
        }
    };

    const showResult = (message, copied) => {
        status && (status.textContent = message);
        button.title = message;
        button.setAttribute('aria-label', message);
        button.classList.toggle('is-copied', copied);
        window.setTimeout(() => {
            const label = button.dataset.copyLabel ?? '';
            button.title = label;
            button.setAttribute('aria-label', label);
            button.classList.remove('is-copied');
            if (status) status.textContent = '';
        }, 1800);
    };

    button.addEventListener('click', async () => {
        const roomCode = normalizeRoomCode();
        if (roomCode.length !== 6) return;

        button.disabled = true;
        try {
            await copyWithFallback(buildRoomUrl(roomCode));
            void touchRoom(roomCode);
            showResult(button.dataset.copiedLabel ?? '', true);
        } catch {
            showResult(button.dataset.copyFailedLabel ?? '', false);
        } finally {
            updateButton();
        }
    });

    new MutationObserver(updateButton).observe(roomCodeLabel, {
        childList: true,
        subtree: true,
        characterData: true
    });
    updateButton();
})();
