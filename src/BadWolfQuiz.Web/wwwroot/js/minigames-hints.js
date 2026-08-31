(() => {
    const root = document.querySelector('[data-minigames-root]');
    if (!root || !window.signalR) return;

    const hubUrl = root.dataset.hubUrl;
    const roomCodeLabel = root.querySelector('[data-room-code]');
    const grid = root.querySelector('[data-minigames-grid]');
    const newGameDialog = root.querySelector('[data-new-game-dialog]');
    const newGameForm = root.querySelector('[data-new-game-form]');
    const newGameCount = root.querySelector('[data-new-game-count]');
    const newGameQuestionCards = root.querySelector('[data-new-game-question-cards]');
    const newGameAllowHints = root.querySelector('[data-new-game-allow-hints]');
    const newGameSubmit = root.querySelector('[data-new-game-submit]');
    const newGameError = root.querySelector('[data-new-game-error]');
    const questionResponseHint = root.querySelector('[data-question-response-hint]');
    const cardHintDialog = root.querySelector('[data-card-hint-dialog]');
    const cardHintTitle = root.querySelector('[data-card-hint-title]');
    const cardHintCurrent = root.querySelector('[data-card-hint-current]');
    const cardHintHistory = root.querySelector('[data-card-hint-history]');
    const cardHintClose = root.querySelector('[data-card-hint-close]');

    if (!hubUrl || !roomCodeLabel || !grid || !newGameDialog || !newGameForm ||
        !newGameCount || !newGameQuestionCards || !newGameAllowHints ||
        !newGameSubmit || !questionResponseHint || !cardHintDialog || !cardHintTitle ||
        !cardHintCurrent || !cardHintHistory || !cardHintClose) {
        return;
    }

    const cardHintCurrentSection = cardHintCurrent.closest('.minigames-hint-section');
    if (!cardHintCurrentSection) return;

    const text = {
        yes: root.dataset.yes ?? 'YES',
        no: root.dataset.no ?? 'NO',
        iconLabel: root.dataset.hintsIconLabel ?? 'Show hints',
        title: root.dataset.hintsTitle ?? 'Hints',
        noCurrent: root.dataset.hintsNoCurrentQuestions ?? 'No Question cards remain.',
        noPrevious: root.dataset.hintsNoPreviousQuestions ?? 'No previous questions.',
        unavailable: root.dataset.hintsUnavailable ?? 'Information unavailable',
        responseLabel: root.dataset.hintsResponseLabel ?? 'Hint',
        loading: root.dataset.hintsLoading ?? 'Loading hints…',
        genericError: root.dataset.genericError ?? 'Something went wrong.',
        invalidCardCount: root.dataset.invalidCardCount ?? 'Invalid card count.',
        questionsUnavailable: root.dataset.questionsUnavailable ?? 'Questions are unavailable.'
    };

    const phase = {
        waitingForGame: 0,
        choosingExclusions: 1,
        playing: 2,
        finished: 3
    };
    const storagePrefix = 'badwolf-minigame-player:';
    const minimumSearchLength = 3;

    let connection = null;
    let currentRoomCode = '';
    let playerToken = '';
    let currentState = null;
    let hintsEnabled = false;
    let responseHintRequestKey = '';
    let refreshPromise = null;
    let searchGameKey = '';
    let searchRequestVersion = 0;

    const get = (value, camelName, pascalName) =>
        value?.[camelName] ?? value?.[pascalName];
    const phaseOf = value => Number(get(value, 'phase', 'Phase') ?? 0);
    const gameNumberOf = value => Number(get(value, 'gameNumber', 'GameNumber') ?? 0);
    const playerNumberOf = value => Number(get(value, 'playerNumber', 'PlayerNumber') ?? 0);
    const questionCardsEnabledOf = value =>
        Boolean(get(value, 'questionCardsEnabled', 'QuestionCardsEnabled') ?? false);
    const pendingQuestionOf = value => get(value, 'pendingQuestion', 'PendingQuestion') ?? '';
    const pendingResponsePlayerOf = value =>
        Number(get(value, 'pendingQuestionResponsePlayerNumber', 'PendingQuestionResponsePlayerNumber') ?? 0);
    const answerYesOf = value => {
        const answer = get(value, 'answerYes', 'AnswerYes');
        return answer === true ? true : answer === false ? false : null;
    };

    const storageKey = roomCode => `${storagePrefix}${roomCode.toUpperCase()}`;

    const roomFromUrl = () =>
        (new URLSearchParams(window.location.search).get('room') ?? '')
            .trim()
            .toUpperCase();

    const resolveMembership = () => {
        const roomCode = (roomCodeLabel.textContent ?? '').trim().toUpperCase() || roomFromUrl();
        const token = roomCode
            ? window.localStorage.getItem(storageKey(roomCode)) ?? ''
            : '';
        currentRoomCode = roomCode;
        playerToken = token;
        return Boolean(currentRoomCode && playerToken);
    };

    const ensureConnection = async () => {
        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            return connection;
        }

        if (!connection) {
            connection = new signalR.HubConnectionBuilder()
                .withUrl(hubUrl)
                .withAutomaticReconnect()
                .build();

            connection.on('roomChanged', () => {
                void refreshState();
            });
            connection.onreconnected(() => {
                void refreshState();
            });
            connection.onreconnecting(() => {
                hintsEnabled = false;
                clearHintUi();
            });
        }

        if (connection.state === signalR.HubConnectionState.Disconnected) {
            await connection.start();
        }
        return connection;
    };

    const runBusy = async action => {
        const busy = window.BadWolfBusy;
        const ownsBusy = Boolean(busy && !busy.isBusy && busy.show());
        try {
            return await action();
        } finally {
            if (ownsBusy) busy.hide();
        }
    };

    const setNewGameError = message => {
        if (!newGameError) return;
        newGameError.textContent = message || '';
        newGameError.classList.toggle('is-hidden', !message);
    };

    const getNewGameError = error => {
        const message = String(error?.message ?? error ?? '');
        if (message.includes('MINIGAME_ROOM_INVALIDCARDCOUNT')) return text.invalidCardCount;
        if (message.includes('MINIGAME_ROOM_QUESTIONSUNAVAILABLE')) return text.questionsUnavailable;
        return text.genericError;
    };

    newGameAllowHints.disabled = false;
    const newGameDialogObserver = new MutationObserver(() => {
        if (!newGameDialog.open) return;
        newGameAllowHints.checked = false;
        newGameAllowHints.disabled = false;
    });
    newGameDialogObserver.observe(newGameDialog, {
        attributes: true,
        attributeFilter: ['open']
    });

    newGameForm.addEventListener('submit', async event => {
        event.preventDefault();
        event.stopImmediatePropagation();
        setNewGameError('');

        const cardCount = Number.parseInt(newGameCount.value, 10);
        const minimum = Number.parseInt(newGameCount.min, 10);
        const maximum = Number.parseInt(newGameCount.max, 10);
        if (!Number.isInteger(cardCount) ||
            cardCount < minimum ||
            cardCount > maximum) {
            setNewGameError(text.invalidCardCount);
            return;
        }

        if (!resolveMembership()) {
            setNewGameError(text.genericError);
            return;
        }

        newGameSubmit.disabled = true;
        try {
            const hub = await ensureConnection();
            await hub.invoke(
                'StartNewGameWithHints',
                currentRoomCode,
                playerToken,
                cardCount,
                newGameQuestionCards.checked,
                newGameAllowHints.checked);
            newGameDialog.close();
            await refreshState();
        } catch (error) {
            setNewGameError(getNewGameError(error));
        } finally {
            newGameSubmit.disabled = Number.isFinite(maximum) &&
                maximum < Number.parseInt(newGameCount.min, 10);
        }
    }, true);

    const answerLabel = answerYes => {
        if (answerYes === true) return text.yes;
        if (answerYes === false) return text.no;
        return text.unavailable;
    };

    const answerClass = answerYes => {
        if (answerYes === true) return 'is-yes';
        if (answerYes === false) return 'is-no';
        return 'is-unavailable';
    };

    const createHintList = rows => {
        const list = document.createElement('ul');
        list.className = 'minigames-hint-list';
        rows.forEach(row => {
            const item = document.createElement('li');
            item.className = 'minigames-hint-row';

            const question = document.createElement('span');
            question.textContent = get(row, 'question', 'Question') ?? '';

            const answerYes = answerYesOf(row);
            const answer = document.createElement('strong');
            answer.className = `minigames-hint-answer ${answerClass(answerYes)}`;
            answer.textContent = answerLabel(answerYes);

            item.append(question, answer);
            list.appendChild(item);
        });
        return list;
    };

    const replaceHintList = (container, rows, emptyText) => {
        if (!Array.isArray(rows) || rows.length === 0) {
            const empty = document.createElement('p');
            empty.className = 'minigames-hint-empty';
            empty.textContent = emptyText;
            container.replaceChildren(empty);
            return;
        }
        container.replaceChildren(createHintList(rows));
    };

    const setHintLoading = () => {
        cardHintCurrentSection.classList.remove('is-hidden');
        cardHintCurrent.replaceChildren();
        cardHintHistory.replaceChildren();
        const loading = document.createElement('p');
        loading.className = 'minigames-hint-loading';
        loading.textContent = text.loading;
        cardHintCurrent.replaceChildren(loading);
    };

    const renderCardHint = snapshot => {
        const gameName = get(snapshot, 'gameName', 'GameName') ?? '';
        const pinned = get(snapshot, 'pinnedQuestions', 'PinnedQuestions') ?? [];
        const asked = get(snapshot, 'askedQuestions', 'AskedQuestions') ?? [];
        cardHintTitle.textContent = gameName ? `${text.title}: ${gameName}` : text.title;
        cardHintCurrentSection.classList.remove('is-hidden');
        replaceHintList(cardHintCurrent, pinned, text.noCurrent);
        replaceHintList(cardHintHistory, asked, text.noPrevious);
    };

    const searchDialog = document.createElement('dialog');
    searchDialog.className = 'minigames-dialog minigames-hint-dialog minigames-hint-search-dialog';

    const searchBody = document.createElement('div');
    searchBody.className = 'minigames-hint-dialog-body minigames-hint-search-body';

    const searchHeader = document.createElement('header');
    searchHeader.className = 'minigames-hint-dialog-header';
    const searchTitle = document.createElement('h2');
    const searchClose = document.createElement('button');
    searchClose.className = 'button minigames-hint-close';
    searchClose.type = 'button';
    searchClose.textContent = '×';
    searchClose.title = cardHintClose.title || text.title;
    searchClose.setAttribute('aria-label', cardHintClose.getAttribute('aria-label') || text.title);
    searchHeader.append(searchTitle, searchClose);

    const searchForm = document.createElement('form');
    searchForm.className = 'minigames-hint-search-form';
    const searchInput = document.createElement('input');
    searchInput.type = 'search';
    searchInput.required = true;
    searchInput.minLength = minimumSearchLength;
    searchInput.maxLength = 100;
    searchInput.autocomplete = 'off';
    searchInput.spellcheck = false;
    searchInput.placeholder = `${text.title}…`;
    searchInput.setAttribute('aria-label', text.title);
    const searchSubmit = document.createElement('button');
    searchSubmit.className = 'button';
    searchSubmit.type = 'submit';
    searchSubmit.textContent = '⌕';
    searchSubmit.title = text.title;
    searchSubmit.setAttribute('aria-label', text.title);
    searchForm.append(searchInput, searchSubmit);

    const searchSection = document.createElement('section');
    searchSection.className = 'minigames-hint-section minigames-hint-search-section';
    const searchResults = document.createElement('div');
    searchResults.className = 'minigames-hint-search-results';
    const searchPaging = document.createElement('div');
    searchPaging.className = 'minigames-hint-search-paging';
    const searchPrevious = document.createElement('button');
    searchPrevious.className = 'button';
    searchPrevious.type = 'button';
    searchPrevious.textContent = '←';
    searchPrevious.disabled = true;
    const searchPageStatus = document.createElement('span');
    searchPageStatus.className = 'minigames-hint-search-page';
    const searchNext = document.createElement('button');
    searchNext.className = 'button';
    searchNext.type = 'button';
    searchNext.textContent = '→';
    searchNext.disabled = true;
    searchPaging.append(searchPrevious, searchPageStatus, searchNext);
    searchSection.append(searchResults, searchPaging);
    searchBody.append(searchHeader, searchForm, searchSection);
    searchDialog.append(searchBody);
    root.appendChild(searchDialog);

    const renderSearchSnapshot = snapshot => {
        const items = get(snapshot, 'items', 'Items') ?? [];
        const page = Number(get(snapshot, 'page', 'Page') ?? 1);
        const totalPages = Number(get(snapshot, 'totalPages', 'TotalPages') ?? 0);
        const totalCount = Number(get(snapshot, 'totalCount', 'TotalCount') ?? 0);
        const gameName = get(snapshot, 'gameName', 'GameName') ?? '';
        if (gameName) searchTitle.textContent = `${text.title}: ${gameName}`;

        searchResults.replaceChildren();
        if (Array.isArray(items) && items.length > 0) {
            searchResults.appendChild(createHintList(items));
        }
        searchPageStatus.textContent = totalPages > 0
            ? `${page} / ${totalPages} · ${totalCount}`
            : '0';
        searchPrevious.disabled = totalPages === 0 || page <= 1;
        searchNext.disabled = totalPages === 0 || page >= totalPages;
        searchPrevious.dataset.page = String(Math.max(1, page - 1));
        searchNext.dataset.page = String(page + 1);
    };

    const searchCardHints = async page => {
        const query = searchInput.value.trim();
        if (query.length < minimumSearchLength) {
            searchInput.setCustomValidity(' ');
            searchInput.reportValidity();
            searchInput.setCustomValidity('');
            return;
        }
        if (!searchGameKey || !resolveMembership()) return;

        const requestVersion = ++searchRequestVersion;
        try {
            const snapshot = await runBusy(async () => {
                const hub = await ensureConnection();
                return await hub.invoke(
                    'SearchCardHints',
                    currentRoomCode,
                    playerToken,
                    searchGameKey,
                    query,
                    page);
            });
            if (requestVersion !== searchRequestVersion || !searchDialog.open) return;
            renderSearchSnapshot(snapshot);
        } catch {
            if (requestVersion !== searchRequestVersion || !searchDialog.open) return;
            searchResults.replaceChildren();
            searchPageStatus.textContent = text.genericError;
            searchPrevious.disabled = true;
            searchNext.disabled = true;
        }
    };

    const openSearchHint = (gameKey, gameName) => {
        searchGameKey = gameKey;
        searchRequestVersion++;
        searchTitle.textContent = gameName ? `${text.title}: ${gameName}` : text.title;
        searchInput.value = '';
        searchResults.replaceChildren();
        searchPageStatus.textContent = '';
        searchPrevious.disabled = true;
        searchNext.disabled = true;
        if (!searchDialog.open) searchDialog.showModal();
        window.setTimeout(() => searchInput.focus(), 0);
    };

    searchForm.addEventListener('submit', event => {
        event.preventDefault();
        void searchCardHints(1);
    });
    searchPrevious.addEventListener('click', () => {
        void searchCardHints(Number(searchPrevious.dataset.page || 1));
    });
    searchNext.addEventListener('click', () => {
        void searchCardHints(Number(searchNext.dataset.page || 1));
    });
    searchClose.addEventListener('click', () => searchDialog.close());
    searchDialog.addEventListener('close', () => {
        searchGameKey = '';
        searchRequestVersion++;
        searchInput.value = '';
        searchResults.replaceChildren();
        searchPageStatus.textContent = '';
    });

    const openCardHint = async (gameKey, gameName) => {
        if (!hintsEnabled || !currentState || phaseOf(currentState) !== phase.playing ||
            !resolveMembership()) {
            return;
        }

        if (!questionCardsEnabledOf(currentState)) {
            openSearchHint(gameKey, gameName);
            return;
        }

        cardHintTitle.textContent = text.title;
        setHintLoading();
        if (!cardHintDialog.open) cardHintDialog.showModal();

        try {
            const hub = await ensureConnection();
            const snapshot = await hub.invoke(
                'GetCardHints',
                currentRoomCode,
                playerToken,
                gameKey);
            if (!cardHintDialog.open) return;
            renderCardHint(snapshot);
        } catch {
            if (!cardHintDialog.open) return;
            const error = document.createElement('p');
            error.className = 'minigames-hint-empty';
            error.textContent = text.genericError;
            cardHintCurrent.replaceChildren(error);
            cardHintHistory.replaceChildren();
        }
    };

    const createHintTrigger = (gameKey, gameName) => {
        const trigger = document.createElement('span');
        trigger.className = 'minigame-card-hint-trigger';
        trigger.dataset.minigameHintTrigger = gameKey;
        trigger.setAttribute('role', 'button');
        trigger.setAttribute('tabindex', '0');
        trigger.setAttribute('aria-label', text.iconLabel);
        trigger.setAttribute('title', text.iconLabel);
        trigger.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true" focusable="false"><circle cx="12" cy="12" r="9"></circle><path d="M12 10.5v6"></path><path d="M12 7.5h.01"></path></svg>';

        trigger.addEventListener('pointerdown', event => {
            event.preventDefault();
            event.stopPropagation();
        });
        trigger.addEventListener('click', event => {
            event.preventDefault();
            event.stopPropagation();
            void openCardHint(gameKey, gameName);
        });
        trigger.addEventListener('keydown', event => {
            if (event.key !== 'Enter' && event.key !== ' ') return;
            event.preventDefault();
            event.stopPropagation();
            void openCardHint(gameKey, gameName);
        });
        return trigger;
    };

    const decorateCards = () => {
        const shouldShow = hintsEnabled && currentState && phaseOf(currentState) === phase.playing;
        grid.querySelectorAll('.minigame-card').forEach(card => {
            const frame = card.querySelector('.minigame-card-frame');
            const gameKey = card.dataset.cardFile;
            const gameName = card.querySelector('.minigame-card-name')?.textContent?.trim() ?? '';
            const existing = frame?.querySelector('[data-minigame-hint-trigger]');
            if (!shouldShow || !frame || !gameKey) {
                existing?.remove();
                return;
            }
            if (!existing) frame.appendChild(createHintTrigger(gameKey, gameName));
        });
    };

    const hideQuestionResponseHint = () => {
        responseHintRequestKey = '';
        questionResponseHint.textContent = '';
        questionResponseHint.classList.add('is-hidden');
    };

    const updateQuestionResponseHint = async () => {
        if (!hintsEnabled || !currentState || phaseOf(currentState) !== phase.playing) {
            hideQuestionResponseHint();
            return;
        }

        const pendingQuestion = pendingQuestionOf(currentState);
        const playerNumber = playerNumberOf(currentState);
        if (!pendingQuestion || pendingResponsePlayerOf(currentState) !== playerNumber) {
            hideQuestionResponseHint();
            return;
        }

        const requestKey = `${gameNumberOf(currentState)}:${playerNumber}:${pendingQuestion}`;
        if (responseHintRequestKey === requestKey &&
            !questionResponseHint.classList.contains('is-hidden')) {
            return;
        }
        responseHintRequestKey = requestKey;
        questionResponseHint.classList.remove('is-hidden');
        questionResponseHint.textContent = `${text.responseLabel}: ${text.loading}`;

        try {
            const hub = await ensureConnection();
            const snapshot = await hub.invoke(
                'GetQuestionResponseHint',
                currentRoomCode,
                playerToken);
            if (responseHintRequestKey !== requestKey) return;
            const answerYes = answerYesOf(snapshot);
            questionResponseHint.replaceChildren();
            const label = document.createElement('strong');
            label.textContent = `${text.responseLabel}: `;
            questionResponseHint.append(label, document.createTextNode(answerLabel(answerYes)));
        } catch {
            if (responseHintRequestKey !== requestKey) return;
            questionResponseHint.replaceChildren();
            const label = document.createElement('strong');
            label.textContent = `${text.responseLabel}: `;
            questionResponseHint.append(label, document.createTextNode(text.unavailable));
        }
    };

    const closeHintDialogs = () => {
        if (cardHintDialog.open) cardHintDialog.close();
        if (searchDialog.open) searchDialog.close();
    };

    const clearHintUi = () => {
        grid.querySelectorAll('[data-minigame-hint-trigger]').forEach(item => item.remove());
        hideQuestionResponseHint();
        closeHintDialogs();
    };

    const refreshState = async () => {
        if (refreshPromise) return refreshPromise;
        refreshPromise = (async () => {
            if (!resolveMembership()) {
                currentState = null;
                hintsEnabled = false;
                clearHintUi();
                return;
            }

            try {
                const hub = await ensureConnection();
                currentState = await hub.invoke(
                    'GetRoomState',
                    currentRoomCode,
                    playerToken,
                    false);
                hintsEnabled = Boolean(await hub.invoke(
                    'GetHintsEnabled',
                    currentRoomCode,
                    playerToken));
                if (!hintsEnabled || phaseOf(currentState) !== phase.playing) {
                    closeHintDialogs();
                }
                decorateCards();
                await updateQuestionResponseHint();
            } catch {
                currentState = null;
                hintsEnabled = false;
                clearHintUi();
            }
        })().finally(() => {
            refreshPromise = null;
        });
        return refreshPromise;
    };

    cardHintClose.addEventListener('click', () => cardHintDialog.close());
    cardHintDialog.addEventListener('close', () => {
        cardHintTitle.textContent = text.title;
        cardHintCurrent.replaceChildren();
        cardHintHistory.replaceChildren();
    });

    const gridObserver = new MutationObserver(() => {
        decorateCards();
    });
    gridObserver.observe(grid, { childList: true, subtree: true });

    const roomObserver = new MutationObserver(() => {
        void refreshState();
    });
    roomObserver.observe(roomCodeLabel, { childList: true, characterData: true, subtree: true });

    void ensureConnection()
        .then(() => refreshState())
        .catch(() => {
            hintsEnabled = false;
            clearHintUi();
        });
})();
