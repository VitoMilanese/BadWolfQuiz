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
    const cardHintDialog = root.querySelector('[data-card-hint-dialog]');
    const cardHintBody = cardHintDialog?.querySelector('.minigames-hint-dialog-body');
    const cardHintCurrent = root.querySelector('[data-card-hint-current]');
    const cardHintHistory = root.querySelector('[data-card-hint-history]');

    if (!hubUrl || !roomCodeLabel || !questionOptions || !newGameDialog ||
        !newGameForm || !newGameCount || !newGameQuestionCards ||
        !newGameFreeSelection || !newGameSubmit || !searchDialog || !searchInput ||
        !searchMessage || !searchResults || !searchPager || !searchPrevious ||
        !searchNext || !searchPage || !searchClose || !cardHintDialog || !cardHintBody ||
        !cardHintCurrent || !cardHintHistory) {
        return;
    }

    const hintCurrentSection = cardHintCurrent.closest('.minigames-hint-section');
    const hintHistorySection = cardHintHistory.closest('.minigames-hint-section');
    const hintHeader = cardHintBody.querySelector('.minigames-hint-dialog-header');
    if (!hintCurrentSection || !hintHistorySection || !hintHeader) return;

    const text = {
        choose: root.dataset.questionSearchChoose ?? 'Choose question',
        help: root.dataset.questionSearchHelp ?? 'Enter at least 3 characters.',
        noResults: root.dataset.questionSearchNoResults ?? 'No questions found.',
        loading: root.dataset.questionSearchLoading ?? 'Searching…',
        page: root.dataset.questionSearchPage ?? 'Page {0} of {1}',
        invalidCardCount: root.dataset.invalidCardCount ?? 'Invalid card count.',
        genericError: root.dataset.genericError ?? 'Something went wrong.',
        yes: root.dataset.yes ?? 'YES',
        no: root.dataset.no ?? 'NO',
        unavailable: root.dataset.hintsUnavailable ?? 'Information unavailable',
        hintCardsTab: root.dataset.hintsCurrentQuestions ?? 'Question cards',
        hintHistoryTab: root.dataset.hintsPreviousQuestions ?? 'Questions asked to opponent',
        hintSearchTab: (searchInput.placeholder || 'Search questions').replace(/[.…]+$/, '')
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
    let hintSearchGameKey = '';
    let hintSearchQuery = '';
    let hintSearchPage = 1;
    let hintSearchTotalPages = 0;
    let hintSearchRequestId = 0;
    let hintSearchDebounce = null;

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
    const hintItemsOf = result => get(result, 'items', 'Items') ?? [];
    const answerYesOf = value => {
        const answer = get(value, 'answerYes', 'AnswerYes');
        return answer === true ? true : answer === false ? false : null;
    };

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

    const hintTabs = document.createElement('div');
    hintTabs.className = 'minigames-hint-tabs is-hidden';
    hintTabs.setAttribute('role', 'tablist');

    const hintCardsTab = document.createElement('button');
    hintCardsTab.type = 'button';
    hintCardsTab.className = 'button minigames-hint-tab-button is-active';
    hintCardsTab.textContent = text.hintCardsTab;
    hintCardsTab.setAttribute('role', 'tab');
    hintCardsTab.setAttribute('aria-selected', 'true');

    const hintSearchTab = document.createElement('button');
    hintSearchTab.type = 'button';
    hintSearchTab.className = 'button minigames-hint-tab-button';
    hintSearchTab.textContent = text.hintSearchTab;
    hintSearchTab.setAttribute('role', 'tab');
    hintSearchTab.setAttribute('aria-selected', 'false');
    hintTabs.append(hintCardsTab, hintSearchTab);

    const hintCardsPanel = document.createElement('div');
    hintCardsPanel.className = 'minigames-hint-tab-panel minigames-hint-cards-panel';
    hintCardsPanel.setAttribute('role', 'tabpanel');
    hintCardsPanel.append(hintCurrentSection, hintHistorySection);

    const hintSearchPanel = document.createElement('div');
    hintSearchPanel.className = 'minigames-hint-tab-panel minigames-hint-search-panel is-hidden';
    hintSearchPanel.setAttribute('role', 'tabpanel');

    const hintSearchInput = document.createElement('input');
    hintSearchInput.type = 'search';
    hintSearchInput.autocomplete = 'off';
    hintSearchInput.spellcheck = false;
    hintSearchInput.minLength = minimumQueryLength;
    hintSearchInput.maxLength = 100;
    hintSearchInput.placeholder = searchInput.placeholder || text.hintSearchTab;

    const hintSearchMessage = document.createElement('p');
    hintSearchMessage.className = 'minigames-question-search-message';
    hintSearchMessage.textContent = text.help;
    hintSearchMessage.setAttribute('aria-live', 'polite');

    const hintSearchResults = document.createElement('div');
    hintSearchResults.className = 'minigames-hint-tab-search-results';

    const hintSearchPager = document.createElement('div');
    hintSearchPager.className = 'minigames-hint-tab-search-pager is-hidden';
    const hintSearchPrevious = document.createElement('button');
    hintSearchPrevious.type = 'button';
    hintSearchPrevious.className = 'button';
    hintSearchPrevious.textContent = searchPrevious.textContent ?? 'Previous';
    const hintSearchPageLabel = document.createElement('span');
    const hintSearchNext = document.createElement('button');
    hintSearchNext.type = 'button';
    hintSearchNext.className = 'button';
    hintSearchNext.textContent = searchNext.textContent ?? 'Next';
    hintSearchPager.append(hintSearchPrevious, hintSearchPageLabel, hintSearchNext);
    hintSearchPanel.append(
        hintSearchInput,
        hintSearchMessage,
        hintSearchResults,
        hintSearchPager);

    hintHeader.after(hintTabs);
    hintTabs.after(hintCardsPanel, hintSearchPanel);
    cardHintBody.classList.add('is-hint-tabs-ready');

    const resetHintSearch = () => {
        hintSearchQuery = '';
        hintSearchPage = 1;
        hintSearchTotalPages = 0;
        hintSearchRequestId++;
        if (hintSearchDebounce !== null) {
            window.clearTimeout(hintSearchDebounce);
            hintSearchDebounce = null;
        }
        hintSearchInput.value = '';
        hintSearchMessage.textContent = text.help;
        hintSearchResults.replaceChildren();
        hintSearchPager.classList.add('is-hidden');
        hintSearchPageLabel.textContent = '';
        hintSearchPrevious.disabled = true;
        hintSearchNext.disabled = true;
    };

    const selectHintTab = tab => {
        const showSearch = tab === 'search';
        hintCardsTab.classList.toggle('is-active', !showSearch);
        hintSearchTab.classList.toggle('is-active', showSearch);
        hintCardsTab.setAttribute('aria-selected', showSearch ? 'false' : 'true');
        hintSearchTab.setAttribute('aria-selected', showSearch ? 'true' : 'false');
        hintCardsPanel.classList.toggle('is-hidden', showSearch);
        hintSearchPanel.classList.toggle('is-hidden', !showSearch);
        if (showSearch) {
            window.setTimeout(() => hintSearchInput.focus(), 0);
        }
    };

    const updateHintTabsAvailability = () => {
        const available = cachedState &&
            cachedMode === searchMode &&
            questionsEnabledOf(cachedState);
        hintTabs.classList.toggle('is-hidden', !available);
        cardHintBody.classList.toggle('is-hint-tabs-visible', Boolean(available));
        hintCurrentSection.classList.toggle('is-hidden', Boolean(available));
        hintCurrentSection.style.display = available ? 'none' : '';
        hintCardsTab.textContent = available ? text.hintHistoryTab : text.hintCardsTab;
        if (!available) selectHintTab('cards');
    };

    const renderAfterBaseClient = () => {
        renderChooseQuestionButton();
        updateHintTabsAvailability();
        requestAnimationFrame(() => {
            renderChooseQuestionButton();
            updateHintTabsAvailability();
        });
        window.setTimeout(() => {
            renderChooseQuestionButton();
            updateHintTabsAvailability();
        }, 50);
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

    const hintAnswerClass = answerYes => {
        if (answerYes === true) return 'is-yes';
        if (answerYes === false) return 'is-no';
        return 'is-unavailable';
    };

    const hintAnswerText = answerYes => {
        if (answerYes === true) return text.yes;
        if (answerYes === false) return text.no;
        return text.unavailable;
    };

    const renderHintSearchResult = result => {
        hintSearchPage = pageOf(result);
        hintSearchTotalPages = totalPagesOf(result);
        const items = hintItemsOf(result);
        hintSearchResults.replaceChildren();

        if (items.length > 0) {
            const list = document.createElement('ul');
            list.className = 'minigames-hint-list';
            items.forEach(item => {
                const row = document.createElement('li');
                row.className = 'minigames-hint-row';
                const question = document.createElement('span');
                question.textContent = get(item, 'question', 'Question') ?? '';
                const answerYes = answerYesOf(item);
                const answer = document.createElement('strong');
                answer.className = `minigames-hint-answer ${hintAnswerClass(answerYes)}`;
                answer.textContent = hintAnswerText(answerYes);
                row.append(question, answer);
                list.appendChild(row);
            });
            hintSearchResults.appendChild(list);
        }

        hintSearchMessage.textContent = items.length === 0 ? text.noResults : '';
        const showPager = hintSearchTotalPages > 1;
        hintSearchPager.classList.toggle('is-hidden', !showPager);
        hintSearchPageLabel.textContent = showPager
            ? formatPage(hintSearchPage, hintSearchTotalPages)
            : '';
        hintSearchPrevious.disabled = hintSearchPage <= 1;
        hintSearchNext.disabled = hintSearchTotalPages === 0 ||
            hintSearchPage >= hintSearchTotalPages;
    };

    const executeHintSearch = async page => {
        const query = hintSearchInput.value.trim();
        hintSearchQuery = query;
        if (query.length < minimumQueryLength) {
            hintSearchPage = 1;
            hintSearchTotalPages = 0;
            hintSearchResults.replaceChildren();
            hintSearchPager.classList.add('is-hidden');
            hintSearchMessage.textContent = text.help;
            return;
        }

        if (!hintSearchGameKey || !resolveMembership()) return;
        const requestId = ++hintSearchRequestId;
        hintSearchMessage.textContent = text.loading;
        hintSearchResults.replaceChildren();
        hintSearchPager.classList.add('is-hidden');
        try {
            const result = await runBusy(async () => {
                const hub = await ensureConnection();
                return await hub.invoke(
                    'SearchCardHints',
                    currentRoomCode,
                    playerToken,
                    hintSearchGameKey,
                    query,
                    page);
            });
            if (requestId !== hintSearchRequestId ||
                hintSearchInput.value.trim() !== hintSearchQuery ||
                hintSearchPanel.classList.contains('is-hidden')) {
                return;
            }
            renderHintSearchResult(result);
        } catch {
            if (requestId === hintSearchRequestId) {
                hintSearchResults.replaceChildren();
                hintSearchPager.classList.add('is-hidden');
                hintSearchMessage.textContent = text.genericError;
            }
        }
    };

    const captureHintCard = trigger => {
        if (!trigger) return;
        const hideCurrentWhileOpening = !cachedState ||
            (cachedMode === searchMode && questionsEnabledOf(cachedState));
        hintCurrentSection.style.display = hideCurrentWhileOpening ? 'none' : '';
        hintSearchGameKey = trigger.dataset.minigameHintTrigger ?? '';
        resetHintSearch();
        selectHintTab('cards');
        hintTabs.classList.add('is-hidden');
        cardHintBody.classList.remove('is-hint-tabs-visible');
        void refreshQuestionMode();
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

    hintCardsTab.addEventListener('click', () => selectHintTab('cards'));
    hintSearchTab.addEventListener('click', () => selectHintTab('search'));
    hintSearchInput.addEventListener('input', () => {
        if (hintSearchDebounce !== null) window.clearTimeout(hintSearchDebounce);
        hintSearchDebounce = window.setTimeout(() => {
            hintSearchDebounce = null;
            void executeHintSearch(1);
        }, 220);
    });
    hintSearchPrevious.addEventListener('click', () => {
        if (hintSearchPage > 1) void executeHintSearch(hintSearchPage - 1);
    });
    hintSearchNext.addEventListener('click', () => {
        if (hintSearchPage < hintSearchTotalPages) {
            void executeHintSearch(hintSearchPage + 1);
        }
    });
    cardHintDialog.addEventListener('close', () => {
        hintSearchGameKey = '';
        resetHintSearch();
        selectHintTab('cards');
    });

    root.addEventListener('click', event => {
        const trigger = event.target.closest?.('[data-minigame-hint-trigger]');
        if (trigger) captureHintCard(trigger);
    }, true);
    root.addEventListener('keydown', event => {
        if (event.key !== 'Enter' && event.key !== ' ') return;
        const trigger = event.target.closest?.('[data-minigame-hint-trigger]');
        if (trigger) captureHintCard(trigger);
    }, true);

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