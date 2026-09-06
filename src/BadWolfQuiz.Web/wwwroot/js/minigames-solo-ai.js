(() => {
    const root = document.querySelector('[data-minigames-root]');
    if (!root || !window.signalR) return;

    const hubUrl = root.dataset.hubUrl;
    const roomCodeLabel = root.querySelector('[data-room-code]');
    const newGameDialog = root.querySelector('[data-new-game-dialog]');
    const newGameForm = root.querySelector('[data-new-game-form]');
    const newGameCount = root.querySelector('[data-new-game-count]');
    const newGameQuestionCards = root.querySelector('[data-new-game-question-cards]');
    const newGameSoloAi = root.querySelector('[data-new-game-solo-ai]');
    const newGameAllowHints = root.querySelector('[data-new-game-allow-hints]');
    const newGameSubmit = root.querySelector('[data-new-game-submit]');
    const newGameError = root.querySelector('[data-new-game-error]');
    const cardCountRange = root.querySelector('[data-card-count-range]');
    const turnLabel = root.querySelector('[data-turn-label]');
    const roomMessage = root.querySelector('[data-room-message]');
    const questionStatus = root.querySelector('[data-question-status]');
    const questionResponseText = root.querySelector('[data-question-response-text]');
    const questionHistory = root.querySelector('[data-question-history]');
    const historyFilters = root.querySelector('[data-question-history-filters]');

    if (!hubUrl || !roomCodeLabel || !newGameDialog || !newGameForm ||
        !newGameCount || !newGameQuestionCards || !newGameSoloAi ||
        !newGameSubmit || !cardCountRange || !turnLabel || !roomMessage ||
        !questionStatus || !questionResponseText || !questionHistory || !historyFilters) {
        return;
    }

    const text = {
        playerOne: root.dataset.playerOne ?? 'Player 1',
        playerTwo: root.dataset.playerTwo ?? 'Player 2',
        aiPlayer: root.dataset.soloAiPlayer ?? 'AI',
        unknown: root.dataset.soloAiUnknown ?? 'I do not know',
        cardRange: root.dataset.soloAiCardRange ?? '{0}-{1}',
        yes: root.dataset.yes ?? 'YES',
        no: root.dataset.no ?? 'NO',
        historyQuestion: root.dataset.questionHistoryEntry ?? '{player}: {question}',
        historyGuessCorrect: root.dataset.historyGuessCorrect ?? '{player} named game {game} (correct)',
        historyGuessIncorrect: root.dataset.historyGuessIncorrect ?? '{player} named game {game} (incorrect)',
        historyTurnEnded: root.dataset.historyTurnEnded ?? '{player} ended the turn',
        historyTurnTimedOut: root.dataset.historyTurnTimedOut ?? '{player} ended the turn (time expired)',
        historyAnswer: root.dataset.historyQuestionAnswer ?? '{player} answered - {answer}',
        invalidCardCount: root.dataset.invalidCardCount ?? 'Invalid card count.',
        roomFull: root.dataset.roomFull ?? 'The room is full.',
        questionsUnavailable: root.dataset.questionsUnavailable ?? 'Questions are unavailable.',
        genericError: root.dataset.genericError ?? 'Something went wrong.'
    };

    const minimumCardCount = Number(root.dataset.minCardCount ?? '10');
    const storagePrefix = 'badwolf-minigame-player:';
    const originalFilterTitles = new Map(
        [...historyFilters.querySelectorAll('[data-history-filter]')]
            .map(button => [button, button.title]));

    let connection = null;
    let currentRoomCode = '';
    let playerToken = '';
    let availability = null;
    let cachedState = null;
    let cachedStatus = null;
    let originalMaximum = newGameCount.max;
    let originalRangeText = cardCountRange.textContent ?? '';
    let questionCardsDisabledBeforeSolo = newGameQuestionCards.disabled;

    const get = (value, camelName, pascalName) =>
        value?.[camelName] ?? value?.[pascalName];
    const historyOf = value => get(value, 'questionHistory', 'QuestionHistory') ?? [];
    const historyPlayerOf = value => Number(get(value, 'playerNumber', 'PlayerNumber') ?? 0);
    const historyKindOf = value => Number(get(value, 'kind', 'Kind') ?? 0);
    const historyValueOf = value => get(value, 'value', 'Value') ?? '';
    const historyCorrectOf = value => Boolean(get(value, 'isCorrect', 'IsCorrect') ?? false);
    const historyAnswerOf = value => get(value, 'answerYes', 'AnswerYes');
    const statusSoloOf = value => Boolean(get(value, 'isSoloGame', 'IsSoloGame') ?? false);
    const statusHumanOpponentOf = value =>
        Boolean(get(value, 'hasHumanOpponent', 'HasHumanOpponent') ?? false);
    const statusCanStartOf = value =>
        Boolean(get(value, 'canStartSoloGame', 'CanStartSoloGame') ?? false);
    const unknownIndexesOf = value =>
        get(value, 'unknownAnswerHistoryIndexes', 'UnknownAnswerHistoryIndexes') ?? [];
    const eligibleCountOf = value =>
        Number(get(value, 'eligibleGameCount', 'EligibleGameCount') ?? 0);
    const availabilityOf = value => Boolean(get(value, 'available', 'Available') ?? false);

    const historyKind = {
        question: 0,
        guess: 1,
        turnEnded: 2,
        turnTimedOut: 3,
        answer: 4
    };

    const storageKey = roomCode => `${storagePrefix}${roomCode.toUpperCase()}`;

    const format = (template, replacements) => {
        let result = template;
        Object.entries(replacements).forEach(([key, value]) => {
            result = result.replaceAll(`{${key}}`, String(value));
        });
        return result;
    };

    const formatRange = maximum =>
        text.cardRange
            .replaceAll('{0}', String(minimumCardCount))
            .replaceAll('{1}', String(maximum));

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
                void refreshSoloUi();
            });
            connection.onreconnected(() => {
                void refreshSoloUi();
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

    const getNewGameError = error => {
        const message = String(error?.message ?? error ?? '');
        if (message.includes('MINIGAME_ROOM_INVALIDCARDCOUNT')) return text.invalidCardCount;
        if (message.includes('MINIGAME_ROOM_ROOMFULL')) return text.roomFull;
        if (message.includes('MINIGAME_ROOM_QUESTIONSUNAVAILABLE')) return text.questionsUnavailable;
        return text.genericError;
    };

    const replacePlayerTwo = element => {
        if (!element || !element.textContent || !text.playerTwo) return;
        element.textContent = element.textContent.replaceAll(text.playerTwo, text.aiPlayer);
    };

    const restoreFilterTitles = () => {
        originalFilterTitles.forEach((title, button) => {
            button.title = title;
        });
    };

    const patchFilterTitles = () => {
        originalFilterTitles.forEach((title, button) => {
            button.title = title.replaceAll(text.playerTwo, text.aiPlayer);
        });
    };

    const createFilteredHistory = (state, status, mode) => {
        const askingPlayer = mode === '1-to-2' ? 1 : 2;
        const unknownIndexes = new Set(
            unknownIndexesOf(status).map(value => Number(value)));
        const entries = historyOf(state);
        const pairs = [];
        let pending = null;

        entries.forEach((entry, index) => {
            const kind = historyKindOf(entry);
            const player = historyPlayerOf(entry);
            if (kind === historyKind.question) {
                pending = player === askingPlayer
                    ? { player, question: historyValueOf(entry) }
                    : null;
                return;
            }

            if (kind !== historyKind.answer || !pending || player === pending.player) {
                return;
            }

            const answerValue = unknownIndexes.has(index)
                ? text.unknown
                : historyAnswerOf(entry) === true
                    ? text.yes
                    : text.no;
            pairs.push({ question: pending, answer: { player, value: answerValue } });
            pending = null;
        });

        if (pairs.length === 0) {
            const empty = document.createElement('p');
            empty.className = 'minigames-question-history-empty is-filtered-projection';
            empty.textContent = root.dataset.questionHistoryEmpty ?? '';
            questionHistory.replaceChildren(empty);
            return;
        }

        const list = document.createElement('ol');
        list.className = 'minigames-question-history-list is-filtered-pairs';
        pairs.forEach(pair => {
            const item = document.createElement('li');
            item.className = `is-asker-${pair.question.player}`;

            const question = document.createElement('span');
            question.className = `minigames-history-pair-part is-player-${pair.question.player}`;
            question.textContent = pair.question.question;

            const separator = document.createElement('span');
            separator.className = 'minigames-history-pair-separator';
            separator.textContent = ' - ';

            const answer = document.createElement('span');
            answer.className = `minigames-history-pair-part is-player-${pair.answer.player}`;
            answer.textContent = pair.answer.value;

            item.append(question, separator, answer);
            list.appendChild(item);
        });
        questionHistory.replaceChildren(list);
        requestAnimationFrame(() => {
            questionHistory.scrollTop = questionHistory.scrollHeight;
        });
    };

    const renderFullHistoryCorrections = (state, status) => {
        const entries = historyOf(state);
        const unknownIndexes = new Set(
            unknownIndexesOf(status).map(value => Number(value)));
        const list = questionHistory.querySelector('.minigames-question-history-list:not(.is-filtered-pairs)');
        if (!list) return;

        const items = [...list.querySelectorAll(':scope > li')];
        entries.forEach((entry, index) => {
            const item = items[index];
            if (!item) return;
            const player = historyPlayerOf(entry);
            const kind = historyKindOf(entry);
            const value = historyValueOf(entry);
            const playerLabel = player === 2 ? text.aiPlayer : text.playerOne;

            if (kind === historyKind.answer && unknownIndexes.has(index)) {
                item.textContent = format(text.historyAnswer, {
                    player: playerLabel,
                    answer: text.unknown
                });
                return;
            }

            if (player !== 2) return;
            if (kind === historyKind.question) {
                item.textContent = format(text.historyQuestion, {
                    player: text.aiPlayer,
                    question: value
                });
            } else if (kind === historyKind.guess) {
                item.textContent = format(
                    historyCorrectOf(entry)
                        ? text.historyGuessCorrect
                        : text.historyGuessIncorrect,
                    { player: text.aiPlayer, game: value });
            } else if (kind === historyKind.turnEnded) {
                item.textContent = format(text.historyTurnEnded, { player: text.aiPlayer });
            } else if (kind === historyKind.turnTimedOut) {
                item.textContent = format(text.historyTurnTimedOut, { player: text.aiPlayer });
            } else if (kind === historyKind.answer) {
                item.textContent = format(text.historyAnswer, {
                    player: text.aiPlayer,
                    answer: historyAnswerOf(entry) === true ? text.yes : text.no
                });
            }
        });
    };

    const renderSoloUi = () => {
        if (!cachedState || !cachedStatus) return;
        const solo = statusSoloOf(cachedStatus);
        root.dataset.soloAiActive = solo ? 'true' : 'false';

        if (!solo) {
            restoreFilterTitles();
            return;
        }

        patchFilterTitles();
        replacePlayerTwo(turnLabel);
        replacePlayerTwo(roomMessage);
        replacePlayerTwo(questionStatus);
        replacePlayerTwo(questionResponseText);

        const activeFilter = historyFilters.querySelector('[data-history-filter].is-active');
        const mode = activeFilter?.dataset.historyFilter ?? 'all';
        if (mode === 'all') {
            renderFullHistoryCorrections(cachedState, cachedStatus);
        } else if (mode === '1-to-2' || mode === '2-to-1') {
            createFilteredHistory(cachedState, cachedStatus, mode);
        }
    };

    const renderAfterMainClient = () => {
        renderSoloUi();
        requestAnimationFrame(renderSoloUi);
        window.setTimeout(renderSoloUi, 50);
    };

    const refreshSoloUi = async () => {
        if (!resolveMembership()) return;
        try {
            const hub = await ensureConnection();
            const [state, status] = await Promise.all([
                hub.invoke('GetRoomState', currentRoomCode, playerToken, false),
                hub.invoke('GetSoloAiStatus', currentRoomCode, playerToken)
            ]);
            cachedState = state;
            cachedStatus = status;
            renderAfterMainClient();
        } catch {
        }
    };

    const restoreRegularGameControls = () => {
        newGameQuestionCards.disabled = questionCardsDisabledBeforeSolo;
        newGameCount.max = originalMaximum;
        cardCountRange.textContent = originalRangeText;
        const maximum = Number.parseInt(originalMaximum, 10);
        const value = Number.parseInt(newGameCount.value, 10);
        if (Number.isFinite(maximum) && Number.isFinite(value) && value > maximum) {
            newGameCount.value = String(maximum);
        }
    };

    const applySoloGameControls = () => {
        if (!newGameSoloAi.checked) {
            restoreRegularGameControls();
            return;
        }

        newGameQuestionCards.checked = true;
        newGameQuestionCards.disabled = true;
        const maximum = eligibleCountOf(availability);
        newGameCount.max = String(maximum);
        cardCountRange.textContent = formatRange(maximum);
        const value = Number.parseInt(newGameCount.value, 10);
        if (!Number.isFinite(value) || value > maximum) {
            newGameCount.value = String(maximum);
        } else if (value < minimumCardCount) {
            newGameCount.value = String(minimumCardCount);
        }
    };

    const prepareNewGameDialog = async () => {
        originalMaximum = newGameCount.max;
        originalRangeText = cardCountRange.textContent ?? '';
        questionCardsDisabledBeforeSolo = newGameQuestionCards.disabled;
        newGameSoloAi.checked = false;
        restoreRegularGameControls();

        try {
            const hub = await ensureConnection();
            availability = await hub.invoke('GetSoloAiAvailability');
            let status = null;
            if (resolveMembership()) {
                status = await hub.invoke('GetSoloAiStatus', currentRoomCode, playerToken);
            }
            const canStart = availabilityOf(availability) &&
                !questionCardsDisabledBeforeSolo &&
                (!status || statusCanStartOf(status)) &&
                (!status || !statusHumanOpponentOf(status));
            newGameSoloAi.disabled = !canStart;
        } catch {
            availability = null;
            newGameSoloAi.disabled = true;
        }
    };

    newGameSoloAi.addEventListener('change', applySoloGameControls);

    const newGameDialogObserver = new MutationObserver(() => {
        if (newGameDialog.open) {
            void prepareNewGameDialog();
        }
    });
    newGameDialogObserver.observe(newGameDialog, {
        attributes: true,
        attributeFilter: ['open']
    });

    newGameForm.addEventListener('submit', async event => {
        if (!newGameSoloAi.checked) return;

        event.preventDefault();
        event.stopImmediatePropagation();
        setNewGameError('');
        if (!resolveMembership()) {
            setNewGameError(text.genericError);
            return;
        }

        const cardCount = Number.parseInt(newGameCount.value, 10);
        const maximum = eligibleCountOf(availability);
        if (!Number.isInteger(cardCount) ||
            cardCount < minimumCardCount ||
            cardCount > maximum) {
            setNewGameError(text.invalidCardCount);
            return;
        }

        newGameSubmit.disabled = true;
        try {
            const hub = await ensureConnection();
            await hub.invoke(
                'StartNewSoloGame',
                currentRoomCode,
                playerToken,
                cardCount,
                Boolean(newGameAllowHints?.checked));
            newGameDialog.close();
            await refreshSoloUi();
        } catch (error) {
            setNewGameError(getNewGameError(error));
        } finally {
            newGameSubmit.disabled = false;
        }
    }, true);

    historyFilters.querySelectorAll('[data-history-filter]').forEach(button => {
        button.addEventListener('click', () => {
            window.setTimeout(renderSoloUi, 0);
        });
    });

    void ensureConnection()
        .then(refreshSoloUi)
        .catch(() => {});
})();
