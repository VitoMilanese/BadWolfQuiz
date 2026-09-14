(() => {
    'use strict';

    const rootSelector = '[data-word-rings-editor-shell]';
    const rulePageSize = 25;
    const ringPages = new Map();
    let enhanceQueued = false;

    const language = () => (document.documentElement.lang || 'uk').toLowerCase();

    const labels = () => {
        const current = language();
        if (current.startsWith('it')) {
            return {
                total: 'Totale',
                previous: 'Indietro',
                next: 'Avanti',
                page: 'Pagina',
                of: 'di',
                pagination: 'Pagine delle regole'
            };
        }
        if (current.startsWith('en')) {
            return {
                total: 'Total',
                previous: 'Previous',
                next: 'Next',
                page: 'Page',
                of: 'of',
                pagination: 'Rule pages'
            };
        }
        return {
            total: 'Усього',
            previous: 'Назад',
            next: 'Далі',
            page: 'Сторінка',
            of: 'з',
            pagination: 'Сторінки правил'
        };
    };

    const buildPageNumbers = (currentPage, totalPages) => {
        if (totalPages <= 7) {
            return Array.from({ length: totalPages }, (_, index) => index + 1);
        }
        if (currentPage <= 4) {
            return [1, 2, 3, 4, 5, null, totalPages];
        }
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
            if (!(usage instanceof HTMLElement) || usage.querySelector('.word-rings-editor-word-usage-total')) {
                return;
            }

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

    const makePager = (ring, position) => {
        const pager = document.createElement('nav');
        pager.className = `minigame-editor-pager word-rings-editor-word-pager word-rings-editor-rule-pager word-rings-editor-rule-pager-${position}`;
        if (position === 'top') {
            pager.classList.add('word-rings-editor-word-pager-top');
        }
        pager.dataset.wordRingsRulePager = position;
        pager.dataset.ring = ring;
        pager.setAttribute('aria-label', labels().pagination);
        return pager;
    };

    const makePageButton = (text, page, currentPage, className = '') => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = `button button-secondary ${className}`.trim();
        button.dataset.wordRingsRulePage = String(page);
        button.textContent = text;
        if (page === currentPage) {
            button.classList.add('is-current');
            button.setAttribute('aria-current', 'page');
            button.disabled = true;
        }
        return button;
    };

    const renderPager = (pager, currentPage, totalPages, setPage) => {
        const text = labels();
        pager.replaceChildren();

        const previous = makePageButton(text.previous, Math.max(1, currentPage - 1), currentPage);
        previous.disabled = currentPage <= 1;
        pager.appendChild(previous);

        const pageNumbers = document.createElement('div');
        pageNumbers.className = 'word-rings-editor-page-numbers';
        buildPageNumbers(currentPage, totalPages).forEach(page => {
            if (page === null) {
                const ellipsis = document.createElement('span');
                ellipsis.className = 'word-rings-editor-page-ellipsis';
                ellipsis.setAttribute('aria-hidden', 'true');
                ellipsis.textContent = '…';
                pageNumbers.appendChild(ellipsis);
                return;
            }

            pageNumbers.appendChild(makePageButton(
                String(page),
                page,
                currentPage,
                'word-rings-editor-page-number'));
        });
        pager.appendChild(pageNumbers);

        const status = document.createElement('span');
        status.className = 'minigame-editor-pager-status';
        status.textContent = `${text.page} ${currentPage} ${text.of} ${totalPages}`;
        pager.appendChild(status);

        const next = makePageButton(text.next, Math.min(totalPages, currentPage + 1), currentPage);
        next.disabled = currentPage >= totalPages;
        pager.appendChild(next);

        pager.querySelectorAll('[data-word-rings-rule-page]').forEach(button => {
            button.addEventListener('click', () => {
                const page = Number.parseInt(button.dataset.wordRingsRulePage ?? '', 10);
                if (Number.isInteger(page)) setPage(page);
            });
        });
    };

    const enhanceRingRulePaging = root => {
        const ring = root.dataset.activeRing ?? '';
        if (!['blue', 'yellow', 'red'].includes(ring)) return;

        const list = root.querySelector('.word-rings-editor-rule-list');
        if (!(list instanceof HTMLElement) || list.dataset.wordRingsRulePagingReady === 'true') return;

        const cards = [...list.querySelectorAll(':scope > .word-rings-editor-rule-card')]
            .filter(card => card instanceof HTMLElement);
        if (cards.length <= rulePageSize) return;

        list.dataset.wordRingsRulePagingReady = 'true';
        const topPager = makePager(ring, 'top');
        const bottomPager = makePager(ring, 'bottom');
        list.before(topPager);
        list.after(bottomPager);

        const totalPages = Math.ceil(cards.length / rulePageSize);
        let currentPage = Math.min(Math.max(ringPages.get(ring) ?? 1, 1), totalPages);

        const render = page => {
            currentPage = Math.min(Math.max(page, 1), totalPages);
            ringPages.set(ring, currentPage);
            const start = (currentPage - 1) * rulePageSize;
            const end = start + rulePageSize;
            cards.forEach((card, index) => {
                card.hidden = index < start || index >= end;
            });
            renderPager(topPager, currentPage, totalPages, render);
            renderPager(bottomPager, currentPage, totalPages, render);
        };

        render(currentPage);
    };

    const enhance = () => {
        enhanceQueued = false;
        const root = document.querySelector(rootSelector);
        if (!(root instanceof HTMLElement)) return;
        enhanceWordUsageTotals(root);
        enhanceRingRulePaging(root);
    };

    const queueEnhance = () => {
        if (enhanceQueued) return;
        enhanceQueued = true;
        queueMicrotask(enhance);
    };

    new MutationObserver(queueEnhance).observe(document.body, { childList: true, subtree: true });
    enhance();
})();
