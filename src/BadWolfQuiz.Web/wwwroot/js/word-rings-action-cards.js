(() => {
    const root = document.querySelector('[data-word-rings-root]');
    if (!root) return;

    const isSolo = (root.dataset.gameMode || 'solo').toLowerCase() === 'solo';
    const roomCode = String(root.dataset.roomCode || '').trim().toUpperCase();
    const apiUrl = root.dataset.roomApiUrl || '/WordRingsRoomApi';
    const wordList = root.querySelector('[data-word-list]');
    const wordBank = root.querySelector('[data-word-bank]');
    const checkButton = root.querySelector('[data-check]');
    const status = root.querySelector('[data-status]');
    const antiForgery = root.querySelector('[data-word-rings-antiforgery] input[name="__RequestVerificationToken"]');
    if (!(wordList instanceof HTMLElement) || !(wordBank instanceof HTMLElement)) return;

    const language = (document.documentElement.lang || 'en').toLowerCase().split('-')[0];
    const translations = {
        en: {
            title: 'Action cards', enable: 'Enable action cards', kps: 'Correct words per card (KPS)', max: 'Maximum cards in hand',
            progress: 'KPS {0}/{1} · cards {2}/{3}', waiting: 'Action cards activate when at least 2 players are playing.',
            confirm: 'Use card', cancel: 'Cancel', target: 'Apply to', word: 'Your word', randomOpponent: 'Random opponent',
            passive: 'Passive', shielded: 'The target shield blocked the effect.', used: 'Action card used.',
            immune: 'Immunity cancelled the failed attempt.', noCards: 'Earn cards by placing words correctly.',
            cards: [
                ['', ''],
                ['Swap', 'Exchange one of your words with a random word from another player.'],
                ['Replace', 'Discard a word and receive a new one.'],
                ['Block', 'Deactivate 2 random words of an opponent for one turn.'],
                ['Temporary word', 'Receive one additional word until your next turn.'],
                ['Immunity', 'Passive: the next failed attempt has no consequences, then this card disappears.'],
                ['Hint', 'Highlight a word that definitely fits.'],
                ['Rest', 'Skip your turn and receive immunity for your next turn.'],
                ['Shuffle', 'Replace all held action cards with the same number of random other cards.'],
                ['Time-out', 'The selected player skips their next turn.'],
                ['Shield', 'Blocks one Block, Time-out, Mask or Anagram card used against you.'],
                ['Mask', 'Hide some letters in the selected player’s words for one attempt.'],
                ['Anagram', 'Shuffle letters in the selected player’s words for one attempt.'],
                ['Cleanse', 'Remove Block, Time-out, Mask and Anagram effects; if none are active, grant one-turn immunity.']
            ]
        },
        uk: {
            title: 'Картки дій', enable: 'Дозволити картки дій', kps: 'Правильних слів на картку (КПС)', max: 'Максимум карток на руках',
            progress: 'КПС {0}/{1} · картки {2}/{3}', waiting: 'Картки дій активуються, коли грає щонайменше 2 гравці.',
            confirm: 'Використати', cancel: 'Скасувати', target: 'Застосувати до', word: 'Ваше слово', randomOpponent: 'Випадковий суперник',
            passive: 'Пасивна', shielded: 'Щит гравця заблокував ефект.', used: 'Картку дій використано.',
            immune: 'Імунітет скасував невдалу спробу.', noCards: 'Правильно розміщуйте слова, щоб отримувати картки.',
            cards: [
                ['', ''],
                ['Обмін', 'Обміняти своє слово на випадкове слово іншого гравця.'],
                ['Заміна', 'Скинути слово й отримати нове.'],
                ['Блокування', 'Деактивувати 2 випадкових слова суперника на один хід.'],
                ['Тимчасове слово', 'Отримати додаткове слово до наступного ходу.'],
                ['Імунітет', 'Пасивна: наступна невдала спроба не має наслідків, після чого картка зникає.'],
                ['Підказка', 'Підсвітити слово, яке точно підходить.'],
                ['Перепочинок', 'Пропустити хід, але отримати імунітет на наступний.'],
                ['Перетасовка', 'Змінити всі свої картки дій на таку ж кількість випадкових інших.'],
                ['Тайм-аут', 'Обраний гравець пропускає наступний хід.'],
                ['Щит', 'Блокує одну картку Блокування, Тайм-аут, Маскування або Анаграма, застосовану проти вас.'],
                ['Маскування', 'Приховати частину літер у словах обраного гравця на одну спробу.'],
                ['Анаграма', 'Перемішати літери в словах обраного гравця на одну спробу.'],
                ['Очищення', 'Прибрати ефекти Блокування, Тайм-аут, Маскування й Анаграма; якщо їх немає — дати імунітет на один хід.']
            ]
        },
        it: {
            title: 'Carte azione', enable: 'Abilita carte azione', kps: 'Parole corrette per carta (KPS)', max: 'Massimo carte in mano',
            progress: 'KPS {0}/{1} · carte {2}/{3}', waiting: 'Le carte azione si attivano con almeno 2 giocatori.',
            confirm: 'Usa carta', cancel: 'Annulla', target: 'Applica a', word: 'La tua parola', randomOpponent: 'Avversario casuale',
            passive: 'Passiva', shielded: 'Lo scudo del giocatore ha bloccato l’effetto.', used: 'Carta azione usata.',
            immune: 'L’immunità ha annullato il tentativo fallito.', noCards: 'Posiziona correttamente le parole per ottenere carte.',
            cards: [
                ['', ''],
                ['Scambio', 'Scambia una tua parola con una parola casuale di un altro giocatore.'],
                ['Sostituzione', 'Scarta una parola e ricevine una nuova.'],
                ['Blocco', 'Disattiva 2 parole casuali di un avversario per un turno.'],
                ['Parola temporanea', 'Ricevi una parola aggiuntiva fino al tuo prossimo turno.'],
                ['Immunità', 'Passiva: il prossimo tentativo fallito non ha conseguenze, poi la carta scompare.'],
                ['Suggerimento', 'Evidenzia una parola che sicuramente appartiene ad almeno un anello.'],
                ['Pausa', 'Salta il turno e ottieni immunità per il turno successivo.'],
                ['Rimescola', 'Sostituisci tutte le carte azione con lo stesso numero di carte casuali diverse.'],
                ['Time-out', 'Il giocatore scelto salta il prossimo turno.'],
                ['Scudo', 'Blocca una carta Blocco, Time-out, Mascheramento o Anagramma usata contro di te.'],
                ['Mascheramento', 'Nascondi alcune lettere nelle parole del giocatore scelto per un tentativo.'],
                ['Anagramma', 'Mescola le lettere nelle parole del giocatore scelto per un tentativo.'],
                ['Purifica', 'Rimuove Blocco, Time-out, Mascheramento e Anagramma; se non ce ne sono, concede immunità per un turno.']
            ]
        },
        ru: null
    };
    translations.ru = {
        title: 'Україна', enable: 'Україна', kps: 'Україна', max: 'Україна', progress: 'Україна {0}/{1} · {2}/{3}',
        waiting: 'Україна', confirm: 'Україна', cancel: 'Україна', target: 'Україна', word: 'Україна', randomOpponent: 'Україна',
        passive: 'Україна', shielded: 'Україна', used: 'Україна', immune: 'Україна', noCards: 'Україна',
        cards: Array.from({ length: 14 }, (_, index) => index === 0 ? ['', ''] : ['Україна', 'Україна'])
    };
    const text = translations[language] || translations.en;
    const icons = ['', '⇄', '↻', '⛔', '＋', '♥', '💡', '☕', '⤨', '⏳', '🛡', '◐', '🔀', '✦'];
    const soloPool = [2, 4, 5, 6, 8];
    const outOfTurn = new Set([8, 10, 13]);
    const passiveCards = new Set([5]);
    const randomTargetCards = new Set([1, 3]);
    const selfOnlyCards = new Set([1, 3, 7]);
    const otherOnlyCards = new Set([9]);
    const explicitTargetCards = new Set([2, 4, 6, 8, 9, 10, 11, 12, 13]);

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
    let soloExpected = new Map();
    let soloHand = [];
    let soloCorrectProgress = 0;
    let soloHintWord = null;
    let soloTemporaryWord = null;
    let soloAttempt = null;

    try {
        const config = root.querySelector('[data-word-rings-puzzle]');
        soloExpected = new Map(Object.entries(JSON.parse(config?.textContent || '{}')));
    } catch {
        soloExpected = new Map();
    }

    const readSoloSettings = () => {
        const fallback = { enabled: false, kps: 3, max: 3 };
        try {
            const parsed = JSON.parse(localStorage.getItem(settingsStorageKey) || 'null');
            return {
                enabled: parsed?.enabled === true,
                kps: Math.max(1, Math.min(20, Number.parseInt(parsed?.kps, 10) || 3)),
                max: Math.max(2, Math.min(4, Number.parseInt(parsed?.max, 10) || 3))
            };
        } catch {
            return fallback;
        }
    };
    let soloSettings = readSoloSettings();

    const configPanel = createElement('section', 'word-rings-action-config');
    const configHeading = createElement('strong', 'word-rings-action-config-title', text.title);
    const enableLabel = createElement('label', 'word-rings-action-enable');
    const enableInput = document.createElement('input');
    enableInput.type = 'checkbox';
    enableInput.checked = soloSettings.enabled;
    const enableSpan = createElement('span', '', text.enable);
    enableLabel.append(enableInput, enableSpan);
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

    const shell = createElement('section', 'word-rings-action-shell');
    shell.dataset.actionCardsShell = 'true';
    const shellHeader = createElement('div', 'word-rings-action-header');
    const shellTitle = createElement('strong', '', text.title);
    const shellMeta = createElement('span', 'word-rings-action-meta');
    shellHeader.append(shellTitle, shellMeta);
    const carousel = createElement('div', 'word-rings-action-carousel');
    carousel.setAttribute('role', 'list');
    const emptyMessage = createElement('p', 'word-rings-action-empty', text.noCards);
    shell.append(shellHeader, carousel, emptyMessage);

    wordList.insertAdjacentElement('afterend', shell);
    if (isSolo) shell.insertAdjacentElement('beforebegin', configPanel);

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
    const confirmButton = dialog.querySelector('[data-action-confirm]');
    targetRow?.querySelector('span')?.replaceChildren(document.createTextNode(text.target));
    wordRow?.querySelector('span')?.replaceChildren(document.createTextNode(text.word));
    if (cancelButton) cancelButton.textContent = text.cancel;
    if (confirmButton) confirmButton.textContent = text.confirm;
    let selectedCard = null;

    const createRoomActionPanel = () => {
        const form = root.querySelector('[data-create-room-form]');
        const error = form?.querySelector('[data-create-room-error]');
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
        kps.value = '3';
        kps.dataset.createRoomActionKps = 'true';
        const kpsField = createElement('label');
        kpsField.append(createElement('span', '', text.kps), kps);
        const max = document.createElement('select');
        max.dataset.createRoomActionMax = 'true';
        for (const count of [2, 3, 4]) {
            const option = document.createElement('option');
            option.value = String(count);
            option.textContent = String(count);
            option.selected = count === 3;
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

    const nativeFetch = window.fetch.bind(window);
    window.fetch = async (input, init = {}) => {
        const url = typeof input === 'string' ? input : String(input?.url || '');
        if (url.includes('handler=CreateRoom') && init?.body instanceof FormData) {
            const enabled = root.querySelector('[data-create-room-action-cards]')?.checked === true;
            const kps = Number.parseInt(root.querySelector('[data-create-room-action-kps]')?.value || '3', 10) || 3;
            const max = Number.parseInt(root.querySelector('[data-create-room-action-max]')?.value || '3', 10) || 3;
            init.body.set('actionCardsEnabled', String(enabled));
            init.body.set('actionCardCorrectWords', String(kps));
            init.body.set('actionCardMaxHand', String(max));
        }
        const response = await nativeFetch(input, init);
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
            method: 'POST',
            body: data,
            headers: { Accept: 'application/json' }
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json();
    };

    const currentSession = () => {
        if (isSolo) return null;
        try {
            const value = JSON.parse(localStorage.getItem(sessionStorageKey) || 'null');
            return value?.token ? value : null;
        } catch {
            return null;
        }
    };

    const cardDefinition = id => ({ id, title: text.cards[id]?.[0] || `#${id}`, description: text.cards[id]?.[1] || '' });

    const makeCard = (card, usable, preview = false) => {
        const id = Number(card.id);
        const definition = cardDefinition(id);
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'word-rings-action-card';
        button.dataset.actionCardId = String(id);
        button.setAttribute('role', 'listitem');
        const passive = passiveCards.has(id);
        button.classList.toggle('is-passive', passive);
        button.disabled = !preview && (!usable || passive);
        const icon = createElement('span', 'word-rings-action-card-icon', icons[id] || '★');
        const name = createElement('strong', 'word-rings-action-card-name', definition.title);
        const description = createElement('span', 'word-rings-action-card-description', definition.description);
        button.append(icon, name, description);
        if (passive) button.append(createElement('span', 'word-rings-action-card-temporary', text.passive));
        if (!preview && !passive) button.addEventListener('click', () => openCardDialog(card));
        return button;
    };

    const renderCards = () => {
        const enabled = isSolo ? soloSettings.enabled : multiplayerSnapshot?.enabled === true;
        shell.hidden = !enabled;
        if (!enabled) {
            applyWordEffects();
            return;
        }

        const cards = isSolo
            ? soloHand.map(id => ({ id, isTemporary: false }))
            : (multiplayerSnapshot?.cards || []);
        const ownTurn = isSolo || multiplayerSnapshot?.isOwnTurn === true;
        const active = isSolo || multiplayerSnapshot?.active === true;
        const progressValue = isSolo ? soloCorrectProgress : (multiplayerSnapshot?.correctProgress || 0);
        const kps = isSolo ? soloSettings.kps : (multiplayerSnapshot?.correctWordsPerCard || 3);
        const max = isSolo ? soloSettings.max : (multiplayerSnapshot?.maximumCards || 3);
        shellMeta.textContent = active
            ? format(text.progress, progressValue, kps, cards.length, max)
            : text.waiting;
        shell.classList.toggle('is-inactive', !active);
        carousel.replaceChildren();
        for (const card of cards) {
            const id = Number(card.id);
            const usable = active && !passiveCards.has(id) && (ownTurn || outOfTurn.has(id));
            carousel.append(makeCard(card, usable));
        }
        emptyMessage.hidden = cards.length > 0;
        emptyMessage.textContent = active ? text.noCards : text.waiting;
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
        if (passiveCards.has(id)) return;
        selectedCard = card;
        if (dialogPreview) {
            dialogPreview.replaceChildren();
            const preview = makeCard(card, true, true);
            preview.classList.add('is-dialog-preview');
            dialogPreview.append(preview);
        }
        if (dialogMessage) dialogMessage.textContent = randomTargetCards.has(id) ? text.randomOpponent : '';

        if (targetRow instanceof HTMLElement && targetSelect instanceof HTMLSelectElement) {
            const targets = cardTargets(id);
            targetRow.hidden = isSolo || !explicitTargetCards.has(id);
            targetSelect.replaceChildren();
            for (const player of targets) {
                const option = document.createElement('option');
                option.value = player.id;
                option.textContent = player.name;
                option.dataset.isSelf = player.isSelf === true ? 'true' : 'false';
                targetSelect.append(option);
            }
        }
        populateWordSelect(id);
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
        if (id === 6) setSoloHint();
    };

    confirmButton?.addEventListener('click', async () => {
        if (!selectedCard || multiplayerUseInFlight) return;
        const id = Number(selectedCard.id);
        if (passiveCards.has(id)) return;
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
        if (confirmButton instanceof HTMLButtonElement) confirmButton.disabled = true;
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
            if (confirmButton instanceof HTMLButtonElement) confirmButton.disabled = false;
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
            ? { blockedWords: [], hintWord: soloHintWord, masked: false, anagrammed: false, isOwnTurn: true }
            : multiplayerSnapshot;
        if (!snapshot) return;
        const blocked = new Set((snapshot.blockedWords || []).map(word => String(word).toLocaleLowerCase()));
        const hint = String(snapshot.hintWord || '').toLocaleLowerCase();
        root.querySelectorAll('.word-rings-word[data-word]').forEach(token => {
            if (!(token instanceof HTMLButtonElement)) return;
            if (token.dataset.seedExample === 'true' || token.dataset.roomSeed === 'true') return;
            const original = token.dataset.word || '';
            const isBlocked = blocked.has(original.toLocaleLowerCase());
            token.classList.toggle('is-action-blocked', isBlocked);
            token.classList.toggle('is-action-hint', Boolean(hint) && original.toLocaleLowerCase() === hint);
            token.classList.toggle('is-action-temporary-word', isSolo && soloTemporaryWord === original);
            if (isBlocked) {
                token.dataset.actionBlockedManaged = 'true';
                if (!token.disabled) token.disabled = true;
            } else if (token.dataset.actionBlockedManaged === 'true') {
                delete token.dataset.actionBlockedManaged;
                if (isSolo || (snapshot.isOwnTurn === true && !root.classList.contains('is-awaiting-host-judgement'))) {
                    token.disabled = false;
                }
            }
            if (snapshot.masked === true) token.textContent = maskWord(original);
            else if (snapshot.anagrammed === true) token.textContent = anagramWord(original);
            else if (token.textContent !== original) token.textContent = original;
        });
    };

    const saveSoloSettings = () => {
        soloSettings = {
            enabled: enableInput.checked,
            kps: Math.max(1, Math.min(20, Number.parseInt(kpsInput.value || '3', 10) || 3)),
            max: Math.max(2, Math.min(4, Number.parseInt(maxSelect.value || '3', 10) || 3))
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
            removeSoloTemporaryWord();
        }
        if (soloCorrectProgress >= soloSettings.kps) soloCorrectProgress = 0;
        renderCards();
    };
    enableInput.addEventListener('change', saveSoloSettings);
    kpsInput.addEventListener('change', saveSoloSettings);
    maxSelect.addEventListener('change', saveSoloSettings);
    saveSoloSettings();

    const findSoloPending = () => {
        const candidates = [
            ...root.querySelectorAll('[data-placed-layer] .word-rings-word[data-word], [data-outside-list] .word-rings-word[data-word]')
        ].filter(token => !token.classList.contains('is-correct') &&
            !token.classList.contains('is-wrong') &&
            !token.classList.contains('is-seed-example'));
        const token = candidates[candidates.length - 1];
        if (!(token instanceof HTMLElement)) return null;
        const word = token.dataset.word || '';
        const actual = [...String(token.dataset.membership || '')].sort().join('');
        const expected = [...String(soloExpected.get(word) || '')].sort().join('');
        return { word, actual, expected, correct: actual === expected };
    };

    const consumeSoloImmunity = () => {
        const index = soloHand.indexOf(5);
        if (index < 0) return false;
        soloHand.splice(index, 1);
        return true;
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
            if (!soloAttempt.correct && consumeSoloImmunity()) {
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
        soloAttempt = null;
        renderCards();
    });

    const observer = new MutationObserver(() => applyWordEffects());
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
        window.setInterval(pollMultiplayer, 900);
    } else {
        renderCards();
    }
})();
