(() => {
    const root = document.querySelector('[data-minigames-root]');
    if (!root || !window.signalR) return;

    const hubUrl = root.dataset.hubUrl;
    const roomCodeLabel = root.querySelector('[data-room-code]');
    const questionOptions = root.querySelector('[data-question-options]');
    const newGameDialog = root.querySelector('[data-new-game-dialog]');
    const newGameForm = root.querySelector('[data-new-game-form]');
    const newGameCount = root.querySelector('[data-new-game-count]');
    const newGameQuestionCards = root.querySelector('[data-new-game-question-cards]');
    const newGameFreeSelection = root.querySelector('[data-new-game-free-question-selection]');
    const newGameSoloAi = root.querySelector('[data-new-game-solo-ai]');
    const newGameAllowHints = root.querySelector('[data-new-game-allow-hints]');
    const newGameSubmit = root.querySelector('[data-new-game-submit]');
    const newGameError = root.querySelector('[data-new-game-error]');
    const searchDialog = root.querySelector('[data-question-search-dialog]');
    const searchInput = root.querySelector('[data-question-search-input]');
    const searchMessage = root.querySelector('[data-question-search-message]');
    const searchResults = root.querySelector('[data-question-search-results]');
    const searchPager = root.querySelector('[data-question-search-pager]');
    const searchPrevious = root.querySelector('[data-question-search-prev]');
    const searchNext = root.querySelector('[data-question-search-next]');
    const searchPage = root.querySelector('[data-question-search-page]');
    const searchClose = root.querySelector('[data-question-search-close]');

    if (!hubUrl || !roomCodeLabel || !questionOptions || !newGameDialog ||
        !newGameForm || !newGameCount || !newGameQuestionCards ||
        !newGameFreeSelection || !newGameSubmit || !searchDialog || !searchInput ||
        !searchMessage || !searchResults || !searchPager || !searchPrevious ||
        !searchNext || !searchPage || !searchClose) {
        return;
    }

    const text = {
        choose: root.dataset.questionSearchChoose ?? 'Choose question',
        help: root.dataset.questionSearchHelp ?? 'Enter at least 3 characters.',
        noResults: root.dataset.questionSearchNoResults ?? 'No questions found.',
        loading: root.dataset.questionSearchLoading ?? 'Searching…',
        page: root.dataset.questionSearchPage ?? 'Page {0} of {1}',
        invalidCardCount: root.dataset.invalidCardCount ?? 'Invalid card count.',
        genericError: root.dataset.genericError ?? 'Something went wrong.'
    };

    const storagePrefix = 'badwolf-minigame-player:';
    const searchMode = 1;
    const playingPhase = 2;
    const minimumQueryLength = 3;

    let connection = null;
    let currentRoomCode = '';
    let playerToken = '';
    let cachedState = null;
    let cachedMode = 0;
    let currentQuery = '';
    let currentPage = 1;
    let totalPages = 0;
    let searchRequestId = 0;
    let searchDebounce = null;
    let applyingQuestionButton = false;

    const get = (value, camelName, pascalName) =>
        value?.[camelName] ?? value?.[pascalName];
    const playerNumberOf = state => Number(get(state, 'playerNumber', 'PlayerNumber') ?? 0);
    const currentPlayerOf = state => Number(get(state, 'currentPlayerNumber', 'CurrentPlayerNumber') ?? 0);
    const phaseOf = state => Number(get(state, 'phase', 'Phase') ?? 0);
    const selectedOf = state => Boolean(get(state, 'hasSelectedQuestionThisTurn', 'HasSelectedQuestionThisTurn') ?? false);
    const pendingPlayerOf = state => Number(get(state, 'pendingQuestionResponsePlayerNumber', 'PendingQuestionResponsePlayerNumber') ?? 0);
    const questionsEnabledOf = state => Boolean(get(state, 'questionCardsEnabled', 'QuestionCardsEnabled') ?? false);
    const questionsOf = result => get(result, 'questions', 'Questions') ?? [];
    const pageOf = result => Number(get(result, 'page', 'Page') ?? 1);
    const totalPagesOf = result => Number(get(result, 'totalPages', 'TotalPages') ?? 0);

    const storageKey = roomCode => `${storagePrefix}${roomCode.toUpperCase()}`;

    const resolveMembership = () => {
        const fromLabel = (roomCodeLabel.textContent ?? '').trim().toUpperCase();
        const fromUrl = (new URLSearchParams(window.location.search).get('room') ?? '')
            .trim()
            .toUpperCase();
        currentRoomCode = fromLabel || fromUrl;
        playerToken = currentRoomCode
            ? window.localStorage.getItem(storageKey(currentRoomCode)) ?? ''
            : '';
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
                void refreshQuestionMode();
            });
            connection.onreconnected(() => {
                void refreshQuestionMode();
            });
        }

        if (connection.state === signalR.HubConnectionState.Disconnected) {
            await connection.start();
        }
        return connection;
    };

    const setNewGameError = message => {
        if (!newGameError) return;
        newGameError.textContent = message || '';
        newGameError.classList.toggle('is-hidden', !message);
    };

    const syncFreeSelectionControl = () => {
        const soloChecked = Boolean(newGameSoloAi?.checked);
        const available = newGameQuestionCards.checked &&
            (!newGameQuestionCards.disabled || soloChecked);
        newGameFreeSelection.disabled = !available;
        if (!newGameQuestionCards.checked) {
            newGameFreeSelection.checked = false;
        }
    };

    const canChooseQuestion = state =>
        state &&
        phaseOf(state) === playingPhase &&
        playerNumberOf(state) === currentPlayerOf(state) &&
        !selectedOf(state) &&
        pendingPlayerOf(state) === 0;

    const renderChooseQuestionButton = () => {
        if (applyingQuestionButton || !cachedState || cachedMode !== searchMode ||
            !questionsEnabledOf(cachedState)) {
            if (cachedMode !== searchMode && searchDialog.open) searchDialog.close();
            return;
        }

        let button = questionOptions.querySelector('[data-open-question-search]');
        if (!button || questionOptions.children.length !== 1) {
            applyingQuestionButton = true;
            button = document.createElement('button');
            button.type = 'button';
            button.className = 'button minigames-question-option minigames-question-search-open';
            button.dataset.openQuestionSearch = '';
            button.textContent = text.choose;
            button.addEventListener('click', () => {
                if (!canChooseQuestion(cachedState)) return;
                currentQuery = '';
                currentPage = 1;
                totalPages = 0;
                searchInput.value = '';
                searchResults.replaceChildren();
                searchPager.classList.add('is-hidden');
                searchMessage.textContent = text.help;
                if (!searchDialog.open) searchDialog.showModal();
                window.setTimeout(() => searchInput.focus(), 0);
            });
            questionOptions.replaceChildren(button);
            applyingQuestionButton = false;
        }

        button.disabled = !canChooseQuestion(cachedState);
    };

    const renderAfterBaseClient = () => {
        renderChooseQuestionButton();
        requestAnimationFrame(renderChooseQuestionButton);
        window.setTimeout(renderChooseQuestionButton, 50);
    };

    const refreshQuestionMode = async () => {
        if (!resolveMembership()) return;
        try {
            const hub = await ensureConnection();
            const [state, mode] = await Promise.all([
                hub.invoke('GetRoomState', currentRoomCode, playerToken, false),
                hub.invoke('GetQuestionSelectionMode', currentRoomCode, playerToken)
            ]);
            cachedState = state;
            cachedMode = Number(mode ?? 0);
            renderAfterBaseClient();
        } catch {
        }
    };

    const formatPage = (page, pages) =>
        text.page
            .replaceAll('{0}', String(page))
            .replaceAll('{1}', String(pages));

    const renderSearchResult = result => {
        currentPage = pageOf(result);
        totalPages = totalPagesOf(result);
        const questions = questionsOf(result);
        searchResults.replaceChildren(...questions.map(question => {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'button minigames-question-search-result';
            button.textContent = question;
            button.addEventListener('click', async () => {
                if (!resolveMembership()) return;
                button.disabled = true;
                try {
                    const hub = await ensureConnection();
                    await hub.invoke(
                        'SelectQuestionByText',
                        currentRoomCode,
                        playerToken,
                        question);
                    searchDialog.close();
                    await refreshQuestionMode();
                } catch {
                    button.disabled = false;
                    searchMessage.textContent = text.genericError;
                }
            });
            return button;
        }));

        searchMessage.textContent = questions.length === 0 ? text.noResults : '';
        const showPager = totalPages > 1;
        searchPager.classList.toggle('is-hidden', !showPager);
        searchPage.textContent = showPager ? formatPage(currentPage, totalPages) : '';
        searchPrevious.disabled = currentPage <= 1;
        searchNext.disabled = totalPages === 0 || currentPage >= totalPages;
    };

    const executeSearch = async page => {
        const query = searchInput.value.trim();
        currentQuery = query;
        if (query.length < minimumQueryLength) {
            currentPage = 1;
            totalPages = 0;
            searchResults.replaceChildren();
            searchPager.classList.add('is-hidden');
            searchMessage.textContent = text.help;
            return;
        }

        if (!resolveMembership()) return;
        const requestId = ++searchRequestId;
        searchMessage.textContent = text.loading;
        searchResults.replaceChildren();
        searchPager.classList.add('is-hidden');
        try {
            const hub = await ensureConnection();
            const result = await hub.invoke(
                'SearchAvailableQuestions',
                currentRoomCode,
                playerToken,
                query,
                page);
            if (requestId !== searchRequestId || searchInput.value.trim() !== currentQuery) {
                return;
            }
            renderSearchResult(result);
        } catch {
            if (requestId === searchRequestId) {
                searchMessage.textContent = text.genericError;
            }
        }
    };

    searchInput.addEventListener('input', () => {
        if (searchDebounce !== null) window.clearTimeout(searchDebounce);
        searchDebounce = window.setTimeout(() => {
            searchDebounce = null;
            void executeSearch(1);
        }, 220);
    });

    searchPrevious.addEventListener('click', () => {
        if (currentPage > 1) void executeSearch(currentPage - 1);
    });
    searchNext.addEventListener('click', () => {
        if (currentPage < totalPages) void executeSearch(currentPage + 1);
    });
    searchClose.addEventListener('click', () => searchDialog.close());
    searchDialog.addEventListener('cancel', () => searchDialog.close());

    newGameQuestionCards.addEventListener('change', syncFreeSelectionControl);
    newGameSoloAi?.addEventListener('change', () => {
        window.setTimeout(syncFreeSelectionControl, 0);
    });

    const newGameDialogObserver = new MutationObserver(() => {
        if (!newGameDialog.open) return;
        newGameFreeSelection.checked = false;
        window.setTimeout(syncFreeSelectionControl, 0);
    });
    newGameDialogObserver.observe(newGameDialog, {
        attributes: true,
        attributeFilter: ['open']
    });

    newGameForm.addEventListener('submit', async event => {
        if (!newGameFreeSelection.checked) return;

        event.preventDefault();
        event.stopImmediatePropagation();
        setNewGameError('');
        if (!resolveMembership() || !newGameQuestionCards.checked) {
            setNewGameError(text.genericError);
            return;
        }

        const cardCount = Number.parseInt(newGameCount.value, 10);
        const minimum = Number.parseInt(newGameCount.min, 10);
        const maximum = Number.parseInt(newGameCount.max, 10);
        if (!Number.isInteger(cardCount) ||
            !Number.isInteger(minimum) ||
            !Number.isInteger(maximum) ||
            cardCount < minimum || cardCount > maximum) {
            setNewGameError(text.invalidCardCount);
            return;
        }

        newGameSubmit.disabled = true;
        try {
            const hub = await ensureConnection();
            const hintsEnabled = Boolean(newGameAllowHints?.checked);
            if (newGameSoloAi?.checked) {
                await hub.invoke(
                    'StartNewSoloGameWithOptions',
                    currentRoomCode,
                    playerToken,
                    cardCount,
                    hintsEnabled,
                    true);
            } else {
                await hub.invoke(
                    'StartNewGameWithOptions',
                    currentRoomCode,
                    playerToken,
                    cardCount,
                    true,
                    hintsEnabled,
                    true);
            }
            newGameDialog.close();
            await refreshQuestionMode();
        } catch {
            setNewGameError(text.genericError);
        } finally {
            newGameSubmit.disabled = false;
        }
    }, true);

    const optionsObserver = new MutationObserver(() => {
        if (!applyingQuestionButton) renderChooseQuestionButton();
    });
    optionsObserver.observe(questionOptions, { childList: true });

    syncFreeSelectionControl();
    void ensureConnection()
        .then(refreshQuestionMode)
        .catch(() => {});
})();