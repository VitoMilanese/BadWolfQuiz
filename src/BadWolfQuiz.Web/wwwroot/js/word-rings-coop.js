(() => {
    const root = document.querySelector('[data-word-rings-root][data-game-mode="cooperative"]');
    if (!root) return;

    const stage = root.querySelector('[data-ring-stage]');
    const placedLayer = root.querySelector('[data-placed-layer]');
    const wordList = root.querySelector('[data-word-list]');
    const wordBank = root.querySelector('[data-word-bank]');
    const wordBankHeading = root.querySelector('[data-word-bank-heading]');
    const outsideZone = root.querySelector('[data-outside-zone]');
    const progress = root.querySelector('[data-progress]');
    const turnTimer = root.querySelector('[data-room-turn-timer]');
    const stageMessage = root.querySelector('[data-room-stage-message]');
    const status = root.querySelector('[data-status]');
    const roomFeedback = root.querySelector('[data-room-feedback]');
    const rules = root.querySelector('[data-rules]');
    const revealButton = root.querySelector('[data-reveal-rules]');
    const checkButton = root.querySelector('[data-check]');
    const playersList = root.querySelector('[data-word-rings-player-list]');
    const startButton = root.querySelector('[data-start-room]');
    const lockButton = root.querySelector('[data-toggle-room-lock]');
    const copyButton = root.querySelector('[data-copy-room-code]');
    const copyLinkButton = root.querySelector('[data-copy-room-link]');
    const toggleCodeButton = root.querySelector('[data-toggle-room-code]');
    const roomCodeLabel = root.querySelector('[data-room-code-label]');
    const soundToggle = root.querySelector('[data-room-sound-enabled]');
    const soundIcon = root.querySelector('[data-room-sound-icon]');
    const joinDialog = root.querySelector('[data-join-room-dialog]');
    const joinForm = root.querySelector('[data-join-room-form]');
    const joinName = root.querySelector('[data-join-room-name]');
    const joinError = root.querySelector('[data-join-room-error]');
    const antiForgery = root.querySelector('[data-word-rings-antiforgery] input[name="__RequestVerificationToken"]');
    const roomCode = String(root.dataset.roomCode || '').trim().toUpperCase();
    const apiUrl = root.dataset.roomApiUrl;
    const nicknameKey = 'badwolf.wordrings.nickname';
    const storageKey = `badwolf.wordrings.room.${roomCode}`;
    const soundStorageKey = 'badwolf.wordrings.sound-enabled';
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
    let roomCodeVisible = false;
    let copyFeedbackTimer = null;
    let linkCopyFeedbackTimer = null;
    let roomFeedbackTimer = null;
    let roomEventCursor = null;
    let roomAudioContext = null;
    let soundEffectsEnabled = true;
    try {
        soundEffectsEnabled = localStorage.getItem(soundStorageKey) !== 'false';
    } catch {
        soundEffectsEnabled = true;
    }
    let lastTimerTickSlot = null;
    let placementHistoryInitialized = false;
    const placementHistory = new Map();
    const placementFeedback = typeof window.BadWolfWordRingsPlacementFeedback === 'function'
        ? window.BadWolfWordRingsPlacementFeedback({ root })
        : null;
    const defaultWordBankHeading = wordBankHeading?.textContent || '';

    const renderSoundToggle = () => {
        if (soundToggle instanceof HTMLInputElement) soundToggle.checked = soundEffectsEnabled;
        if (soundIcon instanceof HTMLElement) soundIcon.textContent = soundEffectsEnabled ? '🔊' : '🔇';
    };
    renderSoundToggle();
    soundToggle?.addEventListener('change', () => {
        soundEffectsEnabled = soundToggle instanceof HTMLInputElement ? soundToggle.checked : true;
        try {
            localStorage.setItem(soundStorageKey, String(soundEffectsEnabled));
        } catch {
            // Local persistence is optional.
        }
        renderSoundToggle();
        if (!soundEffectsEnabled && roomAudioContext?.state === 'running') {
            void roomAudioContext.suspend().catch(() => {});
        } else if (soundEffectsEnabled) {
            unlockRoomAudio();
        }
    });

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

    const createRoomAudioContext = () => {
        const AudioContextType = window.AudioContext || window.webkitAudioContext;
        if (!AudioContextType) return null;
        try {
            roomAudioContext ||= new AudioContextType();
            return roomAudioContext;
        } catch (error) {
            console.debug('Word Rings audio context unavailable.', error);
            return null;
        }
    };

    const unlockRoomAudio = () => {
        if (!soundEffectsEnabled) return;
        const context = createRoomAudioContext();
        if (context?.state === 'suspended') {
            void context.resume().catch(error =>
                console.debug('Word Rings audio context could not resume.', error));
        }
    };

    root.addEventListener('pointerdown', unlockRoomAudio, { capture: true });
    root.addEventListener('keydown', unlockRoomAudio, { capture: true });
    root.addEventListener('touchstart', unlockRoomAudio, { capture: true, passive: true });

    const scheduleRoomSignal = (context, type) => {
        const patterns = {
            'check-submitted': [620],
            'host-correct': [660, 880],
            'host-moved': [520, 420],
            'victory': [660, 880, 1100],
            'defeat': [440, 330],
            'player-joined': [620, 760],
            'player-left': [760, 560],
            'player-kicked': [420, 260],
            'turn-transferred': [600, 720],
            'turn-timed-out': [420, 620, 760]
        };
        const frequencies = patterns[type] || [600];
        const startAt = context.currentTime + 0.01;
        frequencies.forEach((frequency, index) => {
            const oscillator = context.createOscillator();
            const gain = context.createGain();
            const noteStart = startAt + index * 0.13;
            oscillator.frequency.value = frequency;
            oscillator.type = 'sine';
            gain.gain.setValueAtTime(0.0001, noteStart);
            gain.gain.exponentialRampToValueAtTime(0.22, noteStart + 0.015);
            gain.gain.exponentialRampToValueAtTime(0.0001, noteStart + 0.115);
            oscillator.connect(gain);
            gain.connect(context.destination);
            oscillator.start(noteStart);
            oscillator.stop(noteStart + 0.125);
        });
    };

    const playRoomSignal = type => {
        if (!soundEffectsEnabled) return;
        const context = createRoomAudioContext();
        if (!context) return;
        const play = () => {
            if (context.state === 'running') scheduleRoomSignal(context, type);
        };
        if (context.state === 'suspended') {
            void context.resume()
                .then(play)
                .catch(error => console.debug('Word Rings room signal was blocked.', error));
            return;
        }
        play();
    };

    const playTimerTick = slot => {
        if (!soundEffectsEnabled) return;
        const context = createRoomAudioContext();
        if (!context || context.state !== 'running') return;
        const oscillator = context.createOscillator();
        const gain = context.createGain();
        const startAt = context.currentTime + 0.005;
        oscillator.type = 'square';
        oscillator.frequency.value = slot % 2 === 0 ? 920 : 680;
        gain.gain.setValueAtTime(0.0001, startAt);
        gain.gain.exponentialRampToValueAtTime(0.09, startAt + 0.004);
        gain.gain.exponentialRampToValueAtTime(0.0001, startAt + 0.055);
        oscillator.connect(gain);
        gain.connect(context.destination);
        oscillator.start(startAt);
        oscillator.stop(startAt + 0.06);
    };

    const announceRoomSignal = type => playRoomSignal(type);

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

    const showRoomFeedback = message => {
        if (!(roomFeedback instanceof HTMLElement)) return;
        if (roomFeedbackTimer !== null) window.clearTimeout(roomFeedbackTimer);
        roomFeedback.textContent = message || root.dataset.roomError || '';
        roomFeedback.hidden = false;
        roomFeedbackTimer = window.setTimeout(() => {
            roomFeedback.hidden = true;
            roomFeedback.textContent = '';
            roomFeedbackTimer = null;
        }, 4200);
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
    const serverPendingPlacement = () => state?.placements?.find(placement => placement.isPending === true) || null;
    const isOwnTurn = () => state?.phase === 'playing' && state.currentPlayerId === state.playerId;
    const isHostSeedSetup = () => state?.phase === 'playing' &&
        state?.dedicatedHostMode === true && state?.isHost === true && state?.seedSetupPending === true;
    const canJudgePendingPlacement = () =>
        state?.phase === 'playing' && state?.dedicatedHostMode === true && state?.isHost === true;

    const bringToFront = token => {
        if (!(token instanceof HTMLElement)) return;
        topZIndex += 1;
        token.style.zIndex = String(topZIndex);
    };

    placedLayer.addEventListener('pointerdown', event => {
        const token = event.target instanceof Element
            ? event.target.closest('.word-rings-word')
            : null;
        if (token instanceof HTMLElement && placedLayer.contains(token)) bringToFront(token);
    }, true);

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

    const buildRingGeometry = () => ringElements.map(([name, ring]) => {
        if (!ring) return [name, null];
        const rect = ring.getBoundingClientRect();
        return [name, {
            centerX: rect.left + rect.width / 2,
            centerY: rect.top + rect.height / 2,
            radius: Math.min(rect.width, rect.height) / 2
        }];
    });

    const membershipFromGeometry = (clientX, clientY, geometry) => canonical(
        geometry
            .filter(([, item]) => item && Math.hypot(clientX - item.centerX, clientY - item.centerY) <= item.radius)
            .map(([name]) => name)
            .join(''));

    const overlapArea = (left, right) => {
        const width = Math.max(0, Math.min(left.right, right.right) - Math.max(left.left, right.left));
        const height = Math.max(0, Math.min(left.bottom, right.bottom) - Math.max(left.top, right.top));
        return width * height;
    };

    const placementAnchors = {
        A: [26.5, 27.25],
        B: [73.5, 27.25],
        C: [50, 84],
        AB: [50, 16.75],
        AC: [32, 61],
        BC: [68, 61],
        ABC: [50, 46.25],
        '': [8, 88]
    };

    const findBestAutomaticPlacement = (token, membership) => {
        if (!(token instanceof HTMLElement)) return null;
        const targetMembership = canonical(membership);
        const stageRect = stage.getBoundingClientRect();
        const tokenRect = token.getBoundingClientRect();
        const width = Math.max(1, tokenRect.width);
        const height = Math.max(1, tokenRect.height);
        const geometry = buildRingGeometry();
        const occupied = [...placedLayer.querySelectorAll('.word-rings-word')]
            .filter(other => other !== token)
            .map(other => other.getBoundingClientRect());
        const anchor = placementAnchors[targetMembership] || [50, 50];

        let best = null;
        for (let y = 5; y <= 95; y += 2.5) {
            for (let x = 5; x <= 95; x += 2.5) {
                const clientX = stageRect.left + stageRect.width * x / 100;
                const clientY = stageRect.top + stageRect.height * y / 100;
                if (membershipFromGeometry(clientX, clientY, geometry) !== targetMembership) continue;

                const candidate = {
                    left: clientX - width / 2,
                    right: clientX + width / 2,
                    top: clientY - height / 2,
                    bottom: clientY + height / 2
                };
                if (candidate.left < stageRect.left + 4 ||
                    candidate.right > stageRect.right - 4 ||
                    candidate.top < stageRect.top + 4 ||
                    candidate.bottom > stageRect.bottom - 4) continue;

                const overlap = occupied.reduce((sum, rect) => sum + overlapArea(candidate, rect), 0);
                const minimumDistance = occupied.length === 0
                    ? 999
                    : Math.min(...occupied.map(rect => Math.hypot(
                        clientX - (rect.left + rect.width / 2),
                        clientY - (rect.top + rect.height / 2))));
                const anchorDistance = Math.hypot(x - anchor[0], y - anchor[1]);
                const score = overlap * 10000 + anchorDistance - Math.min(minimumDistance, 300) * 0.04;
                if (best === null || score < best.score) best = { x, y, overlap, score };
            }
        }

        return best;
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
            if (source.dataset.roomHostedPending === 'true') {
                root.dispatchEvent(new CustomEvent('wordrings:host-pending-moved'));
            }
        } catch (error) {
            console.error('Could not move checked Word Rings placement.', error);
            setStatus(root.dataset.roomError, 'error');
            await requestState({ preservePending: pending !== null });
        } finally {
            requestInFlight = false;
            updateControls();
        }
    };

    const placeHostSeed = async (word, placement) => {
        if (!session?.token || !isHostSeedSetup() || requestInFlight) return;
        requestInFlight = true;
        updateControls();
        try {
            const payload = await post('PlaceRoomSeed', {
                roomCode,
                playerToken: session.token,
                word,
                membership: placement.membership,
                x: placement.x,
                y: placement.y
            });
            if (!payload.success) {
                setStatus(root.dataset.roomError, 'error');
                return;
            }
            renderState(payload.state);
        } catch (error) {
            console.error('Could not place Word Rings host seed.', error);
            setStatus(root.dataset.roomError, 'error');
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
        if (isHostSeedSetup()) {
            void placeHostSeed(word, placement);
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
            if (source.dataset.roomSeed === 'true') {
                if (state?.seedSetupPending === true) return !requestInFlight && isHostSeedSetup();
                return !requestInFlight && state?.phase === 'playing';
            }
            if (source.dataset.roomHostedPending === 'true') {
                return !requestInFlight && canJudgePendingPlacement();
            }
            return !requestInFlight && (state?.phase === 'playing' || state?.phase === 'finished');
        }
        if (isHostSeedSetup()) return !requestInFlight;
        return !requestInFlight && serverPendingPlacement() === null && isOwnTurn() &&
            (pending === null || pending.word === word);
    };
    const canDropStage = (word, membership, source) => {
        if (isServerPlacementToken(source)) {
            if (source.dataset.roomSeed === 'true') {
                if (!canBegin(word, source)) return false;
                return isHostSeedSetup() ||
                    canonical(membership) === canonical(source.dataset.membership);
            }
            if (source.dataset.roomHostedPending === 'true') return canBegin(word, source);
            return canBegin(word, source) &&
                canonical(membership) === canonical(source.dataset.membership);
        }
        return canBegin(word, source);
    };
    const canDropOutside = (word, source) => {
        if (isServerPlacementToken(source)) {
            if (source.dataset.roomSeed === 'true') {
                if (!canBegin(word, source)) return false;
                return isHostSeedSetup() || canonical(source.dataset.membership) === '';
            }
            if (source.dataset.roomHostedPending === 'true') return canBegin(word, source);
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
        const livePlacementIds = new Set();
        const feedbackQueue = [];
        for (const placement of placements || []) {
            const placementId = String(placement.id);
            livePlacementIds.add(placementId);
            let token = placedLayer.querySelector(`[data-room-server-placement="${placementId}"]`);
            if (!(token instanceof HTMLButtonElement)) {
                token = document.createElement('button');
                token.type = 'button';
                token.disabled = false;
                token.className = 'word-rings-word is-on-stage is-room-server-placement';
                token.dataset.roomServerPlacement = placementId;
                placedLayer.append(token);
                wireWord?.(token);
            }

            token.dataset.word = placement.word;
            token.dataset.membership = canonical(placement.membership);
            token.style.left = `${placement.x}%`;
            token.style.top = `${placement.y}%`;
            token.textContent = placement.word;
            const awaitingHost = placement.isPending === true;
            const seedExample = placement.isSeed === true;
            const previousPlacement = placementHistory.get(placementId);
            if ((placementHistoryInitialized && previousPlacement === undefined) ||
                (awaitingHost && previousPlacement?.isPending !== true)) {
                bringToFront(token);
            }
            token.title = seedExample
                ? ''
                : awaitingHost
                    ? placement.playerName
                    : `${placement.playerName} · +${formatScore(placement.pointsAwarded)}`;
            token.classList.toggle('is-host-judgement-pending', awaitingHost);
            token.classList.toggle('is-seed-example', seedExample);
            if (seedExample) token.dataset.roomSeed = 'true';
            else delete token.dataset.roomSeed;
            if (awaitingHost) token.dataset.roomHostedPending = 'true';
            else delete token.dataset.roomHostedPending;
            token.classList.toggle('is-correct', !seedExample && !awaitingHost && placement.isCorrect === true);
            token.classList.toggle(
                'is-partial',
                !seedExample && !awaitingHost && placement.isCorrect !== true && placement.isPartial === true && Number(placement.pointsAwarded) > 0);
            token.classList.toggle(
                'is-wrong',
                !seedExample && !awaitingHost && placement.isCorrect !== true && !(placement.isPartial === true && Number(placement.pointsAwarded) > 0));

            const previous = previousPlacement;
            if (placementHistoryInitialized && !seedExample && !awaitingHost &&
                (previous === undefined || previous.isPending === true)) {
                const kind = placement.isCorrect === true
                    ? 'correct'
                    : placement.isPartial === true && Number(placement.pointsAwarded) > 0
                        ? 'partial'
                        : 'wrong';
                feedbackQueue.push({ token, kind });
            }
            placementHistory.set(placementId, { isPending: awaitingHost, isSeed: seedExample });
        }

        placedLayer.querySelectorAll('[data-room-server-placement]').forEach(token => {
            const placementId = token.dataset.roomServerPlacement || '';
            if (!livePlacementIds.has(placementId)) {
                token.remove();
                placementHistory.delete(placementId);
            }
        });
        placementHistoryInitialized = true;
        if (feedbackQueue.length > 0) {
            window.requestAnimationFrame(() => feedbackQueue.forEach(item => placementFeedback?.play(item.token, item.kind)));
        }
    };

    const repositionIncorrectPlacementIfNeeded = async result => {
        if (result?.isPending === true || result?.isCorrect === true || !result?.state) return result?.state || null;
        const placement = [...(result.state.placements || [])]
            .reverse()
            .find(item => item.word === result.word && item.playerId === result.state.playerId);
        if (!placement) return result.state;

        const token = placedLayer.querySelector(`[data-room-server-placement="${String(placement.id)}"]`);
        if (!(token instanceof HTMLElement)) return result.state;
        const occupied = [...placedLayer.querySelectorAll('.word-rings-word')]
            .filter(other => other !== token)
            .map(other => other.getBoundingClientRect());
        const currentRect = token.getBoundingClientRect();
        const currentOverlap = occupied.reduce((sum, rect) => sum + overlapArea(currentRect, rect), 0);
        if (currentOverlap <= 0) return result.state;

        const best = findBestAutomaticPlacement(token, placement.membership);
        if (!best || best.overlap >= currentOverlap - 0.5) return result.state;

        const payload = await post('MoveRoomPlacement', {
            roomCode,
            playerToken: session.token,
            placementId: placement.id,
            membership: placement.membership,
            x: best.x,
            y: best.y
        });
        return payload.success ? payload.state : result.state;
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
            if (player.isHost) {
                const badge = document.createElement('span');
                badge.className = 'word-rings-player-badge';
                badge.textContent = '★';
                main.append(badge);
            }
            const name = document.createElement('strong');
            name.textContent = player.name;
            main.append(name);

            const meta = document.createElement('div');
            meta.className = 'word-rings-player-meta';
            const score = document.createElement('span');
            score.textContent = `${formatScore(player.score)} pt`;
            meta.append(score);
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
        const playerScore = nextState.players?.find(player => player.id === nextState.playerId)?.score
            ?? nextState.playerScore
            ?? 0;
        const nonPlayingHost = nextState.dedicatedHostMode === true && nextState.isHost === true;
        if (!nonPlayingHost) announceRoomSignal(won ? 'victory' : 'defeat');
        root.dispatchEvent(new CustomEvent('wordrings:game-ended', {
            detail: {
                won,
                title: won ? root.dataset.winTitle : root.dataset.loseTitle,
                message: format(template, formatScore(playerScore), nextState.targetScore)
            }
        }));
    };

    const processRoomEvents = nextState => {
        const events = [...(nextState?.events || [])]
            .filter(event => Number.isFinite(Number(event.sequence)))
            .sort((left, right) => Number(left.sequence) - Number(right.sequence));
        const latest = events.length ? Number(events[events.length - 1].sequence) : 0;
        if (roomEventCursor === null) {
            roomEventCursor = latest;
            return;
        }

        for (const event of events) {
            const sequence = Number(event.sequence);
            if (sequence <= roomEventCursor) continue;
            announceRoomSignal(event.type);
            roomEventCursor = Math.max(roomEventCursor, sequence);
        }
        roomEventCursor = Math.max(roomEventCursor, latest);
    };

    const renderTurnTimer = () => {
        if (!(turnTimer instanceof HTMLElement)) return;
        const deadline = state?.turnDeadlineUtc ? Date.parse(state.turnDeadlineUtc) : Number.NaN;
        if (state?.phase !== 'playing' || !Number.isFinite(deadline)) {
            turnTimer.hidden = true;
            turnTimer.textContent = '';
            lastTimerTickSlot = null;
            return;
        }
        const remainingMs = Math.max(0, deadline - Date.now());
        const seconds = Math.max(0, Math.ceil(remainingMs / 1000));
        const minutes = Math.floor(seconds / 60);
        turnTimer.textContent = `⏱ ${minutes}:${String(seconds % 60).padStart(2, '0')}`;
        turnTimer.hidden = false;
        if (remainingMs > 0 && remainingMs <= 10000) {
            const tickSlot = Math.floor(Date.now() / 500);
            if (tickSlot !== lastTimerTickSlot) {
                lastTimerTickSlot = tickSlot;
                playTimerTick(tickSlot);
            }
        } else {
            lastTimerTickSlot = null;
        }
    };

    const renderRoomContext = nextState => {
        const dedicatedHost = nextState?.dedicatedHostMode === true && nextState?.isHost === true;
        const seedSetup = dedicatedHost && nextState?.seedSetupPending === true;
        const judging = dedicatedHost && nextState?.phase === 'playing' &&
            (nextState?.placements || []).some(item => item.isPending === true);
        if (wordBankHeading) {
            wordBankHeading.textContent = seedSetup
                ? root.dataset.roomHostSeedTitle || defaultWordBankHeading
                : judging
                    ? root.dataset.roomHostJudgementTitle || defaultWordBankHeading
                    : defaultWordBankHeading;
        }
        if (wordList instanceof HTMLElement) wordList.hidden = dedicatedHost && judging && !seedSetup;
        if (wordBank instanceof HTMLElement) {
            const hasWords = (nextState?.bankWords?.length || 0) + (nextState?.queuedWords?.length || 0) > 0;
            wordBank.hidden = dedicatedHost && !seedSetup && !judging && !hasWords;
        }
        if (stageMessage instanceof HTMLElement) {
            const waitingForHostSeeds = nextState?.dedicatedHostMode === true &&
                nextState?.isHost !== true && nextState?.seedSetupPending === true && nextState?.phase === 'playing';
            stageMessage.textContent = waitingForHostSeeds ? root.dataset.roomWaitingHostSeeds || '' : '';
            stageMessage.hidden = !waitingForHostSeeds;
        }
        renderTurnTimer();
    };

    const updateControls = () => {
        const playing = state?.phase === 'playing';
        const ownTurn = isOwnTurn();
        const awaitingHost = serverPendingPlacement() !== null;
        checkButton.disabled = !playing || !ownTurn || awaitingHost || pending === null || requestInFlight;
        root.classList.toggle('has-pending-word', pending !== null);
        root.classList.toggle('is-awaiting-host-judgement', awaitingHost);
        root.classList.toggle('is-game-over', state?.phase === 'finished');
        root.classList.toggle('is-room-waiting', state?.phase === 'waiting');
        root.classList.toggle('is-not-own-turn', playing && !ownTurn && !isHostSeedSetup());

        const canUseBank = isHostSeedSetup() || ownTurn;
        wordList.querySelectorAll('.word-rings-word').forEach(token => {
            if (token instanceof HTMLButtonElement) token.disabled = !canUseBank || awaitingHost || requestInFlight;
        });
    };

    const renderState = (nextState, { preservePending = false } = {}) => {
        if (!nextState) return;
        const previousDeadline = state?.turnDeadlineUtc || null;
        const pendingStillAvailable = pending && [...(nextState.bankWords || []), ...(nextState.queuedWords || [])]
            .some(word => String(word).toLocaleLowerCase() === String(pending.word).toLocaleLowerCase());
        const keepPending = preservePending && pendingStillAvailable &&
            nextState.phase === 'playing' && nextState.currentPlayerId === nextState.playerId &&
            (previousDeadline === null || (nextState.turnDeadlineUtc || null) === previousDeadline);
        state = nextState;
        processRoomEvents(nextState);
        if (nextState.phase === 'playing') resultShown = false;
        const ownScore = nextState.players?.find(player => player.id === nextState.playerId)?.score
            ?? nextState.playerScore
            ?? 0;
        progress.textContent = format(
            root.dataset.roomScoreTemplate,
            formatScore(ownScore),
            nextState.targetScore);
        renderPlayers(nextState.players);
        renderRules(nextState);
        renderPlacements(nextState.placements);
        renderRoomContext(nextState);
        if (nextState.isHost !== true) {
            if (startButton instanceof HTMLButtonElement) startButton.hidden = true;
            if (lockButton instanceof HTMLButtonElement) lockButton.hidden = true;
        }
        const hostCanRevealRules = nextState.dedicatedHostMode === true &&
            nextState.isHost === true && nextState.phase !== 'finished';
        if (revealButton instanceof HTMLButtonElement) {
            revealButton.hidden = !hostCanRevealRules;
            if (!hostCanRevealRules && rules) {
                rules.classList.add('is-hidden');
                revealButton.textContent = root.dataset.revealRules || revealButton.textContent;
            }
        }

        if (!keepPending) {
            pending = null;
            removePendingToken();
            renderBank(nextState);
        }

        if (nextState.phase === 'waiting') {
            setStatus(root.dataset.roomWaiting || '');
        } else if (nextState.phase === 'playing') {
            const current = currentPlayer();
            setStatus(
                serverPendingPlacement() !== null
                    ? root.dataset.roomAwaitingHost
                    : isOwnTurn()
                        ? root.dataset.roomYourTurn
                        : format(root.dataset.roomOtherTurn, current?.name || ''));
        }

        updateControls();
        showFinishedDialog(nextState);
    };

    const handleRoomError = payload => {
        if (payload?.error === 'InvalidPlayer') {
            if (state && session?.token) {
                announceRoomSignal('player-kicked');
            }
            clearSession();
            showJoinDialog();
            return;
        }
        showRoomFeedback(root.dataset.roomError);
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
            announceRoomSignal('player-joined');
        } catch (error) {
            console.error('Could not join Word Rings room.', error);
            setJoinError(root.dataset.roomError);
        } finally {
            if (submit instanceof HTMLButtonElement) submit.disabled = false;
        }
    });

    const sendDepartureBeacon = () => {
        const token = session?.token;
        if (!token || state?.isHost === true || typeof navigator.sendBeacon !== 'function') return;
        const data = new FormData();
        if (antiForgery instanceof HTMLInputElement) {
            data.set('__RequestVerificationToken', antiForgery.value);
        }
        data.set('roomCode', roomCode);
        data.set('playerToken', token);
        navigator.sendBeacon(`${apiUrl}?handler=PrepareLeaveRoom`, data);
    };
    window.addEventListener('pagehide', sendDepartureBeacon);

    root.querySelectorAll('[data-leave-room]').forEach(button => {
        button.addEventListener('click', async event => {
            event.preventDefault();
            const token = session?.token;
            if (token && state?.isHost !== true) {
                try {
                    await post('LeaveRoom', { roomCode, playerToken: token });
                } catch (error) {
                    console.debug('Could not notify Word Rings room about leaving player.', error);
                }
            }
            clearSession();
            const url = new URL(window.location.href);
            url.search = '';
            window.location.assign(url.toString());
        });
    });

    copyButton?.addEventListener('click', async () => {
        try {
            await navigator.clipboard.writeText(roomCode);
            copyButton.classList.add('is-copied');
            copyButton.textContent = '✓';
            const copiedLabel = root.dataset.roomCodeCopied || root.dataset.roomCopyCode || '';
            copyButton.title = copiedLabel;
            copyButton.setAttribute('aria-label', copiedLabel);
            if (copyFeedbackTimer !== null) window.clearTimeout(copyFeedbackTimer);
            copyFeedbackTimer = window.setTimeout(() => {
                copyButton.classList.remove('is-copied');
                copyButton.textContent = '⧉';
                const copyLabel = root.dataset.roomCopyCode || '';
                copyButton.title = copyLabel;
                copyButton.setAttribute('aria-label', copyLabel);
                copyFeedbackTimer = null;
            }, 1400);
        } catch (error) {
            console.error('Could not copy Word Rings room code.', error);
        }
    });

    copyLinkButton?.addEventListener('click', async () => {
    const url = new URL(window.location.href);
    url.search = '';
    url.hash = '';
    url.searchParams.set('room', roomCode);
    try {
        await navigator.clipboard.writeText(url.toString());
        copyLinkButton.classList.add('is-copied');
        copyLinkButton.textContent = '✓';
        const copiedLabel = root.dataset.roomLinkCopied || root.dataset.roomCopyLink || '';
        copyLinkButton.title = copiedLabel;
        copyLinkButton.setAttribute('aria-label', copiedLabel);
        if (linkCopyFeedbackTimer !== null) window.clearTimeout(linkCopyFeedbackTimer);
        linkCopyFeedbackTimer = window.setTimeout(() => {
            copyLinkButton.classList.remove('is-copied');
            copyLinkButton.textContent = '↗';
            const copyLabel = root.dataset.roomCopyLink || '';
            copyLinkButton.title = copyLabel;
            copyLinkButton.setAttribute('aria-label', copyLabel);
            linkCopyFeedbackTimer = null;
        }, 1400);
    } catch (error) {
        console.error('Could not copy Word Rings room link.', error);
    }
});

    toggleCodeButton?.addEventListener('click', () => {
        roomCodeVisible = !roomCodeVisible;
        if (roomCodeLabel) roomCodeLabel.textContent = roomCodeVisible ? roomCode : '••••••';
        toggleCodeButton.textContent = roomCodeVisible ? '🙈' : '👁';
        toggleCodeButton.setAttribute('aria-pressed', roomCodeVisible ? 'true' : 'false');
        const label = roomCodeVisible ? root.dataset.roomHideCode : root.dataset.roomShowCode;
        toggleCodeButton.title = label || '';
        toggleCodeButton.setAttribute('aria-label', label || '');
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
                showRoomFeedback(
                    payload.error === 'NeedMorePlayers' ? root.dataset.roomNeedPlayers : root.dataset.roomError);
                return;
            }
            renderState(payload.state);
        } catch (error) {
            console.error('Could not start Word Rings room.', error);
            showRoomFeedback(root.dataset.roomError);
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
            if (!result.isCorrect) {
                const settledState = await repositionIncorrectPlacementIfNeeded(result);
                if (settledState && settledState !== result.state) renderState(settledState);
            }
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
            root.querySelector('.word-rings-word.is-dragging')) return;
        requestState({ preservePending: pending !== null });
    }, 900);
    window.setInterval(renderTurnTimer, 250);

    initialize();
})();
