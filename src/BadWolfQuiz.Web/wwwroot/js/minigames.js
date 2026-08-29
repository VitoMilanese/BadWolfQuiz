(() => {
    const root = document.querySelector('[data-minigames-root]');
    if (!root) return;

    const grid = root.querySelector('[data-minigames-grid]');
    const empty = root.querySelector('[data-minigames-empty]');
    const regenerateButton = root.querySelector('[data-minigames-regenerate]');
    const error = root.querySelector('[data-minigames-error]');
    const hubUrl = root.dataset.hubUrl;
    const cardUrl = root.dataset.cardUrl;
    const regenerateError = root.dataset.regenerateError ?? '';
    const inactiveCards = new Set();
    let currentVersion = Number(root.dataset.stateVersion ?? '0');
    let highlightedFile = root.dataset.highlightedFile ?? '';

    if (!grid || !empty || !regenerateButton || !hubUrl || !cardUrl) return;

    const getVersion = state => Number(state?.version ?? state?.Version ?? 0);
    const getCards = state => state?.cards ?? state?.Cards ?? [];
    const getFileName = card => card?.fileName ?? card?.FileName ?? '';
    const getDisplayName = card => card?.displayName ?? card?.DisplayName ?? '';

    const updateTopbarHeight = () => {
        const topbarHeight = document.querySelector('.topbar')?.getBoundingClientRect().height;
        if (topbarHeight && topbarHeight > 0) {
            document.documentElement.style.setProperty(
                '--minigames-topbar-height',
                `${topbarHeight}px`);
        }
    };

    const imageUrl = fileName => {
        const url = new URL(cardUrl, window.location.origin);
        url.searchParams.set('file', fileName);
        return `${url.pathname}${url.search}`;
    };

    const setInactive = (button, inactive) => {
        const fileName = button.dataset.cardFile;
        if (!fileName) return;

        if (inactive) {
            inactiveCards.add(fileName);
        } else {
            inactiveCards.delete(fileName);
        }

        button.classList.toggle('is-inactive', inactive);
        button.setAttribute('aria-pressed', inactive ? 'true' : 'false');
    };

    const bindCard = button => {
        button.addEventListener('click', () => {
            setInactive(button, !button.classList.contains('is-inactive'));
        });
    };

    const chooseHighlightedFile = cards => {
        if (cards.length === 0) return '';
        return getFileName(cards[Math.floor(Math.random() * cards.length)]);
    };

    const createCard = card => {
        const fileName = getFileName(card);
        const displayName = getDisplayName(card);
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'minigame-card';
        button.dataset.cardFile = fileName;
        button.setAttribute('aria-label', displayName);
        button.setAttribute('aria-pressed', 'false');
        button.classList.toggle('is-highlighted', fileName === highlightedFile);

        const frame = document.createElement('span');
        frame.className = 'minigame-card-frame';
        const image = document.createElement('img');
        image.src = imageUrl(fileName);
        image.alt = '';
        image.draggable = false;
        frame.appendChild(image);

        const name = document.createElement('span');
        name.className = 'minigame-card-name';
        name.textContent = displayName;

        button.append(frame, name);
        bindCard(button);
        return button;
    };

    const layoutGrid = () => {
        const cards = [...grid.querySelectorAll('.minigame-card')];
        if (cards.length === 0 || grid.classList.contains('is-hidden')) return;

        const rect = grid.getBoundingClientRect();
        if (rect.width <= 0 || rect.height <= 0) return;

        const styles = getComputedStyle(grid);
        const gap = Number.parseFloat(styles.columnGap || styles.gap) || 0;
        const targetAspect = 1.45;
        let best = { columns: 1, rows: cards.length, score: -1 };

        for (let columns = 1; columns <= cards.length; columns += 1) {
            const rows = Math.ceil(cards.length / columns);
            const cardWidth = (rect.width - gap * (columns - 1)) / columns;
            const cardHeight = (rect.height - gap * (rows - 1)) / rows;
            if (cardWidth <= 0 || cardHeight <= 0) continue;

            const aspect = cardWidth / cardHeight;
            const aspectPenalty = 1 + Math.abs(Math.log(aspect / targetAspect)) * 0.45;
            const score = cardWidth * cardHeight / aspectPenalty;
            if (score > best.score) {
                best = { columns, rows, score };
            }
        }

        grid.style.gridTemplateColumns =
            `repeat(${best.columns}, minmax(0, 1fr))`;
        grid.style.gridTemplateRows =
            `repeat(${best.rows}, minmax(0, 1fr))`;
    };

    const applyState = state => {
        const version = getVersion(state);
        const cards = getCards(state);
        if (!Number.isFinite(version) || version <= currentVersion || !Array.isArray(cards)) {
            return;
        }

        inactiveCards.clear();
        highlightedFile = chooseHighlightedFile(cards);
        currentVersion = version;
        root.dataset.stateVersion = String(version);
        root.dataset.highlightedFile = highlightedFile;

        grid.replaceChildren(...cards.map(createCard));
        const hasCards = cards.length > 0;
        grid.classList.toggle('is-hidden', !hasCards);
        empty.classList.toggle('is-hidden', hasCards);
        requestAnimationFrame(layoutGrid);
    };

    grid.querySelectorAll('.minigame-card').forEach(bindCard);
    updateTopbarHeight();
    requestAnimationFrame(layoutGrid);

    const gridObserver = new ResizeObserver(layoutGrid);
    gridObserver.observe(grid);
    const topbar = document.querySelector('.topbar');
    if (topbar) {
        const topbarObserver = new ResizeObserver(() => {
            updateTopbarHeight();
            requestAnimationFrame(layoutGrid);
        });
        topbarObserver.observe(topbar);
    }

    if (!window.signalR) {
        if (error) {
            error.textContent = regenerateError;
            error.classList.remove('is-hidden');
        }
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build();

    const synchronize = async () => {
        const state = await connection.invoke('GetState');
        applyState(state);
    };

    connection.on('cardsRegenerated', state => applyState(state));

    connection.onreconnecting(() => {
        regenerateButton.disabled = true;
    });

    connection.onreconnected(async () => {
        regenerateButton.disabled = false;
        try {
            await synchronize();
        } catch {
            // The next broadcast or reconnect will synchronize the shared set.
        }
    });

    regenerateButton.addEventListener('click', async () => {
        regenerateButton.disabled = true;
        if (error) error.classList.add('is-hidden');

        try {
            const state = await connection.invoke('Regenerate');
            applyState(state);
        } catch {
            if (error) {
                error.textContent = regenerateError;
                error.classList.remove('is-hidden');
            }
        } finally {
            regenerateButton.disabled =
                connection.state !== signalR.HubConnectionState.Connected;
        }
    });

    const connect = async () => {
        try {
            await connection.start();
            regenerateButton.disabled = false;
            await synchronize();
        } catch {
            regenerateButton.disabled = true;
            window.setTimeout(connect, 2000);
        }
    };

    void connect();
})();
