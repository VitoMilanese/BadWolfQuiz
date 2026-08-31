(() => {
    const showBusy = () => {
        if (window.BadWolfBusy && !window.BadWolfBusy.isBusy) {
            window.BadWolfBusy.show();
        }
    };

    document.querySelectorAll('[data-minigame-editor-busy]').forEach(form => {
        form.addEventListener('submit', event => {
            if (event.defaultPrevented || !form.checkValidity()) return;

            const confirmation = form.dataset.confirm;
            if (confirmation && !window.confirm(confirmation)) {
                event.preventDefault();
                return;
            }

            showBusy();
            form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach(button => {
                button.disabled = true;
            });
        });
    });

    document.querySelectorAll('[data-minigame-editor-nav]').forEach(link => {
        link.addEventListener('click', event => {
            if (event.defaultPrevented ||
                event.button !== 0 ||
                event.metaKey ||
                event.ctrlKey ||
                event.shiftKey ||
                event.altKey) {
                return;
            }

            event.preventDefault();
            if (window.BadWolfBusy) {
                window.BadWolfBusy.navigate(link.href);
            } else {
                window.location.assign(link.href);
            }
        });
    });

    const gamePicker = document.querySelector('[data-minigame-editor-game-picker]');
    gamePicker?.addEventListener('change', () => {
        const baseUrl = gamePicker.dataset.baseUrl;
        const gameId = Number(gamePicker.value);
        if (!baseUrl || !Number.isInteger(gameId) || gameId <= 0) return;

        const url = new URL(baseUrl, window.location.origin);
        url.searchParams.set('section', 'answers');
        url.searchParams.set('gameId', String(gameId));
        if (window.BadWolfBusy) {
            window.BadWolfBusy.navigate(`${url.pathname}${url.search}`);
        } else {
            window.location.assign(`${url.pathname}${url.search}`);
        }
    });
})();
