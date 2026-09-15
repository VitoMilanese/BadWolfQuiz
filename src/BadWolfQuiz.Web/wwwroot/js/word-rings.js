(() => {
    const root = document.querySelector('[data-word-rings-root]');
    if (!root) return;

    const stage = root.querySelector('[data-ring-stage]');
    const placedLayer = root.querySelector('[data-placed-layer]');
    const wordList = root.querySelector('[data-word-list]');
    const outsideZone = root.querySelector('[data-outside-zone]');
    const outsideList = root.querySelector('[data-outside-list]');
    const progress = root.querySelector('[data-progress]');
    const status = root.querySelector('[data-status]');
    const rules = root.querySelector('[data-rules]');
    const revealButton = root.querySelector('[data-reveal-rules]');
    const checkButton = root.querySelector('[data-check]');
    const resetButton = root.querySelector('[data-reset]');
    const puzzleConfig = root.querySelector('[data-word-rings-puzzle]');
    const queueConfig = root.querySelector('[data-word-rings-queue]');
    const isSoloMode = (root.dataset.gameMode || 'solo').toLowerCase() === 'solo';

    const ringElements = [
        ['A', root.querySelector('.word-rings-circle-a')],
        ['B', root.querySelector('.word-rings-circle-b')],
        ['C', root.querySelector('.word-rings-circle-c')]
    ];

    let expected = new Map();
    try {
        expected = new Map(Object.entries(JSON.parse(puzzleConfig?.textContent || '{}')));
    } catch (error) {
        console.error('Failed to read word-rings puzzle config.', error);
    }

    let queuedWords = [];
    try {
        const parsedQueue = JSON.parse(queueConfig?.textContent || '[]');
        if (Array.isArray(parsedQueue)) {
            queuedWords = parsedQueue.filter(word => typeof word === 'string' && expected.has(word));
        }
    } catch (error) {
        console.error('Failed to read word-rings queue config.', error);
    }

    const positiveInteger = (value, fallback) => {
        const parsed = Number.parseInt(value, 10);
        return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
    };
    const maximumBankWords = positiveInteger(root.dataset.bankWordLimit, 10);
    const configuredGameWordLimit = positiveInteger(root.dataset.gameWordLimit, 20);
    let maximumAttempts = Math.min(configuredGameWordLimit, expected.size);
    const configuredCorrectWordTarget = positiveInteger(root.dataset.correctWordTarget, 10);
    let correctWordTarget = Math.min(configuredCorrectWordTarget, maximumAttempts);

    const assignments = new Map();
    const verdicts = new Map();
    const lockedMemberships = new Map();
    let pendingWord = null;
    let wireWord = null;
    let topZIndex = 10;
    let gameOver = maximumAttempts === 0;

    const canonical = value => [...String(value ?? '')].sort().join('');
    const correctCount = () => [...verdicts.values()].filter(result => result === true).length;
    const attemptedCount = () => Math.min(
        maximumAttempts,
        verdicts.size + (pendingWord === null ? 0 : 1));
    const progressText = () => `✓ ${correctCount()}/${correctWordTarget} · ${attemptedCount()}/${maximumAttempts}`;

    const bringToFront = token => {
        if (!(token instanceof HTMLElement)) return;
        topZIndex += 1;
        token.style.zIndex = String(topZIndex);
    };

    const disableAllWords = () => {
        root.querySelectorAll('.word-rings-word').forEach(token => {
            if (token instanceof HTMLButtonElement) token.disabled = true;
        });
    };

    const updateProgress = () => {
        progress.textContent = progressText();
        checkButton.disabled = gameOver || pendingWord === null;
        root.classList.toggle('has-pending-word', !gameOver && pendingWord !== null);
        root.classList.toggle('is-game-over', gameOver);
        if (gameOver) disableAllWords();
    };

    const clearStatus = () => {
        if (gameOver) return;
        status.textContent = '';
        status.classList.remove('is-success', 'is-error');
    };

    const applyVerdict = (token, word) => {
        if (!(token instanceof HTMLElement)) return;
        if (!verdicts.has(word)) {
            token.classList.remove('is-correct', 'is-wrong');
            return;
        }

        const correct = verdicts.get(word) === true;
        token.classList.toggle('is-correct', correct);
        token.classList.toggle('is-wrong', !correct);
    };

    const createBankWord = word => {
        if (!word || wordList.querySelector(`.word-rings-word[data-word="${CSS.escape(word)}"]`)) return null;

        const token = document.createElement('button');
        token.className = 'word-rings-word';
        token.type = 'button';
        token.dataset.word = word;
        token.dataset.runtimeWord = 'true';
        token.textContent = word;
        token.draggable = false;
        wordList.append(token);
        wireWord?.(token);
        return token;
    };

    const visibleBankWords = () => [...wordList.querySelectorAll('.word-rings-word[data-word]')]
        .filter(token => !token.hidden);

    const replenishWordBank = () => {
        if (gameOver) return;

        while (visibleBankWords().length < maximumBankWords && queuedWords.length > 0) {
            const nextWord = queuedWords.shift();
            if (!nextWord || !expected.has(nextWord)) continue;
            createBankWord(nextWord);
        }
    };

    const trimWordBankToLimit = returnedWord => {
        let visible = visibleBankWords();
        while (visible.length > maximumBankWords) {
            const removable = [...visible]
                .reverse()
                .find(token => token.dataset.word !== returnedWord);
            if (!(removable instanceof HTMLElement)) break;

            const word = removable.dataset.word;
            removable.remove();
            if (word) queuedWords.unshift(word);
            visible = visibleBankWords();
        }
    };

    const createPlacedWord = (word, x, y, membership) => {
        const source = wordList.querySelector(`.word-rings-word[data-word="${CSS.escape(word)}"]`);
        if (!source) return;

        source.classList.add('is-placed');
        source.hidden = true;

        const token = source.cloneNode(true);
        token.hidden = false;
        token.disabled = false;
        token.classList.remove('is-placed', 'is-dragging', 'is-correct', 'is-wrong');
        token.classList.add('is-on-stage');
        token.dataset.membership = canonical(membership);
        token.style.left = `${x}%`;
        token.style.top = `${y}%`;
        token.draggable = false;
        applyVerdict(token, word);
        bringToFront(token);
        placedLayer.append(token);
        wireWord?.(token);
    };

    const removePlacedWord = word => {
        placedLayer.querySelectorAll('.word-rings-word').forEach(token => {
            if (token.dataset.word === word) token.remove();
        });
        outsideList.querySelectorAll('.word-rings-word').forEach(token => {
            if (token.dataset.word === word) token.remove();
        });
        const source = wordList.querySelector(`.word-rings-word[data-word="${CSS.escape(word)}"]`);
        if (source) {
            source.hidden = false;
            source.classList.remove('is-placed', 'is-dragging');
        }
    };

    const pointIsInsideRing = (clientX, clientY, ring) => {
        if (!ring) return false;

        const rect = ring.getBoundingClientRect();
        const centerX = rect.left + (rect.width / 2);
        const centerY = rect.top + (rect.height / 2);
        const radius = Math.min(rect.width, rect.height) / 2;

        return Math.hypot(clientX - centerX, clientY - centerY) <= radius;
    };

    const membershipAt = (clientX, clientY) => {
        const stageRect = stage.getBoundingClientRect();
        const x = (clientX - stageRect.left) / stageRect.width;
        const y = (clientY - stageRect.top) / stageRect.height;
        const membership = ringElements
            .filter(([, ring]) => pointIsInsideRing(clientX, clientY, ring))
            .map(([name]) => name)
            .join('');

        return {
            membership: canonical(membership),
            x: Math.max(4, Math.min(96, x * 100)),
            y: Math.max(4, Math.min(96, y * 100))
        };
    };

    const buildRingGeometry = () => ringElements.map(([name, ring]) => {
        if (!ring) return [name, null];
        const rect = ring.getBoundingClientRect();
        return [name, {
            centerX: rect.left + (rect.width / 2),
            centerY: rect.top + (rect.height / 2),
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
        A: [27, 28],
        B: [73, 28],
        C: [50, 76],
        AB: [50, 19],
        AC: [35, 54],
        BC: [65, 54],
        ABC: [50, 43],
        '': [8, 88]
    };

    const findBestPlacement = (word, membership) => {
        const token = placedLayer.querySelector(`.word-rings-word[data-word="${CSS.escape(word)}"]`);
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
                const clientX = stageRect.left + (stageRect.width * x / 100);
                const clientY = stageRect.top + (stageRect.height * y / 100);
                if (membershipFromGeometry(clientX, clientY, geometry) !== targetMembership) continue;

                const candidate = {
                    left: clientX - (width / 2),
                    right: clientX + (width / 2),
                    top: clientY - (height / 2),
                    bottom: clientY + (height / 2)
                };
                if (candidate.left < stageRect.left + 4 ||
                    candidate.right > stageRect.right - 4 ||
                    candidate.top < stageRect.top + 4 ||
                    candidate.bottom > stageRect.bottom - 4) {
                    continue;
                }

                const overlap = occupied.reduce((sum, rect) => sum + overlapArea(candidate, rect), 0);
                const minimumDistance = occupied.length === 0
                    ? 999
                    : Math.min(...occupied.map(rect =>
                        Math.hypot(
                            clientX - (rect.left + rect.width / 2),
                            clientY - (rect.top + rect.height / 2))));
                const anchorDistance = Math.hypot(x - anchor[0], y - anchor[1]);
                const score = (overlap * 10000) + anchorDistance - (Math.min(minimumDistance, 300) * 0.04);

                if (best === null || score < best.score) {
                    best = { x, y, score };
                }
            }
        }

        return best;
    };

    const moveWordToCorrectMembership = (word, membership) => {
        const token = placedLayer.querySelector(`.word-rings-word[data-word="${CSS.escape(word)}"]`);
        if (!(token instanceof HTMLElement)) return false;

        const placement = findBestPlacement(word, membership);
        if (!placement) return false;

        const targetMembership = canonical(membership);
        token.dataset.membership = targetMembership;
        token.style.left = `${placement.x}%`;
        token.style.top = `${placement.y}%`;
        assignments.set(word, targetMembership);
        applyVerdict(token, word);
        bringToFront(token);
        return true;
    };

    const markPending = word => {
        if (!verdicts.has(word)) {
            pendingWord = word;
        }
    };

    const assignToStage = (word, clientX, clientY) => {
        const bankSource = wordList.querySelector(`.word-rings-word[data-word="${CSS.escape(word)}"]`);
        const movedFromBank = bankSource instanceof HTMLElement && !bankSource.hidden;

        removePlacedWord(word);
        const target = membershipAt(clientX, clientY);
        assignments.set(word, target.membership);
        createPlacedWord(word, target.x, target.y, target.membership);
        markPending(word);
        if (movedFromBank) replenishWordBank();
    };

    const assignOutside = word => {
        const bankSource = wordList.querySelector(`.word-rings-word[data-word="${CSS.escape(word)}"]`);
        const movedFromBank = bankSource instanceof HTMLElement && !bankSource.hidden;

        removePlacedWord(word);
        assignments.set(word, '');
        const source = wordList.querySelector(`.word-rings-word[data-word="${CSS.escape(word)}"]`);
        if (!source) return;

        source.hidden = true;
        source.classList.add('is-placed');
        const token = source.cloneNode(true);
        token.hidden = false;
        token.disabled = false;
        token.classList.remove('is-placed', 'is-dragging', 'is-correct', 'is-wrong');
        token.classList.add('is-outside');
        token.dataset.membership = '';
        token.draggable = false;
        applyVerdict(token, word);
        outsideList.append(token);
        wireWord?.(token);
        markPending(word);
        if (movedFromBank) replenishWordBank();
    };

    const returnToBank = word => {
        if (gameOver || verdicts.has(word)) return;
        assignments.delete(word);
        removePlacedWord(word);
        trimWordBankToLimit(word);
        if (pendingWord === word) {
            pendingWord = null;
        }
    };

    const canBegin = word => !gameOver &&
        (verdicts.has(word) || pendingWord === null || pendingWord === word);

    const canDropStage = (word, membership) => {
        if (gameOver) return false;
        const normalized = canonical(membership);
        if (verdicts.has(word)) {
            return lockedMemberships.get(word) === normalized;
        }
        return pendingWord === null || pendingWord === word;
    };

    const canDropOutside = word => canDropStage(word, '');
    const canReturnToBank = word => !gameOver && !verdicts.has(word);

    const onPlacementChanged = () => {
        clearStatus();
        updateProgress();
    };

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

    root.querySelectorAll('.word-rings-word').forEach(word => {
        word.draggable = false;
        wireWord?.(word);
    });

    revealButton.addEventListener('click', () => {
        const hidden = rules.classList.toggle('is-hidden');
        revealButton.textContent = hidden ? root.dataset.revealRules : root.dataset.hideRules;
    });

    const legacyResetQueryKeys = [
        'previousBlueRule',
        'previousYellowRule',
        'previousRedRule',
        'previousWords',
        'refresh'
    ];

    const clearLegacyResetQuery = () => {
        const cleanUrl = new URL(window.location.href);
        let changed = false;
        for (const key of legacyResetQueryKeys) {
            if (!cleanUrl.searchParams.has(key)) continue;
            cleanUrl.searchParams.delete(key);
            changed = true;
        }
        if (!changed) return;
        window.history.replaceState(
            window.history.state,
            '',
            `${cleanUrl.pathname}${cleanUrl.search}${cleanUrl.hash}`);
    };

    const currentRuleValues = () => ({
        previousBlueRule: rules.querySelector('.word-rings-rule-a span')?.textContent?.trim() || '',
        previousYellowRule: rules.querySelector('.word-rings-rule-b span')?.textContent?.trim() || '',
        previousRedRule: rules.querySelector('.word-rings-rule-c span')?.textContent?.trim() || ''
    });

    const applyFreshPuzzle = payload => {
        const words = Array.isArray(payload?.words)
            ? payload.words.filter(word => typeof word === 'string')
            : [];
        expected = new Map(Object.entries(payload?.expected || {}));
        const playableWords = words.filter(word => expected.has(word));
        queuedWords = playableWords.slice(maximumBankWords);
        maximumAttempts = Math.min(configuredGameWordLimit, expected.size);
        correctWordTarget = Math.min(configuredCorrectWordTarget, maximumAttempts);

        assignments.clear();
        verdicts.clear();
        lockedMemberships.clear();
        pendingWord = null;
        topZIndex = 10;
        gameOver = maximumAttempts === 0;

        placedLayer.replaceChildren();
        outsideList.replaceChildren();
        wordList.replaceChildren();
        for (const word of playableWords.slice(0, maximumBankWords)) createBankWord(word);

        const setRuleText = (selector, value) => {
            const target = rules.querySelector(selector);
            if (target) target.textContent = value || '';
        };
        setRuleText('.word-rings-rule-a span', payload?.blueRuleText);
        setRuleText('.word-rings-rule-b span', payload?.yellowRuleText);
        setRuleText('.word-rings-rule-c span', payload?.redRuleText);
        rules.classList.add('is-hidden');
        revealButton.textContent = root.dataset.revealRules || revealButton.textContent;

        status.textContent = '';
        status.classList.remove('is-success', 'is-error');
        root.classList.remove('has-pending-word', 'is-game-over');
        updateProgress();
        root.dispatchEvent(new CustomEvent('wordrings:game-reset'));
    };

    resetButton.addEventListener('click', async () => {
        if (resetButton.disabled) return;
        const requestUrl = new URL(window.location.href);
        requestUrl.search = '';
        requestUrl.hash = '';
        requestUrl.searchParams.set('handler', 'NewPuzzle');
        Object.entries(currentRuleValues()).forEach(([name, value]) => {
            if (value) requestUrl.searchParams.set(name, value);
        });
        requestUrl.searchParams.set('previousWords', [...expected.keys()].join('|'));

        resetButton.disabled = true;
        try {
            const response = await fetch(requestUrl.toString(), {
                headers: { Accept: 'application/json' }
            });
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            applyFreshPuzzle(await response.json());
            clearLegacyResetQuery();
        } catch (error) {
            console.error('Could not reset Word Rings without reloading.', error);
            status.classList.add('is-error');
            status.textContent = root.dataset.roomError || '';
        } finally {
            resetButton.disabled = false;
        }
    });

    const finishGameIfNeeded = () => {
        const successful = correctCount();
        const attempts = verdicts.size;
        if (successful < correctWordTarget && attempts < maximumAttempts) {
            return false;
        }

        gameOver = true;
        pendingWord = null;
        const won = successful >= correctWordTarget;
        status.classList.toggle('is-success', won);
        status.classList.toggle('is-error', !won);
        status.textContent = won
            ? `✓ ${successful}/${correctWordTarget}`
            : `✓ ${successful}/${correctWordTarget} · ${attempts}/${maximumAttempts}`;
        updateProgress();
        return true;
    };

    checkButton.addEventListener('click', () => {
        if (gameOver || pendingWord === null || !assignments.has(pendingWord)) return;

        const word = pendingWord;
        const actual = canonical(assignments.get(word));
        const expectedMembership = canonical(expected.get(word) ?? '');
        const correct = expectedMembership === actual;
        verdicts.set(word, correct);

        root.querySelectorAll(`.word-rings-word[data-word="${CSS.escape(word)}"]`).forEach(token => {
            token.classList.toggle('is-correct', correct);
            token.classList.toggle('is-wrong', !correct);
        });

        if (correct) {
            lockedMemberships.set(word, actual);
        } else if (isSoloMode && moveWordToCorrectMembership(word, expectedMembership)) {
            lockedMemberships.set(word, expectedMembership);
        } else {
            lockedMemberships.set(word, actual);
        }

        pendingWord = null;
        if (finishGameIfNeeded()) return;

        const errors = [...verdicts.values()].filter(result => !result).length;
        const hasNoErrors = errors === 0;
        status.classList.remove('is-success');
        status.classList.toggle('is-error', !hasNoErrors);
        status.textContent = correct
            ? ''
            : root.dataset.hasErrorsTemplate.replace('{0}', errors);

        updateProgress();
    });

    clearLegacyResetQuery();
    updateProgress();
})();
