(() => {
    const root = document.querySelector('[data-word-rings-root][data-game-mode="cooperative"]');
    if (!(root instanceof HTMLElement)) return;

    const roomCode = String(root.dataset.roomCode || '').trim().toUpperCase();
    if (!roomCode) return;

    const apiUrl = '/minigames/word-rings-gameplay-options-api';
    const storageKey = `badwolf.wordrings.room.${roomCode}`;
    const antiForgery = root.querySelector('[data-word-rings-antiforgery] input[name="__RequestVerificationToken"]');
    const variables = Object.freeze([
        '--bg', '--panel', '--panel-2', '--line', '--text', '--muted', '--red', '--red-bright', '--gold',
        '--body-background', '--topbar-bg', '--panel-glass', '--panel-gradient-end', '--accent-shadow'
    ]);
    let busy = false;
    let lastAppliedSignature = '';

    const token = () => {
        try {
            return String(JSON.parse(localStorage.getItem(storageKey) || 'null')?.token || '');
        } catch {
            return '';
        }
    };

    const capture = () => {
        const element = document.documentElement;
        const values = {};
        for (const name of variables) {
            const value = element.style.getPropertyValue(name).trim();
            if (value) values[name] = value;
        }
        return {
            themeId: element.dataset.theme || '',
            variables: values
        };
    };

    const apply = theme => {
        if (!theme) return;
        const normalized = {
            themeId: String(theme.themeId || theme.ThemeId || ''),
            variables: theme.variables || theme.Variables || {}
        };
        const signature = JSON.stringify(normalized);
        if (signature === lastAppliedSignature) return;
        lastAppliedSignature = signature;

        const element = document.documentElement;
        for (const name of variables) element.style.removeProperty(name);
        if (normalized.themeId) element.dataset.theme = normalized.themeId;
        else element.removeAttribute('data-theme');
        for (const [name, value] of Object.entries(normalized.variables)) {
            if (variables.includes(name) && typeof value === 'string' && value) {
                element.style.setProperty(name, value);
            }
        }
        window.BadWolfStarfield?.refresh?.();
    };

    const synchronize = async () => {
        const playerToken = token();
        if (!playerToken || busy) return;
        busy = true;
        try {
            const own = capture();
            const data = new FormData();
            if (antiForgery instanceof HTMLInputElement) data.set(antiForgery.name, antiForgery.value);
            data.set('roomCode', roomCode);
            data.set('playerToken', playerToken);
            data.set('themeId', own.themeId);
            data.set('variablesJson', JSON.stringify(own.variables));
            const response = await fetch(`${apiUrl}?handler=SynchronizeTheme`, {
                method: 'POST',
                body: data,
                headers: { Accept: 'application/json' }
            });
            if (!response.ok) return;
            const payload = await response.json();
            if (payload?.success && payload.theme) apply(payload.theme);
        } catch (error) {
            console.debug('Could not synchronize the Word Rings room theme.', error);
        } finally {
            busy = false;
        }
    };

    void synchronize();
    window.setInterval(synchronize, 1000);
    window.addEventListener('storage', event => {
        if (event.key === storageKey) void synchronize();
    });
})();
