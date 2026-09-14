(() => {
    'use strict';

    const rootSelector = '[data-word-rings-editor-shell]';
    const allowedWordList = /^ *[\p{L}\p{M}\p{N}'’\-]+(?:[ ,;]+[\p{L}\p{M}\p{N}'’\-]+)*[ ,;]*$/u;
    const completionSoundDurationMilliseconds = 420;
    let refreshController = null;
    let membershipRequestId = 0;
    let membershipRing = 'blue';
    let membershipPage = 1;
    const membershipOriginal = new Map();
    const membershipPending = new Map();
    let transferRunning = false;
    let transferBusyDelayHandle = 0;
    let transferBusyOwned = false;
    let audioContext = null;

    const editorRoot = () => document.querySelector(rootSelector);
    const activeTab = () => editorRoot()?.dataset.activeRing ?? 'blue';
    const requestFailedMessage = () =>
        editorRoot()?.dataset.requestFailed ?? 'The operation could not be completed.';
    const realRing = value => ['blue', 'yellow', 'red'].includes(value) ? value : 'blue';

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
        const normalized = realRing(ring);
        document.querySelectorAll('[data-word-rings-dialog-ring]').forEach(input => {
            if (input instanceof HTMLInputElement) {
                input.value = normalized;
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

    const refreshEditorUrl = async (href, updateHistory) => {
        const currentRoot = editorRoot();
        if (!currentRoot) return;

        refreshController?.abort();
        refreshController = new AbortController();
        currentRoot.classList.add('is-loading');
        currentRoot.setAttribute('aria-busy', 'true');

        try {
            const response = await fetch(href, {
                method: 'GET',
                headers: {
                    'Accept': 'text/html',
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
            syncDialogRing(activeTab());

            if (updateHistory) {
                const url = new URL(href, window.location.origin);
                window.history.pushState(
                    { wordRingsTab: activeTab() },
                    '',
                    `${url.pathname}${url.search}${url.hash}`);
            }
        } catch (error) {
            if (error?.name === 'AbortError') return;
            currentRoot.classList.remove('is-loading');
            currentRoot.removeAttribute('aria-busy');
            throw error;
        }
    };

    const refreshCurrent = async () => refreshEditorUrl(window.location.href, false);

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
        if (!form.reportValidity() || !validateWords(form)) {
            return;
        }

        const formData = new FormData(form);
        form.dataset.wordRingsBusy = 'true';
        const controls = [...form.querySelectorAll('button, input, textarea, select')];
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
                if (form.hasAttribute('data-word-rings-membership-form')) {
                    resetMembershipState();
                }
            }

            if (result.success || toggle instanceof HTMLInputElement) {
                await refreshCurrent();
            }
        } catch {
            showStatus(requestFailedMessage(), true);
            if (toggle instanceof HTMLInputElement) {
                try {
                    await refreshCurrent();
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

    const membershipDialog = () => document.querySelector('[data-word-rings-membership-dialog]');
    const membershipWordInput = () => membershipDialog()?.querySelector('[data-word-rings-membership-word]');
    const membershipSaveButton = () => membershipDialog()?.querySelector('[data-word-rings-membership-save]');

    const membershipKey = (ring, ruleId) => `${ring}|${ruleId}`;

    const resetMembershipState = () => {
        membershipRequestId += 1;
        membershipRing = 'blue';
        membershipPage = 1;
        membershipOriginal.clear();
        membershipPending.clear();
    };

    const updateMembershipSaveState = () => {
        const button = membershipSaveButton();
        const input = membershipWordInput();
        if (button instanceof HTMLButtonElement) {
            button.disabled = membershipPending.size === 0 ||
                !(input instanceof HTMLInputElement) ||
                input.value.trim().length === 0;
        }
    };

    const renderMembershipRules = result => {
        const dialog = membershipDialog();
        const list = dialog?.querySelector('[data-word-rings-membership-list]');
        const loading = dialog?.querySelector('[data-word-rings-membership-loading]');
        const previous = dialog?.querySelector('[data-word-rings-membership-previous]');
        const next = dialog?.querySelector('[data-word-rings-membership-next]');
        const page = dialog?.querySelector('[data-word-rings-membership-page]');
        if (!(list instanceof HTMLElement)) return;

        list.replaceChildren();
        const items = Array.isArray(result.items) ? result.items : [];
        items.forEach(item => {
            const key = membershipKey(result.ring, item.id);
            if (!membershipOriginal.has(key)) {
                membershipOriginal.set(key, Boolean(item.included));
            }
            const checked = membershipPending.has(key)
                ? membershipPending.get(key).included
                : Boolean(item.included);

            const row = document.createElement('label');
            row.className = 'word-rings-editor-membership-rule';
            row.classList.toggle('is-disabled-rule', item.enabled === false);

            const checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.checked = checked;
            checkbox.dataset.wordRingsMembershipRule = '';
            checkbox.dataset.ring = result.ring;
            checkbox.dataset.ruleId = item.id;

            const text = document.createElement('span');
            text.textContent = item.text ?? '';

            row.append(checkbox, text);
            list.appendChild(row);
        });

        if (loading instanceof HTMLElement) loading.hidden = true;
        if (previous instanceof HTMLButtonElement) previous.disabled = result.pageNumber <= 1;
        if (next instanceof HTMLButtonElement) next.disabled = result.pageNumber >= result.totalPages;
        if (page instanceof HTMLElement) {
            page.textContent = `${result.pageNumber} / ${result.totalPages}`;
        }
        membershipRing = result.ring;
        membershipPage = result.pageNumber;
        updateMembershipSaveState();
    };

    const loadMembershipRules = async (ring, pageNumber) => {
        const root = editorRoot();
        const dialog = membershipDialog();
        const list = dialog?.querySelector('[data-word-rings-membership-list]');
        const loading = dialog?.querySelector('[data-word-rings-membership-loading]');
        const input = membershipWordInput();
        if (!root?.dataset.wordRulesUrl || !(list instanceof HTMLElement)) return;

        const requestId = ++membershipRequestId;
        if (loading instanceof HTMLElement) loading.hidden = false;
        list.setAttribute('aria-busy', 'true');

        const url = new URL(root.dataset.wordRulesUrl, window.location.origin);
        url.searchParams.set('ring', realRing(ring));
        url.searchParams.set('pageNumber', String(Math.max(1, pageNumber)));
        if (input instanceof HTMLInputElement && input.value.trim()) {
            url.searchParams.set('word', input.value.trim());
        }

        try {
            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'Accept': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin'
            });
            if (!response.ok) throw new Error(`Membership request failed: ${response.status}`);
            const result = await response.json();
            if (requestId !== membershipRequestId) return;
            renderMembershipRules(result);
        } catch {
            if (requestId !== membershipRequestId) return;
            if (loading instanceof HTMLElement) loading.hidden = true;
            showStatus(requestFailedMessage(), true);
        } finally {
            if (requestId === membershipRequestId) {
                list.removeAttribute('aria-busy');
            }
        }
    };

    const openMembershipEditor = (mode, word = '') => {
        const dialog = membershipDialog();
        const input = membershipWordInput();
        const title = dialog?.querySelector('[data-word-rings-membership-title]');
        const ringSelect = dialog?.querySelector('[data-word-rings-membership-ring]');
        const root = editorRoot();
        if (!(dialog instanceof HTMLDialogElement) || !(input instanceof HTMLInputElement)) return;

        resetMembershipState();
        dialog.dataset.mode = mode;
        input.value = word;
        input.readOnly = mode === 'edit';
        if (title instanceof HTMLElement) {
            title.textContent = mode === 'add'
                ? (root?.dataset.addWordTitle ?? '')
                : (root?.dataset.editWordTitle ?? '');
        }
        if (ringSelect instanceof HTMLSelectElement) {
            ringSelect.value = 'blue';
        }

        if (!dialog.open) dialog.showModal();
        if (mode === 'add') input.focus();
        void loadMembershipRules('blue', 1);
    };

    const prepareMembershipPayload = form => {
        const input = membershipWordInput();
        const changesInput = form.querySelector('[data-word-rings-membership-changes]');
        if (!(input instanceof HTMLInputElement) || !(changesInput instanceof HTMLInputElement)) {
            return false;
        }

        if (!input.reportValidity() || membershipPending.size === 0) {
            return false;
        }

        changesInput.value = JSON.stringify([...membershipPending.values()]);
        return true;
    };

    const showImportSummary = summary => {
        const dialog = document.querySelector('[data-word-rings-import-summary-dialog]');
        if (!(dialog instanceof HTMLDialogElement)) return;

        const noChanges = dialog.querySelector('[data-word-rings-import-no-changes]');
        const summaryBody = dialog.querySelector('[data-word-rings-import-summary]');
        const setValue = (selector, value) => {
            const target = dialog.querySelector(selector);
            if (target) target.textContent = String(value ?? 0);
        };

        const hasChanges = Boolean(summary?.hasChanges);
        if (noChanges instanceof HTMLElement) noChanges.hidden = hasChanges;
        if (summaryBody instanceof HTMLElement) summaryBody.hidden = !hasChanges;
        setValue('[data-summary-blue-words]', summary?.blue?.wordsAdded);
        setValue('[data-summary-blue-rules]', summary?.blue?.rulesCreated);
        setValue('[data-summary-yellow-words]', summary?.yellow?.wordsAdded);
        setValue('[data-summary-yellow-rules]', summary?.yellow?.rulesCreated);
        setValue('[data-summary-red-words]', summary?.red?.wordsAdded);
        setValue('[data-summary-red-rules]', summary?.red?.rulesCreated);

        if (!dialog.open) dialog.showModal();
    };

    const armCompletionSound = () => {
        const AudioContextType = window.AudioContext || window.webkitAudioContext;
        if (!AudioContextType) return;

        audioContext ??= new AudioContextType();
        if (audioContext.state === 'suspended') {
            void audioContext.resume().catch(() => { });
        }
    };

    const scheduleCompletionTone = () => {
        if (!audioContext || audioContext.state !== 'running') return;

        const tones = [
            { frequency: 523.25, offset: 0, duration: 0.12 },
            { frequency: 659.25, offset: 0.11, duration: 0.13 },
            { frequency: 783.99, offset: 0.23, duration: 0.18 }
        ];
        const baseTime = audioContext.currentTime + 0.01;
        tones.forEach(tone => {
            const oscillator = audioContext.createOscillator();
            const gain = audioContext.createGain();
            const start = baseTime + tone.offset;
            const end = start + tone.duration;

            oscillator.type = 'sine';
            oscillator.frequency.setValueAtTime(tone.frequency, start);
            gain.gain.setValueAtTime(0.0001, start);
            gain.gain.exponentialRampToValueAtTime(0.075, start + 0.018);
            gain.gain.exponentialRampToValueAtTime(0.0001, end);
            oscillator.connect(gain);
            gain.connect(audioContext.destination);
            oscillator.start(start);
            oscillator.stop(end + 0.01);
        });
    };

    const playCompletionSound = () => {
        if (!audioContext || audioContext.state === 'closed') return 0;
        if (audioContext.state === 'suspended') {
            void audioContext.resume().then(scheduleCompletionTone).catch(() => { });
        } else {
            scheduleCompletionTone();
        }
        return completionSoundDurationMilliseconds;
    };

    const setTransferControlsDisabled = disabled => {
        document.querySelectorAll('[data-word-rings-transfer-control]').forEach(control => {
            if (control instanceof HTMLButtonElement) {
                control.disabled = disabled;
            } else {
                control.classList.toggle('is-disabled', disabled);
                control.setAttribute('aria-disabled', disabled ? 'true' : 'false');
            }
        });
    };

    const startTransferBusy = () => {
        transferRunning = true;
        setTransferControlsDisabled(true);
        transferBusyDelayHandle = window.setTimeout(() => {
            transferBusyDelayHandle = 0;
            transferBusyOwned = window.BadWolfBusy?.show?.() === true;
        }, 180);
    };

    const stopTransferBusy = () => {
        transferRunning = false;
        if (transferBusyDelayHandle) {
            window.clearTimeout(transferBusyDelayHandle);
            transferBusyDelayHandle = 0;
        }
        if (transferBusyOwned) {
            transferBusyOwned = false;
            window.BadWolfBusy?.hide?.();
        }
        setTransferControlsDisabled(false);
    };

    const fileNameFromResponse = response => {
        const disposition = response.headers.get('Content-Disposition') ?? '';
        const utf8 = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
        if (utf8?.[1]) {
            try { return decodeURIComponent(utf8[1]); } catch { return utf8[1]; }
        }
        const basic = /filename="?([^";]+)"?/i.exec(disposition);
        return basic?.[1] ?? 'word-rings.csv';
    };

    const downloadBlob = (blob, fileName) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        window.setTimeout(() => URL.revokeObjectURL(url), 0);
    };

    const runExport = async link => {
        if (transferRunning || !(link instanceof HTMLAnchorElement)) return;
        armCompletionSound();
        startTransferBusy();
        try {
            const response = await fetch(link.href, {
                method: 'GET',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                credentials: 'same-origin'
            });
            if (!response.ok) throw new Error(`Export failed: ${response.status}`);
            const blob = await response.blob();
            downloadBlob(blob, fileNameFromResponse(response));
            playCompletionSound();
        } catch {
            showStatus(requestFailedMessage(), true);
        } finally {
            stopTransferBusy();
        }
    };

    const runImport = async (form, fileInput) => {
        if (transferRunning || !(form instanceof HTMLFormElement) ||
            !(fileInput instanceof HTMLInputElement) || !fileInput.files?.length) {
            return;
        }

        armCompletionSound();
        startTransferBusy();
        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                headers: {
                    'Accept': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin'
            });
            if (!response.ok) throw new Error(`Import failed: ${response.status}`);
            const result = await response.json();
            if (!result.success) {
                showStatus(result.message || requestFailedMessage(), true);
                return;
            }

            playCompletionSound();
            showStatus(result.message, false);
            await refreshCurrent();
            showImportSummary(result.summary);
        } catch {
            showStatus(requestFailedMessage(), true);
        } finally {
            fileInput.value = '';
            stopTransferBusy();
        }
    };

    document.addEventListener('click', async event => {
        const wordPage = event.target.closest('[data-word-rings-word-page]');
        if (wordPage instanceof HTMLAnchorElement) {
            event.preventDefault();
            try {
                await refreshEditorUrl(wordPage.href, true);
            } catch {
                showStatus(requestFailedMessage(), true);
            }
            return;
        }

        const tab = event.target.closest('[data-word-rings-tab]');
        if (tab instanceof HTMLAnchorElement) {
            event.preventDefault();
            if (tab.dataset.ring === activeTab()) return;
            try {
                await refreshEditorUrl(tab.href, true);
            } catch {
                showStatus(requestFailedMessage(), true);
            }
            return;
        }

        const exportLink = event.target.closest('[data-word-rings-export]');
        if (exportLink instanceof HTMLAnchorElement) {
            event.preventDefault();
            void runExport(exportLink);
            return;
        }

        const importTrigger = event.target.closest('[data-word-rings-import-trigger]');
        if (importTrigger instanceof HTMLButtonElement) {
            const form = importTrigger.closest('[data-word-rings-import-form]');
            const fileInput = form?.querySelector('[data-word-rings-import-file]');
            if (!transferRunning && fileInput instanceof HTMLInputElement) {
                fileInput.click();
            }
            return;
        }

        const createButton = event.target.closest('[data-open-word-rings-create]');
        if (createButton) {
            const dialog = document.querySelector('[data-word-rings-create-dialog]');
            syncDialogRing(activeTab());
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

            syncDialogRing(activeTab());
            const id = dialog.querySelector('[data-word-rings-edit-id]');
            const text = dialog.querySelector('[data-word-rings-edit-text]');
            const words = dialog.querySelector('[data-word-rings-edit-words]');
            if (id instanceof HTMLInputElement) id.value = editButton.dataset.ruleId ?? '';
            if (text instanceof HTMLInputElement) text.value = editButton.dataset.ruleText ?? '';
            if (words instanceof HTMLTextAreaElement) {
                words.value = editButton.dataset.ruleWords ?? '';
                words.setCustomValidity('');
            }
            if (!dialog.open) dialog.showModal();
            text?.focus();
            return;
        }

        const deleteButton = event.target.closest('[data-delete-word-rings-rule]');
        if (deleteButton instanceof HTMLButtonElement) {
            const dialog = document.querySelector('[data-word-rings-delete-dialog]');
            if (!(dialog instanceof HTMLDialogElement)) return;

            syncDialogRing(activeTab());
            const id = dialog.querySelector('[data-word-rings-delete-id]');
            const target = dialog.querySelector('[data-word-rings-delete-target]');
            if (id instanceof HTMLInputElement) id.value = deleteButton.dataset.ruleId ?? '';
            if (target) target.textContent = deleteButton.dataset.ruleText ?? '';
            if (!dialog.open) dialog.showModal();
            return;
        }

        const addWordButton = event.target.closest('[data-open-word-rings-word-add]');
        if (addWordButton) {
            openMembershipEditor('add');
            return;
        }

        const editWordButton = event.target.closest('[data-edit-word-rings-word]');
        if (editWordButton instanceof HTMLButtonElement) {
            openMembershipEditor('edit', editWordButton.dataset.word ?? '');
            return;
        }

        const deleteWordButton = event.target.closest('[data-delete-word-rings-word]');
        if (deleteWordButton instanceof HTMLButtonElement) {
            const dialog = document.querySelector('[data-word-rings-delete-word-dialog]');
            if (!(dialog instanceof HTMLDialogElement)) return;
            const value = dialog.querySelector('[data-word-rings-delete-word-value]');
            const target = dialog.querySelector('[data-word-rings-delete-word-target]');
            const word = deleteWordButton.dataset.word ?? '';
            if (value instanceof HTMLInputElement) value.value = word;
            if (target) target.textContent = word;
            if (!dialog.open) dialog.showModal();
            return;
        }

        const previousMembership = event.target.closest('[data-word-rings-membership-previous]');
        if (previousMembership instanceof HTMLButtonElement && membershipPage > 1) {
            void loadMembershipRules(membershipRing, membershipPage - 1);
            return;
        }

        const nextMembership = event.target.closest('[data-word-rings-membership-next]');
        if (nextMembership instanceof HTMLButtonElement && !nextMembership.disabled) {
            void loadMembershipRules(membershipRing, membershipPage + 1);
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

        if (form.hasAttribute('data-word-rings-membership-form') &&
            !prepareMembershipPayload(form)) {
            return;
        }
        void submitMutation(form);
    });

    document.addEventListener('change', event => {
        const checkbox = event.target.closest('[data-word-rings-rule-enabled]');
        if (checkbox instanceof HTMLInputElement) {
            const form = checkbox.form;
            if (form) void submitMutation(form);
            return;
        }

        const membershipCheckbox = event.target.closest('[data-word-rings-membership-rule]');
        if (membershipCheckbox instanceof HTMLInputElement) {
            const ring = membershipCheckbox.dataset.ring ?? membershipRing;
            const ruleId = membershipCheckbox.dataset.ruleId ?? '';
            const key = membershipKey(ring, ruleId);
            const original = membershipOriginal.get(key) ?? false;
            if (membershipCheckbox.checked === original) {
                membershipPending.delete(key);
            } else {
                membershipPending.set(key, {
                    ruleId,
                    included: membershipCheckbox.checked
                });
            }
            updateMembershipSaveState();
            return;
        }

        const ringSelect = event.target.closest('[data-word-rings-membership-ring]');
        if (ringSelect instanceof HTMLSelectElement) {
            void loadMembershipRules(ringSelect.value, 1);
            return;
        }

        const fileInput = event.target.closest('[data-word-rings-import-file]');
        if (fileInput instanceof HTMLInputElement) {
            const form = fileInput.closest('[data-word-rings-import-form]');
            void runImport(form, fileInput);
        }
    });

    document.addEventListener('input', event => {
        const words = event.target.closest('[data-word-rings-words]');
        if (words instanceof HTMLTextAreaElement) {
            words.setCustomValidity('');
            return;
        }

        if (event.target.matches?.('[data-word-rings-membership-word]')) {
            updateMembershipSaveState();
        }
    });

    document.querySelectorAll('dialog').forEach(dialog => {
        dialog.addEventListener('click', event => {
            if (event.target === dialog) {
                closeDialog(dialog);
            }
        });
    });

    window.addEventListener('popstate', async () => {
        try {
            await refreshEditorUrl(window.location.href, false);
        } catch {
            showStatus(requestFailedMessage(), true);
        }
    });

    syncDialogRing(activeTab());
})();
