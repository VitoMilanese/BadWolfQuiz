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
    const answerButton = root.querySelector('[data-answer]');
    const endTurnButton = root.querySelector('[data-end-turn]');
    const turnPanel = root.querySelector('[data-turn-panel]');
    const turnLabel = root.querySelector('[data-turn-label]');
    const turnTimer = root.querySelector('[data-turn-timer]');
    const restartGameButton = root.querySelector('[data-restart-game]');
    const newGameButton = root.querySelector('[data-new-game]');
    const newGameDialog = root.querySelector('[data-new-game-dialog]');
    const newGameForm = root.querySelector('[data-new-game-form]');
    const newGameCount = root.querySelector('[data-new-game-count]');
    const newGameQuestionCards = root.querySelector('[data-new-game-question-cards]');
    const newGameCancel = root.querySelector('[data-new-game-cancel]');
    const newGameSubmit = root.querySelector('[data-new-game-submit]');
    const newGameError = root.querySelector('[data-new-game-error]');
    const gameLayout = root.querySelector('[data-game-layout]');
    const grid = root.querySelector('[data-minigames-grid]');
    const empty = root.querySelector('[data-minigames-empty]');
    const questionPanel = root.querySelector('[data-question-panel]');
    const questionHistory = root.querySelector('[data-question-history]');
    const questionOptions = root.querySelector('[data-question-options]');
    const questionStatus = root.querySelector('[data-question-status]');
    const questionResponseDialog = root.querySelector('[data-question-response-dialog]');
    const questionResponseText = root.querySelector('[data-question-response-text]');
    const questionResponseYes = root.querySelector('[data-question-response-yes]');
    const questionResponseNo = root.querySelector('[data-question-response-no]');

    const hubUrl = root.dataset.hubUrl;
    const cardUrl = root.dataset.cardUrl;
    if (!entry || !roomShell || !createRoomButton || !joinForm ||
        !joinCodeInput || !answerButton || !endTurnButton || !turnPanel ||
        !turnLabel || !turnTimer || !restartGameButton || !newGameDialog || !newGameForm ||
        !newGameCount || !newGameQuestionCards || !newGameCancel || !newGameSubmit ||
        !gameLayout || !grid || !empty || !questionPanel || !questionHistory ||
        !questionOptions || !questionStatus || !questionResponseDialog ||
        !questionResponseText || !questionResponseYes || !questionResponseNo ||
        !hubUrl || !cardUrl) {
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
        turn: root.dataset.turn ?? '',
        answerPrompt: root.dataset.answerPrompt ?? '',
        winner: root.dataset.winner ?? '',
        notYourTurn: root.dataset.notYourTurn ?? '',
        questionHistoryEntry: root.dataset.questionHistoryEntry ?? '{player}: {question}',
        historyGuessCorrect: root.dataset.historyGuessCorrect ?? '{player} named game {game} (correct)',
        historyGuessIncorrect: root.dataset.historyGuessIncorrect ?? '{player} named game {game} (incorrect)',
        historyTurnEnded: root.dataset.historyTurnEnded ?? '{player} ended the turn',
        historyTurnTimedOut: root.dataset.historyTurnTimedOut ?? '{player} ended the turn (time expired)',
        historyQuestionAnswer: root.dataset.historyQuestionAnswer ?? '{player} answered - {answer}',
        questionResponsePrompt: root.dataset.questionResponsePrompt ?? '{player} asks: {question}',
        yes: root.dataset.yes ?? 'YES',
        no: root.dataset.no ?? 'NO',
        questionAwaitingResponse: root.dataset.questionAwaitingResponse ?? 'Waiting for {player}.',
        questionHistoryEmpty: root.dataset.questionHistoryEmpty ?? '',
        questionNotSelected: root.dataset.questionNotSelected ?? '',
        questionChoose: root.dataset.questionChoose ?? '',
        questionSelected: root.dataset.questionSelected ?? '',
        questionWait: root.dataset.questionWait ?? '',
        questionRequired: root.dataset.questionRequired ?? '',
        questionAlreadySelected: root.dataset.questionAlreadySelected ?? '',
        invalidQuestion: root.dataset.invalidQuestion ?? '',
        questionsUnavailable: root.dataset.questionsUnavailable ?? '',
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
    let questionsAvailable = true;
    const storagePrefix = 'badwolf-minigame-player:';
    const inactiveCards = new Set();
    const themeVariableNames = Object.freeze([
        '--bg',
        '--panel',
        '--panel-2',
        '--line',
        '--text',
        '--muted',
        '--red',
        '--red-bright',
        '--gold',
        '--body-background',
        '--topbar-bg',
        '--panel-glass',
        '--panel-gradient-end',
        '--accent-shadow'
    ]);

    const captureTheme = () => {
        const element = document.documentElement;
        const variables = {};
        themeVariableNames.forEach(name => {
            const value = element.style.getPropertyValue(name).trim();
            if (value) variables[name] = value;
        });
        return {
            themeId: element.dataset.theme ?? '',
            variables
        };
    };

    const applyTheme = theme => {
        if (!theme) return;
        const element = document.documentElement;
        const themeId = theme.themeId ?? theme.ThemeId ?? '';
        const variables = theme.variables ?? theme.Variables ?? {};

        themeVariableNames.forEach(name => element.style.removeProperty(name));
        if (themeId) element.dataset.theme = themeId;
        else element.removeAttribute('data-theme');

        Object.entries(variables).forEach(([name, value]) => {
            if (themeVariableNames.includes(name) && typeof value === 'string' && value) {
                element.style.setProperty(name, value);
            }
        });
    };

    const initialTheme = captureTheme();

    let connection = null;
    let currentRoomCode = '';
    let playerToken = '';
    let currentState = null;
    let currentGameNumber = -1;
    let answerMode = false;
    let turnTimerHandle = null;
    let expiryRequestKey = '';
    let lastTurnKey = '';

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
    const currentPlayerOf = value =>
        Number(get(value, 'currentPlayerNumber', 'CurrentPlayerNumber') ?? 0);
    const deadlineOf = value =>
        get(value, 'turnDeadlineUtc', 'TurnDeadlineUtc') ?? '';
    const winnerOf = value =>
        Number(get(value, 'winnerPlayerNumber', 'WinnerPlayerNumber') ?? 0);
    const roomThemeOf = value => get(value, 'theme', 'Theme') ?? null;
    const questionCardsEnabledOf = value =>
        Boolean(get(value, 'questionCardsEnabled', 'QuestionCardsEnabled') ?? false);
    const availableQuestionsOf = value =>
        get(value, 'myAvailableQuestions', 'MyAvailableQuestions') ?? [];
    const questionSelectedThisTurnOf = value =>
        Boolean(get(value, 'hasSelectedQuestionThisTurn', 'HasSelectedQuestionThisTurn') ?? false);
    const pendingQuestionOf = value =>
        get(value, 'pendingQuestion', 'PendingQuestion') ?? '';
    const pendingQuestionResponsePlayerOf = value =>
        Number(get(value, 'pendingQuestionResponsePlayerNumber', 'PendingQuestionResponsePlayerNumber') ?? 0);
    const questionHistoryOf = value =>
        get(value, 'questionHistory', 'QuestionHistory') ?? [];
    const historyPlayerOf = value =>
        Number(get(value, 'playerNumber', 'PlayerNumber') ?? 0);
    const historyKindOf = value =>
        Number(get(value, 'kind', 'Kind') ?? 0);
    const historyValueOf = value =>
        get(value, 'value', 'Value') ?? null;
    const historyCorrectOf = value =>
        Boolean(get(value, 'isCorrect', 'IsCorrect') ?? false);
    const historyAnswerYesOf = value =>
        get(value, 'answerYes', 'AnswerYes') === true;

    const historyKind = {
        question: 0,
        guess: 1,
        turnEnded: 2,
        turnTimedOut: 3,
        answer: 4
    };

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
        applyTheme(initialTheme);
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
        if (message.includes('MINIGAME_ROOM_NOTYOURTURN')) return text.notYourTurn;
        if (message.includes('MINIGAME_ROOM_INVALIDQUESTION')) return text.invalidQuestion;
        if (message.includes('MINIGAME_ROOM_QUESTIONALREADYSELECTED')) {
            return text.questionAlreadySelected;
        }
        if (message.includes('MINIGAME_ROOM_QUESTIONREQUIRED')) return text.questionRequired;
        if (message.includes('MINIGAME_ROOM_QUESTIONSUNAVAILABLE')) return text.questionsUnavailable;
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


    const playerName = number => number === 1 ? text.playerOne : text.playerTwo;

    const stopTurnTimer = () => {
        if (turnTimerHandle !== null) {
            window.clearInterval(turnTimerHandle);
            turnTimerHandle = null;
        }
    };

    const requestTurnExpiry = async deadline => {
        if (!currentRoomCode || !playerToken || !deadline || expiryRequestKey === deadline) return;
        expiryRequestKey = deadline;
        try {
            const state = await connection.invoke('ExpireTurn', currentRoomCode, playerToken);
            applyState(state);
        } catch (error) {
            showError(roomError, getErrorMessage(error));
        }
    };

    const updateTurnTimer = state => {
        const deadline = deadlineOf(state);
        if (!deadline || phaseOf(state) !== phase.playing) {
            turnTimer.textContent = '00:00';
            return;
        }

        const deadlineMs = Date.parse(deadline);
        if (!Number.isFinite(deadlineMs)) {
            turnTimer.textContent = '00:00';
            return;
        }

        const remainingMs = Math.max(0, deadlineMs - Date.now());
        const remainingSeconds = Math.ceil(remainingMs / 1000);
        const minutes = Math.floor(remainingSeconds / 60);
        const seconds = remainingSeconds % 60;
        turnTimer.textContent = `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;

        if (remainingMs <= 0) void requestTurnExpiry(deadline);
    };

    const startTurnTimer = state => {
        stopTurnTimer();
        updateTurnTimer(state);
        if (phaseOf(state) === phase.playing && deadlineOf(state)) {
            turnTimerHandle = window.setInterval(() => updateTurnTimer(currentState), 250);
        }
    };

    const submitGuess = async fileName => {
        if (!answerMode || !currentState ||
            playerNumberOf(currentState) !== currentPlayerOf(currentState)) return;

        clearErrors();
        answerMode = false;
        root.classList.remove('is-answer-mode');
        answerButton.classList.remove('is-active');
        try {
            const state = await connection.invoke(
                'SubmitGuess',
                currentRoomCode,
                playerToken,
                fileName);
            applyState(state);
        } catch (error) {
            showError(roomError, getErrorMessage(error));
        }
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
            button.addEventListener('click', () => {
                if (answerMode && playerNumberOf(currentState) === currentPlayerOf(currentState)) {
                    void submitGuess(fileName);
                    return;
                }
                toggleLocalInactive(button);
            });
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

    const selectQuestion = async optionIndex => {
        if (!currentState || phaseOf(currentState) !== phase.playing ||
            playerNumberOf(currentState) !== currentPlayerOf(currentState) ||
            questionSelectedThisTurnOf(currentState) ||
            pendingQuestionResponsePlayerOf(currentState) > 0) {
            return;
        }

        clearErrors();
        try {
            const state = await connection.invoke(
                'SelectQuestion',
                currentRoomCode,
                playerToken,
                optionIndex);
            applyState(state);
        } catch (error) {
            showError(roomError, getErrorMessage(error));
        }
    };

    const renderQuestions = state => {
        const enabled = questionCardsEnabledOf(state);
        gameLayout.classList.toggle('has-question-panel', enabled);
        questionPanel.classList.toggle('is-hidden', !enabled);
        if (!enabled) {
            questionHistory.replaceChildren();
            questionOptions.replaceChildren();
            questionStatus.textContent = '';
            return;
        }

        const entries = questionHistoryOf(state);
        if (entries.length === 0) {
            const emptyHistory = document.createElement('p');
            emptyHistory.className = 'minigames-question-history-empty';
            emptyHistory.textContent = text.questionHistoryEmpty;
            questionHistory.replaceChildren(emptyHistory);
        } else {
            const list = document.createElement('ol');
            list.className = 'minigames-question-history-list';
            entries.forEach(entry => {
                const item = document.createElement('li');
                const historyPlayer = historyPlayerOf(entry);
                item.classList.add(`is-player-${historyPlayer}`);
                const player = playerName(historyPlayer);
                const kind = historyKindOf(entry);
                const value = historyValueOf(entry) ?? '';

                if (kind === historyKind.guess) {
                    item.textContent = format(
                        historyCorrectOf(entry)
                            ? text.historyGuessCorrect
                            : text.historyGuessIncorrect,
                        { player, game: value });
                } else if (kind === historyKind.turnEnded) {
                    item.textContent = format(text.historyTurnEnded, { player });
                } else if (kind === historyKind.turnTimedOut) {
                    item.textContent = format(text.historyTurnTimedOut, { player });
                } else if (kind === historyKind.answer) {
                    item.textContent = format(text.historyQuestionAnswer, {
                        player,
                        answer: historyAnswerYesOf(entry) ? text.yes : text.no
                    });
                } else {
                    item.textContent = format(text.questionHistoryEntry, {
                        player,
                        question: value
                    });
                }
                list.appendChild(item);
            });
            questionHistory.replaceChildren(list);
            requestAnimationFrame(() => {
                questionHistory.scrollTop = questionHistory.scrollHeight;
            });
        }

        const playerNumber = playerNumberOf(state);
        const currentPhase = phaseOf(state);
        const isMyTurn = currentPhase === phase.playing &&
            currentPlayerOf(state) === playerNumber;
        const selected = questionSelectedThisTurnOf(state);
        const pendingResponsePlayer = pendingQuestionResponsePlayerOf(state);
        const available = availableQuestionsOf(state);
        questionOptions.replaceChildren(...available.map((question, index) => {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'button minigames-question-option';
            button.textContent = question;
            button.disabled = !isMyTurn || selected || pendingResponsePlayer > 0;
            button.addEventListener('click', () => void selectQuestion(index));
            return button;
        }));

        if (currentPhase !== phase.playing) {
            questionStatus.textContent = '';
        } else if (pendingResponsePlayer > 0) {
            questionStatus.textContent = format(text.questionAwaitingResponse, {
                player: playerName(pendingResponsePlayer)
            });
        } else if (selected) {
            questionStatus.textContent = text.questionSelected;
        } else if (isMyTurn) {
            questionStatus.textContent = text.questionChoose;
        } else {
            questionStatus.textContent = text.questionWait;
        }
    };

    const renderQuestionResponseDialog = state => {
        const pendingPlayer = pendingQuestionResponsePlayerOf(state);
        const question = pendingQuestionOf(state);
        const playerNumber = playerNumberOf(state);
        const shouldOpen = questionCardsEnabledOf(state) &&
            pendingPlayer === playerNumber &&
            Boolean(question);

        if (!shouldOpen) {
            if (questionResponseDialog.open) questionResponseDialog.close();
            return;
        }

        const askingPlayer = pendingPlayer === 1 ? 2 : 1;
        questionResponseText.textContent = format(text.questionResponsePrompt, {
            player: playerName(askingPlayer),
            question
        });
        questionResponseYes.disabled = false;
        questionResponseNo.disabled = false;
        if (!questionResponseDialog.open) questionResponseDialog.showModal();
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
            roomMessage.textContent = answerMode ? text.answerPrompt : text.gameReady;
        } else if (currentPhase === phase.finished) {
            roomMessage.textContent = format(text.winner, { player: playerName(winnerOf(state)) });
        } else {
            roomMessage.textContent = '';
        }

        const currentPlayer = currentPlayerOf(state);
        const isMyTurn = currentPhase === phase.playing && currentPlayer === playerNumber;
        turnPanel.classList.toggle('is-hidden', currentPhase !== phase.playing);
        turnLabel.textContent = currentPhase === phase.playing
            ? format(text.turn, { player: playerName(currentPlayer) })
            : '';
        answerButton.disabled = !isMyTurn;
        endTurnButton.disabled = !isMyTurn;
        restartGameButton.disabled = ![
            phase.playing,
            phase.finished
        ].includes(currentPhase);
        if (!isMyTurn) {
            answerMode = false;
            root.classList.remove('is-answer-mode');
            answerButton.classList.remove('is-active');
        }
        startTurnTimer(state);
        renderQuestions(state);
        renderQuestionResponseDialog(state);

        const cards = cardsOf(state);
        grid.replaceChildren(...cards.map(card => createCard(card, state)));
        grid.classList.toggle('is-hidden', cards.length === 0);
        empty.classList.toggle('is-hidden', cards.length !== 0);
        requestAnimationFrame(layoutGrid);
    };

    const applyState = state => {
        if (!state) return;
        if (currentState && versionOf(state) < versionOf(currentState)) return;

        applyTheme(roomThemeOf(state));
        const gameNumber = gameNumberOf(state);
        const turnKey = `${gameNumber}:${currentPlayerOf(state)}:${deadlineOf(state)}`;
        if (gameNumber !== currentGameNumber) {
            inactiveCards.clear();
            answerMode = false;
            root.classList.remove('is-answer-mode');
            answerButton.classList.remove('is-active');
            currentGameNumber = gameNumber;
        } else if (lastTurnKey && turnKey !== lastTurnKey) {
            answerMode = false;
            root.classList.remove('is-answer-mode');
            answerButton.classList.remove('is-active');
        }
        if (turnKey !== lastTurnKey) expiryRequestKey = '';
        lastTurnKey = turnKey;

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

    const synchronize = async (touchActivity = true) => {
        if (!currentRoomCode || !playerToken) return false;
        const state = await connection.invoke(
            'GetRoomState',
            currentRoomCode,
            playerToken,
            touchActivity);
        applyState(state);
        return true;
    };

    const refreshCatalog = async () => {
        const catalog = await connection.invoke('GetCatalog');
        maximumCardCount = Number(
            get(catalog, 'maximumCardCount', 'MaximumCardCount') ?? maximumCardCount);
        defaultCardCount = Number(
            get(catalog, 'defaultCardCount', 'DefaultCardCount') ?? defaultCardCount);
        questionsAvailable = Boolean(
            get(catalog, 'questionsAvailable', 'QuestionsAvailable') ?? false);
        newGameCount.min = String(minimumCardCount);
        newGameCount.max = String(maximumCardCount);
        newGameCount.value = String(defaultCardCount || minimumCardCount);
        newGameQuestionCards.disabled = !questionsAvailable;
        if (!questionsAvailable) newGameQuestionCards.checked = false;
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
        newGameQuestionCards.checked = false;
        if (!newGameDialog.open) newGameDialog.showModal();
    };

    createRoomButton.addEventListener('click', async () => {
        clearErrors();
        createRoomButton.disabled = true;
        try {
            const membership = await connection.invoke('CreateRoom', captureTheme());
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

    answerButton.addEventListener('click', async () => {
        if (!currentState || phaseOf(currentState) !== phase.playing ||
            playerNumberOf(currentState) !== currentPlayerOf(currentState)) return;
        await touchRoom();
        answerMode = !answerMode;
        root.classList.toggle('is-answer-mode', answerMode);
        answerButton.classList.toggle('is-active', answerMode);
        renderRoom(currentState);
    });

    const submitQuestionResponse = async answerYes => {
        if (!currentState ||
            pendingQuestionResponsePlayerOf(currentState) !== playerNumberOf(currentState)) {
            return;
        }

        clearErrors();
        questionResponseYes.disabled = true;
        questionResponseNo.disabled = true;
        try {
            const state = await connection.invoke(
                'SubmitQuestionResponse',
                currentRoomCode,
                playerToken,
                answerYes);
            applyState(state);
        } catch (error) {
            showError(roomError, getErrorMessage(error));
            questionResponseYes.disabled = false;
            questionResponseNo.disabled = false;
        }
    };

    questionResponseDialog.addEventListener('cancel', event => {
        event.preventDefault();
    });
    questionResponseYes.addEventListener('click', () => void submitQuestionResponse(true));
    questionResponseNo.addEventListener('click', () => void submitQuestionResponse(false));

    endTurnButton.addEventListener('click', async () => {
        if (!currentState || phaseOf(currentState) !== phase.playing ||
            playerNumberOf(currentState) !== currentPlayerOf(currentState)) return;
        clearErrors();
        endTurnButton.disabled = true;
        try {
            const state = await connection.invoke('EndTurn', currentRoomCode, playerToken);
            applyState(state);
        } catch (error) {
            showError(roomError, getErrorMessage(error));
        }
    });

    restartGameButton.addEventListener('click', async () => {
        if (!currentState || ![
            phase.playing,
            phase.finished
        ].includes(phaseOf(currentState))) {
            return;
        }

        clearErrors();
        restartGameButton.disabled = true;
        try {
            const state = await connection.invoke(
                'RestartGame',
                currentRoomCode,
                playerToken);
            applyState(state);
        } catch (error) {
            showError(roomError, getErrorMessage(error));
            renderRoom(currentState);
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
                cardCount,
                newGameQuestionCards.checked);
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
            void synchronize(false).catch(error => {
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
        answerButton.disabled = true;
        endTurnButton.disabled = true;
        restartGameButton.disabled = true;
        questionOptions.querySelectorAll('button').forEach(button => {
            button.disabled = true;
        });
        newGameButton.disabled = true;
    });

    connection.onreconnected(async () => {
        createRoomButton.disabled = false;
        newGameButton.disabled = false;
        restartGameButton.disabled = !currentState || ![
            phase.playing,
            phase.finished
        ].includes(phaseOf(currentState));
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
            restartGameButton.disabled = true;
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
            restartGameButton.disabled = true;
            window.setTimeout(connect, 2000);
        }
    };

    void connect();
})();
