(() => {
    'use strict';

    const importRuleDetailPageSize = 5;
    const previousFetch = window.fetch.bind(window);
    let latestExportSnapshot = null;
    let lastRuleDetails = emptyRuleDetails();
    let activeDetailType = '';
    let activeRingFilter = 'all';
    let activePage = 1;

    const language = () => (document.documentElement.lang || 'uk').toLowerCase();

    const labels = () => {
        const current = language();
        if (current.startsWith('ru')) {
            return new Proxy({}, { get: () => 'Україна' });
        }
        if (current.startsWith('it')) {
            return {
                all: 'Tutti gli anelli',
                blue: 'Anello blu',
                yellow: 'Anello giallo',
                red: 'Anello rosso',
                newRulesTitle: 'Nuove regole aggiunte',
                updatedRulesTitle: 'Regole esistenti con nuove parole',
                addedWords: 'Parole aggiunte',
                active: 'Attiva',
                inactive: 'Disattivata',
                close: 'Chiudi',
                empty: 'Nessun elemento per questo filtro.',
                page: 'Pagina',
                of: 'di',
                firstPage: 'Prima pagina',
                previousPage: 'Pagina precedente',
                nextPage: 'Pagina successiva',
                lastPage: 'Ultima pagina'
            };
        }
        if (current.startsWith('en')) {
            return {
                all: 'All rings',
                blue: 'Blue ring',
                yellow: 'Yellow ring',
                red: 'Red ring',
                newRulesTitle: 'New rules added',
                updatedRulesTitle: 'Existing rules with new words',
                addedWords: 'Words added',
                active: 'Active',
                inactive: 'Inactive',
                close: 'Close',
                empty: 'No items for this filter.',
                page: 'Page',
                of: 'of',
                firstPage: 'First page',
                previousPage: 'Previous page',
                nextPage: 'Next page',
                lastPage: 'Last page'
            };
        }
        return {
            all: 'Усі кільця',
            blue: 'Синє кільце',
            yellow: 'Жовте кільце',
            red: 'Червоне кільце',
            newRulesTitle: 'Нові правила, додані під час імпорту',
            updatedRulesTitle: 'Старі правила, в які додано нові слова',
            addedWords: 'Додано слів',
            active: 'Активне',
            inactive: 'Неактивне',
            close: 'Закрити',
            empty: 'Для цього фільтра немає елементів.',
            page: 'Сторінка',
            of: 'з',
            firstPage: 'Перша сторінка',
            previousPage: 'Попередня сторінка',
            nextPage: 'Наступна сторінка',
            lastPage: 'Остання сторінка'
        };
    };

    function emptyRuleDetails() {
        return {
            newRules: [],
            updatedExistingRules: []
        };
    }

    const fold = value => String(value ?? '').trim().toLocaleLowerCase('uk-UA');
    const wordFold = value => fold(value);
    const ruleKey = (ring, text) => `${fold(ring)}\u001f${fold(text)}`;

    const parseCsvLine = line => {
        const fields = [];
        let value = '';
        let quoted = false;

        for (let index = 0; index < line.length; index += 1) {
            const character = line[index];
            if (character === '"') {
                if (quoted && index + 1 < line.length && line[index + 1] === '"') {
                    value += '"';
                    index += 1;
                } else {
                    quoted = !quoted;
                }
                continue;
            }
            if (character === ',' && !quoted) {
                fields.push(value);
                value = '';
                continue;
            }
            value += character;
        }

        if (quoted) return null;
        fields.push(value);
        return fields;
    };

    const splitWords = value => String(value ?? '')
        .split(/[ ,;]+/u)
        .map(word => word.trim())
        .filter(Boolean);

    const parseConfigurationCsv = text => {
        if (!text) return null;
        const lines = String(text).replace(/^\uFEFF/, '').split(/\r?\n/u);
        const header = parseCsvLine(lines.shift() ?? '');
        if (!header || header.length !== 4 ||
            fold(header[0]) !== 'ring' ||
            fold(header[1]) !== 'rule' ||
            fold(header[2]) !== 'enabled' ||
            fold(header[3]) !== 'words') {
            return null;
        }

        const rules = new Map();
        for (const line of lines) {
            if (!line.trim()) continue;
            const fields = parseCsvLine(line);
            if (!fields || fields.length !== 4) return null;

            const ring = fold(fields[0]);
            const text = fields[1].trim();
            if (!['blue', 'yellow', 'red'].includes(ring) || !text) return null;

            const key = ruleKey(ring, text);
            let rule = rules.get(key);
            if (!rule) {
                rule = {
                    ring,
                    text,
                    enabled: fold(fields[2]) === 'true' || fields[2].trim() === '1',
                    words: new Map()
                };
                rules.set(key, rule);
            }

            for (const word of splitWords(fields[3])) {
                const keyWord = wordFold(word);
                if (keyWord && !rule.words.has(keyWord)) rule.words.set(keyWord, word);
            }
        }

        return rules;
    };

    const compareAlphabetically = (left, right) =>
        String(left ?? '').localeCompare(
            String(right ?? ''),
            document.documentElement.lang || 'uk',
            { sensitivity: 'base' });

    const sortWords = values => [...values].sort(compareAlphabetically);
    const sortRules = values => [...values].sort((left, right) =>
        compareAlphabetically(left.text, right.text) ||
        compareAlphabetically(left.ring, right.ring));

    const compareConfigurations = (beforeRules, importedRules) => {
        if (!(beforeRules instanceof Map) || !(importedRules instanceof Map)) {
            return emptyRuleDetails();
        }

        const newRules = [];
        const updatedExistingRules = [];
        importedRules.forEach((incoming, key) => {
            const existing = beforeRules.get(key);
            if (!existing) {
                newRules.push({
                    ring: incoming.ring,
                    text: incoming.text,
                    enabled: incoming.enabled,
                    wordsAdded: sortWords(incoming.words.values())
                });
                return;
            }

            const additions = [];
            incoming.words.forEach((word, wordKey) => {
                if (!existing.words.has(wordKey)) additions.push(word);
            });
            if (additions.length > 0) {
                updatedExistingRules.push({
                    ring: existing.ring,
                    text: existing.text,
                    enabled: incoming.enabled,
                    wordsAdded: sortWords(additions)
                });
            }
        });

        return {
            newRules: sortRules(newRules),
            updatedExistingRules: sortRules(updatedExistingRules)
        };
    };

    const requestHandler = input => {
        const rawUrl = input instanceof Request ? input.url : String(input ?? '');
        try {
            return new URL(rawUrl, window.location.href).searchParams.get('handler') ?? '';
        } catch {
            return '';
        }
    };

    window.fetch = async (input, init) => {
        const method = String(init?.method ?? (input instanceof Request ? input.method : 'GET')).toUpperCase();
        const handler = requestHandler(input);

        if (method === 'GET' && handler === 'ExportCsv') {
            const response = await previousFetch(input, init);
            if (response.ok) {
                try {
                    latestExportSnapshot = await response.clone().text();
                } catch {
                    latestExportSnapshot = null;
                }
            }
            return response;
        }

        if (method !== 'POST' || handler !== 'ImportCsv') {
            return previousFetch(input, init);
        }

        const file = init?.body instanceof FormData ? init.body.get('csvFile') : null;
        const importedTextPromise = file instanceof Blob ? file.text() : Promise.resolve(null);
        const response = await previousFetch(input, init);
        if (!response.ok) return response;

        try {
            const payload = await response.clone().json();
            const importedText = await importedTextPromise;
            if (payload?.success && importedText && latestExportSnapshot) {
                lastRuleDetails = compareConfigurations(
                    parseConfigurationCsv(latestExportSnapshot),
                    parseConfigurationCsv(importedText));
            }
        } catch {
            lastRuleDetails = emptyRuleDetails();
        }

        return response;
    };

    const ringLabel = ring => {
        const text = labels();
        if (ring === 'yellow') return text.yellow;
        if (ring === 'red') return text.red;
        return text.blue;
    };

    const upper = value => String(value ?? '').toLocaleUpperCase(document.documentElement.lang || 'uk');

    const ensureDialog = () => {
        let dialog = document.querySelector('[data-word-rings-ring-filter-dialog]');
        if (dialog instanceof HTMLDialogElement) return dialog;

        const text = labels();
        dialog = document.createElement('dialog');
        dialog.className = 'app-dialog word-rings-editor-dialog word-rings-editor-import-details-dialog word-rings-editor-ring-filter-dialog';
        dialog.dataset.wordRingsRingFilterDialog = '';
        dialog.innerHTML = `
            <div class="dialog-card word-rings-editor-dialog-card">
                <div class="dialog-heading">
                    <div><h2 data-word-rings-ring-filter-title></h2></div>
                    <button class="dialog-close" type="button" data-word-rings-ring-filter-close aria-label="${text.close}">×</button>
                </div>
                <div class="word-rings-editor-import-ring-filters" data-word-rings-import-ring-filters>
                    <button type="button" class="word-rings-editor-import-ring-filter is-active" data-word-rings-import-ring-filter="all" aria-pressed="true">${text.all}</button>
                    <button type="button" class="word-rings-editor-import-ring-filter is-blue" data-word-rings-import-ring-filter="blue" aria-pressed="false">${text.blue}</button>
                    <button type="button" class="word-rings-editor-import-ring-filter is-yellow" data-word-rings-import-ring-filter="yellow" aria-pressed="false">${text.yellow}</button>
                    <button type="button" class="word-rings-editor-import-ring-filter is-red" data-word-rings-import-ring-filter="red" aria-pressed="false">${text.red}</button>
                </div>
                <div class="word-rings-editor-import-details-body" data-word-rings-ring-filter-body></div>
                <div class="word-rings-editor-import-details-pager" data-word-rings-ring-filter-pager hidden>
                    <button class="button button-secondary word-rings-editor-import-details-page-button" type="button" data-word-rings-ring-filter-page="first" aria-label="${text.firstPage}" title="${text.firstPage}">«</button>
                    <button class="button button-secondary word-rings-editor-import-details-page-button" type="button" data-word-rings-ring-filter-page="previous" aria-label="${text.previousPage}" title="${text.previousPage}">‹</button>
                    <span class="word-rings-editor-import-details-page-status" data-word-rings-ring-filter-page-status></span>
                    <button class="button button-secondary word-rings-editor-import-details-page-button" type="button" data-word-rings-ring-filter-page="next" aria-label="${text.nextPage}" title="${text.nextPage}">›</button>
                    <button class="button button-secondary word-rings-editor-import-details-page-button" type="button" data-word-rings-ring-filter-page="last" aria-label="${text.lastPage}" title="${text.lastPage}">»</button>
                </div>
                <div class="form-actions dialog-actions">
                    <button class="button button-primary" type="button" data-word-rings-ring-filter-close>${text.close}</button>
                </div>
            </div>`;
        dialog.addEventListener('click', event => {
            if (event.target === dialog || event.target.closest('[data-word-rings-ring-filter-close]')) {
                dialog.close();
            }
        });
        document.body.appendChild(dialog);
        return dialog;
    };

    const filteredRuleItems = () => {
        const source = Array.isArray(lastRuleDetails[activeDetailType])
            ? lastRuleDetails[activeDetailType]
            : [];
        const filtered = activeRingFilter === 'all'
            ? source
            : source.filter(item => item.ring === activeRingFilter);
        return sortRules(filtered);
    };

    const renderRuleItem = (item, text) => {
        const card = document.createElement('article');
        card.className = `word-rings-editor-import-rule-detail word-rings-editor-import-rule-detail-${item.ring}`;

        const header = document.createElement('div');
        const ring = document.createElement('span');
        ring.textContent = ringLabel(item.ring);
        const state = document.createElement('span');
        state.textContent = item.enabled ? text.active : text.inactive;
        header.append(ring, state);

        const heading = document.createElement('strong');
        heading.textContent = item.text ?? '';
        card.append(header, heading);

        if (activeDetailType === 'updatedExistingRules') {
            const added = document.createElement('p');
            added.innerHTML = `<span>${text.addedWords}: <b>${item.wordsAdded?.length ?? 0}</b></span>`;
            const words = document.createElement('small');
            words.textContent = sortWords(item.wordsAdded ?? []).map(upper).join(', ');
            added.appendChild(words);
            card.appendChild(added);
        }

        return card;
    };

    const syncFilterButtons = dialog => {
        dialog.querySelectorAll('[data-word-rings-import-ring-filter]').forEach(button => {
            const selected = button.dataset.wordRingsImportRingFilter === activeRingFilter;
            button.classList.toggle('is-active', selected);
            button.setAttribute('aria-pressed', selected ? 'true' : 'false');
        });
    };

    const renderPage = () => {
        const dialog = ensureDialog();
        const body = dialog.querySelector('[data-word-rings-ring-filter-body]');
        const pager = dialog.querySelector('[data-word-rings-ring-filter-pager]');
        const status = dialog.querySelector('[data-word-rings-ring-filter-page-status]');
        if (!(body instanceof HTMLElement) || !(pager instanceof HTMLElement) || !(status instanceof HTMLElement)) return;

        const text = labels();
        const items = filteredRuleItems();
        const totalPages = Math.max(1, Math.ceil(items.length / importRuleDetailPageSize));
        activePage = Math.min(Math.max(activePage, 1), totalPages);
        const start = (activePage - 1) * importRuleDetailPageSize;
        const pageItems = items.slice(start, start + importRuleDetailPageSize);

        body.replaceChildren();
        if (pageItems.length === 0) {
            const empty = document.createElement('p');
            empty.className = 'muted';
            empty.textContent = text.empty;
            body.appendChild(empty);
        } else {
            const list = document.createElement('div');
            list.className = 'word-rings-editor-import-rule-list';
            pageItems.forEach(item => list.appendChild(renderRuleItem(item, text)));
            body.appendChild(list);
        }

        const first = pager.querySelector('[data-word-rings-ring-filter-page="first"]');
        const previous = pager.querySelector('[data-word-rings-ring-filter-page="previous"]');
        const next = pager.querySelector('[data-word-rings-ring-filter-page="next"]');
        const last = pager.querySelector('[data-word-rings-ring-filter-page="last"]');
        const isFirst = activePage <= 1;
        const isLast = activePage >= totalPages;
        if (first instanceof HTMLButtonElement) first.disabled = isFirst;
        if (previous instanceof HTMLButtonElement) previous.disabled = isFirst;
        if (next instanceof HTMLButtonElement) next.disabled = isLast;
        if (last instanceof HTMLButtonElement) last.disabled = isLast;
        status.textContent = `${text.page} ${activePage} ${text.of} ${totalPages}`;
        pager.hidden = items.length <= importRuleDetailPageSize;
        syncFilterButtons(dialog);
    };

    const openRuleDetails = type => {
        const dialog = ensureDialog();
        const title = dialog.querySelector('[data-word-rings-ring-filter-title]');
        if (!(title instanceof HTMLElement)) return;

        const text = labels();
        activeDetailType = type;
        activeRingFilter = 'all';
        activePage = 1;
        title.textContent = type === 'newRules'
            ? text.newRulesTitle
            : text.updatedRulesTitle;
        renderPage();
        if (!dialog.open) dialog.showModal();
    };

    const changePage = action => {
        const totalPages = Math.max(1, Math.ceil(filteredRuleItems().length / importRuleDetailPageSize));
        if (action === 'first') activePage = 1;
        else if (action === 'previous') activePage = Math.max(1, activePage - 1);
        else if (action === 'next') activePage = Math.min(totalPages, activePage + 1);
        else if (action === 'last') activePage = totalPages;
        else return;
        renderPage();
    };

    document.addEventListener('click', event => {
        const detailButton = event.target.closest('[data-word-rings-import-detail]');
        if (detailButton instanceof HTMLButtonElement) {
            const type = detailButton.dataset.wordRingsImportDetail ?? '';
            if (type === 'newRules' || type === 'updatedExistingRules') {
                event.preventDefault();
                event.stopImmediatePropagation();
                openRuleDetails(type);
                return;
            }
        }

        const filter = event.target.closest('[data-word-rings-import-ring-filter]');
        if (filter instanceof HTMLButtonElement) {
            event.preventDefault();
            event.stopImmediatePropagation();
            const ring = filter.dataset.wordRingsImportRingFilter ?? 'all';
            activeRingFilter = ['blue', 'yellow', 'red'].includes(ring) ? ring : 'all';
            activePage = 1;
            renderPage();
            return;
        }

        const pageButton = event.target.closest('[data-word-rings-ring-filter-page]');
        if (pageButton instanceof HTMLButtonElement) {
            event.preventDefault();
            event.stopImmediatePropagation();
            changePage(pageButton.dataset.wordRingsRingFilterPage ?? '');
        }
    }, true);
})();