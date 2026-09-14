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

    const assignments = new Map();
    const verdicts = new Map();
    const lockedMemberships = new Map();
    let pendingWord = null;
    let wireWord = null;
    let topZIndex = 10;

    const canonical = value => [...String(value ?? '')].sort().join('');
    const progressText = () => root.dataset.progressTemplate
        .replace('{0}', verdicts.size)
        .replace('{1}', expected.size);

    const bringToFront = token => {
        if (!(token instanceof HTMLElement)) return;
        topZIndex += 1;
        token.style.zIndex = String(topZIndex);
    };

    const updateProgress = () => {
        progress.textContent = progressText();
        checkButton.disabled = pendingWord === null;
        root.classList.toggle('has-pending-word', pendingWord !== null);
    };

    const clearStatus = () => {
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

    const createPlacedWord = (word, x, y, membership) => {
        const source = wordList.querySelector(`.word-rings-word[data-word="${CSS.escape(word)}"]`);
        if (!source) return;

        source.classList.add('is-placed');
        source.hidden = true;

        const token = source.cloneNode(true);
        token.hidden = false;
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
        removePlacedWord(word);
        const target = membershipAt(clientX, clientY);
        assignments.set(word, target.membership);
        createPlacedWord(word, target.x, target.y, target.membership);
        markPending(word);
    };

    const assignOutside = word => {
        removePlacedWord(word);
        assignments.set(word, '');
        const source = wordList.querySelector(`.word-rings-word[data-word="${CSS.escape(word)}"]`);
        if (!source) return;

        source.hidden = true;
        source.classList.add('is-placed');
        const token = source.cloneNode(true);
        token.hidden = false;
        token.classList.remove('is-placed', 'is-dragging', 'is-correct', 'is-wrong');
        token.classList.add('is-outside');
        token.dataset.membership = '';
        token.draggable = false;
        applyVerdict(token, word);
        outsideList.append(token);
        wireWord?.(token);
        markPending(word);
    };

    const returnToBank = word => {
        if (verdicts.has(word)) return;
        assignments.delete(word);
        removePlacedWord(word);
        if (pendingWord === word) {
            pendingWord = null;
        }
    };

    const canBegin = word => verdicts.has(word) || pendingWord === null || pendingWord === word;

    const canDropStage = (word, membership) => {
        const normalized = canonical(membership);
        if (verdicts.has(word)) {
            return lockedMemberships.get(word) === normalized;
        }
        return pendingWord === null || pendingWord === word;
    };

    const canDropOutside = word => canDropStage(word, '');
    const canReturnToBank = word => !verdicts.has(word);

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

    resetButton.addEventListener('click', () => {
        assignments.clear();
        verdicts.clear();
        lockedMemberships.clear();
        pendingWord = null;
        placedLayer.innerHTML = '';
        outsideList.innerHTML = '';
        wordList.querySelectorAll('.word-rings-word').forEach(word => {
            word.hidden = false;
            word.draggable = false;
            word.style.zIndex = '';
            word.classList.remove('is-correct', 'is-wrong', 'is-placed', 'is-dragging');
        });
        clearStatus();
        updateProgress();
    });

    checkButton.addEventListener('click', () => {
        if (pendingWord === null || !assignments.has(pendingWord)) return;

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
        const errors = [...verdicts.values()].filter(result => !result).length;
        status.classList.toggle('is-success', verdicts.size === expected.size && errors === 0);
        status.classList.toggle('is-error', errors > 0);

        if (verdicts.size === expected.size) {
            status.textContent = errors === 0
                ? root.dataset.allCorrect
                : root.dataset.hasErrorsTemplate.replace('{0}', errors);
        } else if (correct) {
            status.textContent = '';
        } else {
            status.textContent = root.dataset.hasErrorsTemplate.replace('{0}', errors);
        }

        updateProgress();
    });

    updateProgress();
})();
