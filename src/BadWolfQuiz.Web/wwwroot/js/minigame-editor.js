(() => {
    const pageSize = 25;
    let flushMinigameEditorAnswers = null;
    let pagingRequestId = 0;

    const showBusy = () => {
        if (window.BadWolfBusy && !window.BadWolfBusy.isBusy) {
            window.BadWolfBusy.show();
        }
    };

    const formSubmitters = form => {
        const controls = [
            ...form.querySelectorAll('button[type="submit"], input[type="submit"]'),
            ...document.querySelectorAll('button[type="submit"][form], input[type="submit"][form]')
        ];

        return controls.filter((control, index, all) =>
            control.form === form && all.indexOf(control) === index);
    };

    const isPlainPrimaryClick = event =>
        !event.defaultPrevented &&
        event.button === 0 &&
        !event.metaKey &&
        !event.ctrlKey &&
        !event.shiftKey &&
        !event.altKey;

    const performNavigation = href => {
        if (window.BadWolfBusy) {
            window.BadWolfBusy.navigate(href);
        } else {
            window.location.assign(href);
        }
    };

    const flushAnswers = async () => {
        if (!flushMinigameEditorAnswers) return true;
        return await flushMinigameEditorAnswers();
    };

    const navigateEditor = async href => {
        if (!await flushAnswers()) return;
        performNavigation(href);
    };

    document.addEventListener('submit', event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) ||
            !form.matches('[data-minigame-editor-busy]') ||
            event.defaultPrevented ||
            !form.checkValidity()) {
            return;
        }

        const confirmation = form.dataset.confirm;
        if (confirmation && !window.confirm(confirmation)) {
            event.preventDefault();
            return;
        }

        showBusy();
        formSubmitters(form).forEach(button => {
            button.disabled = true;
        });
    });

    const getSection = url => {
        const value = new URL(url, window.location.origin).searchParams.get('section');
        return value === 'questions' || value === 'answers' ? value : 'games';
    };

    const getPagedContentSelector = section => section === 'questions'
        ? '.minigame-editor-question-panel'
        : section === 'answers'
            ? '.minigame-editor-answer-form'
            : '.minigame-editor-game-list';

    const getTotalCount = section => {
        const counts = [...document.querySelectorAll('.minigame-editor-counts strong')];
        const source = section === 'games' ? counts[0] : counts[1];
        const total = Number.parseInt(source?.textContent?.replace(/[^0-9]/g, '') ?? '', 10);
        return Number.isFinite(total) && total >= 0 ? total : 0;
    };

    const getCurrentPage = url => {
        const value = Number.parseInt(
            new URL(url, window.location.origin).searchParams.get('pageNumber') ?? '1',
            10);
        return Number.isInteger(value) && value > 0 ? value : 1;
    };

    const buildPageNumbers = (currentPage, totalPages) => {
        if (totalPages <= 9) {
            return Array.from({ length: totalPages }, (_, index) => index + 1);
        }

        if (currentPage <= 5) {
            return [1, 2, 3, 4, 5, 6, 7, null, totalPages];
        }

        if (currentPage >= totalPages - 4) {
            return [
                1,
                null,
                totalPages - 6,
                totalPages - 5,
                totalPages - 4,
                totalPages - 3,
                totalPages - 2,
                totalPages - 1,
                totalPages
            ];
        }

        return [
            1,
            null,
            currentPage - 2,
            currentPage - 1,
            currentPage,
            currentPage + 1,
            currentPage + 2,
            null,
            totalPages
        ];
    };

    const buildPageHref = pageNumber => {
        const url = new URL(window.location.href);
        url.searchParams.set('section', getSection(url.href));
        url.searchParams.set('pageNumber', String(pageNumber));
        return `${url.pathname}${url.search}${url.hash}`;
    };

    const stylePageNumber = element => {
        element.style.boxSizing = 'border-box';
        element.style.flex = '0 0 40px';
        element.style.width = '40px';
        element.style.minWidth = '40px';
        element.style.height = '40px';
        element.style.minHeight = '40px';
        element.style.padding = '0';
        element.style.display = 'inline-grid';
        element.style.placeItems = 'center';
        element.style.fontVariantNumeric = 'tabular-nums';
    };

    const enhancePager = pager => {
        if (!(pager instanceof HTMLElement)) return;

        pager.querySelector('[data-minigame-editor-page-numbers]')?.remove();
        const section = getSection(window.location.href);
        const totalPages = Math.max(1, Math.ceil(getTotalCount(section) / pageSize));
        const currentPage = Math.min(getCurrentPage(window.location.href), totalPages);
        if (totalPages <= 1) return;

        const directControls = [...pager.children]
            .filter(element => element.matches('.button'));
        directControls[0]?.classList.add('minigame-editor-pager-edge');
        directControls.at(-1)?.classList.add('minigame-editor-pager-edge');

        const pages = document.createElement('div');
        pages.dataset.minigameEditorPageNumbers = '';
        pages.className = 'minigame-editor-pager-pages';
        Object.assign(pages.style, {
            display: 'flex',
            flex: '1 1 320px',
            minWidth: '0',
            alignItems: 'center',
            justifyContent: 'center',
            flexWrap: 'wrap',
            gap: '6px'
        });

        buildPageNumbers(currentPage, totalPages).forEach(pageNumber => {
            if (pageNumber === null) {
                const ellipsis = document.createElement('span');
                ellipsis.className = 'minigame-editor-pager-ellipsis';
                ellipsis.textContent = '…';
                ellipsis.setAttribute('aria-hidden', 'true');
                ellipsis.style.minWidth = '18px';
                ellipsis.style.textAlign = 'center';
                pages.appendChild(ellipsis);
                return;
            }

            if (pageNumber === currentPage) {
                const current = document.createElement('span');
                current.className = 'button button-secondary minigame-editor-page-number is-current';
                current.textContent = String(pageNumber);
                current.setAttribute('aria-current', 'page');
                current.style.borderColor = 'var(--gold)';
                current.style.color = 'var(--text)';
                stylePageNumber(current);
                pages.appendChild(current);
                return;
            }

            const link = document.createElement('a');
            link.className = 'button button-secondary minigame-editor-page-number';
            link.href = buildPageHref(pageNumber);
            link.textContent = String(pageNumber);
            link.dataset.minigameEditorPageLink = '';
            stylePageNumber(link);
            pages.appendChild(link);
        });

        const status = pager.querySelector('.minigame-editor-pager-status');
        if (status) {
            pager.insertBefore(pages, status);
        } else {
            pager.appendChild(pages);
        }
    };

    const setupPagerCopies = () => {
        document.querySelectorAll('[data-minigame-editor-pager-position="top"]')
            .forEach(pager => pager.remove());

        const bottomPager = [...document.querySelectorAll('.minigame-editor-pager')]
            .find(pager => pager.dataset.minigameEditorPagerPosition !== 'top');
        if (!bottomPager) return;

        bottomPager.dataset.minigameEditorPagerPosition = 'bottom';
        enhancePager(bottomPager);

        const section = getSection(window.location.href);
        const content = document.querySelector(getPagedContentSelector(section));
        if (!content) return;

        const topPager = bottomPager.cloneNode(true);
        topPager.dataset.minigameEditorPagerPosition = 'top';
        topPager.setAttribute('aria-label', bottomPager.getAttribute('aria-label') ?? '');
        content.before(topPager);
    };

    const syncPageNumberInputs = () => {
        const pageNumber = getCurrentPage(window.location.href);
        document.querySelectorAll('input[name="pageNumber"]')
            .forEach(input => {
                input.value = String(pageNumber);
            });
    };

    const setPagersBusy = busy => {
        document.querySelectorAll('.minigame-editor-pager').forEach(pager => {
            pager.toggleAttribute('aria-busy', busy);
            pager.style.pointerEvents = busy ? 'none' : '';
            pager.style.opacity = busy ? '0.65' : '';
        });
    };

    const replacePagedContent = (nextDocument, href) => {
        const section = getSection(href);
        const selector = getPagedContentSelector(section);
        const currentContent = document.querySelector(selector);
        const nextContent = nextDocument.querySelector(selector);
        const currentBottomPager = document.querySelector(
            '.minigame-editor-pager[data-minigame-editor-pager-position="bottom"]');
        const nextBottomPager = nextDocument.querySelector('.minigame-editor-pager');

        if (!currentContent || !nextContent || !currentBottomPager || !nextBottomPager) {
            return false;
        }

        document.querySelector('[data-minigame-editor-pager-position="top"]')?.remove();
        currentContent.replaceWith(document.importNode(nextContent, true));
        currentBottomPager.replaceWith(document.importNode(nextBottomPager, true));
        return true;
    };

    const loadPagedUrl = async (href, pushHistory) => {
        if (!await flushAnswers()) return false;

        const requestId = ++pagingRequestId;
        const previousScrollY = window.scrollY;
        setPagersBusy(true);

        try {
            const response = await fetch(href, {
                method: 'GET',
                headers: {
                    'Accept': 'text/html',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });
            if (!response.ok) {
                throw new Error(`Paging request failed with ${response.status}.`);
            }

            const html = await response.text();
            if (requestId !== pagingRequestId) return false;

            const nextDocument = new DOMParser().parseFromString(html, 'text/html');
            if (!replacePagedContent(nextDocument, href)) {
                throw new Error('Paged editor region was not found in the response.');
            }

            const url = new URL(href, window.location.origin);
            const relativeUrl = `${url.pathname}${url.search}${url.hash}`;
            if (pushHistory) {
                window.history.pushState({ minigameEditorPage: true }, '', relativeUrl);
            }

            syncPageNumberInputs();
            setupPagerCopies();
            setupAnswerEditor();
            window.scrollTo(0, previousScrollY);
            return true;
        } catch (error) {
            console.error('Minigame Editor paging failed:', error);
            if (pushHistory) {
                performNavigation(href);
            } else {
                window.location.reload();
            }
            return false;
        } finally {
            setPagersBusy(false);
        }
    };

    document.addEventListener('click', event => {
        if (!isPlainPrimaryClick(event)) return;

        const pageLink = event.target.closest('[data-minigame-editor-page-link], .minigame-editor-pager a');
        if (pageLink instanceof HTMLAnchorElement) {
            event.preventDefault();
            void loadPagedUrl(pageLink.href, true);
            return;
        }

        const navLink = event.target.closest('[data-minigame-editor-nav]');
        if (!(navLink instanceof HTMLAnchorElement)) return;

        event.preventDefault();
        void navigateEditor(navLink.href);
    });

    const gamePicker = document.querySelector('[data-minigame-editor-game-picker]');
    gamePicker?.addEventListener('change', () => {
        const baseUrl = gamePicker.dataset.baseUrl;
        const gameId = Number(gamePicker.value);
        if (!baseUrl || !Number.isInteger(gameId) || gameId <= 0) return;

        const url = new URL(baseUrl, window.location.origin);
        url.searchParams.set('section', 'answers');
        url.searchParams.set('gameId', String(gameId));
        void navigateEditor(`${url.pathname}${url.search}`);
    });

    const setupAnswerEditor = () => {
        flushMinigameEditorAnswers = null;
        const answerForm = document.querySelector('[data-minigame-answer-form]');
        if (!answerForm) return;

        const selects = [...answerForm.querySelectorAll('[data-minigame-answer-select]')];
        const status = document.querySelector('[data-minigame-answer-status]');
        const assignedCount = document.querySelector('[data-minigame-assigned-count]');
        const answerTableWrap = answerForm.querySelector('.minigame-editor-answer-table-wrap');
        const answerHeader = answerForm.querySelector('.minigame-editor-answer-table th:last-child');
        const filterButtons = [];
        let activeAnswerFilter = null;
        let saveTimer = 0;
        let savePromise = null;
        let dirty = false;

        const setStatus = message => {
            if (status) {
                status.textContent = message || '';
            }
        };

        const applyAnswerFilter = () => {
            let visibleIndex = 0;
            selects.forEach(select => {
                const row = select.closest('tr');
                if (!row) return;

                const visible = activeAnswerFilter === null ||
                    select.value === activeAnswerFilter;
                row.hidden = !visible;
                row.classList.remove('is-filter-even');
                if (visible) {
                    visibleIndex += 1;
                    row.classList.toggle('is-filter-even', visibleIndex % 2 === 0);
                }
            });

            answerForm.classList.toggle(
                'is-answer-filtered',
                activeAnswerFilter !== null);
            filterButtons.forEach(button => {
                const isActive = button.dataset.minigameAnswerFilter === activeAnswerFilter;
                button.classList.toggle('is-active', isActive);
                button.setAttribute('aria-pressed', isActive ? 'true' : 'false');
            });
        };

        const createAnswerFilter = () => {
            if (!answerTableWrap || selects.length === 0) return;

            answerForm.querySelector(':scope > .minigame-editor-answer-filter')?.remove();

            const filter = document.createElement('div');
            filter.className = 'minigame-editor-answer-filter';
            filter.setAttribute('role', 'group');

            const answerLabel = (answerHeader?.textContent ?? '').trim();
            if (answerLabel) {
                filter.setAttribute('aria-label', answerLabel);
                const label = document.createElement('span');
                label.className = 'minigame-editor-answer-filter-label';
                label.textContent = `${answerLabel}:`;
                filter.appendChild(label);
            }

            ['1', '0', ''].forEach(value => {
                const option = [...selects[0].options]
                    .find(item => item.value === value);
                if (!option) return;

                const button = document.createElement('button');
                button.type = 'button';
                button.className = 'button button-secondary';
                button.dataset.minigameAnswerFilter = value;
                button.setAttribute('aria-pressed', 'false');
                button.textContent = option.textContent?.trim() ?? '';
                button.addEventListener('click', () => {
                    activeAnswerFilter = activeAnswerFilter === value ? null : value;
                    applyAnswerFilter();
                });
                filterButtons.push(button);
                filter.appendChild(button);
            });

            if (filterButtons.length > 0) {
                answerTableWrap.before(filter);
            }
        };

        const buildPayload = () => selects.map(select => ({
            QuestionId: Number(select.dataset.questionId),
            Value: select.value
        }));

        const scheduleSave = (delay = 250) => {
            window.clearTimeout(saveTimer);
            saveTimer = window.setTimeout(() => {
                if (savePromise) {
                    scheduleSave(delay);
                    return;
                }
                void saveAnswers();
            }, delay);
        };

        const saveAnswers = () => {
            if (savePromise) return savePromise;
            if (!dirty) return Promise.resolve(true);

            window.clearTimeout(saveTimer);
            const formData = new FormData(answerForm);
            formData.set('answersJson', JSON.stringify(buildPayload()));
            dirty = false;
            setStatus(answerForm.dataset.saving);

            savePromise = (async () => {
                try {
                    const response = await fetch(answerForm.action, {
                        method: 'POST',
                        body: formData,
                        keepalive: true,
                        headers: {
                            'Accept': 'application/json',
                            'X-Requested-With': 'XMLHttpRequest'
                        }
                    });

                    let result = null;
                    try {
                        result = await response.json();
                    } catch {
                        // The fallback error below covers non-JSON failures.
                    }

                    if (!response.ok || !result?.success) {
                        throw new Error(result?.message || answerForm.dataset.saveFailed);
                    }

                    if (assignedCount && Number.isInteger(result.assignedAnswerCount)) {
                        assignedCount.textContent = String(result.assignedAnswerCount);
                    }
                    setStatus(answerForm.dataset.saved);
                    return true;
                } catch (error) {
                    dirty = true;
                    console.error('Minigame answer autosave failed:', error);
                    setStatus(
                        error instanceof Error && error.message
                            ? error.message
                            : answerForm.dataset.saveFailed);
                    return false;
                } finally {
                    savePromise = null;
                }
            })();

            return savePromise;
        };

        flushMinigameEditorAnswers = async () => {
            window.clearTimeout(saveTimer);
            if (savePromise) {
                const saved = await savePromise;
                if (!saved) return false;
            }
            return dirty ? await saveAnswers() : true;
        };

        answerForm.addEventListener('submit', event => {
            event.preventDefault();
        });

        createAnswerFilter();
        applyAnswerFilter();

        selects.forEach(select => {
            select.addEventListener('change', () => {
                dirty = true;
                applyAnswerFilter();
                setStatus(answerForm.dataset.saving);
                scheduleSave();
            });
        });
    };

    if (window.history?.replaceState) {
        window.history.replaceState(
            { ...(window.history.state ?? {}), minigameEditorPage: true },
            '',
            window.location.href);
    }

    window.addEventListener('popstate', event => {
        if (!event.state?.minigameEditorPage) return;
        void loadPagedUrl(window.location.href, false);
    });

    syncPageNumberInputs();
    setupPagerCopies();
    setupAnswerEditor();
})();