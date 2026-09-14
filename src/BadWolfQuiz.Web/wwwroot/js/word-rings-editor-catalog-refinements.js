(() => {
    'use strict';

    const rootSelector = '[data-word-rings-editor-shell]';
    const wordCatalogPath = '/Admin/WordRingsWordCatalog';
    const rulePageSize = 25;
    const ringStates = new Map();
    const originalFetch = window.fetch.bind(window);
    let queued = false;

    const language = () => (document.documentElement.lang || 'uk').toLowerCase();
    const fold = value => String(value ?? '').trim().toLocaleLowerCase(document.documentElement.lang || 'uk');

    const labels = () => {
        const current = language();
        if (current.startsWith('ru')) {
            return new Proxy({}, { get: () => 'Україна' });
        }
        if (current.startsWith('it')) {
            return {
                direction: 'Ordine', ascending: 'Crescente', descending: 'Decrescente',
                search: 'Cerca', searchRulesAndWords: 'Cerca condizione o parola',
                status: 'Stato', all: 'Tutte', active: 'Attive', inactive: 'Inattive',
                previous: 'Indietro', next: 'Avanti', page: 'Pagina', of: 'di', pagination: 'Pagine',
                noMatches: 'Nessun risultato.'
            };
        }
        if (current.startsWith('en')) {
            return {
                direction: 'Order', ascending: 'Ascending', descending: 'Descending',
                search: 'Search', searchRulesAndWords: 'Search condition or word',
                status: 'Status', all: 'All', active: 'Active', inactive: 'Inactive',
                previous: 'Previous', next: 'Next', page: 'Page', of: 'of', pagination: 'Pages',
                noMatches: 'No matches.'
            };
        }
        return {
            direction: 'Порядок', ascending: 'За зростанням', descending: 'За спаданням',
            search: 'Пошук', searchRulesAndWords: 'Пошук умови або слова',
            status: 'Статус', all: 'Усі', active: 'Активні', inactive: 'Неактивні',
            previous: 'Назад', next: 'Далі', page: 'Сторінка', of: 'з', pagination: 'Сторінки',
            noMatches: 'Нічого не знайдено.'
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
        pager.className = `minigame-editor-pager word-rings-editor-word-pager word-rings-editor-rule-pager word-rings-editor-catalog-pager word-rings-editor-catalog-pager-${position}`;
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

    const makeSelectControl = (captionText, dataName, options, value) => {
        const label = document.createElement('label');
        label.className = 'word-rings-editor-catalog-control word-rings-editor-catalog-control-compact';

        const caption = document.createElement('span');
        caption.textContent = captionText;

        const select = document.createElement('select');
        select.dataset[dataName] = '';
        options.forEach(([optionValue, optionText]) => {
            const option = document.createElement('option');
            option.value = optionValue;
            option.textContent = optionText;
            select.appendChild(option);
        });
        select.value = value;
        label.append(caption, select);
        return { label, select };
    };

    window.fetch = (input, init) => {
        try {
            const requestUrl = input instanceof URL
                ? new URL(input.href)
                : typeof input === 'string'
                    ? new URL(input, window.location.origin)
                    : null;
            if (requestUrl && requestUrl.origin === window.location.origin && requestUrl.pathname === wordCatalogPath) {
                const pageUrl = new URL(window.location.href);
                requestUrl.searchParams.set(
                    'direction',
                    pageUrl.searchParams.get('direction') === 'desc' ? 'desc' : 'asc');
                input = input instanceof URL ? requestUrl : requestUrl.toString();
            }
        } catch {
            // Keep the original request untouched if URL parsing fails.
        }
        return originalFetch(input, init);
    };

    const enhanceWordDirection = root => {
        if (root.dataset.activeRing !== 'words' || root.dataset.wordDirectionReady === 'true') return;
        const controls = root.querySelector('[data-word-rings-word-catalog-controls]');
        const sortSelect = controls?.querySelector('[data-word-rings-word-sort]');
        if (!(controls instanceof HTMLElement) || !(sortSelect instanceof HTMLSelectElement)) return;

        root.dataset.wordDirectionReady = 'true';
        const text = labels();
        const pageUrl = new URL(window.location.href);
        const currentDirection = pageUrl.searchParams.get('direction') === 'desc' ? 'desc' : 'asc';
        const { label, select } = makeSelectControl(
            text.direction,
            'wordRingsWordDirection',
            [['asc', text.ascending], ['desc', text.descending]],
            currentDirection);
        controls.appendChild(label);

        select.addEventListener('change', () => {
            const url = new URL(window.location.href);
            if (select.value === 'desc') url.searchParams.set('direction', 'desc');
            else url.searchParams.delete('direction');
            url.searchParams.delete('pageNumber');
            window.history.replaceState(window.history.state, '', `${url.pathname}${url.search}${url.hash}`);
            sortSelect.dispatchEvent(new Event('change', { bubbles: true }));
        });

        if (currentDirection === 'desc') {
            sortSelect.dispatchEvent(new Event('change', { bubbles: true }));
        }
    };

    const isRuleActive = card => {
        const checkbox = card.querySelector('[data-word-rings-rule-enabled]');
        return checkbox instanceof HTMLInputElement ? checkbox.checked : !card.classList.contains('is-disabled');
    };

    const ruleMatches = (card, query, status) => {
        if (status === 'active' && !isRuleActive(card)) return false;
        if (status === 'inactive' && isRuleActive(card)) return false;
        if (!query) return true;

        const condition = card.querySelector('h3')?.textContent || '';
        const words = card.querySelector('.word-rings-editor-rule-words p')?.textContent || '';
        return fold(condition).includes(query) || fold(words).includes(query);
    };

    const renderRingRules = (root, ring, cards, state) => {
        root.querySelectorAll('.word-rings-editor-rule-pager').forEach(node => node.remove());
        root.querySelectorAll('.word-rings-editor-rule-filter-empty').forEach(node => node.remove());
        const list = root.querySelector('.word-rings-editor-rule-list');
        if (!(list instanceof HTMLElement)) return;

        const query = fold(state.query);
        const filtered = cards.filter(card => ruleMatches(card, query, state.status));
        const totalPages = Math.max(1, Math.ceil(filtered.length / rulePageSize));
        state.page = Math.min(Math.max(state.page, 1), totalPages);
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

        const start = (state.page - 1) * rulePageSize;
        filtered.slice(start, start + rulePageSize).forEach(card => { card.hidden = false; });

        if (filtered.length > rulePageSize) {
            const go = target => {
                state.page = target;
                renderRingRules(root, ring, cards, state);
            };
            list.before(makePager('top', state.page, totalPages, go));
            list.after(makePager('bottom', state.page, totalPages, go));
        }
    };

    const enhanceRingFilters = root => {
        const ring = root.dataset.activeRing || '';
        if (!['blue', 'yellow', 'red'].includes(ring) || root.dataset.ringRefinementsReady === 'true') return;
        if (root.dataset.ringCatalogReady !== 'true') return;

        const toolbar = root.querySelector(`.word-rings-editor-toolbar.word-rings-editor-ring-${ring}`);
        const list = root.querySelector('.word-rings-editor-rule-list');
        if (!(toolbar instanceof HTMLElement) || !(list instanceof HTMLElement)) return;

        root.dataset.ringRefinementsReady = 'true';
        root.querySelector('[data-word-rings-ring-catalog-controls]')?.remove();
        root.querySelectorAll('.word-rings-editor-rule-pager').forEach(node => node.remove());
        root.querySelectorAll('.word-rings-editor-rule-filter-empty').forEach(node => node.remove());

        const cards = [...root.querySelectorAll('.word-rings-editor-rule-card')]
            .filter(card => card instanceof HTMLElement);
        cards.forEach(card => { card.hidden = false; });
        if (cards.length === 0) return;

        const text = labels();
        const state = ringStates.get(ring) || { query: '', status: 'all', page: 1 };
        ringStates.set(ring, state);

        const controls = document.createElement('div');
        controls.className = 'word-rings-editor-catalog-controls';
        controls.dataset.wordRingsRingRefinementControls = '';

        const searchLabel = document.createElement('label');
        searchLabel.className = 'word-rings-editor-catalog-control';
        const searchCaption = document.createElement('span');
        searchCaption.textContent = text.search;
        const searchInput = document.createElement('input');
        searchInput.type = 'search';
        searchInput.autocomplete = 'off';
        searchInput.placeholder = text.searchRulesAndWords;
        searchInput.value = state.query;
        searchInput.dataset.wordRingsRuleSearch = '';
        searchLabel.append(searchCaption, searchInput);
        controls.appendChild(searchLabel);

        const statusControl = makeSelectControl(
            text.status,
            'wordRingsRuleStatus',
            [['all', text.all], ['active', text.active], ['inactive', text.inactive]],
            state.status);
        controls.appendChild(statusControl.label);
        toolbar.after(controls);

        searchInput.addEventListener('input', () => {
            state.query = searchInput.value;
            state.page = 1;
            renderRingRules(root, ring, cards, state);
        });
        statusControl.select.addEventListener('change', () => {
            state.status = statusControl.select.value;
            state.page = 1;
            renderRingRules(root, ring, cards, state);
        });

        renderRingRules(root, ring, cards, state);
    };

    const enhance = () => {
        queued = false;
        document.querySelectorAll('.word-rings-editor-mark-caption').forEach(node => node.remove());
        const root = document.querySelector(rootSelector);
        if (!(root instanceof HTMLElement)) return;
        enhanceWordDirection(root);
        enhanceRingFilters(root);
    };

    const queueEnhance = () => {
        if (queued) return;
        queued = true;
        window.setTimeout(enhance, 0);
    };

    new MutationObserver(queueEnhance).observe(document.body, { childList: true, subtree: true });
    enhance();
})();
