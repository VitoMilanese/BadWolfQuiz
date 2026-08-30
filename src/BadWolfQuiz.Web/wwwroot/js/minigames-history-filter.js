(() => {
    const root = document.querySelector('[data-minigames-root]');
    if (!root) return;

    const history = root.querySelector('[data-question-history]');
    const filters = root.querySelector('[data-question-history-filters]');
    if (!history || !filters) return;

    const buttons = [...filters.querySelectorAll('[data-history-filter]')];
    if (buttons.length === 0) return;

    const text = {
        playerOne: root.dataset.playerOne ?? 'Player 1',
        playerTwo: root.dataset.playerTwo ?? 'Player 2',
        questionEntry: root.dataset.questionHistoryEntry ?? '{player}: {question}',
        answerEntry: root.dataset.historyQuestionAnswer ?? '{player} answered - {answer}',
        yes: root.dataset.yes ?? 'YES',
        no: root.dataset.no ?? 'NO',
        empty: root.dataset.questionHistoryEmpty ?? ''
    };

    let mode = 'all';
    let sourceNodes = [];

    const format = (template, replacements) => {
        let result = template;
        Object.entries(replacements).forEach(([key, value]) => {
            result = result.replaceAll(`{${key}}`, String(value));
        });
        return result;
    };

    const playerName = playerNumber => playerNumber === 1
        ? text.playerOne
        : text.playerTwo;

    const playerNumberFromItem = item => {
        if (item.classList.contains('is-player-1')) return 1;
        if (item.classList.contains('is-player-2')) return 2;
        return 0;
    };

    const extractTemplateValue = (template, placeholder, replacements, value) => {
        const token = `{${placeholder}}`;
        const partiallyFormatted = format(template, replacements);
        const tokenIndex = partiallyFormatted.indexOf(token);
        if (tokenIndex < 0) return null;

        const prefix = partiallyFormatted.slice(0, tokenIndex);
        const suffix = partiallyFormatted.slice(tokenIndex + token.length);
        if (!value.startsWith(prefix) || !value.endsWith(suffix)) return null;

        const endIndex = suffix.length > 0 ? value.length - suffix.length : value.length;
        if (endIndex < prefix.length) return null;
        return value.slice(prefix.length, endIndex);
    };

    const parseHistoryItem = item => {
        const player = playerNumberFromItem(item);
        if (!player) return null;

        const playerLabel = playerName(player);
        const value = item.textContent?.trim() ?? '';
        const yesText = format(text.answerEntry, { player: playerLabel, answer: text.yes });
        const noText = format(text.answerEntry, { player: playerLabel, answer: text.no });
        if (value === yesText) {
            return { kind: 'answer', player, value: text.yes };
        }
        if (value === noText) {
            return { kind: 'answer', player, value: text.no };
        }

        const question = extractTemplateValue(
            text.questionEntry,
            'question',
            { player: playerLabel },
            value);
        return question === null
            ? null
            : { kind: 'question', player, value: question };
    };

    const buildQuestionAnswerPairs = askingPlayer => {
        const sourceList = sourceNodes
            .map(node => node.matches?.('.minigames-question-history-list') ? node : null)
            .find(Boolean);
        if (!sourceList) return [];

        const pairs = [];
        let pendingQuestion = null;
        sourceList.querySelectorAll(':scope > li').forEach(item => {
            const entry = parseHistoryItem(item);
            if (!entry) return;

            if (entry.kind === 'question') {
                pendingQuestion = entry.player === askingPlayer ? entry : null;
                return;
            }

            if (entry.kind === 'answer' &&
                pendingQuestion &&
                entry.player !== pendingQuestion.player) {
                pairs.push({ question: pendingQuestion, answer: entry });
                pendingQuestion = null;
            }
        });
        return pairs;
    };

    const createEmptyState = () => {
        const empty = document.createElement('p');
        empty.className = 'minigames-question-history-empty is-filtered-projection';
        empty.textContent = text.empty;
        return empty;
    };

    const createPairList = pairs => {
        const list = document.createElement('ol');
        list.className = 'minigames-question-history-list is-filtered-pairs';
        pairs.forEach(pair => {
            const item = document.createElement('li');
            item.className = `is-asker-${pair.question.player}`;

            const question = document.createElement('span');
            question.className = `minigames-history-pair-part is-player-${pair.question.player}`;
            question.textContent = pair.question.value;

            const separator = document.createElement('span');
            separator.className = 'minigames-history-pair-separator';
            separator.textContent = ' - ';

            const answer = document.createElement('span');
            answer.className = `minigames-history-pair-part is-player-${pair.answer.player}`;
            answer.textContent = pair.answer.value;

            item.append(question, separator, answer);
            list.appendChild(item);
        });
        return list;
    };

    const scrollToLatest = () => {
        requestAnimationFrame(() => {
            history.scrollTop = history.scrollHeight;
        });
    };

    const render = () => {
        buttons.forEach(button => {
            const active = button.dataset.historyFilter === mode;
            button.classList.toggle('is-active', active);
            button.setAttribute('aria-pressed', active ? 'true' : 'false');
        });

        if (mode === 'all') {
            history.replaceChildren(...sourceNodes.map(node => node.cloneNode(true)));
            scrollToLatest();
            return;
        }

        const askingPlayer = mode === '1-to-2' ? 1 : 2;
        const pairs = buildQuestionAnswerPairs(askingPlayer);
        history.replaceChildren(pairs.length > 0 ? createPairList(pairs) : createEmptyState());
        scrollToLatest();
    };

    const captureSource = () => {
        const projection = history.firstElementChild;
        if (projection?.matches('.is-filtered-pairs, .is-filtered-projection')) return false;
        sourceNodes = [...history.childNodes].map(node => node.cloneNode(true));
        return true;
    };

    buttons.forEach(button => {
        button.addEventListener('click', () => {
            const nextMode = button.dataset.historyFilter;
            if (!['1-to-2', '2-to-1', 'all'].includes(nextMode) || nextMode === mode) return;
            if (mode === 'all') captureSource();
            mode = nextMode;
            render();
        });
    });

    captureSource();
    const observer = new MutationObserver(() => {
        if (!captureSource()) return;
        if (mode !== 'all') render();
    });
    observer.observe(history, { childList: true });
})();
