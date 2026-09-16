(() => {
    const root = document.querySelector('[data-word-rings-root]');
    if (!root) return;

    const isCooperative = (root.dataset.gameMode || '').toLowerCase() === 'cooperative';
    const roomCode = String(root.dataset.roomCode || '').trim().toUpperCase();
    const roomApiUrl = root.dataset.roomApiUrl || '';
    const patchApiUrl = '/minigames/word-rings-action-cards-patch-api';
    const storageKey = `badwolf.wordrings.room.${roomCode}`;
    const antiForgery = root.querySelector('[data-word-rings-antiforgery] input[name="__RequestVerificationToken"]');
    const wordList = root.querySelector('[data-word-list]');
    const toolbarActions = root.querySelector('.word-rings-actions');
    const judgeButton = root.querySelector('[data-room-host-resolve]');
    const baseFetch = window.fetch.bind(window);

    const language = (document.documentElement.lang || 'en').toLowerCase().split('-')[0];
    const translations = {
        en: {
            cardNames: ['', 'Swap', 'Replace', 'Block', 'Theft', 'Immunity', 'Hint', 'Rest', 'Shuffle', 'Time-out', 'Shield', 'Mask', 'Anagram', 'Cleanse'],
            negative: 'Player {0} used the card "{1}" on player {2}.',
            positive: 'Player {0} used the card "{1}" on player {2}.',
            shield: 'Player {0} protected themselves with "Shield" from "{1}" used by {2}.',
            immunity: 'Player {0} was protected by the "Immunity" card.',
            immunityTitle: 'Immunity triggered',
            immunityQuestion: 'Return "{0}" to your word list so you can try it again?',
            yes: 'Yes', no: 'No',
            debugTitle: 'Grant action card', player: 'Player', card: 'Action card', grant: 'Grant', close: 'Close',
            debugButton: 'Grant action card',
            duplicate: 'The player already has this card.',
            full: 'The player already has the maximum number of cards.',
            unavailable: 'Action cards are unavailable in this room.',
            genericError: 'Could not complete the action.',
            maskDescription: 'Hide some letters in the selected player’s words until they check any word.',
            anagramDescription: 'Shuffle letters in the selected player’s words until their next correct check is confirmed.'
        },
        uk: {
            cardNames: ['', 'Обмін', 'Заміна', 'Блокування', 'Крадіжка', 'Імунітет', 'Підказка', 'Перепочинок', 'Перетасовка', 'Тайм-аут', 'Щит', 'Маскування', 'Анаграма', 'Очищення'],
            negative: 'Гравець {0} застосував картку "{1}" до гравця {2}.',
            positive: 'Гравець {0} застосував картку "{1}" до гравця {2}.',
            shield: 'Гравець {0} захистився карткою "Щит" від картки "{1}" гравця {2}.',
            immunity: 'Гравець {0} захистився карткою "Імунітет".',
            immunityTitle: 'Спрацював Імунітет',
            immunityQuestion: 'Повернути слово "{0}" у ваш список, щоб спробувати ще раз?',
            yes: 'Так', no: 'Ні',
            debugTitle: 'Видати картку дій', player: 'Гравець', card: 'Картка дій', grant: 'Видати', close: 'Закрити',
            debugButton: 'Видати картку дій',
            duplicate: 'У гравця вже є така картка.',
            full: 'У гравця вже максимальна кількість карток.',
            unavailable: 'Картки дій недоступні в цій кімнаті.',
            genericError: 'Не вдалося виконати дію.',
            maskDescription: 'Приховати частину літер у словах обраного гравця до перевірки будь-якого його слова.',
            anagramDescription: 'Перемішати літери у словах обраного гравця до наступної підтвердженої правильної перевірки.'
        },
        it: {
            cardNames: ['', 'Scambio', 'Sostituzione', 'Blocco', 'Furto', 'Immunità', 'Suggerimento', 'Pausa', 'Rimescola', 'Time-out', 'Scudo', 'Mascheramento', 'Anagramma', 'Purifica'],
            negative: 'Il giocatore {0} ha usato la carta "{1}" sul giocatore {2}.',
            positive: 'Il giocatore {0} ha usato la carta "{1}" sul giocatore {2}.',
            shield: 'Il giocatore {0} si è protetto con "Scudo" dalla carta "{1}" usata da {2}.',
            immunity: 'Il giocatore {0} è stato protetto dalla carta "Immunità".',
            immunityTitle: 'Immunità attivata',
            immunityQuestion: 'Restituire "{0}" alla tua lista di parole per riprovare?',
            yes: 'Sì', no: 'No',
            debugTitle: 'Assegna carta azione', player: 'Giocatore', card: 'Carta azione', grant: 'Assegna', close: 'Chiudi',
            debugButton: 'Assegna carta azione',
            duplicate: 'Il giocatore possiede già questa carta.',
            full: 'Il giocatore ha già il numero massimo di carte.',
            unavailable: 'Le carte azione non sono disponibili in questa stanza.',
            genericError: 'Impossibile completare l’azione.',
            maskDescription: 'Nasconde alcune lettere nelle parole del giocatore scelto finché non verifica una parola qualsiasi.',
            anagramDescription: 'Mescola le lettere nelle parole del giocatore scelto fino alla prossima verifica corretta confermata.'
        }
    };
    const text = translations[language] || translations.en;
    const format = (template, ...values) => values.reduce(
        (value, replacement, index) => value.replace(`{${index}}`, String(replacement)), template || '');

    const currentSession = () => {
        if (!isCooperative) return null;
        try {
            const value = JSON.parse(localStorage.getItem(storageKey) || 'null');
            return value?.token ? value : null;
        } catch {
            return null;
        }
    };

    const patchPost = async (handler, fields = {}) => {
        const data = new FormData();
        if (antiForgery instanceof HTMLInputElement) data.set(antiForgery.name, antiForgery.value);
        Object.entries(fields).forEach(([key, value]) => {
            if (value !== null && value !== undefined && value !== '') data.set(key, String(value));
        });
        const response = await baseFetch(`${patchApiUrl}?handler=${encodeURIComponent(handler)}`, {
            method: 'POST',
            body: data,
            headers: { Accept: 'application/json' }
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json();
    };

    const handlerFromUrl = url => {
        try {
            const parsed = new URL(url, window.location.href);
            if (!roomApiUrl || !parsed.pathname.endsWith(new URL(roomApiUrl, window.location.href).pathname)) return '';
            return parsed.searchParams.get('handler') || '';
        } catch {
            return '';
        }
    };

    const syntheticJsonResponse = (payload, sourceResponse = null) => new Response(JSON.stringify(payload), {
        status: sourceResponse?.status || 200,
        statusText: sourceResponse?.statusText || 'OK',
        headers: { 'Content-Type': 'application/json; charset=utf-8' }
    });

    const formValue = (init, key) => init?.body instanceof FormData ? init.body.get(key) : null;
    const optimisticSeedWords = new Set();

    const hideOptimisticSeedWords = () => {
        if (!(wordList instanceof HTMLElement) || optimisticSeedWords.size === 0) return;
        wordList.querySelectorAll('.word-rings-word[data-word]').forEach(token => {
            const word = String(token.dataset.word || '').toLocaleLowerCase();
            if (!optimisticSeedWords.has(word)) return;
            token.hidden = true;
            token.dataset.actionSeedOptimistic = 'true';
        });
    };

    const restoreOptimisticSeedWord = word => {
        optimisticSeedWords.delete(word);
        if (!(wordList instanceof HTMLElement)) return;
        wordList.querySelectorAll('.word-rings-word[data-action-seed-optimistic="true"]').forEach(token => {
            if (String(token.dataset.word || '').toLocaleLowerCase() !== word) return;
            token.hidden = false;
            delete token.dataset.actionSeedOptimistic;
        });
    };

    if (wordList instanceof HTMLElement) {
        new MutationObserver(hideOptimisticSeedWords).observe(wordList, { childList: true, subtree: true });
    }

    window.fetch = async (input, init = {}) => {
        const url = typeof input === 'string' ? input : String(input?.url || '');
        const handler = isCooperative ? handlerFromUrl(url) : '';

        if (handler === 'UseActionCard') {
            return baseFetch(`${patchApiUrl}?handler=UseActionCard`, init);
        }

        if (handler === 'ResolveRoomPlacement') {
            const placementId = formValue(init, 'placementId');
            await patchPost('EnsureTargetWords', {
                roomCode: formValue(init, 'roomCode'),
                playerToken: formValue(init, 'playerToken')
            });
            const queued = await patchPost('TryQueueHostedImmunity', {
                roomCode: formValue(init, 'roomCode'),
                playerToken: formValue(init, 'playerToken'),
                placementId
            });
            if (queued?.success && queued?.queued) {
                return syntheticJsonResponse({ success: true, state: queued.state, actionCardImmune: true });
            }

            const response = await baseFetch(input, init);
            try {
                const payload = await response.clone().json();
                if (payload?.success) {
                    await patchPost('RecordHostedResolutionLifecycle', {
                        roomCode: formValue(init, 'roomCode'),
                        playerToken: formValue(init, 'playerToken'),
                        placementId
                    });
                }
            } catch (error) {
                console.debug('Could not synchronize hosted action-card effects.', error);
            }
            return response;
        }

        if (handler === 'SubmitRoomWord') {
            await patchPost('EnsureTargetWords', {
                roomCode: formValue(init, 'roomCode'),
                playerToken: formValue(init, 'playerToken')
            });
            const response = await baseFetch(input, init);
            try {
                const payload = await response.clone().json();
                if (payload?.success && payload?.result) {
                    const result = payload.result;
                    const placement = [...(result.state?.placements || [])]
                        .reverse()
                        .find(item => item.word === result.word && item.playerId === result.state?.playerId);
                    await patchPost('RecordSubmissionLifecycle', {
                        roomCode: formValue(init, 'roomCode'),
                        playerToken: formValue(init, 'playerToken'),
                        placementId: placement?.id || ''
                    });
                }
            } catch (error) {
                console.debug('Could not synchronize action-card submission effects.', error);
            }
            return response;
        }

        if (handler === 'StartRoom') {
            const response = await baseFetch(input, init);
            try {
                const payload = await response.clone().json();
                if (payload?.success) {
                    const ensured = await patchPost('EnsureTargetWords', {
                        roomCode: formValue(init, 'roomCode'),
                        playerToken: formValue(init, 'playerToken')
                    });
                    if (ensured?.success && ensured?.state) {
                        payload.state = ensured.state;
                        return syntheticJsonResponse(payload, response);
                    }
                }
            } catch (error) {
                console.debug('Could not ensure the Word Rings target remains reachable.', error);
            }
            return response;
        }

        if (handler === 'PlaceRoomSeed') {
            const rawWord = String(formValue(init, 'word') || '').trim();
            const key = rawWord.toLocaleLowerCase();
            if (key) {
                optimisticSeedWords.add(key);
                hideOptimisticSeedWords();
            }
            const response = await baseFetch(input, init);
            let succeeded = false;
            try {
                const payload = await response.clone().json();
                succeeded = payload?.success === true;
            } catch { }
            if (key) {
                if (succeeded) {
                    window.setTimeout(() => optimisticSeedWords.delete(key), 1200);
                } else {
                    restoreOptimisticSeedWord(key);
                }
            }
            return response;
        }

        return baseFetch(input, init);
    };

    const notice = document.createElement('div');
    notice.className = 'word-rings-action-effect-notice';
    notice.setAttribute('role', 'status');
    notice.setAttribute('aria-live', 'assertive');
    notice.hidden = true;
    root.append(notice);
    let noticeTimer = null;
    let lastNoticeId = 0;

    const showNotice = value => {
        if (!value) return;
        const cardName = text.cardNames[Number(value.cardId)] || `#${value.cardId}`;
        let message;
        if (value.kind === 'shield-blocked') {
            message = format(text.shield, value.targetName, cardName, value.actorName);
        } else if (value.kind === 'immunity') {
            message = format(text.immunity, value.targetName);
        } else {
            message = format(value.kind === 'positive' ? text.positive : text.negative, value.actorName, cardName, value.targetName);
        }
        if (noticeTimer !== null) window.clearTimeout(noticeTimer);
        notice.textContent = message;
        notice.classList.remove('is-positive', 'is-negative', 'is-neutral', 'is-visible');
        notice.classList.add(value.kind === 'negative' ? 'is-negative' : value.kind === 'shield-blocked' ? 'is-neutral' : 'is-positive');
        notice.hidden = false;
        void notice.offsetWidth;
        notice.classList.add('is-visible');
        noticeTimer = window.setTimeout(() => {
            notice.classList.remove('is-visible');
            notice.hidden = true;
            noticeTimer = null;
        }, 3100);
    };

    const immunityDialog = document.createElement('dialog');
    immunityDialog.className = 'word-rings-action-patch-dialog';
    const immunityCard = document.createElement('div');
    immunityCard.className = 'word-rings-action-patch-card';
    const immunityTitle = document.createElement('h2');
    immunityTitle.textContent = text.immunityTitle;
    const immunityQuestion = document.createElement('p');
    const immunityActions = document.createElement('div');
    immunityActions.className = 'word-rings-action-patch-actions';
    const immunityNo = document.createElement('button');
    immunityNo.type = 'button';
    immunityNo.className = 'button';
    immunityNo.textContent = text.no;
    const immunityYes = document.createElement('button');
    immunityYes.type = 'button';
    immunityYes.className = 'button button-primary';
    immunityYes.textContent = text.yes;
    immunityActions.append(immunityNo, immunityYes);
    immunityCard.append(immunityTitle, immunityQuestion, immunityActions);
    immunityDialog.append(immunityCard);
    root.append(immunityDialog);
    let currentImmunityDecision = null;
    let immunityDecisionBusy = false;

    const resolveImmunity = async returnWord => {
        if (!currentImmunityDecision || immunityDecisionBusy) return;
        const session = currentSession();
        if (!session?.token) return;
        immunityDecisionBusy = true;
        immunityYes.disabled = true;
        immunityNo.disabled = true;
        try {
            const payload = await patchPost('ResolveImmunityDecision', {
                roomCode,
                playerToken: session.token,
                decisionId: currentImmunityDecision.id,
                returnWord
            });
            if (!payload?.success) throw new Error(payload?.error || 'ActionCardImmunityDecisionFailed');
            currentImmunityDecision = null;
            if (immunityDialog.open) immunityDialog.close();
        } catch (error) {
            console.error('Could not resolve Word Rings immunity decision.', error);
        } finally {
            immunityDecisionBusy = false;
            immunityYes.disabled = false;
            immunityNo.disabled = false;
        }
    };
    immunityYes.addEventListener('click', () => void resolveImmunity(true));
    immunityNo.addEventListener('click', () => void resolveImmunity(false));
    immunityDialog.addEventListener('cancel', event => event.preventDefault());

    const debugButton = document.createElement('button');
    debugButton.type = 'button';
    debugButton.className = 'button word-rings-host-toolbar-icon word-rings-action-debug-button';
    debugButton.textContent = '🃏';
    debugButton.title = text.debugButton;
    debugButton.setAttribute('aria-label', text.debugButton);
    debugButton.hidden = true;
    toolbarActions?.insertBefore(debugButton, toolbarActions.lastElementChild || null);

    const debugDialog = document.createElement('dialog');
    debugDialog.className = 'word-rings-action-patch-dialog';
    const debugCard = document.createElement('div');
    debugCard.className = 'word-rings-action-patch-card';
    const debugTitle = document.createElement('h2');
    debugTitle.textContent = text.debugTitle;
    const playerLabel = document.createElement('label');
    playerLabel.textContent = text.player;
    const playerSelect = document.createElement('select');
    playerLabel.append(playerSelect);
    const cardLabel = document.createElement('label');
    cardLabel.textContent = text.card;
    const cardSelect = document.createElement('select');
    text.cardNames.slice(1).forEach((name, index) => {
        const option = document.createElement('option');
        option.value = String(index + 1);
        option.textContent = `${index + 1}. ${name}`;
        cardSelect.append(option);
    });
    cardLabel.append(cardSelect);
    const debugError = document.createElement('p');
    debugError.className = 'word-rings-action-patch-error';
    const debugActions = document.createElement('div');
    debugActions.className = 'word-rings-action-patch-actions';
    const debugClose = document.createElement('button');
    debugClose.type = 'button';
    debugClose.className = 'button';
    debugClose.textContent = text.close;
    const debugGrant = document.createElement('button');
    debugGrant.type = 'button';
    debugGrant.className = 'button button-primary';
    debugGrant.textContent = text.grant;
    debugActions.append(debugClose, debugGrant);
    debugCard.append(debugTitle, playerLabel, cardLabel, debugError, debugActions);
    debugDialog.append(debugCard);
    root.append(debugDialog);
    let debugPlayers = [];

    const renderDebugPlayers = players => {
        debugPlayers = Array.isArray(players) ? players : [];
        const previous = playerSelect.value;
        playerSelect.replaceChildren();
        debugPlayers.forEach(player => {
            const option = document.createElement('option');
            option.value = String(player.id);
            option.textContent = player.name;
            playerSelect.append(option);
        });
        if ([...playerSelect.options].some(option => option.value === previous)) playerSelect.value = previous;
        debugGrant.disabled = debugPlayers.length === 0;
    };

    debugButton.addEventListener('click', () => {
        debugError.textContent = '';
        if (!debugDialog.open) debugDialog.showModal();
    });
    debugClose.addEventListener('click', () => debugDialog.close());
    debugDialog.addEventListener('cancel', event => {
        event.preventDefault();
        debugDialog.close();
    });
    debugGrant.addEventListener('click', async () => {
        const session = currentSession();
        if (!session?.token || !playerSelect.value) return;
        debugGrant.disabled = true;
        debugError.textContent = '';
        try {
            const payload = await patchPost('GrantDebugActionCard', {
                roomCode,
                playerToken: session.token,
                playerId: playerSelect.value,
                cardId: cardSelect.value
            });
            if (!payload?.success) {
                const messages = {
                    ActionCardDuplicate: text.duplicate,
                    ActionCardHandFull: text.full,
                    ActionCardsUnavailable: text.unavailable
                };
                throw new Error(messages[payload?.error] || text.genericError);
            }
            debugDialog.close();
        } catch (error) {
            debugError.textContent = error?.message || text.genericError;
        } finally {
            debugGrant.disabled = debugPlayers.length === 0;
        }
    });

    const refreshDescriptions = () => {
        root.querySelectorAll('.word-rings-action-card[data-action-card-id="11"] .word-rings-action-card-description')
            .forEach(element => { if (element.textContent !== text.maskDescription) element.textContent = text.maskDescription; });
        root.querySelectorAll('.word-rings-action-card[data-action-card-id="12"] .word-rings-action-card-description')
            .forEach(element => { if (element.textContent !== text.anagramDescription) element.textContent = text.anagramDescription; });
    };
    new MutationObserver(refreshDescriptions).observe(root, { childList: true, subtree: true });

    let patchPolling = false;
    let pendingForHost = false;
    const applyHostPendingState = () => {
        if (!(judgeButton instanceof HTMLButtonElement)) return;
        if (pendingForHost && !judgeButton.disabled) judgeButton.disabled = true;
    };
    if (judgeButton instanceof HTMLButtonElement) {
        new MutationObserver(applyHostPendingState).observe(judgeButton, { attributes: true, attributeFilter: ['disabled'] });
    }

    const pollPatchState = async () => {
        if (!isCooperative || patchPolling) return;
        const session = currentSession();
        if (!session?.token) {
            debugButton.hidden = true;
            return;
        }
        patchPolling = true;
        try {
            const payload = await patchPost('State', { roomCode, playerToken: session.token });
            if (!payload?.success) return;

            const nextNotice = payload.notice;
            const noticeId = Number(nextNotice?.id || 0);
            if (noticeId > lastNoticeId) {
                lastNoticeId = noticeId;
                const age = Date.now() - Number(nextNotice?.createdAtUnixMilliseconds || 0);
                if (age >= 0 && age < 10000) showNotice(nextNotice);
            }

            const decision = payload.pendingImmunityDecision;
            if (decision) {
                const decisionId = Number(decision.id || 0);
                if (!currentImmunityDecision || Number(currentImmunityDecision.id) !== decisionId) {
                    currentImmunityDecision = decision;
                    immunityQuestion.textContent = format(text.immunityQuestion, decision.word || '');
                    if (!immunityDialog.open) immunityDialog.showModal();
                }
            } else if (!immunityDecisionBusy) {
                currentImmunityDecision = null;
                if (immunityDialog.open) immunityDialog.close();
            }

            pendingForHost = payload.isHost === true && payload.anyPendingImmunityDecision === true;
            applyHostPendingState();
            const showDebug = payload.debugMode === true && payload.isHost === true;
            debugButton.hidden = !showDebug;
            if (showDebug) renderDebugPlayers(payload.players);
            else if (debugDialog.open) debugDialog.close();
            refreshDescriptions();
        } catch (error) {
            console.debug('Could not refresh Word Rings action-card patch state.', error);
        } finally {
            patchPolling = false;
        }
    };

    if (isCooperative) {
        void pollPatchState();
        window.setInterval(pollPatchState, 600);
    }
    refreshDescriptions();
})();
