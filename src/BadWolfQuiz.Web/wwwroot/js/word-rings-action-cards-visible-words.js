(() => {
    const root = document.querySelector('[data-word-rings-root]');
    if (!(root instanceof HTMLElement)) return;

    const actionMeta = root.querySelector('.word-rings-action-meta');
    const wordList = root.querySelector('[data-word-list]');
    const wordSelect = root.querySelector('[data-action-word]');

    const language = (document.documentElement.lang || 'en').toLowerCase().split('-')[0];
    const progressPrefixes = {
        en: ['KPS ', 'Next card '],
        uk: ['КПС ', 'До картки '],
        it: ['KPS ', 'Prossima carta ']
    };

    const normalizeProgressLabel = () => {
        if (!(actionMeta instanceof HTMLElement)) return;
        const replacement = progressPrefixes[language];
        if (!replacement) return;
        const [legacyPrefix, clearPrefix] = replacement;
        const current = actionMeta.textContent || '';
        if (!current.startsWith(legacyPrefix)) return;
        actionMeta.textContent = clearPrefix + current.slice(legacyPrefix.length);
    };

    const normalizeWord = value => String(value || '').trim().toLocaleLowerCase(language || undefined);

    const visibleWordKeys = () => {
        if (!(wordList instanceof HTMLElement)) return new Set();
        return new Set(
            [...wordList.querySelectorAll('.word-rings-word[data-word]')]
                .filter(token => token instanceof HTMLElement &&
                    !token.hidden &&
                    token.closest('[hidden]') === null &&
                    window.getComputedStyle(token).display !== 'none')
                .map(token => normalizeWord(token.dataset.word))
                .filter(Boolean));
    };

    const filterWordOptionsToVisibleBank = () => {
        if (!(wordSelect instanceof HTMLSelectElement)) return;
        const visibleWords = visibleWordKeys();
        let changed = false;
        for (const option of [...wordSelect.options]) {
            if (visibleWords.has(normalizeWord(option.value))) continue;
            option.remove();
            changed = true;
        }
        if (changed && wordSelect.options.length > 0 && wordSelect.selectedIndex < 0) {
            wordSelect.selectedIndex = 0;
        }
    };

    const sync = () => {
        normalizeProgressLabel();
        filterWordOptionsToVisibleBank();
    };

    const observer = new MutationObserver(sync);
    if (actionMeta instanceof HTMLElement) {
        observer.observe(actionMeta, { childList: true, characterData: true, subtree: true });
    }
    if (wordSelect instanceof HTMLSelectElement) {
        observer.observe(wordSelect, { childList: true });
    }
    if (wordList instanceof HTMLElement) {
        observer.observe(wordList, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ['hidden', 'style']
        });
    }

    root.addEventListener('wordrings:bank-rendered', filterWordOptionsToVisibleBank);
    root.addEventListener('change', event => {
        if (!(event.target instanceof HTMLSelectElement) || !event.target.matches('[data-action-target]')) return;
        queueMicrotask(filterWordOptionsToVisibleBank);
    });

    sync();
})();
