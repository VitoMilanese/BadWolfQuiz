(() => {
    const root = document.querySelector('[data-word-rings-root][data-game-mode="cooperative"]');
    if (!root) return;

    const stage = root.querySelector('[data-ring-stage]');
    const placedLayer = root.querySelector('[data-placed-layer]');
    const wordList = root.querySelector('[data-word-list]');
    const outsideZone = root.querySelector('[data-outside-zone]');
    const progress = root.querySelector('[data-progress]');
    const status = root.querySelector('[data-status]');
    const rules = root.querySelector('[data-rules]');
    const revealButton = root.querySelector('[data-reveal-rules]');
    const checkButton = root.querySelector('[data-check]');
    const playersList = root.querySelector('[data-word-rings-player-list]');
    const startButton = root.querySelector('[data-start-room]');
    const copyButton = root.querySelector('[data-copy-room-link]');
    const joinDialog = root.querySelector('[data-join-room-dialog]');
    const joinForm = root.querySelector('[data-join-room-form]');
    const joinName = root.querySelector('[data-join-room-name]');
    const joinError = root.querySelector('[data-join-room-error]');
    const antiForgery = root.querySelector('[data-word-rings-antiforgery] input[name="__RequestVerificationToken"]');
    const roomCode = String(root.dataset.roomCode || '').trim().toUpperCase();
    const apiUrl = root.dataset.roomApiUrl;
    const nicknameKey = 'badwolf.wordrings.nickname';
    const storageKey = `badwolf.wordrings.room.${roomCode}`;
    const maximumBankWords = Number.parseInt(root.dataset.bankWordLimit || '10', 10) || 10;

    if (!stage || !placedLayer || !wordList || !progress || !checkButton || !roomCode || !apiUrl) return;

    const ringElements = [
        ['A', root.querySelector('.word-rings-circle-a')],
        ['B', root.querySelector('.word-rings-circle-b')],
        ['C', root.querySelector('.word-rings-circle-c')]
    ];

    let session = null;
    let state = null;
    let pending = null;
    let localQueuedWords = [];
    let wireWord = null;
    let requestInFlight = false;
    let polling = false;
    let resultShown = false;
    let topZIndex = 100;

    const format = (template, ...values) => values.reduce(
        (result, value, index) => result.replace(`{${index}}`, String(value)),
        template || '');
    const canonical = value => [...String(value || '')]
        .filter(ch => ch === 'A' || ch === 'B' || ch === 'C')
        .sort()
        .join('');
    const formatScore = value => {
        const number = Number(value || 0);
        return Number.isInteger(number) ? String(number) : number.toFixed(1).replace('.', ',');
    };

    const setStatus = (message, kind = '') => {
        if (!status) return;
        status.textContent = message || '';
        status.classList.toggle('is-success', kind === 'success');
        status.classList.toggle('is-error', kind === 'error');
        status.classList.toggle('is-partial', kind === 'partial');
    };

    const setJoinError = message => {
        if (joinError) joinError.textContent = message || '';
    };

    const loadSession = () => {
        try {
            const parsed = JSON.parse(localStorage.getItem(storageKey) || 'null');
            if (parsed?.token) return parsed;
        } catch {
            localStorage.removeItem(storageKey);
        }
        return null;
    };

    const saveSession = connection => {
        const name = connection?.state?.players?.find(player => player.id === connection?.state?.playerId)?.name
            || joinName?.value?.trim()
            || localStorage.getItem(nicknameKey)
            || '';
        session = { token: connection.playerToken, name };
        localStorage.setItem(storageKey, JSON.stringify(session));
        if (name) localStorage.setItem(nicknameKey, name);
    };

    const clearSession = () => {
        localStorage.removeItem(storageKey);
        session = null;
    };

    const post = async (handler, fields = {}) => {
        const data = new FormData();
        if (antiForgery instanceof HTMLInputElement) {
            data.set('__RequestVerificationToken', antiForgery.value);
        }
        Object.entries(fields).forEach(([key, value]) => data.set(key, String(value)));
        const response = await fetch(`${apiUrl}?handler=${encodeURIComponent(handler)}`, {
            method: 'POST',
            body: data,
            headers: { 'Accept': 'application/json' }
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json();
    };

    const currentPlayer = () => state?.players?.find(player => player.id === state.currentPlayerId) || null;
    const ownPlayer = () => state?.players?.find(player => player.id === state.playerId) || null;
    const isOwnTurn = () => state?.phase === 'playing' && state.currentPlayerId === state.playerId;

    const bringToFront = token => {
        if (!(token instanceof HTMLElement)) return;
        topZIndex += 1;
        token.style.zIndex = String(topZIndex);
    };

    const pointIsInsideRing = (clientX, clientY, ring) => {
        if (!ring) return false;
        const rect = ring.getBoundingClientRect();
        const centerX = rect.left + rect.width / 2;
        const centerY = rect.top + rect.height / 2;
        return Math.hypot(clientX - centerX, clientY - centerY) <= Math.min(rect.width, rect.height) / 2;
    };

    const membershipAt = (clientX, clientY) => {
        const rect = stage.getBoundingClientRect();
        return {
            membership: canonical(ringElements
                .filter(([, ring]) => pointIsInsideRing(clientX, clientY, ring))
                .map(([name]) => name)
                .join('')),
            x: Math.max(3, Math.min(97, ((clientX - rect.left) / rect.width) * 100)),
            y: Math.max(3, Math.min(97, ((clientY - rect.top) / rect.height) * 100))
        };
    };

    const createBankWord = word => {
        if (!word || wordList.querySelector(`.word-rings-word[data-word="${CSS.escape(word)}"]`)) return null;
        const token = document.createElement('button');
        token.className = 'word-rings-word';
        token.type = 'button';
        token.dataset.word = word;
        token.textContent = word;
        token.draggable = false;
        wordList.append(token);
        wireWord?.(token);
        return token;
    };

    const visibleBankWords = () => [...wordList.querySelectorAll('.word-rings-word[data-word]')]
        .filter(token => !token.hidden);

    const replenishLocalBank = () => {
        while (visibleBankWords().length < maximumBankWords && localQueuedWords.length > 0) {
            createBankWord(localQueuedWords.shift());
        }
    };

    const trimLocalBank = returnedWord => {
        let visible = visibleBankWords();
        while (visible.length > maximumBankWords) {
            const removable = [...visible].reverse().find(item => item.dataset.word !== returnedWord);
            if (!(removable instanceof HTMLElement)) break;
            if (removable.dataset.word) localQueuedWords.unshift(removable.dataset.word);
            removable.remove();
            visible = visibleBankWords();
        }
    };

    const renderBank = nextState => {
        wordList.replaceChildren();
        for (const word of nextState.bankWords || []) createBankWord(word);
        localQueuedWords = [...(nextState.queuedWords || [])];
    };

    const removePendingToken = () => {
        placedLayer.querySelector('[data-room-pending-word]')?.remove();
    };

    const createPendingToken = (word, placement) => {
        const source = wordList.querySelector(`.word-rings-word[data-word="${CSS.escape(word)}"]`);
        if (!(source instanceof HTMLElement)) return;
        source.hidden = true;
        source.classList.add('is-placed');
        removePendingToken();
        const token = source.cloneNode(true);
        token.hidden = false;
        token.classList.remove('is-placed', 'is-correct', 'is-wrong', 'is-partial');
        token.classList.add('is-on-stage', 'is-room-pending');
        token.dataset.roomPendingWord = 'true';
        token.dataset.membership = placement.membership;
        token.style.left = `${placement.x}%`;
        token.style.top = `${placement.y}%`;
        token.draggable = false;
        placedLayer.append(token);
        bringToFront(token);
        wireWord?.(token);
    };

    const isServerPlacementToken = source =>
        source instanceof HTMLElement && Boolean(source.dataset.roomServerPlacement);

    const moveServerPlacement = async (source, placement) => {
        if (!isServerPlacementToken(source) || !session?.token || requestInFlight) return;
        const placementId = Number.parseInt(source.dataset.roomServerPlacement || '', 10);
        if (!Number.isFinite(placementId)) return;

        source.style.left = `${placement.x}%`;
        source.style.top = `${placement.y}%`;
        bringToFront(source);
        requestInFlight = true;
        updateControls();
        try {
            const payload = await post('MoveRoomPlacement', {
                roomCode,
                playerToken: session.token,
                placementId,
                membership: placement.membership,
                x: placement.x,
                y: placement.y
            });
            if (!payload.success) {
                setStatus(root.dataset.roomError, 'error');
                await requestState({ preservePending: pending !== null });
                return;
            }
            renderState(payload.state, { preservePending: pending !== null });
        } catch (error) {
            console.error('Could not move checked Word Rings placement.', error);
            setStatus(root.dataset.roomError, 'error');
            await requestState({ preservePending: pending !== null });
        } finally {
            requestInFlight = false;
            updateControls();
        }
    };

    const assignToStage = (word, clientX, clientY, dragSource) => {
        const placement = membershipAt(clientX, clientY);
        if (isServerPlacementToken(dragSource)) {
            void moveServerPlacement(dragSource, placement);
            return;
        }

        const source = wordList.querySelector(`.word-rings-word[data-word="${CSS.escape(word)}"]`);
        const movedFromBank = source instanceof HTMLElement && !source.hidden;
        pending = { word, ...placement };
        createPendingToken(word, placement);
        if (movedFromBank) replenishLocalBank();
        setStatus('');
        updateControls();
    };

    const assignOutside = (word, dragSource) => {
        const rect = stage.getBoundingClientRect();
        assignToStage(word, rect.left + 12, rect.bottom - 12, dragSource);
    };

    const returnToBank = (word, dragSource) => {
        if (isServerPlacementToken(dragSource)) return;
        if (!pending || pending.word !== word) return;
        removePendingToken();
        const source = wordList.querySelector(`.word-rings-word[data-word="${CSS.escape(word)}"]`);
        if (source instanceof HTMLElement) {
            source.hidden = false;
            source.classList.remove('is-placed');
        }
        trimLocalBank(word);
        pending = null;
        updateControls();
    };

    const canBegin = (word, source) => {
        if (isServerPlacementToken(source)) {
            return !requestInFlight && state?.phase === 'playing';
        }
        return !requestInFlight && isOwnTurn() &&
            (pending === null || pending.word === word);
    };
    const canDropStage = (word, membership, source) => {
        if (isServerPlacementToken(source)) {
            return canBegin(word, source) &&
                canonical(membership) === canonical(source.dataset.membership);
        }
        return canBegin(word, source);
    };
    const canDropOutside = (word, source) => {
        if (isServerPlacementToken(source)) {
            return canBegin(word, source) && canonical(source.dataset.membership) === '';
        }
        return canBegin(word, source);
    };
    const canReturnToBank = (word, source) =>
        !isServerPlacementToken(source) && isOwnTurn() && pending?.word === word;
    const onPlacementChanged = () => updateControls();

    if (typeof window.BadWolfWordRingsPointerDrag === 'function') {
        wireWord = window.BadWolfWordRingsPointerDrag({
            stage,
            wordList,
            outsideZone,
            membershipAt,
            assignToStage,
            assignOutside,
            returnToBank,
            canBegin,
            canDropStage,
            canDropOutside,
            canReturnToBank,
            bringToFront,
            onChanged: onPlacementChanged
        });
    }

    const renderPlacements = placements => {
        placedLayer.querySelectorAll('[data-room-server-placement]').forEach(token => token.remove());
        for (const placement of placements || []) {
            const token = document.createElement('button');
            token.type = 'button';
            token.disabled = false;
            token.className = 'word-rings-word is-on-stage is-room-server-placement';
            token.dataset.roomServerPlacement = String(placement.id);
            token.dataset.membership = canonical(placement.membership);
            token.style.left = `${placement.x}%`;
            token.style.top = `${placement.y}%`;
            token.textContent = placement.word;
            token.title = `${placement.playerName} · +${formatScore(placement.pointsAwarded)}`;
            token.classList.toggle('is-correct', placement.isCorrect === true);
            token.classList.toggle(
                'is-partial',
                placement.isCorrect !== true && placement.isPartial === true && Number(placement.pointsAwarded) > 0);
            token.classList.toggle(
                'is-wrong',
                placement.isCorrect !== true && !(placement.isPartial === true && Number(placement.pointsAwarded) > 0));
            placedLayer.append(token);
            wireWord?.(token);
        }
    };

    const renderPlayers = players => {
        if (!playersList) return;
        playersList.replaceChildren();
        for (const player of players || []) {
            const card = document.createElement('article');
            card.className = 'word-rings-player-card';
            card.classList.toggle('is-current', player.isCurrentTurn === true);
            card.classList.toggle('is-self', player.id === state?.playerId);

            const main = document.createElement('div');
            main.className = 'word-rings-player-main';
            const name = document.createElement('strong');
            name.textContent = player.name;
            main.append(name);
            if (player.isHost) {
                const badge = document.createElement('span');
                badge.className = 'word-rings-player-badge';
                badge.textContent = '★';
                main.append(badge);
            }

            const meta = document.createElement('div');
            meta.className = 'word-rings-player-meta';
            const score = document.createElement('span');
            score.textContent = `${formatScore(player.score)} pt`;
            const words = document.createElement('span');
            words.textContent = `${player.remainingWords}`;
            meta.append(score, words);
            card.append(main, meta);
            playersList.append(card);
        }
    };

    const renderRules = nextState => {
        const values = [nextState.blueRuleText, nextState.yellowRuleText, nextState.redRuleText];
        ['a', 'b', 'c'].forEach((ring, index) => {
            const target = rules?.querySelector(`.word-rings-rule-${ring} span`);
            if (target) target.textContent = values[index] || '';
        });
    };

    const showFinishedDialog = nextState => {
        if (resultShown || nextState.phase !== 'finished') return;
        resultShown = true;
        const won = nextState.outcome === 'won';
        const template = won ? root.dataset.coopWinTemplate : root.dataset.coopLoseTemplate;
        root.dispatchEvent(new CustomEvent('wordrings:game-ended', {
            detail: {
                won,
                title: won ? root.dataset.winTitle : root.dataset.loseTitle,
                message: format(template, formatScore(nextState.teamScore), nextState.targetScore)
            }
        }));
    };

    const updateControls = () => {
        const playing = state?.phase === 'playing';
        const ownTurn = isOwnTurn();
        checkButton.disabled = !playing || !ownTurn || pending === null || requestInFlight;
        root.classList.toggle('has-pending-word', pending !== null);
        root.classList.toggle('is-game-over', state?.phase === 'finished');
        root.classList.toggle('is-room-waiting', state?.phase === 'waiting');
        root.classList.toggle('is-not-own-turn', playing && !ownTurn);

        if (startButton instanceof HTMLButtonElement) {
            startButton.hidden = state?.isHost !== true || state?.phase !== 'waiting';
            startButton.disabled = state?.canStart !== true || requestInFlight;
        }

        wordList.querySelectorAll('.word-rings-word').forEach(token => {
            if (token instanceof HTMLButtonElement) token.disabled = !ownTurn || requestInFlight;
        });
    };

    const renderState = (nextState, { preservePending = false } = {}) => {
        if (!nextState) return;
        state = nextState;
        progress.textContent = format(
            root.dataset.roomScoreTemplate,
            formatScore(nextState.teamScore),
            nextState.targetScore);
        renderPlayers(nextState.players);
        renderRules(nextState);
        renderPlacements(nextState.placements);

        if (!preservePending) {
            pending = null;
            removePendingToken();
            renderBank(nextState);
        }

        if (nextState.phase === 'waiting') {
            setStatus(root.dataset.roomWaiting || '');
        } else if (nextState.phase === 'playing') {
            const current = currentPlayer();
            setStatus(
                isOwnTurn()
                    ? root.dataset.roomYourTurn
                    : format(root.dataset.roomOtherTurn, current?.name || ''));
        }

        updateControls();
        showFinishedDialog(nextState);
    };

    const handleRoomError = payload => {
        if (payload?.error === 'InvalidPlayer') {
            clearSession();
            showJoinDialog();
            return;
        }
        setStatus(root.dataset.roomError, 'error');
    };

    const requestState = async ({ preservePending = false } = {}) => {
        if (!session?.token || polling) return;
        polling = true;
        try {
            const payload = await post('RoomState', {
                roomCode,
                playerToken: session.token
            });
            if (!payload.success) {
                handleRoomError(payload);
                return;
            }
            renderState(payload.state, { preservePending });
        } catch (error) {
            console.error('Could not refresh Word Rings room.', error);
        } finally {
            polling = false;
        }
    };

    const showJoinDialog = () => {
        if (!(joinDialog instanceof HTMLDialogElement)) return;
        const savedName = localStorage.getItem(nicknameKey) || '';
        if (joinName instanceof HTMLInputElement && !joinName.value) joinName.value = savedName;
        if (!joinDialog.open) joinDialog.showModal();
        joinName?.focus();
    };

    joinForm?.addEventListener('submit', async event => {
        event.preventDefault();
        const name = joinName?.value?.trim() || '';
        if (!name) {
            setJoinError(root.dataset.roomInvalidName);
            joinName?.focus();
            return;
        }
        const submit = joinForm.querySelector('[type="submit"]');
        if (submit instanceof HTMLButtonElement) submit.disabled = true;
        setJoinError('');
        try {
            const payload = await post('JoinRoom', { roomCode, playerName: name });
            if (!payload.success) {
                setJoinError(root.dataset.roomError);
                return;
            }
            saveSession(payload.connection);
            joinDialog.close();
            renderState(payload.connection.state);
        } catch (error) {
            console.error('Could not join Word Rings room.', error);
            setJoinError(root.dataset.roomError);
        } finally {
            if (submit instanceof HTMLButtonElement) submit.disabled = false;
        }
    });

    root.querySelectorAll('[data-leave-room]').forEach(button => {
        button.addEventListener('click', () => {
            clearSession();
            const url = new URL(window.location.href);
            url.search = '';
            window.location.assign(url.toString());
        });
    });

    copyButton?.addEventListener('click', async () => {
        const url = new URL(window.location.href);
        url.search = '';
        url.searchParams.set('room', roomCode);
        try {
            await navigator.clipboard.writeText(url.toString());
            copyButton.classList.add('is-copied');
            window.setTimeout(() => copyButton.classList.remove('is-copied'), 1200);
        } catch (error) {
            console.error('Could not copy Word Rings room link.', error);
        }
    });

    revealButton?.addEventListener('click', () => {
        const hidden = rules?.classList.toggle('is-hidden') ?? true;
        revealButton.textContent = hidden ? root.dataset.revealRules : root.dataset.hideRules;
    });

    startButton?.addEventListener('click', async () => {
        if (!session?.token || requestInFlight) return;
        requestInFlight = true;
        updateControls();
        try {
            const payload = await post('StartRoom', { roomCode, playerToken: session.token });
            if (!payload.success) {
                setStatus(
                    payload.error === 'NeedMorePlayers' ? root.dataset.roomNeedPlayers : root.dataset.roomError,
                    'error');
                return;
            }
            renderState(payload.state);
        } catch (error) {
            console.error('Could not start Word Rings room.', error);
            setStatus(root.dataset.roomError, 'error');
        } finally {
            requestInFlight = false;
            updateControls();
        }
    });

    checkButton.addEventListener('click', async () => {
        if (!session?.token || !pending || requestInFlight || !isOwnTurn()) return;
        requestInFlight = true;
        updateControls();
        const submitted = { ...pending };
        try {
            const payload = await post('SubmitRoomWord', {
                roomCode,
                playerToken: session.token,
                word: submitted.word,
                membership: submitted.membership,
                x: submitted.x,
                y: submitted.y
            });
            if (!payload.success) {
                setStatus(root.dataset.roomError, 'error');
                return;
            }

            const result = payload.result;
            renderState(result.state);
            if (result.isCorrect) {
                setStatus(
                    Number(result.pointsAwarded) > 0
                        ? format(root.dataset.roomScoreAward, formatScore(result.pointsAwarded))
                        : root.dataset.roomOutsideNoPoint,
                    'success');
            } else if (result.isPartial && Number(result.pointsAwarded) > 0) {
                setStatus(root.dataset.roomPartialAward, 'partial');
            }
        } catch (error) {
            console.error('Could not submit Word Rings cooperative placement.', error);
            setStatus(root.dataset.roomError, 'error');
        } finally {
            requestInFlight = false;
            updateControls();
        }
    });

    const initialize = async () => {
        session = loadSession();
        if (!session?.token) {
            showJoinDialog();
            progress.textContent = '';
            updateControls();
            return;
        }
        await requestState();
    };

    window.setInterval(() => {
        if (!session?.token ||
            requestInFlight ||
            state?.phase === 'finished' ||
            root.querySelector('.word-rings-word.is-dragging')) return;
        requestState({ preservePending: pending !== null });
    }, 900);

    initialize();
})();
