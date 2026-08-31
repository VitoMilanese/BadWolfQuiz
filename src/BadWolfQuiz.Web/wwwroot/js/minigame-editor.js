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
    let saveTimer = 0;
    let saving = false;
    let saveQueued = false;

    const setStatus = message => {
        if (status) {
            status.textContent = message || '';
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

    selects.forEach(select => {
        select.addEventListener('change', () => {
            setStatus(answerForm.dataset.saving);
            scheduleSave();
        });
    });
})();
