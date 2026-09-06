(() => {
    const root = document.querySelector('[data-minigames-root]');
    if (!root || !window.signalR || window.BadWolfMinigamesAiHookInstalled) return;
    window.BadWolfMinigamesAiHookInstalled = true;

    const aiCheckbox = root.querySelector('[data-new-game-ai]');
    const questionCardsCheckbox = root.querySelector('[data-new-game-question-cards]');
    const countInput = root.querySelector('[data-new-game-count]');
    const submitButton = root.querySelector('[data-new-game-submit]');
    const newGameForm = root.querySelector('[data-new-game-form]');
    const newGameDialog = root.querySelector('[data-new-game-dialog]');
    const newGameError = root.querySelector('[data-new-game-error]');
    const createRoomButton = root.querySelector('[data-create-room]');
    const newGameButton = root.querySelector('[data-new-game]');
    const roomMessage = root.querySelector('[data-room-message]');
    const turnLabel = root.querySelector('[data-turn-label]');
    const questionHistory = root.querySelector('[data-question-history]');
    const questionResponseText = root.querySelector('[data-question-response-text]');
    const questionResponseUnknown = root.querySelector('[data-question-response-unknown]');
    if (!aiCheckbox || !questionCardsCheckbox || !countInput || !submitButton ||
        !newGameForm || !newGameDialog || !newGameError || !roomMessage ||
        !turnLabel || !questionHistory || !questionResponseText || !questionResponseUnknown) {
        return;
    }

    const get = (value, camelName, pascalName) =>
        value?.[camelName] ?? value?.[pascalName];
    const minimumCardCount = Number(root.dataset.minCardCount ?? '10');
    const playerTwo = root.dataset.playerTwo ?? 'Player 2';
    const aiPlayer = root.dataset.aiPlayer ?? 'AI';
    const drawText = root.dataset.draw ?? 'Draw';
    const dontKnow = root.dataset.dontKnow ?? "Don't know";
    const aiUnavailable = root.dataset.aiUnavailable ?? '';
    const historyAnswerTemplate = root.dataset.historyQuestionAnswer ?? '{player} answered - {answer}';

    let maximumCardCount = Number(root.dataset.maxCardCount ?? '0');
    let defaultCardCount = Number(root.dataset.defaultCardCount ?? '0');
    let aiMaximumCardCount = 0;
    let questionsAvailable = true;
    let lastState = null;
    let aiErrorPending = false;
    let applyingPresentation = false;
    let activeConnection = null;
    let activeRoomCode = null;
    let activePlayerToken = null;

    const format = (template, replacements) => {
        let result = template;
        Object.entries(replacements).forEach(([key, value]) => {
            result = result.replaceAll(`{${key}}`, String(value));
        });
        return result;
    };

    const boolOf = (value, camelName, pascalName) =>
        Boolean(get(value, camelName, pascalName) ?? false);
    const phaseOf = value => Number(get(value, 'phase', 'Phase') ?? -1);
    const roomCodeOf = value => String(get(value, 'roomCode', 'RoomCode') ?? '');
    const historyOf = value => get(value, 'questionHistory', 'QuestionHistory') ?? [];
    const historyKindOf = value => Number(get(value, 'kind', 'Kind') ?? -1);
    const historyPlayerOf = value => Number(get(value, 'playerNumber', 'PlayerNumber') ?? 0);
    const historyAnswerOf = value => get(value, 'answerYes', 'AnswerYes');

    const updateCatalog = catalog => {
        maximumCardCount = Number(
            get(catalog, 'maximumCardCount', 'MaximumCardCount') ?? maximumCardCount);
        defaultCardCount = Number(
            get(catalog, 'defaultCardCount', 'DefaultCardCount') ?? defaultCardCount);
        aiMaximumCardCount = Number(
            get(catalog, 'aiMaximumCardCount', 'AiMaximumCardCount') ?? 0);
        questionsAvailable = Boolean(
            get(catalog, 'questionsAvailable', 'QuestionsAvailable') ?? false);
        syncMode();
    };

    const trackState = (result, status = null) => {
        const state = get(result, 'state', 'State') ?? result;
        if (!state || !roomCodeOf(state) || phaseOf(state) < 0) return;
        if (status) {
            state.isAiOpponent = Boolean(get(status, 'isAiOpponent', 'IsAiOpponent') ?? false);
            state.isDraw = Boolean(get(status, 'isDraw', 'IsDraw') ?? false);
        }
        lastState = state;
        root.dataset.aiActive = boolOf(state, 'isAiOpponent', 'IsAiOpponent')
            ? 'true'
            : 'false';
        queueMicrotask(applyAiPresentation);
    };

    const syncMode = () => {
        const aiEnabled = aiCheckbox.checked;
        if (aiEnabled) {
            questionCardsCheckbox.checked = true;
            questionCardsCheckbox.disabled = true;
        } else {
            questionCardsCheckbox.disabled = !questionsAvailable;
            if (!questionsAvailable) questionCardsCheckbox.checked = false;
        }

        const maximum = aiEnabled ? aiMaximumCardCount : maximumCardCount;
        countInput.max = String(Math.max(0, maximum));
        if (maximum >= minimumCardCount) {
            const current = Number.parseInt(countInput.value, 10);
            if (!Number.isInteger(current) || current > maximum || current < minimumCardCount) {
                countInput.value = String(Math.min(
                    Math.max(defaultCardCount, minimumCardCount),
                    maximum));
            }
        }
        submitButton.disabled = maximum < minimumCardCount;

        if (aiEnabled && maximum < minimumCardCount && aiUnavailable) {
            newGameError.textContent = aiUnavailable;
            newGameError.classList.remove('is-hidden');
        } else if (newGameError.textContent === aiUnavailable) {
            newGameError.textContent = '';
            newGameError.classList.add('is-hidden');
        }
    };

    const resetAiChoice = () => {
        aiCheckbox.checked = false;
        syncMode();
    };

    aiCheckbox.addEventListener('change', syncMode);
    createRoomButton?.addEventListener('click', resetAiChoice, true);
    newGameButton?.addEventListener('click', resetAiChoice, true);

    newGameForm.addEventListener('submit', event => {
        if (!aiCheckbox.checked) return;
        questionCardsCheckbox.checked = true;
        const count = Number.parseInt(countInput.value, 10);
        if (!Number.isInteger(count) ||
            count < minimumCardCount ||
            count > aiMaximumCardCount) {
            event.preventDefault();
            event.stopImmediatePropagation();
            newGameError.textContent = aiUnavailable;
            newGameError.classList.remove('is-hidden');
        }
        window.setTimeout(syncMode, 0);
    }, true);

    const dialogObserver = new MutationObserver(() => {
        if (newGameDialog.open) syncMode();
    });
    dialogObserver.observe(newGameDialog, { attributes: true, attributeFilter: ['open'] });

    const replacePlayerTwo = element => {
        if (!element || !element.textContent?.includes(playerTwo)) return;
        element.textContent = element.textContent.replaceAll(playerTwo, aiPlayer);
    };

    const applyAiPresentation = () => {
        if (applyingPresentation) return;
        applyingPresentation = true;
        try {
            if (aiErrorPending && !newGameError.classList.contains('is-hidden')) {
                newGameError.textContent = aiUnavailable;
                aiErrorPending = false;
            }

            const aiActive = lastState && root.dataset.aiActive === 'true';
            questionResponseUnknown.classList.toggle('is-hidden', !aiActive);
            if (!aiActive) return;

            if (phaseOf(lastState) === 3 && boolOf(lastState, 'isDraw', 'IsDraw')) {
                if (roomMessage.textContent !== drawText) roomMessage.textContent = drawText;
            } else {
                replacePlayerTwo(roomMessage);
            }
            replacePlayerTwo(turnLabel);
            replacePlayerTwo(questionResponseText);

            const entries = historyOf(lastState);
            const list = questionHistory.querySelector(
                ':scope > .minigames-question-history-list:not(.is-filtered-pairs)');
            const items = list ? [...list.querySelectorAll(':scope > li')] : [];
            if (items.length === entries.length) {
                entries.forEach((entry, index) => {
                    const item = items[index];
                    if (historyKindOf(entry) === 4 && historyAnswerOf(entry) == null) {
                        const player = historyPlayerOf(entry) === 2 ? aiPlayer :
                            (root.dataset.playerOne ?? 'Player 1');
                        const value = format(historyAnswerTemplate, {
                            player,
                            answer: dontKnow
                        });
                        if (item.textContent !== value) item.textContent = value;
                    } else {
                        replacePlayerTwo(item);
                    }
                });
            }
        } finally {
            applyingPresentation = false;
        }
    };

    const presentationObserver = new MutationObserver(() => {
        queueMicrotask(applyAiPresentation);
    });
    presentationObserver.observe(root, {
        subtree: true,
        childList: true,
        characterData: true
    });

    questionResponseUnknown.addEventListener('click', async () => {
        if (root.dataset.aiActive !== 'true' ||
            !activeConnection ||
            !activeRoomCode ||
            !activePlayerToken) {
            return;
        }
        try {
            await activeConnection.invoke(
                'SubmitQuestionResponse',
                activeRoomCode,
                activePlayerToken,
                null);
        } catch {
            // The regular room synchronization path renders server errors.
        }
    });

    const builderPrototype = signalR.HubConnectionBuilder?.prototype;
    if (!builderPrototype?.build) return;
    const originalBuild = builderPrototype.build;
    builderPrototype.build = function (...buildArgs) {
        const connection = originalBuild.apply(this, buildArgs);
        activeConnection = connection;
        const originalInvoke = connection.invoke.bind(connection);
        connection.invoke = async (methodName, ...invokeArgs) => {
            if (invokeArgs.length >= 2 &&
                typeof invokeArgs[0] === 'string' &&
                typeof invokeArgs[1] === 'string') {
                activeRoomCode = invokeArgs[0];
                activePlayerToken = invokeArgs[1];
            }

            if (methodName === 'StartNewGame') {
                const aiEnabled = aiCheckbox.checked;
                if (aiEnabled) invokeArgs[3] = true;
                invokeArgs[4] = aiEnabled;
            }

            try {
                const result = await originalInvoke(methodName, ...invokeArgs);
                if (methodName === 'GetCatalog') {
                    updateCatalog(result);
                } else {
                    let status = null;
                    if (invokeArgs.length >= 2 &&
                        typeof invokeArgs[0] === 'string' &&
                        typeof invokeArgs[1] === 'string' &&
                        methodName !== 'GetAiStatus') {
                        status = await originalInvoke(
                            'GetAiStatus',
                            invokeArgs[0],
                            invokeArgs[1]);
                    }
                    trackState(result, status);
                }
                return result;
            } catch (error) {
                if (methodName === 'StartNewGame' &&
                    String(error?.message ?? error).includes('MINIGAME_ROOM_AIUNAVAILABLE')) {
                    aiErrorPending = true;
                    window.setTimeout(() => {
                        if (!newGameError.classList.contains('is-hidden')) {
                            newGameError.textContent = aiUnavailable;
                            aiErrorPending = false;
                        }
                    }, 0);
                }
                throw error;
            }
        };
        return connection;
    };
})();
