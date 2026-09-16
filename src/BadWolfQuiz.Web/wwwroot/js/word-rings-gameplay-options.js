(() => {
    const root = document.querySelector('[data-word-rings-root]');
    if (!(root instanceof HTMLElement)) return;

    const isSolo = (root.dataset.gameMode || 'solo').toLowerCase() === 'solo';
    const roomCode = String(root.dataset.roomCode || '').trim().toUpperCase();
    const roomApiUrl = root.dataset.roomApiUrl || '/WordRingsRoomApi';
    const gameplayApiUrl = '/minigames/word-rings-gameplay-options-api';
    const actionPatchApiPath = '/minigames/word-rings-action-cards-patch-api';
    const antiForgery = root.querySelector('[data-word-rings-antiforgery] input[name="__RequestVerificationToken"]');
    const wordList = root.querySelector('[data-word-list]');
    const progress = root.querySelector('[data-progress]');
    const status = root.querySelector('[data-status]');
    const playersList = root.querySelector('[data-word-rings-player-list]');
    const nativeFetch = window.fetch.bind(window);
    const language = (document.documentElement.lang || 'en').toLowerCase().split('-')[0];
    const text = ({
        en: { hand: 'Words in hand', target: 'Play to', settings: 'Game settings', game: 'Game' },
        uk: { hand: 'Слів на руках', target: 'Грати до', settings: 'Налаштування гри', game: 'Гра' },
        it: { hand: 'Parole in mano', target: 'Gioca fino a', settings: 'Impostazioni di gioco', game: 'Gioco' }
    })[language] || { hand: 'Words in hand', target: 'Play to', settings: 'Game settings', game: 'Game' };

    const soloStorageKey = 'badwolf.wordrings.gameplay-options';
    const roomStorageKey = `badwolf.wordrings.room.${roomCode}`;
    const firstTurnRouletteStorageKey = `badwolf.wordrings.first-turn-roulette.${roomCode}`;
    let currentOptions = null;
    let lastRoomState = null;
    let lastObservedRoomPhase = null;
    let rebalancing = false;
    let rouletteRunning = false;
    let rouletteAudio = null;

    const clampTarget = value => Math.max(5, Math.min(20, Number.parseInt(value, 10) || 5));
    const clampHand = (value, target) => Math.max(5, Math.min(10, target, Number.parseInt(value, 10) || 5));

    const readSoloOptions = () => {
        try {
            const parsed = JSON.parse(localStorage.getItem(soloStorageKey) || 'null');
            const targetScore = clampTarget(parsed?.targetScore ?? 5);
            return { targetScore, handSize: clampHand(parsed?.handSize ?? 5, targetScore) };
        } catch {
            return { targetScore: 5, handSize: 5 };
        }
    };

    const writeSoloOptions = value => {
        try { localStorage.setItem(soloStorageKey, JSON.stringify(value)); } catch { }
    };

    const currentToken = () => {
        if (!roomCode) return '';
        try { return String(JSON.parse(localStorage.getItem(roomStorageKey) || 'null')?.token || ''); }
        catch { return ''; }
    };

    const firstTurnRouletteDone = () => {
        if (!roomCode) return false;
        try { return sessionStorage.getItem(firstTurnRouletteStorageKey) === 'done'; }
        catch { return false; }
    };

    const markFirstTurnRouletteDone = () => {
        if (!roomCode) return;
        try { sessionStorage.setItem(firstTurnRouletteStorageKey, 'done'); } catch { }
    };

    const resetFirstTurnRoulette = () => {
        if (!roomCode) return;
        try { sessionStorage.removeItem(firstTurnRouletteStorageKey); } catch { }
    };

    const formValue = (init, key) => init?.body instanceof FormData ? init.body.get(key) : null;

    const gameplayPost = async (handler, fields = {}) => {
        const data = new FormData();
        if (antiForgery instanceof HTMLInputElement) data.set(antiForgery.name, antiForgery.value);
        Object.entries(fields).forEach(([key, value]) => {
            if (value !== null && value !== undefined && value !== '') data.set(key, String(value));
        });
        const response = await nativeFetch(`${gameplayApiUrl}?handler=${encodeURIComponent(handler)}`, {
            method: 'POST', body: data, headers: { Accept: 'application/json' }
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json();
    };

    const syntheticJsonResponse = (payload, sourceResponse = null) => new Response(JSON.stringify(payload), {
        status: sourceResponse?.status || 200,
        statusText: sourceResponse?.statusText || 'OK',
        headers: { 'Content-Type': 'application/json; charset=utf-8' }
    });

    const handlerFromUrl = (url, expectedPath) => {
        try {
            const parsed = new URL(url, window.location.href);
            if (!parsed.pathname.endsWith(expectedPath)) return '';
            return parsed.searchParams.get('handler') || '';
        } catch {
            return '';
        }
    };

    const effectiveHandSize = (options, targetScore, score) => {
        const remaining = Number(targetScore || options?.targetScore || 5) - Number(score || 0);
        if (remaining <= 0) return 0;
        return Math.min(Number(options?.handSize || 5), Math.max(1, Math.ceil(remaining)));
    };

    const pendingHandDeficit = () =>
        root.classList.contains('has-pending-word') || root.classList.contains('is-awaiting-host-judgement') ? 1 : 0;

    const statePendingHandDeficit = state => {
        const hasOwnServerPending = Array.isArray(state?.placements) && state.placements.some(placement =>
            placement?.isPending === true && placement?.playerId === state?.playerId);
        return hasOwnServerPending || root.classList.contains('has-pending-word') ? 1 : 0;
    };

    const decorateRoomState = (state, options = currentOptions) => {
        if (!state || !Array.isArray(state.bankWords) || !Array.isArray(state.queuedWords) || !options) return state;
        const allWords = [...state.bankWords, ...state.queuedWords];
        const baseLimit = state.dedicatedHostMode === true && state.isHost === true
            ? Number(options.handSize || 5)
            : effectiveHandSize(options, state.targetScore, state.playerScore);
        const deficit = state.dedicatedHostMode === true && state.isHost === true ? 0 : statePendingHandDeficit(state);
        const limit = Math.min(Math.max(0, baseLimit - deficit), allWords.length);
        state.bankWords = allWords.slice(0, limit);
        state.queuedWords = allWords.slice(limit);
        return state;
    };

    const decorateHostState = (state, options = currentOptions) => {
        if (state && options && Object.prototype.hasOwnProperty.call(state, 'handLimit')) {
            state.handLimit = options.handSize;
        }
        return state;
    };

    const ensureOptions = async token => {
        if (currentOptions) return currentOptions;
        if (!roomCode || !token) return null;
        const payload = await gameplayPost('Options', { roomCode, playerToken: token });
        if (payload?.success && payload.options) currentOptions = payload.options;
        return currentOptions;
    };

    const bootstrapSolo = () => {
        if (!isSolo) return;
        const options = readSoloOptions();
        currentOptions = options;
        root.dataset.bankWordLimit = String(options.handSize);
        root.dataset.correctWordTarget = String(options.targetScore);

        const queueConfig = root.querySelector('[data-word-rings-queue]');
        if (!(wordList instanceof HTMLElement) || !(queueConfig instanceof HTMLElement)) return;
        const initial = [...wordList.querySelectorAll(':scope > .word-rings-word[data-word]')];
        if (initial.length <= options.handSize) return;
        let queue = [];
        try {
            const parsed = JSON.parse(queueConfig.textContent || '[]');
            if (Array.isArray(parsed)) queue = parsed;
        } catch { }
        const overflow = initial.slice(options.handSize);
        const words = overflow.map(token => token.dataset.word).filter(Boolean);
        overflow.forEach(token => token.remove());
        queueConfig.textContent = JSON.stringify([...words, ...queue]);
    };

    bootstrapSolo();

    const enhanceCreateDialog = () => {
        const targetInput = root.querySelector('[data-create-room-target]');
        if (!(targetInput instanceof HTMLInputElement)) return;
        targetInput.min = '5';
        targetInput.max = '20';
        targetInput.value = '5';
        if (root.querySelector('[data-create-room-hand-size]')) return;

        const handInput = document.createElement('input');
        handInput.type = 'number';
        handInput.min = '5';
        handInput.max = '5';
        handInput.value = '5';
        handInput.required = true;
        handInput.dataset.createRoomHandSize = 'true';
        const label = document.createElement('label');
        const caption = document.createElement('span');
        caption.textContent = text.hand;
        label.append(caption, handInput);

        const targetLabel = targetInput.closest('label');
        const timerLabel = targetLabel?.nextElementSibling;
        if (timerLabel?.parentElement) timerLabel.parentElement.insertBefore(label, timerLabel);
        else targetLabel?.parentElement?.append(label);

        const sync = () => {
            const target = clampTarget(targetInput.value);
            targetInput.value = String(target);
            handInput.max = String(Math.min(10, target));
            handInput.value = String(clampHand(handInput.value, target));
        };
        targetInput.addEventListener('input', sync);
        handInput.addEventListener('input', sync);
        sync();
    };

    enhanceCreateDialog();

    const enhanceSoloSettings = () => {
        if (!isSolo) return;
        const actionConfig = root.querySelector('.word-rings-action-config');
        if (!(actionConfig instanceof HTMLElement) || root.querySelector('[data-solo-gameplay-options]')) return;
        const dialog = actionConfig.closest('dialog');
        const container = actionConfig.parentElement;
        if (!(dialog instanceof HTMLDialogElement) || !(container instanceof HTMLElement)) return;

        const options = readSoloOptions();
        const panel = document.createElement('section');
        panel.className = 'word-rings-gameplay-options';
        panel.dataset.soloGameplayOptions = 'true';
        const heading = document.createElement('strong');
        heading.textContent = text.game;
        const fields = document.createElement('div');
        fields.className = 'word-rings-gameplay-option-fields';

        const target = document.createElement('input');
        target.type = 'number'; target.min = '5'; target.max = '20'; target.value = String(options.targetScore);
        const targetLabel = document.createElement('label');
        const targetCaption = document.createElement('span'); targetCaption.textContent = text.target;
        targetLabel.append(targetCaption, target);

        const hand = document.createElement('input');
        hand.type = 'number'; hand.min = '5'; hand.max = String(Math.min(10, options.targetScore)); hand.value = String(options.handSize);
        const handLabel = document.createElement('label');
        const handCaption = document.createElement('span'); handCaption.textContent = text.hand;
        handLabel.append(handCaption, hand);
        fields.append(targetLabel, handLabel);
        panel.append(heading, fields);
        container.insertBefore(panel, actionConfig);

        const title = dialog.querySelector('h2');
        if (title) title.textContent = text.settings;
        const settingsButton = root.querySelector('.word-rings-action-settings-button');
        if (settingsButton instanceof HTMLElement) {
            settingsButton.title = text.settings;
            settingsButton.setAttribute('aria-label', text.settings);
        }

        let dirty = false;
        const save = () => {
            const targetScore = clampTarget(target.value);
            const handSize = clampHand(hand.value, targetScore);
            target.value = String(targetScore);
            hand.max = String(Math.min(10, targetScore));
            hand.value = String(handSize);
            writeSoloOptions({ targetScore, handSize });
            dirty = true;
        };
        target.addEventListener('input', save);
        hand.addEventListener('input', save);
        dialog.addEventListener('close', () => {
            if (dirty) window.location.reload();
        });
    };

    new MutationObserver(() => {
        enhanceCreateDialog();
        enhanceSoloSettings();
    }).observe(root, { childList: true, subtree: true });
    enhanceSoloSettings();

    const parseProgress = () => {
        const values = String(progress?.textContent || '')
            .match(/\d+(?:[.,]\d+)?/g)
            ?.map(value => Number(value.replace(',', '.'))) || [];
        return {
            correct: values[0] ?? 0,
            target: values[1] ?? Number(currentOptions?.targetScore || 5),
            attempts: values[2] ?? 0,
            maximumAttempts: values[3] ?? 0
        };
    };

    const rebalanceWordList = (allowed, cooperative = false) => {
        if (!(wordList instanceof HTMLElement) || rebalancing) return;
        rebalancing = true;
        try {
            const candidates = [...wordList.querySelectorAll(':scope > .word-rings-word[data-word]')]
                .filter(token => !token.classList.contains('is-placed') &&
                    (!token.hidden || token.dataset.gameplayHandOverflow === 'true'));
            candidates.forEach((token, index) => {
                const shouldShow = index < allowed;
                if (shouldShow && token.dataset.gameplayHandOverflow === 'true') {
                    token.hidden = false;
                    delete token.dataset.gameplayHandOverflow;
                } else if (!shouldShow && !token.hidden) {
                    token.hidden = true;
                    token.dataset.gameplayHandOverflow = 'true';
                }
            });
            if (cooperative) root.dispatchEvent(new CustomEvent('wordrings:gameplay-hand-rebalanced'));
        } finally {
            rebalancing = false;
        }
    };

    const rebalanceSoloHand = () => {
        if (!isSolo || !currentOptions) return;
        const values = parseProgress();
        const allowed = Math.max(0,
            effectiveHandSize(currentOptions, values.target, values.correct) -
            (root.classList.contains('has-pending-word') ? 1 : 0));
        rebalanceWordList(allowed);
    };

    const rebalanceCooperativeHand = () => {
        if (isSolo || !currentOptions || !lastRoomState) return;
        const allowed = lastRoomState.dedicatedHostMode === true && lastRoomState.isHost === true
            ? currentOptions.handSize
            : Math.max(0,
                effectiveHandSize(currentOptions, lastRoomState.targetScore, lastRoomState.playerScore) - pendingHandDeficit());
        rebalanceWordList(allowed, true);
    };

    const format = (template, ...values) => values.reduce(
        (result, value, index) => String(result || '').replace(`{${index}}`, String(value)), template || '');

    const overrideSoloExhaustionResult = () => {
        if (!isSolo || !root.classList.contains('is-game-over') || root.dataset.gameplayExhaustionWin === 'true') return;
        const values = parseProgress();
        if (values.maximumAttempts <= 0 || values.attempts < values.maximumAttempts || values.correct >= values.target) return;
        root.dataset.gameplayExhaustionWin = 'true';
        if (status instanceof HTMLElement) {
            status.classList.remove('is-error');
            status.classList.add('is-success');
        }
        root.dispatchEvent(new CustomEvent('wordrings:game-ended', {
            detail: {
                won: true,
                title: root.dataset.winTitle || '',
                message: format(root.dataset.soloWinTemplate, values.correct, values.target)
            }
        }));
    };

    if (progress instanceof HTMLElement) {
        new MutationObserver(() => {
            queueMicrotask(rebalanceSoloHand);
            queueMicrotask(overrideSoloExhaustionResult);
        }).observe(progress, { childList: true, characterData: true, subtree: true });
    }
    new MutationObserver(() => {
        queueMicrotask(isSolo ? rebalanceSoloHand : rebalanceCooperativeHand);
        queueMicrotask(overrideSoloExhaustionResult);
    }).observe(root, { attributes: true, attributeFilter: ['class'] });
    if (wordList instanceof HTMLElement) {
        new MutationObserver(() => queueMicrotask(isSolo ? rebalanceSoloHand : rebalanceCooperativeHand))
            .observe(wordList, { childList: true, subtree: true, attributes: true, attributeFilter: ['hidden', 'class'] });
    }

    const soundEnabled = () => {
        try { return localStorage.getItem('badwolf.wordrings.sound-enabled') !== 'false'; }
        catch { return true; }
    };

    const unlockRouletteAudio = () => {
        if (!soundEnabled()) return;
        const AudioContextType = window.AudioContext || window.webkitAudioContext;
        if (!AudioContextType) return;
        try {
            rouletteAudio ||= new AudioContextType();
            if (rouletteAudio.state === 'suspended') void rouletteAudio.resume().catch(() => {});
        } catch { }
    };
    root.addEventListener('pointerdown', unlockRouletteAudio, { capture: true });
    root.addEventListener('keydown', unlockRouletteAudio, { capture: true });

    const rouletteTick = index => {
        if (!soundEnabled()) return;
        unlockRouletteAudio();
        if (!rouletteAudio || rouletteAudio.state !== 'running') return;
        const oscillator = rouletteAudio.createOscillator();
        const gain = rouletteAudio.createGain();
        const now = rouletteAudio.currentTime;
        oscillator.type = 'square';
        oscillator.frequency.value = 520 + (index % 5) * 55;
        gain.gain.setValueAtTime(0.0001, now);
        gain.gain.exponentialRampToValueAtTime(0.07, now + 0.004);
        gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.055);
        oscillator.connect(gain); gain.connect(rouletteAudio.destination);
        oscillator.start(now); oscillator.stop(now + 0.06);
    };

    const playerCards = () => playersList instanceof HTMLElement
        ? [...playersList.querySelectorAll(':scope > .word-rings-player-card')]
        : [];

    const maybeStartFirstTurnRoulette = state => {
        if (isSolo || rouletteRunning || firstTurnRouletteDone() || !state || state.phase !== 'playing' || !state.currentPlayerId || state.seedSetupPending === true) return;
        const participants = (state.players || [])
            .map((player, index) => ({ player, index }))
            .filter(item => !(state.dedicatedHostMode === true && item.player.isHost === true));
        if (participants.length === 0 || participants.some(item => Number(item.player.score || 0) !== 0)) return;
        const played = (state.placements || []).some(item => item.isSeed !== true);
        if (played) {
            markFirstTurnRouletteDone();
            return;
        }
        const selected = participants.findIndex(item => item.player.id === state.currentPlayerId);
        if (selected < 0) return;
        markFirstTurnRouletteDone();

        window.setTimeout(async () => {
            const cards = playerCards();
            if (cards.length === 0) return;
            rouletteRunning = true;
            let steps = Math.max(16, participants.length * 5);
            while ((steps - 1) % participants.length !== selected) steps++;
            try {
                for (let step = 0; step < steps; step++) {
                    const currentCards = playerCards();
                    currentCards.forEach(card => card.classList.remove('is-first-turn-roulette', 'is-first-turn-selected'));
                    const participant = participants[step % participants.length];
                    const card = currentCards[participant.index];
                    if (card instanceof HTMLElement) {
                        card.classList.add('is-first-turn-roulette');
                        rouletteTick(step);
                    }
                    const progressValue = steps <= 1 ? 1 : step / (steps - 1);
                    const delay = 65 + 190 * Math.pow(Math.abs(progressValue * 2 - 1), 1.55);
                    await new Promise(resolve => window.setTimeout(resolve, delay));
                }
                const currentCards = playerCards();
                currentCards.forEach(card => card.classList.remove('is-first-turn-roulette', 'is-first-turn-selected'));
                const finalCard = currentCards[participants[selected].index];
                if (finalCard instanceof HTMLElement) {
                    finalCard.classList.add('is-first-turn-selected');
                    rouletteTick(selected + 7);
                    window.setTimeout(() => finalCard.classList.remove('is-first-turn-selected'), 1100);
                }
            } finally {
                rouletteRunning = false;
            }
        }, 0);
    };

    const processRoomState = state => {
        if (!state) return state;
        if (state.phase === 'waiting' || (lastObservedRoomPhase === 'finished' && state.phase === 'playing')) {
            resetFirstTurnRoulette();
        }
        lastObservedRoomPhase = state.phase;
        lastRoomState = decorateRoomState(state);
        window.setTimeout(() => {
            rebalanceCooperativeHand();
            maybeStartFirstTurnRoulette(lastRoomState);
        }, 0);
        return lastRoomState;
    };

    window.fetch = async (input, init = {}) => {
        const url = typeof input === 'string' ? input : String(input?.url || '');
        const roomHandler = handlerFromUrl(url, new URL(roomApiUrl, window.location.href).pathname);
        const patchHandler = handlerFromUrl(url, actionPatchApiPath);

        if (patchHandler === 'EnsureTargetWords') {
            const token = String(formValue(init, 'playerToken') || currentToken());
            const code = String(formValue(init, 'roomCode') || roomCode);
            const payload = await gameplayPost('CurrentState', { roomCode: code, playerToken: token });
            if (payload?.success && payload.state) {
                await ensureOptions(token);
                payload.state = processRoomState(payload.state);
            }
            return syntheticJsonResponse(payload);
        }

        if (['UseActionCard', 'RecordSubmissionLifecycle', 'RecordHostedResolutionLifecycle', 'ResolveImmunityDecision'].includes(patchHandler)) {
            const token = String(formValue(init, 'playerToken') || currentToken());
            const code = String(formValue(init, 'roomCode') || roomCode);
            let captureId = '';
            try {
                const captured = await gameplayPost('CaptureWordCounts', { roomCode: code, playerToken: token });
                captureId = captured?.capture?.id || '';
            } catch { }
            const response = await nativeFetch(input, init);
            let payload = null;
            try { payload = await response.clone().json(); } catch { }
            if (captureId) {
                try {
                    await gameplayPost('RestoreWordCounts', {
                        roomCode: code,
                        playerToken: token,
                        captureId,
                        allowCallerReturnedWord: patchHandler === 'ResolveImmunityDecision' && String(formValue(init, 'returnWord')).toLowerCase() === 'true'
                    });
                } catch { }
            }
            if (payload?.success && patchHandler === 'UseActionCard' && payload.result) {
                try {
                    const normalized = await gameplayPost('NormalizeActionCard', {
                        roomCode: code,
                        playerToken: token,
                        cardId: Number(formValue(init, 'cardId') || 0),
                        targetName: payload.result.targetName || ''
                    });
                    if (normalized?.success && normalized.actionCards) payload.result.state = normalized.actionCards;
                } catch { }
                return syntheticJsonResponse(payload, response);
            }
            if (payload?.success && patchHandler === 'ResolveImmunityDecision') {
                try {
                    let current = await gameplayPost('CurrentState', { roomCode: code, playerToken: token });
                    if (String(formValue(init, 'returnWord')).toLowerCase() !== 'true' && current?.success && current.state) {
                        const finalized = await gameplayPost('FinalizePlayerExhaustion', {
                            roomCode: code,
                            playerToken: token,
                            playerId: current.state.playerId
                        });
                        if (finalized?.success && finalized.state) current = finalized;
                    }
                    if (current?.success && current.state) {
                        await ensureOptions(token);
                        payload.state = processRoomState(current.state);
                    }
                } catch { }
                return syntheticJsonResponse(payload, response);
            }
            return response;
        }

        if (roomHandler === 'CreateRoom') {
            const requestedTarget = clampTarget(root.querySelector('[data-create-room-target]')?.value || formValue(init, 'targetScore') || 5);
            const requestedHand = clampHand(root.querySelector('[data-create-room-hand-size]')?.value || 5, requestedTarget);
            const response = await nativeFetch(input, init);
            try {
                const payload = await response.clone().json();
                const connection = payload?.connection;
                if (payload?.success && connection?.roomCode && connection?.playerToken) {
                    const configured = await gameplayPost('ConfigureRoom', {
                        roomCode: connection.roomCode,
                        playerToken: connection.playerToken,
                        targetScore: requestedTarget,
                        handSize: requestedHand
                    });
                    if (configured?.success && configured.options) currentOptions = configured.options;
                    if (connection.state) connection.state = decorateRoomState(connection.state, currentOptions);
                    return syntheticJsonResponse(payload, response);
                }
            } catch { }
            return response;
        }

        if (roomHandler === 'JoinRoom') {
            const response = await nativeFetch(input, init);
            try {
                const payload = await response.clone().json();
                const connection = payload?.connection;
                if (payload?.success && connection?.playerToken) {
                    const optionsPayload = await gameplayPost('Options', {
                        roomCode: connection.roomCode || roomCode,
                        playerToken: connection.playerToken
                    });
                    if (optionsPayload?.success) currentOptions = optionsPayload.options;
                    if (connection.state) connection.state = decorateRoomState(connection.state, currentOptions);
                    return syntheticJsonResponse(payload, response);
                }
            } catch { }
            return response;
        }

        if (roomHandler === 'StartRoom') {
            resetFirstTurnRoulette();
            const response = await nativeFetch(input, init);
            try {
                const payload = await response.clone().json();
                if (payload?.success) {
                    const token = String(formValue(init, 'playerToken') || currentToken());
                    await ensureOptions(token);
                    const prepared = await gameplayPost('PrepareRound', {
                        roomCode: formValue(init, 'roomCode') || roomCode,
                        playerToken: token
                    });
                    if (prepared?.success && prepared.state) payload.state = processRoomState(prepared.state);
                    return syntheticJsonResponse(payload, response);
                }
            } catch { }
            return response;
        }

        if (roomHandler === 'ConfirmRoomSeeds') {
            const response = await nativeFetch(input, init);
            try {
                const payload = await response.clone().json();
                if (payload?.success) {
                    const token = String(formValue(init, 'playerToken') || currentToken());
                    await ensureOptions(token);
                    const selected = await gameplayPost('SelectFirstPlayer', {
                        roomCode: formValue(init, 'roomCode') || roomCode,
                        playerToken: token
                    });
                    if (selected?.success && selected.state) payload.state = decorateHostState(selected.state);
                    return syntheticJsonResponse(payload, response);
                }
            } catch { }
            return response;
        }

        if (roomHandler === 'RoomState') {
            const response = await nativeFetch(input, init);
            try {
                const payload = await response.clone().json();
                if (payload?.success && payload.state) {
                    const token = String(formValue(init, 'playerToken') || currentToken());
                    await ensureOptions(token);
                    payload.state = processRoomState(payload.state);
                    return syntheticJsonResponse(payload, response);
                }
            } catch { }
            return response;
        }

        if (roomHandler === 'SubmitRoomWord') {
            const response = await nativeFetch(input, init);
            try {
                const payload = await response.clone().json();
                if (payload?.success && payload.result?.state) {
                    const token = String(formValue(init, 'playerToken') || currentToken());
                    await ensureOptions(token);
                    if (payload.result.isPending !== true) {
                        const finalized = await gameplayPost('FinalizePlayerExhaustion', {
                            roomCode: formValue(init, 'roomCode') || roomCode,
                            playerToken: token,
                            playerId: payload.result.state.playerId
                        });
                        if (finalized?.success && finalized.state) payload.result.state = finalized.state;
                    }
                    payload.result.state = processRoomState(payload.result.state);
                    return syntheticJsonResponse(payload, response);
                }
            } catch { }
            return response;
        }

        if (roomHandler === 'ResolveRoomPlacement') {
            const response = await nativeFetch(input, init);
            try {
                const payload = await response.clone().json();
                if (payload?.success) {
                    const token = String(formValue(init, 'playerToken') || currentToken());
                    await ensureOptions(token);
                    const finalized = await gameplayPost('FinalizePlacementExhaustion', {
                        roomCode: formValue(init, 'roomCode') || roomCode,
                        playerToken: token,
                        placementId: formValue(init, 'placementId')
                    });
                    if (finalized?.success && finalized.state) payload.state = decorateHostState(finalized.state);
                    return syntheticJsonResponse(payload, response);
                }
            } catch { }
            return response;
        }

        const response = await nativeFetch(input, init);
        if (!roomHandler) return response;
        try {
            const payload = await response.clone().json();
            const token = String(formValue(init, 'playerToken') || currentToken());
            await ensureOptions(token);
            if (payload?.state) {
                if (Array.isArray(payload.state.bankWords)) payload.state = processRoomState(payload.state);
                else payload.state = decorateHostState(payload.state);
                return syntheticJsonResponse(payload, response);
            }
        } catch { }
        return response;
    };

    rebalanceSoloHand();
})();