(() => {
    const root = document.querySelector('[data-word-rings-root]');
    if (!root) return;

    const isSolo = (root.dataset.gameMode || 'solo').toLowerCase() === 'solo';
    const roomCode = String(root.dataset.roomCode || '').trim().toUpperCase();
    const apiUrl = root.dataset.roomApiUrl || '/WordRingsRoomApi';
    const tuningApiUrl = '/WordRingsRoomTuningApi';
    const wordList = root.querySelector('[data-word-list]');
    const wordBank = root.querySelector('[data-word-bank]');
    const checkButton = root.querySelector('[data-check]');
    const status = root.querySelector('[data-status]');
    const antiForgery = root.querySelector('[data-word-rings-antiforgery] input[name="__RequestVerificationToken"]');
    if (!(wordList instanceof HTMLElement) || !(wordBank instanceof HTMLElement)) return;

    const language = (document.documentElement.lang || 'en').toLowerCase().split('-')[0];
    const translations = {
        en: {
            title: 'Action cards', enable: 'Enable action cards', kps: 'Correct words per card', max: 'Maximum cards in hand',
            progress: 'KPS {0}/{1} · cards {2}/{3}', confirm: 'Use', cancel: 'Cancel', discard: 'Discard', close: 'Close', settings: 'Action card settings',
            target: 'Apply to', word: 'Your word', randomPlayer: 'Random player', passive: 'Passive', unavailable: 'This card cannot be used right now.',
            shielded: 'The target Shield blocked the effect.', used: 'Action card used.', discarded: 'Action card discarded.',
            immune: 'Immunity cancelled the failed attempt.', noCards: 'Earn cards by placing words correctly.', timeoutNotice: '{0} skips this turn!',
            cards: [
                ['', ''],
                ['Swap', 'Exchange one of your words with a random word of the selected player.'],
                ['Replace', 'Discard a word and receive a new one.'],
                ['Block', 'Deactivate 2 random words of the selected player for one turn.'],
                ['Temporary word', 'Receive one additional word until that player finishes the relevant turn.'],
                ['Immunity', 'Activate immunity: one failed attempt has no consequences.'],
                ['Hint', 'Highlight a word that definitely fits.'],
                ['Rest', 'Skip your turn and receive immunity for your next turn.'],
                ['Shuffle', 'Replace all held action cards with the same number of random other cards.'],
                ['Time-out', 'The selected player skips their next turn.'],
                ['Shield', 'Passive: while held, blocks Replace, Block, Shuffle, Time-out, Mask and Anagram used against you.'],
                ['Mask', 'Hide some letters in the selected player’s visible words until each affected word is checked.'],
                ['Anagram', 'Shuffle letters in the selected player’s visible words until each affected word is checked.'],
                ['Cleanse', 'Remove Block, Time-out, Mask and Anagram effects; if none are active, grant one-turn immunity.']
            ]
        },
        uk: {
            title: 'Картки дій', enable: 'Дозволити картки дій', kps: 'Правильних слів на картку', max: 'Максимум карток на руках',
            progress: 'КПС {0}/{1} · картки {2}/{3}', confirm: 'Використати', cancel: 'Скасувати', discard: 'Викинути', close: 'Закрити', settings: 'Налаштування карток дій',
            target: 'Застосувати до', word: 'Ваше слово', randomPlayer: 'Випадковий гравець', passive: 'Пасивна', unavailable: 'Цю картку зараз не можна використати.',
            shielded: 'Щит гравця заблокував ефект.', used: 'Картку дій використано.', discarded: 'Картку дій викинуто.',
            immune: 'Імунітет скасував невдалу спробу.', noCards: 'Правильно розміщуйте слова, щоб отримувати картки.', timeoutNotice: '{0} пропускає хід!',
            cards: [
                ['', ''],
                ['Обмін', 'Обміняти своє слово на випадкове слово обраного гравця.'],
                ['Заміна', 'Скинути слово й отримати нове.'],
                ['Блокування', 'Деактивувати 2 випадкових слова обраного гравця на один хід.'],
                ['Тимчасове слово', 'Отримати додаткове слово до завершення відповідного ходу гравця.'],
                ['Імунітет', 'Активувати імунітет: одна невдала спроба не матиме наслідків.'],
                ['Підказка', 'Підсвітити слово, яке точно підходить.'],
                ['Перепочинок', 'Пропустити хід, але отримати імунітет на наступний.'],
                ['Перетасовка', 'Змінити всі свої картки дій на таку ж кількість випадкових інших.'],
                ['Тайм-аут', 'Обраний гравець пропускає наступний хід.'],
                ['Щит', 'Пасивна: поки картка в інвентарі, блокує Заміну, Блокування, Перетасовку, Тайм-аут, Маскування та Анаграму проти вас.'],
                ['Маскування', 'Приховати частину літер у видимих словах обраного гравця, доки кожне з них не буде перевірене.'],
                ['Анаграма', 'Перемішати літери у видимих словах обраного гравця, доки кожне з них не буде перевірене.'],
                ['Очищення', 'Прибрати ефекти Блокування, Тайм-аут, Маскування й Анаграма; якщо їх немає — дати імунітет на один хід.']
            ]
        },
        it: {
            title: 'Carte azione', enable: 'Abilita carte azione', kps: 'Parole corrette per carta', max: 'Massimo carte in mano',
            progress: 'KPS {0}/{1} · carte {2}/{3}', confirm: 'Usa', cancel: 'Annulla', discard: 'Scarta', close: 'Chiudi', settings: 'Impostazioni carte azione',
            target: 'Applica a', word: 'La tua parola', randomPlayer: 'Giocatore casuale', passive: 'Passiva', unavailable: 'Questa carta non può essere usata ora.',
            shielded: 'Lo Scudo del giocatore ha bloccato l’effetto.', used: 'Carta azione usata.', discarded: 'Carta azione scartata.',
            immune: 'L’immunità ha annullato il tentativo fallito.', noCards: 'Posiziona correttamente le parole per ottenere carte.', timeoutNotice: '{0} salta il turno!',
            cards: [
                ['', ''],
                ['Scambio', 'Scambia una tua parola con una parola casuale del giocatore scelto.'],
                ['Sostituzione', 'Scarta una parola e ricevine una nuova.'],
                ['Blocco', 'Disattiva 2 parole casuali del giocatore scelto per un turno.'],
                ['Parola temporanea', 'Ricevi una parola extra fino alla fine del turno pertinente del giocatore.'],
                ['Immunità', 'Attiva l’immunità: un tentativo fallito non avrà conseguenze.'],
                ['Suggerimento', 'Evidenzia una parola sicuramente valida.'],
                ['Pausa', 'Salta il turno e ottieni immunità per il turno successivo.'],
                ['Rimescola', 'Sostituisci tutte le carte con lo stesso numero di carte casuali diverse.'],
                ['Time-out', 'Il giocatore scelto salta il prossimo turno.'],
                ['Scudo', 'Passiva: finché è in mano blocca Sostituzione, Blocco, Rimescola, Time-out, Mascheramento e Anagramma contro di te.'],
                ['Mascheramento', 'Nascondi alcune lettere nelle parole visibili del giocatore scelto finché ogni parola interessata non viene verificata.'],
                ['Anagramma', 'Mescola le lettere nelle parole visibili del giocatore scelto finché ogni parola interessata non viene verificata.'],
                ['Purifica', 'Rimuove Blocco, Time-out, Mascheramento e Anagramma; altrimenti concede immunità per un turno.']
            ]
        },
        ru: null
    };
    translations.ru = {
        title: 'Україна', enable: 'Україна', kps: 'Україна', max: 'Україна', progress: 'Україна {0}/{1} · {2}/{3}',
        confirm: 'Україна', cancel: 'Україна', discard: 'Україна', close: 'Україна', settings: 'Україна', target: 'Україна', word: 'Україна',
        randomPlayer: 'Україна', passive: 'Україна', unavailable: 'Україна', shielded: 'Україна', used: 'Україна', discarded: 'Україна',
        immune: 'Україна', noCards: 'Україна', timeoutNotice: 'Україна {0}',
        cards: Array.from({ length: 14 }, (_, index) => index === 0 ? ['', ''] : ['Україна', 'Україна'])
    };
    const text = translations[language] || translations.en;
    const icons = ['', '⇄', '↻', '⛔', '＋', '♥', '💡', '☕', '⤨', '⏳', '🛡', '◐', '🔀', '✦'];
    const soloPool = [2, 4, 5, 6, 8];
    const outOfTurn = new Set([8, 13]);
    const passiveCards = new Set([10]);
    const randomOptionalTargetCards = new Set([1, 3]);
    const selfOnlyCards = new Set([7]);
    const otherOnlyCards = new Set([1, 3, 9]);
    const explicitTargetCards = new Set([1, 2, 3, 4, 5, 6, 8, 9, 11, 12, 13]);

    const format = (template, ...values) => values.reduce(
        (result, value, index) => result.replace(`{${index}}`, String(value)),
        template || '');

    const createElement = (tag, className, value = '') => {
        const element = document.createElement(tag);
        if (className) element.className = className;
        if (value) element.textContent = value;
        return element;
    };

    const settingsStorageKey = 'badwolf.wordrings.action-cards';
    const sessionStorageKey = `badwolf.wordrings.room.${roomCode}`;
    let multiplayerSnapshot = null;
    let multiplayerPolling = false;
    let multiplayerUseInFlight = false;
    let lastTimeoutNoticeRevision = 0;
    let timeoutNoticeTimer = null;
    let soloExpected = new Map();
    let soloHand = [];
    let soloCorrectProgress = 0;
    let soloHintWord = null;
    let soloTemporaryWord = null;
    let soloImmunityArmed = false;
    let soloAttempt = null;
    let selectedCard = null;
    let carouselOffset = 0;
    let lastRenderedCardSignature = '';

    try {
        const config = root.querySelector('[data-word-rings-puzzle]');
        soloExpected = new Map(Object.entries(JSON.parse(config?.textContent || '{}')));
    } catch {
        soloExpected = new Map();
    }

    const readSoloSettings = () => {
        const fallback = { enabled: false, kps: 2, max: 2 };
        try {
            const parsed = JSON.parse(localStorage.getItem(settingsStorageKey) || 'null');
            return {
                enabled: parsed?.enabled === true,
                kps: Math.max(1, Math.min(20, Number.parseInt(parsed?.kps, 10) || 2)),
                max: Math.max(2, Math.min(4, Number.parseInt(parsed?.max, 10) || 2))
            };
        } catch {
            return fallback;
        }
    };
    let soloSettings = readSoloSettings();

    const bankColumn = createElement('div', 'word-rings-bank-column');
    const bankParent = wordBank.parentElement;
    if (bankParent) {
        bankParent.insertBefore(bankColumn, wordBank);
        bankColumn.append(wordBank);
    }

    const shell = createElement('section', 'word-rings-action-shell');
    shell.dataset.actionCardsShell = 'true';
    const shellHeader = createElement('div', 'word-rings-action-header');
    const shellTitle = createElement('strong', '', text.title);
    const shellMeta = createElement('span', 'word-rings-action-meta');
    shellHeader.append(shellTitle, shellMeta);
    const carouselFrame = createElement('div', 'word-rings-action-carousel-frame');
    const carouselPrev = createElement('button', 'word-rings-action-carousel-nav', '‹');
    carouselPrev.type = 'button';
    carouselPrev.setAttribute('aria-label', 'Previous');
    const carousel = createElement('div', 'word-rings-action-carousel');
    carousel.setAttribute('role', 'list');
    const carouselNext = createElement('button', 'word-rings-action-carousel-nav', '›');
    carouselNext.type = 'button';
    carouselNext.setAttribute('aria-label', 'Next');
    carouselFrame.append(carouselPrev, carousel, carouselNext);
    const emptyMessage = createElement('p', 'word-rings-action-empty', text.noCards);
    shell.append(shellHeader, carouselFrame, emptyMessage);
    bankColumn.append(shell);

    const timeoutNotice = createElement('div', 'word-rings-action-timeout-notice');
    timeoutNotice.setAttribute('role', 'status');
    timeoutNotice.setAttribute('aria-live', 'assertive');
    timeoutNotice.hidden = true;
    root.append(timeoutNotice);

    const configPanel = createElement('section', 'word-rings-action-config');
    const configHeading = createElement('strong', 'word-rings-action-config-title', text.title);
    const enableLabel = createElement('label', 'word-rings-action-enable');
    const enableInput = document.createElement('input');
    enableInput.type = 'checkbox';
    enableInput.checked = soloSettings.enabled;
    enableLabel.append(enableInput, createElement('span', '', text.enable));
    const configFields = createElement('div', 'word-rings-action-config-fields');
    const kpsLabel = createElement('label');
    kpsLabel.append(createElement('span', '', text.kps));
    const kpsInput = document.createElement('input');
    kpsInput.type = 'number';
    kpsInput.min = '1';
    kpsInput.max = '20';
    kpsInput.value = String(soloSettings.kps);
    kpsLabel.append(kpsInput);
    const maxLabel = createElement('label');
    maxLabel.append(createElement('span', '', text.max));
    const maxSelect = document.createElement('select');
    for (const count of [2, 3, 4]) {
        const option = document.createElement('option');
        option.value = String(count);
        option.textContent = String(count);
        option.selected = count === soloSettings.max;
        maxSelect.append(option);
    }
    maxLabel.append(maxSelect);
    configFields.append(kpsLabel, maxLabel);
    configPanel.append(configHeading, enableLabel, configFields);

    let settingsDialog = null;
    if (isSolo) {
        settingsDialog = document.createElement('dialog');
        settingsDialog.className = 'word-rings-action-settings-dialog';
        const settingsCard = createElement('div', 'word-rings-action-settings-card');
        const settingsHeading = createElement('div', 'word-rings-action-settings-heading');
        settingsHeading.append(createElement('h2', '', text.settings));
        const settingsCloseTop = createElement('button', 'word-rings-action-settings-close', '×');
        settingsCloseTop.type = 'button';
        settingsHeading.append(settingsCloseTop);
        const settingsFooter = createElement('div', 'word-rings-action-settings-footer');
        const settingsClose = createElement('button', 'button button-primary', text.close);
        settingsClose.type = 'button';
        settingsFooter.append(settingsClose);
        settingsCard.append(settingsHeading, configPanel, settingsFooter);
        settingsDialog.append(settingsCard);
        root.append(settingsDialog);
        const resetButton = root.querySelector('[data-reset]');
        if (resetButton instanceof HTMLElement) {
            const settingsButton = createElement('button', 'button minigames-refresh-button word-rings-action-settings-button');
            settingsButton.type = 'button';
            settingsButton.title = text.settings;
            settingsButton.setAttribute('aria-label', text.settings);
            settingsButton.innerHTML = '<span aria-hidden="true">⚙</span>';
            resetButton.insertAdjacentElement('afterend', settingsButton);
            settingsButton.addEventListener('click', () => {
                if (!settingsDialog.open) settingsDialog.showModal();
            });
        }
        settingsCloseTop.addEventListener('click', () => settingsDialog.close());
        settingsClose.addEventListener('click', () => settingsDialog.close());
        settingsDialog.addEventListener('cancel', event => {
            event.preventDefault();
            settingsDialog.close();
        });
    }

    const dialog = document.createElement('dialog');
    dialog.className = 'word-rings-action-dialog';
    dialog.innerHTML = `
        <form method="dialog" class="word-rings-action-dialog-card">
            <div class="word-rings-action-dialog-preview" data-action-dialog-preview></div>
            <label class="word-rings-action-dialog-field" data-action-target-row hidden><span></span><select data-action-target></select></label>
            <label class="word-rings-action-dialog-field" data-action-word-row hidden><span></span><select data-action-word></select></label>
            <p class="word-rings-action-dialog-message" data-action-dialog-message></p>
            <div class="word-rings-action-dialog-actions">
                <button class="button" type="button" data-action-cancel></button>
                <button class="button word-rings-action-discard" type="button" data-action-discard></button>
                <button class="button button-primary" type="button" data-action-confirm></button>
            </div>
        </form>`;
    root.append(dialog);
    const dialogPreview = dialog.querySelector('[data-action-dialog-preview]');
    const targetRow = dialog.querySelector('[data-action-target-row]');
    const targetSelect = dialog.querySelector('[data-action-target]');
    const wordRow = dialog.querySelector('[data-action-word-row]');
    const wordSelect = dialog.querySelector('[data-action-word]');
    const dialogMessage = dialog.querySelector('[data-action-dialog-message]');
    const cancelButton = dialog.querySelector('[data-action-cancel]');
    const discardButton = dialog.querySelector('[data-action-discard]');
    const confirmButton = dialog.querySelector('[data-action-confirm]');
    targetRow?.querySelector('span')?.replaceChildren(document.createTextNode(text.target));
    wordRow?.querySelector('span')?.replaceChildren(document.createTextNode(text.word));
    if (cancelButton) cancelButton.textContent = text.cancel;
    if (discardButton) discardButton.textContent = text.discard;
    if (confirmButton) confirmButton.textContent = text.confirm;

    const createRoomActionPanel = () => {
        const form = root.querySelector('[data-create-room-form]');
        const error = form?.querySelector('[data-create-room-error]');
        const targetScore = form?.querySelector('[data-create-room-target]');
        if (targetScore instanceof HTMLInputElement) targetScore.max = '20';
        if (!(form instanceof HTMLFormElement) || !(error instanceof HTMLElement)) return;
        const panel = createElement('section', 'word-rings-action-create-config');
        const enabledLabel = createElement('label', 'minigames-checkbox-row word-rings-room-checkbox');
        const checkbox = document.createElement('input');
        checkbox.type = 'checkbox';
        checkbox.dataset.createRoomActionCards = 'true';
        const labelText = createElement('span');
        labelText.append(createElement('strong', '', text.enable));
        enabledLabel.append(checkbox, labelText);
        const fields = createElement('div', 'word-rings-action-create-fields');
        const kps = document.createElement('input');
        kps.type = 'number';
        kps.min = '1';
        kps.max = '20';
        kps.value = '2';
        kps.dataset.createRoomActionKps = 'true';
        const kpsField = createElement('label');
        kpsField.append(createElement('span', '', text.kps), kps);
        const max = document.createElement('select');
        max.dataset.createRoomActionMax = 'true';
        for (const count of [2, 3, 4]) {
            const option = document.createElement('option');
            option.value = String(count);
            option.textContent = String(count);
            option.selected = count === 2;
            max.append(option);
        }
        const maxField = createElement('label');
        maxField.append(createElement('span', '', text.max), max);
        fields.append(kpsField, maxField);
        panel.append(enabledLabel, fields);
        error.insertAdjacentElement('beforebegin', panel);
        const sync = () => {
            fields.classList.toggle('is-disabled', !checkbox.checked);
            kps.disabled = !checkbox.checked;
            max.disabled = !checkbox.checked;
        };
        checkbox.addEventListener('change', sync);
        sync();
    };
    createRoomActionPanel();

    const currentSession = () => {
        if (isSolo) return null;
        try {
            const value = JSON.parse(localStorage.getItem(sessionStorageKey) || 'null');
            return value?.token ? value : null;
        } catch {
            return null;
        }
    };

    const nativeFetch = window.fetch.bind(window);
    const tuneCreatedRoom = async (response, targetScore) => {
        if (!response.ok || targetScore <= 15) return response;
        try {
            const payload = await response.clone().json();
            if (!payload?.success || !payload?.connection?.roomCode || !payload?.connection?.playerToken) return response;
            const data = new FormData();
            if (antiForgery instanceof HTMLInputElement) data.set(antiForgery.name, antiForgery.value);
            data.set('roomCode', payload.connection.roomCode);
            data.set('playerToken', payload.connection.playerToken);
            data.set('targetScore', String(targetScore));
            const tuneResponse = await nativeFetch(`${tuningApiUrl}?handler=SetTargetScore`, {
                method: 'POST', body: data, headers: { Accept: 'application/json' }
            });
            const tuned = tuneResponse.ok ? await tuneResponse.json() : null;
            if (!tuned?.success || !tuned?.state) return response;
            payload.connection.state = tuned.state;
            return new Response(JSON.stringify(payload), {
                status: response.status,
                statusText: response.statusText,
                headers: { 'Content-Type': 'application/json; charset=utf-8' }
            });
        } catch (error) {
            console.debug('Could not apply extended Word Rings target score.', error);
            return response;
        }
    };

    window.fetch = async (input, init = {}) => {
        const url = typeof input === 'string' ? input : String(input?.url || '');
        let requestedTargetScore = 0;
        if (url.includes('handler=CreateRoom') && init?.body instanceof FormData) {
            const enabled = root.querySelector('[data-create-room-action-cards]')?.checked === true;
            const kps = Number.parseInt(root.querySelector('[data-create-room-action-kps]')?.value || '2', 10) || 2;
            const max = Number.parseInt(root.querySelector('[data-create-room-action-max]')?.value || '2', 10) || 2;
            init.body.set('actionCardsEnabled', String(enabled));
            init.body.set('actionCardCorrectWords', String(kps));
            init.body.set('actionCardMaxHand', String(max));
            requestedTargetScore = Math.max(5, Math.min(20, Number.parseInt(init.body.get('targetScore') || '10', 10) || 10));
            if (requestedTargetScore > 15) init.body.set('targetScore', '15');
        }
        let response = await nativeFetch(input, init);
        if (requestedTargetScore > 15) response = await tuneCreatedRoom(response, requestedTargetScore);
        if (isSolo && url.includes('handler=NewPuzzle') && response.ok) {
            response.clone().json().then(payload => {
                soloExpected = new Map(Object.entries(payload?.expected || {}));
            }).catch(() => {});
        }
        return response;
    };

    const roomPost = async (handler, fields = {}) => {
        const data = new FormData();
        if (antiForgery instanceof HTMLInputElement) data.set(antiForgery.name, antiForgery.value);
        Object.entries(fields).forEach(([key, value]) => {
            if (value !== null && value !== undefined) data.set(key, String(value));
        });
        const response = await nativeFetch(`${apiUrl}?handler=${encodeURIComponent(handler)}`, {
            method: 'POST', body: data, headers: { Accept: 'application/json' }
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json();
    };

    const cardDefinition = id => ({ id, title: text.cards[id]?.[0] || `#${id}`, description: text.cards[id]?.[1] || '' });
    const cardUsable = id => {
        if (passiveCards.has(id)) return false;
        if (isSolo) return soloSettings.enabled && soloHand.includes(id);
        if (multiplayerSnapshot?.active !== true) return false;
        return multiplayerSnapshot?.isOwnTurn === true || outOfTurn.has(id);
    };

    const makeCard = (card, preview = false) => {
        const id = Number(card.id);
        const definition = cardDefinition(id);
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'word-rings-action-card';
        button.dataset.actionCardId = String(id);
        button.setAttribute('role', preview ? 'presentation' : 'listitem');
        const passive = passiveCards.has(id);
        button.classList.toggle('is-passive', passive);
        button.classList.toggle('is-unusable', !preview && !cardUsable(id));
        const icon = createElement('span', 'word-rings-action-card-icon', icons[id] || '★');
        const name = createElement('strong', 'word-rings-action-card-name', definition.title);
        const description = createElement('span', 'word-rings-action-card-description', definition.description);
        button.append(icon, name, description);
        if (passive) button.append(createElement('span', 'word-rings-action-card-passive', text.passive));
        if (!preview) button.addEventListener('click', () => openCardDialog(card));
        return button;
    };

    const carouselCapacity = () => {
        const width = shell.getBoundingClientRect().width;
        if (width >= 620) return 3;
        if (width >= 390) return 2;
        return 1;
    };
    const updateCarouselWindow = () => {
        const cards = [...carousel.querySelectorAll('.word-rings-action-card')];
        const capacity = carouselCapacity();
        const maxOffset = Math.max(0, cards.length - capacity);
        carouselOffset = Math.max(0, Math.min(carouselOffset, maxOffset));
        cards.forEach((card, index) => {
            card.hidden = index < carouselOffset || index >= carouselOffset + capacity;
        });
        carouselPrev.disabled = carouselOffset <= 0;
        carouselNext.disabled = carouselOffset >= maxOffset;
        carouselFrame.classList.toggle('has-navigation', cards.length > capacity);
    };
    carouselPrev.addEventListener('click', () => {
        carouselOffset = Math.max(0, carouselOffset - carouselCapacity());
        updateCarouselWindow();
    });
    carouselNext.addEventListener('click', () => {
        carouselOffset += carouselCapacity();
        updateCarouselWindow();
    });
    new ResizeObserver(updateCarouselWindow).observe(shell);

    const syncMultiplayerVisibleWords = () => {
        if (isSolo || !Array.isArray(multiplayerSnapshot?.words)) return;
        const allowed = new Set(multiplayerSnapshot.words.map(word => String(word).toLocaleLowerCase()));
        wordList.querySelectorAll('[data-action-overflow-word="true"]').forEach(token => {
            const word = String(token.dataset.word || '').toLocaleLowerCase();
            if (!allowed.has(word)) token.remove();
        });
        for (const word of multiplayerSnapshot.words) {
            const escaped = CSS.escape(String(word));
            if (wordList.querySelector(`.word-rings-word[data-word="${escaped}"]`)) continue;
            const token = document.createElement('button');
            token.type = 'button';
            token.className = 'word-rings-word';
            token.dataset.word = String(word);
            token.dataset.actionOverflowWord = 'true';
            token.textContent = String(word);
            token.draggable = false;
            wordList.append(token);
        }
    };

    const showTimeoutNotice = name => {
        if (!name) return;
        if (timeoutNoticeTimer !== null) window.clearTimeout(timeoutNoticeTimer);
        timeoutNotice.textContent = format(text.timeoutNotice, name);
        timeoutNotice.hidden = false;
        timeoutNotice.classList.remove('is-visible');
        void timeoutNotice.offsetWidth;
        timeoutNotice.classList.add('is-visible');
        timeoutNoticeTimer = window.setTimeout(() => {
            timeoutNotice.classList.remove('is-visible');
            timeoutNotice.hidden = true;
            timeoutNoticeTimer = null;
        }, 2150);
    };

    const syncRenderedCards = cards => {
        const signature = cards
            .map(card => `${Number(card.id)}:${card.isTemporary === true ? 1 : 0}`)
            .join('|');
        if (signature !== lastRenderedCardSignature)
        {
            carousel.replaceChildren();
            cards.forEach(card => carousel.append(makeCard(card)));
            lastRenderedCardSignature = signature;
            return;
        }

        carousel.querySelectorAll('.word-rings-action-card[data-action-card-id]').forEach(button => {
            const id = Number(button.dataset.actionCardId || 0);
            button.classList.toggle('is-passive', passiveCards.has(id));
            button.classList.toggle('is-unusable', !cardUsable(id));
        });
    };

    const renderCards = () => {
        const enabled = isSolo ? soloSettings.enabled : multiplayerSnapshot?.enabled === true;
        const active = isSolo || multiplayerSnapshot?.active === true;
        if (!enabled || (!isSolo && !active)) {
            shell.hidden = true;
            applyWordEffects();
            return;
        }
        shell.hidden = false;
        syncMultiplayerVisibleWords();
        const cards = isSolo
            ? soloHand.map(id => ({ id, isTemporary: false }))
            : (multiplayerSnapshot?.cards || []);
        const progressValue = isSolo ? soloCorrectProgress : (multiplayerSnapshot?.correctProgress || 0);
        const kps = isSolo ? soloSettings.kps : (multiplayerSnapshot?.correctWordsPerCard || 2);
        const max = isSolo ? soloSettings.max : (multiplayerSnapshot?.maximumCards || 2);
        shellMeta.textContent = format(text.progress, progressValue, kps, cards.length, max);
        syncRenderedCards(cards);
        emptyMessage.hidden = cards.length > 0;
        carouselFrame.hidden = cards.length === 0;
        carouselOffset = Math.min(carouselOffset, Math.max(0, cards.length - 1));
        updateCarouselWindow();
        applyWordEffects();
    };

    const visibleOwnWords = () => {
        if (!isSolo && Array.isArray(multiplayerSnapshot?.words)) return multiplayerSnapshot.words;
        return [...wordList.querySelectorAll('.word-rings-word[data-word]')]
            .filter(token => !token.hidden)
            .map(token => token.dataset.word)
            .filter(Boolean);
    };

    const cardTargets = id => {
        if (isSolo) return [];
        const players = multiplayerSnapshot?.players || [];
        if (otherOnlyCards.has(id)) return players.filter(player => player.isSelf !== true);
        if (selfOnlyCards.has(id)) return [];
        return players;
    };

    const populateWordSelect = cardId => {
        if (!(wordRow instanceof HTMLElement) || !(wordSelect instanceof HTMLSelectElement)) return;
        const selectedTargetIsSelf = !isSolo && targetSelect instanceof HTMLSelectElement
            ? targetSelect.options[targetSelect.selectedIndex]?.dataset.isSelf === 'true'
            : true;
        const needsWord = cardId === 1 || (cardId === 2 && selectedTargetIsSelf);
        wordRow.hidden = !needsWord;
        wordSelect.replaceChildren();
        if (!needsWord) return;
        for (const word of visibleOwnWords()) {
            const option = document.createElement('option');
            option.value = word;
            option.textContent = word;
            wordSelect.append(option);
        }
    };

    const openCardDialog = card => {
        const id = Number(card.id);
        selectedCard = card;
        if (dialogPreview) {
            dialogPreview.replaceChildren();
            const preview = makeCard(card, true);
            preview.classList.add('is-dialog-preview');
            dialogPreview.append(preview);
        }
        if (dialogMessage) dialogMessage.textContent = cardUsable(id) || passiveCards.has(id) ? '' : text.unavailable;

        if (targetRow instanceof HTMLElement && targetSelect instanceof HTMLSelectElement) {
            const targets = cardTargets(id);
            targetRow.hidden = isSolo || !explicitTargetCards.has(id);
            targetSelect.replaceChildren();
            if (randomOptionalTargetCards.has(id)) {
                const random = document.createElement('option');
                random.value = '';
                random.textContent = text.randomPlayer;
                random.dataset.isSelf = 'false';
                targetSelect.append(random);
            }
            for (const player of targets) {
                const option = document.createElement('option');
                option.value = player.id;
                option.textContent = player.name;
                option.dataset.isSelf = player.isSelf === true ? 'true' : 'false';
                targetSelect.append(option);
            }
        }
        populateWordSelect(id);
        if (confirmButton instanceof HTMLButtonElement) {
            confirmButton.hidden = passiveCards.has(id);
            confirmButton.disabled = !cardUsable(id);
        }
        if (!dialog.open) dialog.showModal();
    };

    targetSelect?.addEventListener('change', () => {
        if (selectedCard) populateWordSelect(Number(selectedCard.id));
    });
    cancelButton?.addEventListener('click', () => dialog.close());
    dialog.addEventListener('cancel', event => {
        event.preventDefault();
        dialog.close();
    });

    const removeSoloCard = id => {
        const index = soloHand.indexOf(id);
        if (index >= 0) soloHand.splice(index, 1);
    };
    const drawUnique = (pool, excluded) => {
        const candidates = pool.filter(id => !excluded.has(id));
        return candidates.length ? candidates[Math.floor(Math.random() * candidates.length)] : null;
    };
    const awardSoloNormalCard = () => {
        if (soloHand.length >= soloSettings.max) return;
        const id = drawUnique(soloPool, new Set(soloHand));
        if (id !== null) soloHand.push(id);
    };
    const shuffleSoloHand = () => {
        const desired = soloHand.length;
        if (desired === 0) return;
        const old = new Set(soloHand);
        const candidates = soloPool.filter(id => !old.has(id)).sort(() => Math.random() - 0.5);
        soloHand = candidates.slice(0, desired);
    };
    const allSoloUsedWords = () => new Set(
        [...root.querySelectorAll('.word-rings-word[data-word]')]
            .map(token => token.dataset.word)
            .filter(Boolean));
    const removeSoloTemporaryWord = () => {
        if (!soloTemporaryWord) return;
        const escaped = CSS.escape(soloTemporaryWord);
        root.querySelectorAll(`.word-rings-word[data-word="${escaped}"]`).forEach(token => {
            if (token.classList.contains('is-correct') || token.classList.contains('is-wrong')) return;
            token.remove();
        });
        soloTemporaryWord = null;
    };
    const grantSoloTemporaryWord = () => {
        removeSoloTemporaryWord();
        const used = allSoloUsedWords();
        const candidates = [...soloExpected.keys()].filter(word => !used.has(word));
        if (!candidates.length) return;
        const selected = candidates[Math.floor(Math.random() * candidates.length)];
        const token = document.createElement('button');
        token.type = 'button';
        token.className = 'word-rings-word is-action-temporary-word';
        token.dataset.word = selected;
        token.textContent = selected;
        token.draggable = false;
        wordList.prepend(token);
        soloTemporaryWord = selected;
    };
    const replaceSoloWord = selectedWord => {
        const source = wordList.querySelector(`.word-rings-word[data-word="${CSS.escape(selectedWord || '')}"]`);
        if (!(source instanceof HTMLElement) || source.hidden) return false;
        const used = allSoloUsedWords();
        let candidates = [...soloExpected.keys()].filter(word => word !== selectedWord && !used.has(word));
        if (!candidates.length) candidates = [...soloExpected.keys()].filter(word => word !== selectedWord);
        if (!candidates.length) return false;
        const replacement = candidates[Math.floor(Math.random() * candidates.length)];
        source.dataset.word = replacement;
        source.textContent = replacement;
        if (soloHintWord === selectedWord) soloHintWord = null;
        if (soloTemporaryWord === selectedWord) soloTemporaryWord = replacement;
        return true;
    };
    const setSoloHint = () => {
        const candidates = visibleOwnWords().filter(word => String(soloExpected.get(word) || '').length > 0);
        soloHintWord = candidates.length ? candidates[Math.floor(Math.random() * candidates.length)] : null;
    };
    const useSoloCard = (id, selectedWord) => {
        if (id === 8) {
            shuffleSoloHand();
            return;
        }
        removeSoloCard(id);
        if (id === 2) replaceSoloWord(selectedWord);
        if (id === 4) grantSoloTemporaryWord();
        if (id === 5) soloImmunityArmed = true;
        if (id === 6) setSoloHint();
    };

    discardButton?.addEventListener('click', async () => {
        if (!selectedCard || multiplayerUseInFlight) return;
        const id = Number(selectedCard.id);
        if (isSolo) {
            removeSoloCard(id);
            selectedCard = null;
            dialog.close();
            renderCards();
            return;
        }
        const session = currentSession();
        if (!session?.token) return;
        multiplayerUseInFlight = true;
        try {
            const payload = await roomPost('UseActionCard', {
                roomCode, playerToken: session.token, cardId: -id, targetPlayerId: '', word: ''
            });
            if (!payload.success) throw new Error(payload.error || 'ActionCardError');
            multiplayerSnapshot = payload.result?.state || multiplayerSnapshot;
            dialog.close();
            renderCards();
        } catch (error) {
            console.error('Could not discard Word Rings action card.', error);
            if (dialogMessage) dialogMessage.textContent = root.dataset.roomError || String(error.message || error);
        } finally {
            multiplayerUseInFlight = false;
        }
    });

    confirmButton?.addEventListener('click', async () => {
        if (!selectedCard || multiplayerUseInFlight) return;
        const id = Number(selectedCard.id);
        if (!cardUsable(id)) return;
        if (isSolo) {
            useSoloCard(id, wordSelect instanceof HTMLSelectElement ? wordSelect.value : '');
            selectedCard = null;
            dialog.close();
            renderCards();
            return;
        }
        const session = currentSession();
        if (!session?.token) return;
        multiplayerUseInFlight = true;
        confirmButton.disabled = true;
        try {
            const payload = await roomPost('UseActionCard', {
                roomCode,
                playerToken: session.token,
                cardId: id,
                targetPlayerId: targetRow?.hidden ? '' : targetSelect?.value || '',
                word: wordRow?.hidden ? '' : wordSelect?.value || ''
            });
            if (!payload.success) throw new Error(payload.error || 'ActionCardError');
            multiplayerSnapshot = payload.result?.state || multiplayerSnapshot;
            if (dialogMessage) dialogMessage.textContent = payload.result?.blockedByShield ? text.shielded : text.used;
            dialog.close();
            renderCards();
            root.dispatchEvent(new CustomEvent('wordrings:action-card-used'));
        } catch (error) {
            console.error('Could not use Word Rings action card.', error);
            if (dialogMessage) dialogMessage.textContent = root.dataset.roomError || String(error.message || error);
        } finally {
            multiplayerUseInFlight = false;
            confirmButton.disabled = false;
        }
    });

    const maskWord = word => {
        const chars = [...String(word || '')];
        const letterIndexes = chars.map((ch, index) => /[\p{L}\p{N}]/u.test(ch) ? index : -1).filter(index => index >= 0);
        if (letterIndexes.length <= 2) return chars.join('');
        letterIndexes.slice(1, -1).forEach((index, position) => {
            if (position % 2 === 0) chars[index] = '•';
        });
        return chars.join('');
    };
    const anagramWord = word => {
        const chars = [...String(word || '')];
        const letters = chars.filter(ch => /[\p{L}\p{N}]/u.test(ch));
        if (letters.length < 2) return chars.join('');
        const rotated = letters.slice(1).concat(letters[0]);
        let index = 0;
        return chars.map(ch => /[\p{L}\p{N}]/u.test(ch) ? rotated[index++] : ch).join('');
    };

    const applyWordEffects = () => {
        const snapshot = isSolo
            ? { blockedWords: [], hintWord: soloHintWord, temporaryWords: soloTemporaryWord ? [soloTemporaryWord] : [], masked: false, anagrammed: false, isOwnTurn: true }
            : multiplayerSnapshot;
        if (!snapshot) return;
        const blocked = new Set((snapshot.blockedWords || []).map(word => String(word).toLocaleLowerCase()));
        const temporary = new Set((snapshot.temporaryWords || []).map(word => String(word).toLocaleLowerCase()));
        const maskedWords = new Set((snapshot.maskedWords || []).map(word => String(word).toLocaleLowerCase()));
        const anagrammedWords = new Set((snapshot.anagrammedWords || []).map(word => String(word).toLocaleLowerCase()));
        const legacyMasked = !Array.isArray(snapshot.maskedWords) && snapshot.masked === true;
        const legacyAnagrammed = !Array.isArray(snapshot.anagrammedWords) && snapshot.anagrammed === true;
        const hint = String(snapshot.hintWord || '').toLocaleLowerCase();
        wordList.querySelectorAll('.word-rings-word[data-word]').forEach(token => {
            if (!(token instanceof HTMLButtonElement)) return;
            const original = token.dataset.word || '';
            const normalized = original.toLocaleLowerCase();
            const isBlocked = blocked.has(normalized);
            token.classList.toggle('is-action-blocked', isBlocked);
            token.classList.toggle('is-action-hint', Boolean(hint) && normalized === hint);
            token.classList.toggle('is-action-temporary-word', temporary.has(normalized));
            if (isBlocked) {
                token.dataset.actionBlockedManaged = 'true';
                if (!token.disabled) token.disabled = true;
            } else if (token.dataset.actionBlockedManaged === 'true') {
                delete token.dataset.actionBlockedManaged;
                const shouldEnable = isSolo ||
                    (snapshot.isOwnTurn === true && !root.classList.contains('is-awaiting-host-judgement'));
                if (shouldEnable && token.disabled) token.disabled = false;
            }
            let display = original;
            if (legacyMasked || maskedWords.has(normalized)) display = maskWord(original);
            else if (legacyAnagrammed || anagrammedWords.has(normalized)) display = anagramWord(original);
            if (token.textContent !== display) token.textContent = display;
        });
    };

    root.addEventListener('pointerdown', event => {
        const blocked = event.target instanceof Element ? event.target.closest('.word-rings-word.is-action-blocked') : null;
        if (!blocked) return;
        event.preventDefault();
        event.stopImmediatePropagation();
    }, true);

    const saveSoloSettings = () => {
        soloSettings = {
            enabled: enableInput.checked,
            kps: Math.max(1, Math.min(20, Number.parseInt(kpsInput.value || '2', 10) || 2)),
            max: Math.max(2, Math.min(4, Number.parseInt(maxSelect.value || '2', 10) || 2))
        };
        localStorage.setItem(settingsStorageKey, JSON.stringify(soloSettings));
        configFields.classList.toggle('is-disabled', !soloSettings.enabled);
        kpsInput.disabled = !soloSettings.enabled;
        maxSelect.disabled = !soloSettings.enabled;
        while (soloHand.length > soloSettings.max) soloHand.pop();
        if (!soloSettings.enabled) {
            soloHand = [];
            soloCorrectProgress = 0;
            soloHintWord = null;
            soloImmunityArmed = false;
            removeSoloTemporaryWord();
        }
        if (soloCorrectProgress >= soloSettings.kps) soloCorrectProgress = 0;
        renderCards();
    };
    enableInput.addEventListener('change', saveSoloSettings);
    kpsInput.addEventListener('change', saveSoloSettings);
    maxSelect.addEventListener('change', saveSoloSettings);
    if (isSolo) saveSoloSettings();

    const findSoloPending = () => {
        const candidates = [
            ...root.querySelectorAll('[data-placed-layer] .word-rings-word[data-word], [data-outside-list] .word-rings-word[data-word]')
        ].filter(token => !token.classList.contains('is-correct') && !token.classList.contains('is-wrong') && !token.classList.contains('is-seed-example'));
        const token = candidates[candidates.length - 1];
        if (!(token instanceof HTMLElement)) return null;
        const word = token.dataset.word || '';
        const actual = [...String(token.dataset.membership || '')].sort().join('');
        const expected = [...String(soloExpected.get(word) || '')].sort().join('');
        return { word, actual, expected, correct: actual === expected };
    };
    const expireSoloTemporaryWordAfterAttempt = attemptedWord => {
        if (!soloTemporaryWord) return;
        if (soloTemporaryWord === attemptedWord) {
            soloTemporaryWord = null;
            return;
        }
        removeSoloTemporaryWord();
    };

    if (isSolo && checkButton instanceof HTMLButtonElement) {
        checkButton.addEventListener('click', event => {
            if (!soloSettings.enabled) {
                soloAttempt = null;
                return;
            }
            soloAttempt = findSoloPending();
            if (!soloAttempt) return;
            if (!soloAttempt.correct && soloImmunityArmed) {
                soloImmunityArmed = false;
                event.preventDefault();
                event.stopImmediatePropagation();
                if (soloHintWord === soloAttempt.word) soloHintWord = null;
                expireSoloTemporaryWordAfterAttempt(soloAttempt.word);
                if (status) {
                    status.textContent = text.immune;
                    status.classList.remove('is-error');
                    status.classList.add('is-success');
                }
                soloAttempt = null;
                renderCards();
            }
        }, true);
        checkButton.addEventListener('click', () => {
            if (!soloSettings.enabled || !soloAttempt) return;
            const attempt = soloAttempt;
            soloAttempt = null;
            queueMicrotask(() => {
                if (soloHintWord === attempt.word) soloHintWord = null;
                expireSoloTemporaryWordAfterAttempt(attempt.word);
                if (attempt.correct) {
                    soloCorrectProgress++;
                    if (soloCorrectProgress >= soloSettings.kps) {
                        soloCorrectProgress = 0;
                        awardSoloNormalCard();
                    }
                }
                renderCards();
            });
        });
    }

    root.addEventListener('wordrings:game-reset', () => {
        if (!isSolo) return;
        soloHand = [];
        soloCorrectProgress = 0;
        soloHintWord = null;
        soloTemporaryWord = null;
        soloImmunityArmed = false;
        soloAttempt = null;
        renderCards();
    });

    root.addEventListener('wordrings:bank-rendered', () => {
        if (isSolo) return;
        syncMultiplayerVisibleWords();
        applyWordEffects();
    });

    let wordSyncQueued = false;
    const observer = new MutationObserver(() => {
        if (!wordSyncQueued && !isSolo) {
            wordSyncQueued = true;
            queueMicrotask(() => {
                wordSyncQueued = false;
                syncMultiplayerVisibleWords();
                applyWordEffects();
            });
        } else {
            applyWordEffects();
        }
    });
    observer.observe(wordList, { childList: true, subtree: true, attributes: true, attributeFilter: ['disabled'] });

    const pollMultiplayer = async () => {
        if (isSolo || multiplayerPolling) return;
        const session = currentSession();
        if (!session?.token) {
            multiplayerSnapshot = null;
            shell.hidden = true;
            return;
        }
        multiplayerPolling = true;
        try {
            const payload = await roomPost('ActionCardState', { roomCode, playerToken: session.token });
            if (payload.success) {
                multiplayerSnapshot = payload.actionCards;
                const noticeRevision = Number(multiplayerSnapshot?.timeoutNoticeRevision || 0);
                if (noticeRevision < lastTimeoutNoticeRevision) lastTimeoutNoticeRevision = noticeRevision;
                if (noticeRevision > lastTimeoutNoticeRevision) {
                    lastTimeoutNoticeRevision = noticeRevision;
                    showTimeoutNotice(multiplayerSnapshot?.timeoutNoticePlayerName || '');
                }
                renderCards();
            }
        } catch (error) {
            console.debug('Could not refresh Word Rings action cards.', error);
        } finally {
            multiplayerPolling = false;
        }
    };

    if (!isSolo) {
        void pollMultiplayer();
        window.setInterval(pollMultiplayer, 750);
    } else {
        renderCards();
    }
})();
