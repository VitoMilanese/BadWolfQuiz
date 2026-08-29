(() => {
    const root = document.querySelector('[data-minigames-root]');
    if (!root) return;

    const entry = root.querySelector('[data-room-entry]');
    const roomShell = root.querySelector('[data-room-shell]');
    const createRoomButton = root.querySelector('[data-create-room]');
    const joinForm = root.querySelector('[data-join-room-form]');
    const joinCodeInput = root.querySelector('[data-join-room-code]');
    const entryError = root.querySelector('[data-minigames-entry-error]');
    const roomError = root.querySelector('[data-minigames-room-error]');
    const roomCodeLabel = root.querySelector('[data-room-code]');
    const playerLabel = root.querySelector('[data-player-label]');
    const roomMessage = root.querySelector('[data-room-message]');
    const opponentStatus = root.querySelector('[data-opponent-status]');
    const newGameButton = root.querySelector('[data-new-game]');
    const newGameDialog = root.querySelector('[data-new-game-dialog]');
    const newGameForm = root.querySelector('[data-new-game-form]');
    const newGameCount = root.querySelector('[data-new-game-count]');
    const newGameCancel = root.querySelector('[data-new-game-cancel]');
    const newGameSubmit = root.querySelector('[data-new-game-submit]');
    const newGameError = root.querySelector('[data-new-game-error]');
    const grid = root.querySelector('[data-minigames-grid]');
    const empty = root.querySelector('[data-minigames-empty]');

    const hubUrl = root.dataset.hubUrl;
    const cardUrl = root.dataset.cardUrl;
    if (!entry || !roomShell || !createRoomButton || !joinForm ||
        !joinCodeInput || !newGameDialog || !newGameForm || !newGameCount ||
        !newGameCancel || !newGameSubmit || !grid || !empty || !hubUrl || !cardUrl) {
        return;
    }

    const text = {
        playerOne: root.dataset.playerOne ?? 'Player 1',
        playerTwo: root.dataset.playerTwo ?? 'Player 2',
        waitingOpponent: root.dataset.waitingOpponent ?? '',
        waitingGame: root.dataset.waitingGame ?? '',
        chooseExclusions: root.dataset.chooseExclusions ?? '',
        exclusionProgress: root.dataset.exclusionProgress ?? '',
        gameReady: root.dataset.gameReady ?? '',
        roomNotFound: root.dataset.roomNotFound ?? '',
        roomExpired: root.dataset.roomExpired ?? '',
        roomFull: root.dataset.roomFull ?? '',
        invalidPlayer: root.dataset.invalidPlayer ?? '',
        invalidCardCount: root.dataset.invalidCardCount ?? '',
        cardAlreadyExcluded: root.dataset.cardAlreadyExcluded ?? '',
        exclusionLimit: root.dataset.exclusionLimit ?? '',
        genericError: root.dataset.genericError ?? ''
    };

    const minimumCardCount = Number(root.dataset.minCardCount ?? '10');
    let maximumCardCount = Number(root.dataset.maxCardCount ?? '0');
    let defaultCardCount = Number(root.dataset.defaultCardCount ?? '0');
    const storagePrefix = 'badwolf-minigame-player:';
    const inactiveCards = new Set();

    let connection = null;
    let currentRoomCode = '';
    let playerToken = '';
    let currentState = null;
    let currentGameNumber = -1;

    const get = (value, camelName, pascalName) =>
        value?.[camelName] ?? value?.[pascalName];
    const roomCodeOf = value => get(value, 'roomCode', 'RoomCode') ?? '';
    const playerTokenOf = value => get(value, 'playerToken', 'PlayerToken') ?? '';
    const stateOf = value => get(value, 'state', 'State');
    const cardsOf = value => get(value, 'cards', 'Cards') ?? [];
    const fileNameOf = value => get(value, 'fileName', 'FileName') ?? '';
    const displayNameOf = value => get(value, 'displayName', 'DisplayName') ?? '';
    const phaseOf = value => Number(get(value, 'phase', 'Phase') ?? 0);
    const versionOf = value => Number(get(value, 'version', 'Version') ?? 0);
    const gameNumberOf = value => Number(get(value, 'gameNumber', 'GameNumber') ?? 0);
    const playerNumberOf = value => Number(get(value, 'playerNumber', 'PlayerNumber') ?? 0);
    const playerCountOf = value => Number(get(value, 'playerCount', 'PlayerCount') ?? 0);
    const requiredExclusionsOf = value =>
        Number(get(value, 'requiredExclusionsPerPlayer', 'RequiredExclusionsPerPlayer') ?? 0);
    const myExcludedOf = value => get(value, 'myExcludedFiles', 'MyExcludedFiles') ?? [];
    const opponentExcludedOf = value =>
        get(value, 'opponentExcludedFiles', 'OpponentExcludedFiles') ?? [];
    const mySecretOf = value =>
        get(value, 'mySecretCardFileName', 'MySecretCardFileName') ?? '';

    const phase = {
        waitingForGame: 0,
        choosingExclusions: 1,
        playing: 2,
        finished: 3
    };

    const format = (template, replacements) => {
        let result = template;
        Object.entries(replacements).forEach(([key, value]) => {
            result = result.replaceAll(`{${key}}`, String(value));
        });
        return result;
    };

    const storageKey = roomCode => `${storagePrefix}${roomCode.toUpperCase()}`;

    const updateRoomUrl = roomCode => {
        const url = new URL(window.location.href);
        if (roomCode) url.searchParams.set('room', roomCode);
        else url.searchParams.delete('room');
        window.history.replaceState({}, '', `${url.pathname}${url.search}${url.hash}`);
    };

    const readRoomFromUrl = () =>
        (new URLSearchParams(window.location.search).get('room') ?? '')
            .trim()
            .toUpperCase();

    const rememberMembership = membership => {
        currentRoomCode = roomCodeOf(membership).toUpperCase();
        playerToken = playerTokenOf(membership);
        if (!currentRoomCode || !playerToken) return false;
        window.localStorage.setItem(storageKey(currentRoomCode), playerToken);
        updateRoomUrl(currentRoomCode);
        return true;
    };

    const forgetMembership = () => {
        if (currentRoomCode) {
            window.localStorage.removeItem(storageKey(currentRoomCode));
        }
        currentRoomCode = '';
        playerToken = '';
        currentState = null;
        currentGameNumber = -1;
        inactiveCards.clear();
        updateRoomUrl('');
    };

    const showError = (element, message) => {
        if (!element) return;
        element.textContent = message;
        element.classList.toggle('is-hidden', !message);
    };

    const clearErrors = () => {
        showError(entryError, '');
        showError(roomError, '');
        showError(newGameError, '');
    };

    const getErrorMessage = error => {
        const message = String(error?.message ?? error ?? '');
        if (message.includes('MINIGAME_ROOM_ROOMNOTFOUND')) return text.roomNotFound;
        if (message.includes('MINIGAME_ROOM_ROOMEXPIRED')) return text.roomExpired;
        if (message.includes('MINIGAME_ROOM_ROOMFULL')) return text.roomFull;
        if (message.includes('MINIGAME_ROOM_INVALIDPLAYER')) return text.invalidPlayer;
        if (message.includes('MINIGAME_ROOM_INVALIDCARDCOUNT')) return text.invalidCardCount;
        if (message.includes('MINIGAME_ROOM_CARDALREADYEXCLUDED')) {
            return text.cardAlreadyExcluded;
        }
        if (message.includes('MINIGAME_ROOM_EXCLUSIONLIMITREACHED')) {
            return text.exclusionLimit;
        }
        return text.genericError;
    };

    const imageUrl = fileName => {
        const url = new URL(cardUrl, window.location.origin);
        url.searchParams.set('file', fileName);
        return `${url.pathname}${url.search}`;
    };

    const updateTopbarHeight = () => {
        const topbarHeight = document.querySelector('.topbar')?.getBoundingClientRect().height;
        if (topbarHeight && topbarHeight > 0) {
            document.documentElement.style.setProperty(
                '--minigames-topbar-height',
                `${topbarHeight}px`);
        }
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
            if (score > best.score) best = { columns, rows, score };
        }

        grid.style.gridTemplateColumns = `repeat(${best.columns}, minmax(0, 1fr))`;
        grid.style.gridTemplateRows = `repeat(${best.rows}, minmax(0, 1fr))`;
    };

    const touchRoom = async () => {
        if (!connection || !currentRoomCode || !playerToken ||
            connection.state !== signalR.HubConnectionState.Connected) {
            return;
        }
        try {
            await connection.invoke('TouchRoom', currentRoomCode, playerToken);
        } catch (error) {
            const message = getErrorMessage(error);
            if (message === text.roomExpired || message === text.roomNotFound ||
                message === text.invalidPlayer) {
                forgetMembership();
                renderEntry(message);
            }
        }
    };

    const toggleLocalInactive = button => {
        const fileName = button.dataset.cardFile;
        if (!fileName) return;
        const inactive = !button.classList.contains('is-inactive');
        button.classList.toggle('is-inactive', inactive);
        button.setAttribute('aria-pressed', inactive ? 'true' : 'false');
        if (inactive) inactiveCards.add(fileName);
        else inactiveCards.delete(fileName);
        void touchRoom();
    };

    const createCard = (card, state) => {
        const fileName = fileNameOf(card);
        const displayName = displayNameOf(card);
        const currentPhase = phaseOf(state);
        const myExcluded = new Set(myExcludedOf(state));
        const opponentExcluded = new Set(opponentExcludedOf(state));
        const required = requiredExclusionsOf(state);
        const ownSelected = myExcluded.has(fileName);
        const opponentSelected = opponentExcluded.has(fileName);
        const ownLimitReached = myExcluded.size >= required;

        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'minigame-card';
        button.dataset.cardFile = fileName;
        button.setAttribute('aria-label', displayName);
        button.setAttribute('aria-pressed', 'false');

        if (currentPhase === phase.choosingExclusions) {
            button.classList.toggle('is-excluded-own', ownSelected);
            button.classList.toggle('is-excluded-opponent', opponentSelected);
            button.setAttribute('aria-pressed', ownSelected ? 'true' : 'false');
            button.disabled = opponentSelected || (ownLimitReached && !ownSelected);
            button.addEventListener('click', async () => {
                clearErrors();
                button.disabled = true;
                try {
                    const next = await connection.invoke(
                        'ToggleExclusion',
                        currentRoomCode,
                        playerToken,
                        fileName);
                    applyState(next);
                } catch (error) {
                    showError(roomError, getErrorMessage(error));
                    button.disabled = false;
                }
            });
        } else if (currentPhase === phase.playing) {
            button.classList.toggle('is-highlighted', fileName === mySecretOf(state));
            button.classList.toggle('is-inactive', inactiveCards.has(fileName));
            button.setAttribute(
                'aria-pressed',
                inactiveCards.has(fileName) ? 'true' : 'false');
            button.addEventListener('click', () => toggleLocalInactive(button));
        } else {
            button.disabled = true;
        }

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
        return button;
    };

    const renderEntry = message => {
        entry.classList.remove('is-hidden');
        roomShell.classList.add('is-hidden');
        if (message) showError(entryError, message);
    };

    const renderRoom = state => {
        entry.classList.add('is-hidden');
        roomShell.classList.remove('is-hidden');
        const playerNumber = playerNumberOf(state);
        const playerCount = playerCountOf(state);
        const currentPhase = phaseOf(state);
        const required = requiredExclusionsOf(state);
        const myExcluded = myExcludedOf(state);

        roomCodeLabel.textContent = roomCodeOf(state);
        playerLabel.textContent = playerNumber === 1 ? text.playerOne : text.playerTwo;
        opponentStatus.textContent = playerCount < 2 ? text.waitingOpponent : '';

        if (currentPhase === phase.waitingForGame) {
            roomMessage.textContent = text.waitingGame;
        } else if (currentPhase === phase.choosingExclusions) {
            roomMessage.textContent = `${format(text.chooseExclusions, { count: required })} ${format(
                text.exclusionProgress,
                { selected: myExcluded.length, count: required })}`;
        } else if (currentPhase === phase.playing) {
            roomMessage.textContent = text.gameReady;
        } else {
            roomMessage.textContent = '';
        }

        const cards = cardsOf(state);
        grid.replaceChildren(...cards.map(card => createCard(card, state)));
        grid.classList.toggle('is-hidden', cards.length === 0);
        empty.classList.toggle('is-hidden', cards.length !== 0);
        requestAnimationFrame(layoutGrid);
    };

    const applyState = state => {
        if (!state) return;
        const gameNumber = gameNumberOf(state);
        if (gameNumber !== currentGameNumber) {
            inactiveCards.clear();
            currentGameNumber = gameNumber;
        }
        currentState = state;
        currentRoomCode = roomCodeOf(state).toUpperCase();
        renderRoom(state);
    };

    const applyMembership = membership => {
        if (!rememberMembership(membership)) {
            throw new Error('Invalid room membership response.');
        }
        applyState(stateOf(membership));
    };

    const synchronize = async () => {
        if (!currentRoomCode || !playerToken) return false;
        const state = await connection.invoke(
            'GetRoomState',
            currentRoomCode,
            playerToken);
        applyState(state);
        return true;
    };

    const refreshCatalog = async () => {
        const catalog = await connection.invoke('GetCatalog');
        maximumCardCount = Number(
            get(catalog, 'maximumCardCount', 'MaximumCardCount') ?? maximumCardCount);
        defaultCardCount = Number(
            get(catalog, 'defaultCardCount', 'DefaultCardCount') ?? defaultCardCount);
        newGameCount.min = String(minimumCardCount);
        newGameCount.max = String(maximumCardCount);
        newGameCount.value = String(defaultCardCount || minimumCardCount);
        newGameSubmit.disabled = maximumCardCount < minimumCardCount;
    };

    const openNewGameDialog = async () => {
        clearErrors();
        await touchRoom();
        try {
            await refreshCatalog();
        } catch (error) {
            showError(roomError, getErrorMessage(error));
            return;
        }
        if (!newGameDialog.open) newGameDialog.showModal();
    };

    createRoomButton.addEventListener('click', async () => {
        clearErrors();
        createRoomButton.disabled = true;
        try {
            const membership = await connection.invoke('CreateRoom');
            applyMembership(membership);
            await openNewGameDialog();
        } catch (error) {
            showError(entryError, getErrorMessage(error));
        } finally {
            createRoomButton.disabled = false;
        }
    });

    joinCodeInput.addEventListener('input', () => {
        joinCodeInput.value = joinCodeInput.value.toUpperCase().replace(/[^A-Z0-9]/g, '');
    });

    joinForm.addEventListener('submit', async event => {
        event.preventDefault();
        clearErrors();
        const code = joinCodeInput.value.trim().toUpperCase();
        if (!code) return;

        const submit = joinForm.querySelector('button[type="submit"]');
        if (submit) submit.disabled = true;
        try {
            const membership = await connection.invoke('JoinRoom', code);
            applyMembership(membership);
        } catch (error) {
            showError(entryError, getErrorMessage(error));
        } finally {
            if (submit) submit.disabled = false;
        }
    });

    newGameButton.addEventListener('click', () => void openNewGameDialog());

    newGameCancel.addEventListener('click', () => {
        void touchRoom();
        newGameDialog.close();
    });

    newGameForm.addEventListener('submit', async event => {
        event.preventDefault();
        clearErrors();
        const cardCount = Number.parseInt(newGameCount.value, 10);
        if (!Number.isInteger(cardCount) ||
            cardCount < minimumCardCount ||
            cardCount > maximumCardCount) {
            showError(newGameError, text.invalidCardCount);
            return;
        }

        newGameSubmit.disabled = true;
        try {
            const state = await connection.invoke(
                'StartNewGame',
                currentRoomCode,
                playerToken,
                cardCount);
            applyState(state);
            newGameDialog.close();
        } catch (error) {
            showError(newGameError, getErrorMessage(error));
        } finally {
            newGameSubmit.disabled = maximumCardCount < minimumCardCount;
        }
    });

    updateTopbarHeight();
    const gridObserver = new ResizeObserver(layoutGrid);
    gridObserver.observe(grid);
    window.addEventListener('resize', layoutGrid);

    const topbar = document.querySelector('.topbar');
    if (topbar) {
        const topbarObserver = new ResizeObserver(() => {
            updateTopbarHeight();
            requestAnimationFrame(layoutGrid);
        });
        topbarObserver.observe(topbar);
    }

    if (!window.signalR) {
        renderEntry(text.genericError);
        return;
    }

    connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build();

    connection.on('roomChanged', () => {
        if (currentRoomCode && playerToken) {
            void synchronize().catch(error => {
                showError(roomError, getErrorMessage(error));
            });
        }
    });

    connection.on('roomExpired', roomCode => {
        if (String(roomCode).toUpperCase() !== currentRoomCode) return;
        forgetMembership();
        renderEntry(text.roomExpired);
    });

    connection.onreconnecting(() => {
        createRoomButton.disabled = true;
        newGameButton.disabled = true;
    });

    connection.onreconnected(async () => {
        createRoomButton.disabled = false;
        newGameButton.disabled = false;
        if (currentRoomCode && playerToken) {
            try {
                await synchronize();
            } catch (error) {
                const message = getErrorMessage(error);
                forgetMembership();
                renderEntry(message);
            }
        }
    });

    const connect = async () => {
        try {
            await connection.start();
            createRoomButton.disabled = false;
            newGameButton.disabled = false;
            await refreshCatalog();

            const roomFromUrl = readRoomFromUrl();
            if (roomFromUrl) {
                const savedToken = window.localStorage.getItem(storageKey(roomFromUrl));
                if (savedToken) {
                    currentRoomCode = roomFromUrl;
                    playerToken = savedToken;
                    try {
                        await synchronize();
                        return;
                    } catch (error) {
                        const message = getErrorMessage(error);
                        forgetMembership();
                        renderEntry(message);
                        return;
                    }
                }
                joinCodeInput.value = roomFromUrl;
            }

            renderEntry('');
        } catch {
            createRoomButton.disabled = true;
            newGameButton.disabled = true;
            window.setTimeout(connect, 2000);
        }
    };

    void connect();
})();
