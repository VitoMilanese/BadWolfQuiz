(() => {
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

    document.querySelectorAll('[data-minigame-editor-busy]').forEach(form => {
        form.addEventListener('submit', event => {
            if (event.defaultPrevented || !form.checkValidity()) return;

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
    });

    document.querySelectorAll('[data-minigame-editor-nav]').forEach(link => {
        link.addEventListener('click', event => {
            if (event.defaultPrevented ||
                event.button !== 0 ||
                event.metaKey ||
                event.ctrlKey ||
                event.shiftKey ||
                event.altKey) {
                return;
            }

            event.preventDefault();
            if (window.BadWolfBusy) {
                window.BadWolfBusy.navigate(link.href);
            } else {
                window.location.assign(link.href);
            }
        });
    });

    const gamePicker = document.querySelector('[data-minigame-editor-game-picker]');
    gamePicker?.addEventListener('change', () => {
        const baseUrl = gamePicker.dataset.baseUrl;
        const gameId = Number(gamePicker.value);
        if (!baseUrl || !Number.isInteger(gameId) || gameId <= 0) return;

        const url = new URL(baseUrl, window.location.origin);
        url.searchParams.set('section', 'answers');
        url.searchParams.set('gameId', String(gameId));
        if (window.BadWolfBusy) {
            window.BadWolfBusy.navigate(`${url.pathname}${url.search}`);
        } else {
            window.location.assign(`${url.pathname}${url.search}`);
        }
    });

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
    let saving = false;
    let saveQueued = false;

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
            if (saving) {
                saveQueued = true;
                return;
            }

            void saveAnswers();
        }, delay);
    };

    const saveAnswers = async () => {
        saving = true;
        setStatus(answerForm.dataset.saving);

        try {
            const formData = new FormData(answerForm);
            formData.set('answersJson', JSON.stringify(buildPayload()));

            const response = await fetch(answerForm.action, {
                method: 'POST',
                body: formData,
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
        } catch (error) {
            console.error('Minigame answer autosave failed:', error);
            setStatus(
                error instanceof Error && error.message
                    ? error.message
                    : answerForm.dataset.saveFailed);
        } finally {
            saving = false;
            if (saveQueued) {
                saveQueued = false;
                scheduleSave(0);
            }
        }
    };

    answerForm.addEventListener('submit', event => {
        event.preventDefault();
    });

    createAnswerFilter();
    applyAnswerFilter();

    selects.forEach(select => {
        select.addEventListener('change', () => {
            applyAnswerFilter();
            setStatus(answerForm.dataset.saving);
            scheduleSave();
        });
    });
})();
