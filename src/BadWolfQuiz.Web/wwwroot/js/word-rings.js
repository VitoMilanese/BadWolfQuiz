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
    let wireWord = null;

    const canonical = value => [...value].sort().join('');
    const progressText = () => root.dataset.progressTemplate
        .replace('{0}', assignments.size)
        .replace('{1}', expected.size);

    const updateProgress = () => {
        progress.textContent = progressText();
        checkButton.disabled = assignments.size !== expected.size;
    };

    const resetFeedback = () => {
        status.textContent = '';
        status.classList.remove('is-success', 'is-error');
        root.querySelectorAll('.word-rings-word').forEach(word => word.classList.remove('is-correct', 'is-wrong'));
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

    const assignToStage = (word, clientX, clientY) => {
        removePlacedWord(word);
        const target = membershipAt(clientX, clientY);
        assignments.set(word, target.membership);
        createPlacedWord(word, target.x, target.y, target.membership);
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
        outsideList.append(token);
        wireWord?.(token);
    };

    const returnToBank = word => {
        assignments.delete(word);
        removePlacedWord(word);
    };

    const onPlacementChanged = () => {
        resetFeedback();
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
        placedLayer.innerHTML = '';
        outsideList.innerHTML = '';
        wordList.querySelectorAll('.word-rings-word').forEach(word => {
            word.hidden = false;
            word.draggable = false;
            word.classList.remove('is-correct', 'is-wrong', 'is-placed', 'is-dragging');
        });
        resetFeedback();
        updateProgress();
    });

    checkButton.addEventListener('click', () => {
        if (assignments.size !== expected.size) {
            status.textContent = root.dataset.placeAll;
            status.classList.add('is-error');
            return;
        }

        let errors = 0;
        assignments.forEach((actual, word) => {
            const correct = canonical(expected.get(word) ?? '') === canonical(actual);
            if (!correct) errors += 1;
            root.querySelectorAll(`.word-rings-word[data-word="${CSS.escape(word)}"]`).forEach(token => {
                token.classList.toggle('is-correct', correct);
                token.classList.toggle('is-wrong', !correct);
            });
        });

        status.classList.toggle('is-success', errors === 0);
        status.classList.toggle('is-error', errors !== 0);
        status.textContent = errors === 0
            ? root.dataset.allCorrect
            : root.dataset.hasErrorsTemplate.replace('{0}', errors);
    });

    updateProgress();
})();
