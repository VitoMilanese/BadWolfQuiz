(() => {
    const createDialog = document.querySelector('[data-word-rings-create-dialog]');
    const deleteDialog = document.querySelector('[data-word-rings-delete-dialog]');
    const createButton = document.querySelector('[data-open-word-rings-create]');
    const deleteId = deleteDialog?.querySelector('[data-word-rings-delete-id]');
    const deleteTarget = deleteDialog?.querySelector('[data-word-rings-delete-target]');
    const wordsInput = createDialog?.querySelector('[data-word-rings-words]');
    const createForm = createDialog?.querySelector('[data-word-rings-create-form]');

    const allowedWordList = /^ *[\p{L}\p{M}\p{N}'’\-]+(?:[ ,;]+[\p{L}\p{M}\p{N}'’\-]+)*[ ,;]*$/u;

    const closeDialog = dialog => {
        if (dialog instanceof HTMLDialogElement && dialog.open) {
            dialog.close();
        }
    };

    createButton?.addEventListener('click', () => {
        if (createDialog instanceof HTMLDialogElement && !createDialog.open) {
            createDialog.showModal();
            createDialog.querySelector('input[name="text"]')?.focus();
        }
    });

    document.querySelectorAll('[data-delete-word-rings-rule]').forEach(button => {
        button.addEventListener('click', () => {
            if (!(deleteDialog instanceof HTMLDialogElement)) return;
            if (deleteId instanceof HTMLInputElement) {
                deleteId.value = button.dataset.ruleId ?? '';
            }
            if (deleteTarget) {
                deleteTarget.textContent = button.dataset.ruleText ?? '';
            }
            if (!deleteDialog.open) {
                deleteDialog.showModal();
            }
        });
    });

    document.querySelectorAll('[data-word-rings-rule-enabled]').forEach(checkbox => {
        checkbox.addEventListener('change', () => {
            checkbox.form?.requestSubmit();
        });
    });

    document.querySelectorAll('[data-close-word-rings-dialog]').forEach(button => {
        button.addEventListener('click', () => closeDialog(button.closest('dialog')));
    });

    for (const dialog of [createDialog, deleteDialog]) {
        dialog?.addEventListener('click', event => {
            if (event.target === dialog) {
                closeDialog(dialog);
            }
        });
    }

    const validateWords = () => {
        if (!(wordsInput instanceof HTMLTextAreaElement)) return true;
        const value = wordsInput.value;
        const valid = value.trim().length > 0 && allowedWordList.test(value);
        wordsInput.setCustomValidity(valid ? '' : (wordsInput.dataset.invalidSeparators ?? ''));
        return valid;
    };

    wordsInput?.addEventListener('input', validateWords);
    createForm?.addEventListener('submit', event => {
        if (!validateWords()) {
            event.preventDefault();
            wordsInput?.reportValidity();
        }
    });
})();
