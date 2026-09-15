(() => {
    'use strict';

    const rootSelector = '[data-word-rings-editor-shell]';
    const rulePageSize = 25;
    const wordCatalogUrl = '/Admin/WordRingsWordCatalog';
    const ringPages = new Map();
    const ringFilters = new Map();
    let enhanceQueued = false;
    let wordCatalogAbort = null;
    let wordFilterTimer = 0;

    const language = () => (document.documentElement.lang || 'uk').toLowerCase();
    const isRussian = () => language().startsWith('ru');
    const upper = value => String(value ?? '').toLocaleUpperCase(document.documentElement.lang || 'uk');
    const fold = value => String(value ?? '').trim().toLocaleLowerCase(document.documentElement.lang || 'uk');

    const labels = () => {
        const current = language();
        if (current.startsWith('ru')) {
            return new Proxy({}, { get: () => 'Україна' });
        }
        if (current.startsWith('it')) {
            return {
                total: 'Totale', previous: 'Indietro', next: 'Avanti', page: 'Pagina', of: 'di',
                pagination: 'Pagine', searchWords: 'Cerca parola', searchRules: 'Cerca regola',
                search: 'Cerca', sort: 'Ordina', alphabetical: 'Alfabetico', usage: 'Numero di regole',
                blue: 'Anello blu', yellow: 'Anello giallo', red: 'Anello rosso',
                editWord: 'Modifica parola', deleteWord: 'Elimina parola', noMatches: 'Nessun risultato.',
                noWords: 'Nessuna parola.', rules: 'Regole', active: 'Attive'
            };
        }
        if (current.startsWith('en')) {
            return {
                total: 'Total', previous: 'Previous', next: 'Next', page: 'Page', of: 'of',
                pagination: 'Pages', searchWords: 'Search word', searchRules: 'Search rule',
                search: 'Search', sort: 'Sort', alphabetical: 'Alphabetical', usage: 'Rule count',
                blue: 'Blue ring', yellow: 'Yellow ring', red: 'Red ring',
                editWord: 'Edit word', deleteWord: 'Delete word', noMatches: 'No matches.',
                noWords: 'No words.', rules: 'Rules', active: 'Active'
            };
        }
        return {
            total: 'Усього', previous: 'Назад', next: 'Далі', page: 'Сторінка', of: 'з',
            pagination: 'Сторінки', searchWords: 'Пошук слова', searchRules: 'Пошук правила',
            search: 'Пошук', sort: 'Сортування', alphabetical: 'За алфавітом', usage: 'За кількістю правил',
            blue: 'Синє кільце', yellow: 'Жовте кільце', red: 'Червоне кільце',
            editWord: 'Редагувати слово', deleteWord: 'Видалити слово', noMatches: 'Нічого не знайдено.',
            noWords: 'Немає слів.', rules: 'Правил', active: 'Активних'
        };
    };

    const buildPageNumbers = (currentPage, totalPages) => {
        if (totalPages <= 7) return Array.from({ length: totalPages }, (_, index) => index + 1);
        if (currentPage <= 4) return [1, 2, 3, 4, 5, null, totalPages];
        if (currentPage >= totalPages - 3) {
            return [1, null, totalPages - 4, totalPages - 3, totalPages - 2, totalPages - 1, totalPages];
        }
        return [1, null, currentPage - 1, currentPage, currentPage + 1, null, totalPages];
    };

    const parseUsageCount = element => {
        const match = element?.textContent?.match(/(\d+)\s*$/u);
        return match ? Number.parseInt(match[1], 10) : 0;
    };

    const enhanceWordUsageTotals = root => {
        root.querySelectorAll('.word-rings-editor-word-usage').forEach(usage => {
            if (!(usage instanceof HTMLElement) || usage.querySelector('.word-rings-editor-word-usage-total')) return;
            const total = [
                '.word-rings-editor-word-usage-blue',
                '.word-rings-editor-word-usage-yellow',
                '.word-rings-editor-word-usage-red'
            ].reduce((sum, selector) => sum + parseUsageCount(usage.querySelector(selector)), 0);
            const badge = document.createElement('span');
            badge.className = 'word-rings-editor-word-usage-total';
            badge.textContent = `${labels().total} · ${total}`;
            usage.prepend(badge);
        });
    };

    const makePageButton = (text, page, currentPage, className = '') => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = `button button-secondary ${className}`.trim();
        button.textContent = text;
        if (page === currentPage) {
            button.classList.add('is-current');
            button.setAttribute('aria-current', 'page');
            button.disabled = true;
        }
        return button;
    };

    const makePager = (position, currentPage, totalPages, onPage) => {
        const text = labels();
        const pager = document.createElement('nav');
        pager.className = `minigame-editor-pager word-rings-editor-word-pager word-rings-editor-catalog-pager word-rings-editor-catalog-pager-${position}`;
        if (position === 'top') pager.classList.add('word-rings-editor-word-pager-top');
        pager.setAttribute('aria-label', text.pagination);

        const previous = makePageButton(text.previous, Math.max(1, currentPage - 1), currentPage);
        previous.disabled = currentPage <= 1;
        previous.addEventListener('click', () => onPage(currentPage - 1));
        pager.appendChild(previous);

        const numbers = document.createElement('div');
        numbers.className = 'word-rings-editor-page-numbers';
        buildPageNumbers(currentPage, totalPages).forEach(page => {
            if (page === null) {
                const ellipsis = document.createElement('span');
                ellipsis.className = 'word-rings-editor-page-ellipsis';
                ellipsis.textContent = '…';
                ellipsis.setAttribute('aria-hidden', 'true');
                numbers.appendChild(ellipsis);
                return;
            }
            const button = makePageButton(String(page), page, currentPage, 'word-rings-editor-page-number');
            if (page !== currentPage) button.addEventListener('click', () => onPage(page));
            numbers.appendChild(button);
        });
        pager.appendChild(numbers);

        const status = document.createElement('span');
        status.className = 'minigame-editor-pager-status';
        status.textContent = `${text.page} ${currentPage} ${text.of} ${totalPages}`;
        pager.appendChild(status);

        const next = makePageButton(text.next, Math.min(totalPages, currentPage + 1), currentPage);
        next.disabled = currentPage >= totalPages;
        next.addEventListener('click', () => onPage(currentPage + 1));
        pager.appendChild(next);
        return pager;
    };

    const createControls = (searchLabel, searchPlaceholder, includeSort) => {
        const text = labels();
        const controls = document.createElement('div');
        controls.className = 'word-rings-editor-catalog-controls';

        const searchWrap = document.createElement('label');
        searchWrap.className = 'word-rings-editor-catalog-control';
        const caption = document.createElement('span');
        caption.textContent = searchLabel;
        const input = document.createElement('input');
        input.type = 'search';
        input.autocomplete = 'off';
        input.placeholder = searchPlaceholder;
        input.dataset.wordRingsCatalogSearch = '';
        searchWrap.append(caption, input);
        controls.appendChild(searchWrap);

        if (includeSort) {
            const sortWrap = document.createElement('label');
            sortWrap.className = 'word-rings-editor-catalog-control';
            const sortCaption = document.createElement('span');
            sortCaption.textContent = text.sort;
            const select = document.createElement('select');
            select.dataset.wordRingsWordSort = '';
            const alphabetical = document.createElement('option');
            alphabetical.value = 'alphabetical';
            alphabetical.textContent = text.alphabetical;
            const usage = document.createElement('option');
            usage.value = 'usage';
            usage.textContent = text.usage;
            select.append(alphabetical, usage);
            sortWrap.append(sortCaption, select);
            controls.appendChild(sortWrap);
        }

        return controls;
    };

    const wordRow = item => {
        const text = labels();
        const displayWord = upper(item.word);
        const article = document.createElement('article');
        article.className = 'word-rings-editor-word-row';
        article.dataset.wordRingsWordRow = '';
        article.dataset.word = displayWord;

        const main = document.createElement('div');
        main.className = 'word-rings-editor-word-main';
        const strong = document.createElement('strong');
        strong.textContent = displayWord;
        const usage = document.createElement('div');
        usage.className = 'word-rings-editor-word-usage';
        const values = [
            ['word-rings-editor-word-usage-total', text.total, item.totalRuleCount],
            ['word-rings-editor-word-usage-blue', text.blue, item.blueRuleCount],
            ['word-rings-editor-word-usage-yellow', text.yellow, item.yellowRuleCount],
            ['word-rings-editor-word-usage-red', text.red, item.redRuleCount]
        ];
        values.forEach(([className, label, value]) => {
            const span = document.createElement('span');
            span.className = className;
            span.textContent = `${label} · ${value}`;
            usage.appendChild(span);
        });
        main.append(strong, usage);

        const actions = document.createElement('div');
        actions.className = 'word-rings-editor-word-actions';
        const edit = document.createElement('button');
        edit.type = 'button';
        edit.className = 'button word-rings-rule-edit';
        edit.dataset.editWordRingsWord = '';
        edit.dataset.word = displayWord;
        edit.setAttribute('aria-label', text.editWord);
        edit.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true" focusable="false"><path d="M4 20h4l10.5-10.5a2.1 2.1 0 0 0-4-4L4 16v4Z"></path><path d="m13.5 6.5 4 4"></path></svg>';
        const remove = document.createElement('button');
        remove.type = 'button';
        remove.className = 'button word-rings-rule-delete';
        remove.dataset.deleteWordRingsWord = '';
        remove.dataset.word = displayWord;
        remove.setAttribute('aria-label', text.deleteWord);
        remove.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true" focusable="false"><path d="M3 6h18M8 6V4h8v2M19 6l-1 14H6L5 6M10 11v5M14 11v5"></path></svg>';
        actions.append(edit, remove);
        article.append(main, actions);
        return article;
    };

    const updateWordUrl = (search, sort, pageNumber) => {
        const url = new URL(window.location.href);
        url.searchParams.set('ring', 'words');
        if (search) url.searchParams.set('search', search); else url.searchParams.delete('search');
        if (sort === 'usage') url.searchParams.set('sort', 'usage'); else url.searchParams.delete('sort');
        if (pageNumber > 1) url.searchParams.set('pageNumber', String(pageNumber)); else url.searchParams.delete('pageNumber');
        window.history.replaceState({ wordRingsTab: 'words' }, '', `${url.pathname}${url.search}${url.hash}`);
    };

    const renderWordCatalog = (root, result, search, sort) => {
        root.querySelectorAll('.word-rings-editor-word-pager').forEach(node => node.remove());
        root.querySelectorAll('.word-rings-editor-catalog-empty').forEach(node => node.remove());
        const serverEmpty = root.querySelector('.word-rings-editor-empty');
        if (serverEmpty instanceof HTMLElement) serverEmpty.hidden = true;

        let list = root.querySelector('.word-rings-editor-word-list');
        if (!(list instanceof HTMLElement)) {
            list = document.createElement('section');
            list.className = 'word-rings-editor-word-list';
            const controls = root.querySelector('[data-word-rings-word-catalog-controls]');
            controls?.after(list);
        }

        const items = Array.isArray(result.items) ? result.items : [];
        list.replaceChildren(...items.map(wordRow));
        list.hidden = items.length === 0;

        if (items.length === 0) {
            const empty = document.createElement('section');
            empty.className = 'content-panel empty-state word-rings-editor-catalog-empty';
            const heading = document.createElement('h2');
            heading.textContent = result.totalCount === 0 && !search ? labels().noWords : labels().noMatches;
            empty.appendChild(heading);
            list.after(empty);
        }

        if (result.totalPages > 1 && items.length > 0) {
            const go = page => loadWordCatalog(root, search, sort, page, true);
            list.before(makePager('top', result.pageNumber, result.totalPages, go));
            list.after(makePager('bottom', result.pageNumber, result.totalPages, go));
        }
    };

    const loadWordCatalog = async (root, search, sort, pageNumber, updateUrl) => {
        wordCatalogAbort?.abort();
        wordCatalogAbort = new AbortController();
        const url = new URL(wordCatalogUrl, window.location.origin);
        if (search) url.searchParams.set('search', search);
        url.searchParams.set('sort', sort);
        url.searchParams.set('pageNumber', String(pageNumber));
        const list = root.querySelector('.word-rings-editor-word-list');
        list?.setAttribute('aria-busy', 'true');

        try {
            const response = await fetch(url, {
                method: 'GET',
                headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
                credentials: 'same-origin',
                signal: wordCatalogAbort.signal
            });
            if (!response.ok) throw new Error(`Word catalog failed: ${response.status}`);
            const result = await response.json();
            renderWordCatalog(root, result, search, sort);
            if (updateUrl) updateWordUrl(search, sort, result.pageNumber);
        } catch (error) {
            if (error?.name !== 'AbortError') {
                window.badWolfShowEditorStatus?.(root.dataset.requestFailed || labels().noMatches, true);
            }
        } finally {
            list?.removeAttribute('aria-busy');
        }
    };

    const enhanceWordCatalog = root => {
        if (root.dataset.activeRing !== 'words' || root.dataset.wordCatalogReady === 'true') return;
        root.dataset.wordCatalogReady = 'true';
        enhanceWordUsageTotals(root);

        const toolbar = root.querySelector('.word-rings-editor-toolbar-words');
        if (!(toolbar instanceof HTMLElement)) return;
        const controls = createControls(labels().search, labels().searchWords, true);
        controls.dataset.wordRingsWordCatalogControls = '';
        toolbar.after(controls);

        const url = new URL(window.location.href);
        const search = (url.searchParams.get('search') || '').trim();
        const sort = url.searchParams.get('sort') === 'usage' ? 'usage' : 'alphabetical';
        const pageNumber = Math.max(1, Number.parseInt(url.searchParams.get('pageNumber') || '1', 10) || 1);
        const input = controls.querySelector('[data-word-rings-catalog-search]');
        const select = controls.querySelector('[data-word-rings-word-sort]');
        if (input instanceof HTMLInputElement) input.value = search;
        if (select instanceof HTMLSelectElement) select.value = sort;

        input?.addEventListener('input', () => {
            window.clearTimeout(wordFilterTimer);
            wordFilterTimer = window.setTimeout(() => {
                const value = input instanceof HTMLInputElement ? input.value.trim() : '';
                const order = select instanceof HTMLSelectElement ? select.value : 'alphabetical';
                void loadWordCatalog(root, value, order, 1, true);
            }, 250);
        });
        select?.addEventListener('change', () => {
            const value = input instanceof HTMLInputElement ? input.value.trim() : '';
            const order = select instanceof HTMLSelectElement ? select.value : 'alphabetical';
            void loadWordCatalog(root, value, order, 1, true);
        });

        void loadWordCatalog(root, search, sort, pageNumber, false);
    };

    const renderRingRules = (root, ring, cards, filterValue, page) => {
        root.querySelectorAll('.word-rings-editor-rule-pager').forEach(node => node.remove());
        root.querySelectorAll('.word-rings-editor-rule-filter-empty').forEach(node => node.remove());
        const list = root.querySelector('.word-rings-editor-rule-list');
        if (!(list instanceof HTMLElement)) return;

        const query = fold(filterValue);
        const filtered = cards.filter(card => {
            const text = card.querySelector('h3')?.textContent || '';
            return !query || fold(text).includes(query);
        });
        const totalPages = Math.max(1, Math.ceil(filtered.length / rulePageSize));
        const currentPage = Math.min(Math.max(page, 1), totalPages);
        ringPages.set(ring, currentPage);
        cards.forEach(card => { card.hidden = true; });

        if (filtered.length === 0) {
            const empty = document.createElement('section');
            empty.className = 'content-panel empty-state word-rings-editor-rule-filter-empty';
            const heading = document.createElement('h2');
            heading.textContent = labels().noMatches;
            empty.appendChild(heading);
            list.after(empty);
            return;
        }

        const start = (currentPage - 1) * rulePageSize;
        filtered.slice(start, start + rulePageSize).forEach(card => { card.hidden = false; });
        if (filtered.length > rulePageSize) {
            const go = target => renderRingRules(root, ring, cards, filterValue, target);
            const top = makePager('top', currentPage, totalPages, go);
            const bottom = makePager('bottom', currentPage, totalPages, go);
            top.classList.add('word-rings-editor-rule-pager');
            bottom.classList.add('word-rings-editor-rule-pager');
            list.before(top);
            list.after(bottom);
        }
    };

    const enhanceRingRules = root => {
        const ring = root.dataset.activeRing || '';
        if (!['blue', 'yellow', 'red'].includes(ring) || root.dataset.ringCatalogReady === 'true') return;
        root.dataset.ringCatalogReady = 'true';

        const toolbar = root.querySelector(`.word-rings-editor-toolbar.word-rings-editor-ring-${ring}`);
        if (!(toolbar instanceof HTMLElement)) return;
        const cards = [...root.querySelectorAll('.word-rings-editor-rule-card')]
            .filter(card => card instanceof HTMLElement);

        const heading = toolbar.querySelector('h2');
        const headingWrap = heading?.parentElement;
        if (headingWrap instanceof HTMLElement) {
            headingWrap.classList.add('word-rings-editor-ring-heading');
            let stats = headingWrap.querySelector('.word-rings-editor-ring-stats');
            if (!(stats instanceof HTMLElement)) {
                stats = document.createElement('span');
                stats.className = 'muted word-rings-editor-ring-stats';
                heading?.after(stats);
            }
            const activeCount = cards.filter(card => card.querySelector('[data-word-rings-rule-enabled]')?.checked).length;
            stats.textContent = `${labels().rules}: ${cards.length} · ${labels().active}: ${activeCount}`;
        }

        if (cards.length === 0) return;
        const controls = createControls(labels().search, labels().searchRules, false);
        controls.dataset.wordRingsRingCatalogControls = '';
        toolbar.after(controls);
        const input = controls.querySelector('[data-word-rings-catalog-search]');
        const saved = ringFilters.get(ring) || '';
        if (input instanceof HTMLInputElement) input.value = saved;
        input?.addEventListener('input', () => {
            const value = input instanceof HTMLInputElement ? input.value : '';
            ringFilters.set(ring, value);
            ringPages.set(ring, 1);
            renderRingRules(root, ring, cards, value, 1);
        });
        renderRingRules(root, ring, cards, saved, ringPages.get(ring) || 1);
    };

    const patchRussianGeneratedUi = () => {
        if (!isRussian()) return;
        const ukraine = 'Україна';
        document.querySelectorAll('[data-word-rings-import-details-title], [data-word-rings-ring-filter-title], [data-word-rings-import-details-page-status], [data-word-rings-ring-filter-page-status]').forEach(node => {
            node.textContent = ukraine;
        });
        document.querySelectorAll('[data-word-rings-import-ring-filter]').forEach(button => {
            button.textContent = ukraine;
        });
        document.querySelectorAll('.word-rings-editor-import-rule-detail > div > span').forEach(node => {
            node.textContent = ukraine;
        });
        document.querySelectorAll('.word-rings-editor-import-detail-action > span, .word-rings-editor-import-rule-detail > p > span').forEach(node => {
            const bold = node.querySelector('b');
            if (bold) node.childNodes[0].textContent = `${ukraine}: `;
            else node.textContent = ukraine;
        });
        document.querySelectorAll('[data-word-rings-import-details-close], [data-word-rings-ring-filter-close]').forEach(button => {
            if (button.classList.contains('dialog-close')) {
                button.setAttribute('aria-label', ukraine);
            } else {
                button.textContent = ukraine;
            }
        });
        document.querySelectorAll('.word-rings-editor-import-details-page-button, .word-rings-editor-import-detail-view').forEach(button => {
            button.setAttribute('aria-label', ukraine);
            button.setAttribute('title', ukraine);
        });
        document.querySelectorAll('.word-rings-editor-import-details-body > p.muted').forEach(node => {
            node.textContent = ukraine;
        });
    };

    const enhance = () => {
        enhanceQueued = false;
        const root = document.querySelector(rootSelector);
        if (root instanceof HTMLElement) {
            enhanceWordCatalog(root);
            enhanceRingRules(root);
        }
        // Dynamic Word Rings UI localizes itself through labels().
    };

    const queueEnhance = () => {
        if (enhanceQueued) return;
        enhanceQueued = true;
        queueMicrotask(enhance);
    };

    new MutationObserver(queueEnhance).observe(document.body, { childList: true, subtree: true });
    enhance();
})();
