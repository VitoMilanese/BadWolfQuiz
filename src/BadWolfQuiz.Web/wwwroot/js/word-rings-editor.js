(() => {
    'use strict';

    const rootSelector = '[data-word-rings-editor-shell]';
    const allowedWordList = /^ *[\p{L}\p{M}\p{N}'’\-]+(?:[ ,;]+[\p{L}\p{M}\p{N}'’\-]+)*[ ,;]*$/u;
    let refreshController = null;

    const editorRoot = () => document.querySelector(rootSelector);
    const activeRing = () => editorRoot()?.dataset.activeRing ?? 'blue';
    const requestFailedMessage = () =>
        editorRoot()?.dataset.requestFailed ?? 'The operation could not be completed.';

    const closeDialog = dialog => {
        if (dialog instanceof HTMLDialogElement && dialog.open) {
            dialog.close();
        }
    };

    const showStatus = (message, isError = false) => {
        if (!message) return;

        if (typeof window.badWolfShowEditorStatus === 'function') {
            window.badWolfShowEditorStatus(message, isError);
            return;
        }

        const status = document.createElement('div');
        status.className = `alert ${isError ? 'alert-error' : 'alert-success'} editor-save-overlay`;
        status.setAttribute('role', isError ? 'alert' : 'status');
        status.textContent = message;
        document.body.appendChild(status);
        window.setTimeout(() => status.remove(), 1800);
    };

    const syncDialogRing = ring => {
        document.querySelectorAll('[data-word-rings-dialog-ring]').forEach(input => {
            if (input instanceof HTMLInputElement) {
                input.value = ring;
            }
        });
    };

    const validateWords = form => {
        const wordsInput = form?.querySelector('[data-word-rings-words]');
        if (!(wordsInput instanceof HTMLTextAreaElement)) return true;

        const value = wordsInput.value;
        const valid = value.trim().length > 0 && allowedWordList.test(value);
        wordsInput.setCustomValidity(valid ? '' : (wordsInput.dataset.invalidSeparators ?? ''));
        if (!valid) {
            wordsInput.reportValidity();
        }
        return valid;
    };

    const refreshEditor = async (ring, updateHistory) => {
        const currentRoot = editorRoot();
        if (!currentRoot) return;

        refreshController?.abort();
        refreshController = new AbortController();
        currentRoot.classList.add('is-loading');
        currentRoot.setAttribute('aria-busy', 'true');

        const url = new URL(window.location.href);
        url.searchParams.set('ring', ring);

        try {
            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin',
                signal: refreshController.signal
            });
            if (!response.ok) {
                throw new Error(`Editor refresh failed: ${response.status}`);
            }

            const html = await response.text();
            const documentFragment = new DOMParser().parseFromString(html, 'text/html');
            const nextRoot = documentFragment.querySelector(rootSelector);
            if (!(nextRoot instanceof HTMLElement)) {
                throw new Error('Editor refresh did not return the editor shell.');
            }

            currentRoot.replaceWith(nextRoot);
            syncDialogRing(ring);

            if (updateHistory) {
                window.history.pushState({ wordRingsRing: ring }, '', url);
            }
        } catch (error) {
            if (error?.name === 'AbortError') return;
            currentRoot.classList.remove('is-loading');
            currentRoot.removeAttribute('aria-busy');
            throw error;
        }
    };

    const postForm = async (form, formData) => {
        const response = await fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: {
                'Accept': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            },
            credentials: 'same-origin'
        });

        if (!response.ok) {
            throw new Error(`Mutation failed: ${response.status}`);
        }

        return response.json();
    };

    const submitMutation = async form => {
        if (!(form instanceof HTMLFormElement) || form.dataset.wordRingsBusy === 'true') {
            return;
        }
        if (!validateWords(form)) {
            return;
        }

        const formData = new FormData(form);
        form.dataset.wordRingsBusy = 'true';
        const controls = [...form.querySelectorAll('button, input, textarea')];
        const toggle = form.querySelector('[data-word-rings-rule-enabled]');
        controls.forEach(control => {
            control.dataset.wasDisabled = control.disabled ? 'true' : 'false';
            control.disabled = true;
        });

        try {
            const result = await postForm(form, formData);
            showStatus(result.message, !result.success);

            if (result.success) {
                const dialog = form.closest('dialog');
                closeDialog(dialog);
                if (form.hasAttribute('data-word-rings-create-form')) {
                    form.reset();
                }
            }

            if (result.success || toggle instanceof HTMLInputElement) {
                await refreshEditor(result.ring ?? activeRing(), false);
            }
        } catch {
            showStatus(requestFailedMessage(), true);
            if (toggle instanceof HTMLInputElement) {
                try {
                    await refreshEditor(activeRing(), false);
                } catch {
                    // Keep the current UI if the recovery refresh also fails.
                }
            }
        } finally {
            form.dataset.wordRingsBusy = 'false';
            controls.forEach(control => {
                control.disabled = control.dataset.wasDisabled === 'true';
                delete control.dataset.wasDisabled;
            });
        }
    };

    document.addEventListener('click', async event => {
        const tab = event.target.closest('[data-word-rings-tab]');
        if (tab instanceof HTMLAnchorElement) {
            event.preventDefault();
            const ring = tab.dataset.ring ?? 'blue';
            if (ring === activeRing()) return;

            try {
                await refreshEditor(ring, true);
            } catch {
                showStatus(requestFailedMessage(), true);
            }
            return;
        }

        const createButton = event.target.closest('[data-open-word-rings-create]');
        if (createButton) {
            const dialog = document.querySelector('[data-word-rings-create-dialog]');
            syncDialogRing(activeRing());
            if (dialog instanceof HTMLDialogElement && !dialog.open) {
                dialog.showModal();
                dialog.querySelector('input[name="text"]')?.focus();
            }
            return;
        }

        const editButton = event.target.closest('[data-edit-word-rings-rule]');
        if (editButton instanceof HTMLButtonElement) {
            const dialog = document.querySelector('[data-word-rings-edit-dialog]');
            if (!(dialog instanceof HTMLDialogElement)) return;

            syncDialogRing(activeRing());
            const id = dialog.querySelector('[data-word-rings-edit-id]');
            const text = dialog.querySelector('[data-word-rings-edit-text]');
            const words = dialog.querySelector('[data-word-rings-edit-words]');
            if (id instanceof HTMLInputElement) id.value = editButton.dataset.ruleId ?? '';
            if (text instanceof HTMLInputElement) text.value = editButton.dataset.ruleText ?? '';
            if (words instanceof HTMLTextAreaElement) {
                words.value = editButton.dataset.ruleWords ?? '';
                words.setCustomValidity('');
            }

            if (!dialog.open) {
                dialog.showModal();
            }
            text?.focus();
            return;
        }

        const deleteButton = event.target.closest('[data-delete-word-rings-rule]');
        if (deleteButton instanceof HTMLButtonElement) {
            const dialog = document.querySelector('[data-word-rings-delete-dialog]');
            if (!(dialog instanceof HTMLDialogElement)) return;

            syncDialogRing(activeRing());
            const id = dialog.querySelector('[data-word-rings-delete-id]');
            const target = dialog.querySelector('[data-word-rings-delete-target]');
            if (id instanceof HTMLInputElement) id.value = deleteButton.dataset.ruleId ?? '';
            if (target) target.textContent = deleteButton.dataset.ruleText ?? '';
            if (!dialog.open) dialog.showModal();
            return;
        }

        const closeButton = event.target.closest('[data-close-word-rings-dialog]');
        if (closeButton) {
            closeDialog(closeButton.closest('dialog'));
        }
    });

    document.addEventListener('submit', event => {
        const form = event.target.closest('[data-word-rings-ajax-form]');
        if (!(form instanceof HTMLFormElement)) return;
        event.preventDefault();
        void submitMutation(form);
    });

    document.addEventListener('change', event => {
        const checkbox = event.target.closest('[data-word-rings-rule-enabled]');
        if (!(checkbox instanceof HTMLInputElement)) return;
        const form = checkbox.form;
        if (form) {
            void submitMutation(form);
        }
    });

    document.addEventListener('input', event => {
        const words = event.target.closest('[data-word-rings-words]');
        if (words instanceof HTMLTextAreaElement) {
            words.setCustomValidity('');
        }
    });

    document.querySelectorAll('[data-word-rings-create-dialog], [data-word-rings-edit-dialog], [data-word-rings-delete-dialog]')
        .forEach(dialog => {
            dialog.addEventListener('click', event => {
                if (event.target === dialog) {
                    closeDialog(dialog);
                }
            });
        });

    window.addEventListener('popstate', async () => {
        const ring = new URL(window.location.href).searchParams.get('ring') ?? 'blue';
        try {
            await refreshEditor(ring, false);
        } catch {
            showStatus(requestFailedMessage(), true);
        }
    });

    syncDialogRing(activeRing());
})();
