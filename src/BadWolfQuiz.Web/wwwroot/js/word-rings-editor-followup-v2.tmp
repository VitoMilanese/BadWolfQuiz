(() => {
    'use strict';

    const rootSelector = '[data-word-rings-editor-shell]';
    const minimumWordLength = 3;
    const originalFetch = window.fetch.bind(window);
    let lastImportDetails = emptyImportDetails();

    const membershipInput = () =>
        document.querySelector('[data-word-rings-membership-word]');

    const enforceMinimumWordLength = () => {
        const input = membershipInput();
        if (!(input instanceof HTMLInputElement)) return;

        input.minLength = minimumWordLength;
        const save = document.querySelector('[data-word-rings-membership-save]');
        if (save instanceof HTMLButtonElement && input.value.trim().length < minimumWordLength) {
            save.disabled = true;
        }
    };

    const enhanceWordPager = root => {
        if (!(root instanceof HTMLElement)) return;
        if (root.querySelector('[data-word-rings-word-pager-position="top"]')) return;

        const bottom = root.querySelector('.word-rings-editor-word-pager');
        const list = root.querySelector('.word-rings-editor-word-list');
        if (!(bottom instanceof HTMLElement) || !(list instanceof HTMLElement)) return;

        bottom.dataset.wordRingsWordPagerPosition = 'bottom';
        const top = bottom.cloneNode(true);
        if (!(top instanceof HTMLElement)) return;

        top.dataset.wordRingsWordPagerPosition = 'top';
        top.classList.add('word-rings-editor-word-pager-top');
        list.before(top);
    };

    const language = () => (document.documentElement.lang || 'uk').toLowerCase();

    const labels = () => {
        const current = language();
        if (current.startsWith('it')) {
            return {
                newWords: 'Parole nuove',
                newRules: 'Regole nuove',
                updatedRules: 'Regole esistenti aggiornate',
                show: 'Mostra',
                newWordsTitle: 'Nuove parole aggiunte',
                newRulesTitle: 'Nuove regole aggiunte',
                updatedRulesTitle: 'Regole esistenti con nuove parole',
                addedWords: 'Parole aggiunte',
                active: 'Attiva',
                inactive: 'Disattivata',
                close: 'Chiudi',
                empty: 'Nessun elemento.',
                blue: 'Anello blu',
                yellow: 'Anello giallo',
                red: 'Anello rosso'
            };
        }
        if (current.startsWith('en')) {
            return {
                newWords: 'New words',
                newRules: 'New rules',
                updatedRules: 'Existing rules updated',
                show: 'Show',
                newWordsTitle: 'New words added',
                newRulesTitle: 'New rules added',
                updatedRulesTitle: 'Existing rules with new words',
                addedWords: 'Words added',
                active: 'Active',
                inactive: 'Inactive',
                close: 'Close',
                empty: 'No items.',
                blue: 'Blue ring',
                yellow: 'Yellow ring',
                red: 'Red ring'
            };
        }
        return {
            newWords: 'Нових слів',
            newRules: 'Нових правил',
            updatedRules: 'Старих правил доповнено',
            show: 'Показати',
            newWordsTitle: 'Нові слова, додані під час імпорту',
            newRulesTitle: 'Нові правила, додані під час імпорту',
            updatedRulesTitle: 'Старі правила, в які додано нові слова',
            addedWords: 'Додано слів',
            active: 'Активне',
            inactive: 'Неактивне',
            close: 'Закрити',
            empty: 'Немає елементів.',
            blue: 'Синє кільце',
            yellow: 'Жовте кільце',
            red: 'Червоне кільце'
        };
    };

    const fold = value => String(value ?? '').trim().toLocaleLowerCase('uk-UA');
    const wordFold = value => fold(value);
    const ruleKey = (ring, text) => `${fold(ring)}\u001f${fold(text)}`;

    function emptyImportDetails() {
        return {
            newWords: [],
            newRules: [],
            updatedExistingRules: []
        };
    }

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
            const textValue = fields[1].trim();
            if (!['blue', 'yellow', 'red'].includes(ring) || !textValue) return null;

            const key = ruleKey(ring, textValue);
            let rule = rules.get(key);
            if (!rule) {
                rule = {
                    ring,
                    text: textValue,
                    enabled: fold(fields[2]) === 'true' || fields[2].trim() === '1',
                    words: new Map()
                };
                rules.set(key, rule);
            }

            for (const word of splitWords(fields[3])) {
                const keyWord = wordFold(word);
                if (keyWord && !rule.words.has(keyWord)) {
                    rule.words.set(keyWord, word);
                }
            }
        }

        return rules;
    };

    const sortWords = values => [...values].sort((left, right) =>
        left.localeCompare(right, document.documentElement.lang || 'uk', { sensitivity: 'base' }));

    const sortRules = values => {
        const ringOrder = new Map([['blue', 0], ['yellow', 1], ['red', 2]]);
        return [...values].sort((left, right) =>
            (ringOrder.get(left.ring) ?? 99) - (ringOrder.get(right.ring) ?? 99) ||
            left.text.localeCompare(right.text, document.documentElement.lang || 'uk', { sensitivity: 'base' }));
    };

    const compareConfigurations = (beforeRules, importedRules) => {
        if (!(beforeRules instanceof Map) || !(importedRules instanceof Map)) {
            return emptyImportDetails();
        }

        const allExistingWords = new Set();
        beforeRules.forEach(rule => {
            rule.words.forEach((_word, key) => allExistingWords.add(key));
        });

        const newWords = new Map();
        const newRules = [];
        const updatedExistingRules = [];

        importedRules.forEach((incoming, key) => {
            incoming.words.forEach((word, wordKey) => {
                if (!allExistingWords.has(wordKey) && !newWords.has(wordKey)) {
                    newWords.set(wordKey, word);
                }
            });

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
            newWords: sortWords(newWords.values()),
            newRules: sortRules(newRules),
            updatedExistingRules: sortRules(updatedExistingRules)
        };
    };

    const captureImportDetails = async body => {
        if (!(body instanceof FormData)) return emptyImportDetails();
        const file = body.get('csvFile');
        const exportLink = document.querySelector('[data-word-rings-export]');
        if (!(file instanceof Blob) || !(exportLink instanceof HTMLAnchorElement)) {
            return emptyImportDetails();
        }

        try {
            const [importedText, exportResponse] = await Promise.all([
                file.text(),
                originalFetch(exportLink.href, {
                    method: 'GET',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' },
                    credentials: 'same-origin'
                })
            ]);
            if (!exportResponse.ok) return emptyImportDetails();

            const currentText = await exportResponse.text();
            return compareConfigurations(
                parseConfigurationCsv(currentText),
                parseConfigurationCsv(importedText));
        } catch {
            return emptyImportDetails();
        }
    };

    const isImportRequest = (input, init) => {
        const method = String(init?.method ?? (input instanceof Request ? input.method : 'GET')).toUpperCase();
        if (method !== 'POST') return false;
        const rawUrl = input instanceof Request ? input.url : String(input ?? '');
        try {
            return new URL(rawUrl, window.location.href).searchParams.get('handler') === 'ImportCsv';
        } catch {
            return false;
        }
    };

    const ringLabel = ring => {
        const text = labels();
        if (ring === 'yellow') return text.yellow;
        if (ring === 'red') return text.red;
        return text.blue;
    };

    const upper = value => String(value ?? '').toLocaleUpperCase(document.documentElement.lang || 'uk');

    const ensureDetailDialog = () => {
        let dialog = document.querySelector('[data-word-rings-import-details-dialog]');
        if (dialog instanceof HTMLDialogElement) return dialog;

        const text = labels();
        dialog = document.createElement('dialog');
        dialog.className = 'app-dialog word-rings-editor-dialog word-rings-editor-import-details-dialog';
        dialog.dataset.wordRingsImportDetailsDialog = '';
        dialog.innerHTML = `
            <div class="dialog-card word-rings-editor-dialog-card">
                <div class="dialog-heading">
                    <div><h2 data-word-rings-import-details-title></h2></div>
                    <button class="dialog-close" type="button" data-word-rings-import-details-close aria-label="${text.close}">×</button>
                </div>
                <div class="word-rings-editor-import-details-body" data-word-rings-import-details-body></div>
                <div class="form-actions dialog-actions">
                    <button class="button button-primary" type="button" data-word-rings-import-details-close>${text.close}</button>
                </div>
            </div>`;
        dialog.addEventListener('click', event => {
            if (event.target === dialog || event.target.closest('[data-word-rings-import-details-close]')) {
                dialog.close();
            }
        });
        document.body.appendChild(dialog);
        return dialog;
    };

    const renderImportDetailActions = details => {
        const dialog = document.querySelector('[data-word-rings-import-summary-dialog]');
        const summary = dialog?.querySelector('[data-word-rings-import-summary]');
        if (!(dialog instanceof HTMLDialogElement) || !(summary instanceof HTMLElement)) return;

        let panel = dialog.querySelector('[data-word-rings-import-detail-actions]');
        if (!(panel instanceof HTMLElement)) {
            panel = document.createElement('div');
            panel.className = 'word-rings-editor-import-detail-actions';
            panel.dataset.wordRingsImportDetailActions = '';
            summary.after(panel);
        }

        const text = labels();
        const cards = [
            { type: 'newWords', label: text.newWords, count: details.newWords.length },
            { type: 'newRules', label: text.newRules, count: details.newRules.length },
            { type: 'updatedExistingRules', label: text.updatedRules, count: details.updatedExistingRules.length }
        ];

        panel.replaceChildren(...cards.map(card => {
            const wrapper = document.createElement('div');
            wrapper.className = 'word-rings-editor-import-detail-action';
            wrapper.innerHTML = `
                <span>${card.label}: <b>${card.count}</b></span>
                <button class="button button-secondary" type="button" data-word-rings-import-detail="${card.type}" ${card.count === 0 ? 'disabled' : ''}>
                    ${text.show}
                </button>`;
            return wrapper;
        }));
    };

    const openImportDetails = type => {
        const dialog = ensureDetailDialog();
        const title = dialog.querySelector('[data-word-rings-import-details-title]');
        const body = dialog.querySelector('[data-word-rings-import-details-body]');
        if (!(title instanceof HTMLElement) || !(body instanceof HTMLElement)) return;

        const text = labels();
        const items = Array.isArray(lastImportDetails[type]) ? lastImportDetails[type] : [];
        title.textContent = type === 'newWords'
            ? text.newWordsTitle
            : type === 'newRules'
                ? text.newRulesTitle
                : text.updatedRulesTitle;
        body.replaceChildren();

        if (items.length === 0) {
            const empty = document.createElement('p');
            empty.className = 'muted';
            empty.textContent = text.empty;
            body.appendChild(empty);
        } else if (type === 'newWords') {
            const list = document.createElement('div');
            list.className = 'word-rings-editor-import-word-list';
            items.forEach(word => {
                const chip = document.createElement('span');
                chip.textContent = upper(word);
                list.appendChild(chip);
            });
            body.appendChild(list);
        } else {
            const list = document.createElement('div');
            list.className = 'word-rings-editor-import-rule-list';
            items.forEach(rule => {
                const card = document.createElement('article');
                card.className = `word-rings-editor-import-rule-detail word-rings-editor-import-rule-detail-${rule.ring}`;

                const header = document.createElement('div');
                const ring = document.createElement('span');
                ring.textContent = ringLabel(rule.ring);
                const state = document.createElement('span');
                state.textContent = rule.enabled ? text.active : text.inactive;
                header.append(ring, state);

                const heading = document.createElement('strong');
                heading.textContent = rule.text ?? '';
                card.append(header, heading);

                if (type === 'updatedExistingRules') {
                    const added = document.createElement('p');
                    added.innerHTML = `<span>${text.addedWords}: <b>${rule.wordsAdded?.length ?? 0}</b></span>`;
                    const words = document.createElement('small');
                    words.textContent = (rule.wordsAdded ?? []).map(upper).join(', ');
                    added.appendChild(words);
                    card.appendChild(added);
                }

                list.appendChild(card);
            });
            body.appendChild(list);
        }

        if (!dialog.open) dialog.showModal();
    };

    window.fetch = async (input, init) => {
        if (!isImportRequest(input, init)) {
            return originalFetch(input, init);
        }

        const details = await captureImportDetails(init?.body);
        const response = await originalFetch(input, init);
        if (!response.ok) return response;

        try {
            const payload = await response.clone().json();
            if (payload?.success) {
                lastImportDetails = details;
                window.setTimeout(() => renderImportDetailActions(details), 0);
            }
        } catch {
            // Keep the normal import flow if the response cannot be inspected.
        }
        return response;
    };

    const enhance = () => {
        enforceMinimumWordLength();
        enhanceWordPager(document.querySelector(rootSelector));
    };

    const initialRoot = document.querySelector(rootSelector);
    const host = initialRoot?.parentElement;
    if (host) {
        new MutationObserver(enhance).observe(host, { childList: true });
    }

    document.addEventListener('click', event => {
        const detailButton = event.target.closest('[data-word-rings-import-detail]');
        if (detailButton instanceof HTMLButtonElement) {
            openImportDetails(detailButton.dataset.wordRingsImportDetail ?? '');
        }
    });

    document.addEventListener('input', event => {
        if (event.target instanceof HTMLInputElement &&
            event.target.matches('[data-word-rings-membership-word]')) {
            enforceMinimumWordLength();
        }
    });

    document.addEventListener('change', event => {
        if (event.target instanceof HTMLElement &&
            (event.target.matches('[data-word-rings-membership-rule]') ||
             event.target.matches('[data-word-rings-membership-ring]'))) {
            queueMicrotask(enforceMinimumWordLength);
        }
    });

    enhance();
})();
