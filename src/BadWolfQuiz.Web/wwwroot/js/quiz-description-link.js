(() => {
    const buttons = document.querySelectorAll('[data-copy-quiz-description-link]');
    if (buttons.length === 0) {
        return;
    }

    let status = document.querySelector('[data-quiz-description-copy-status]');
    if (!status) {
        status = document.createElement('div');
        status.hidden = true;
        status.setAttribute('role', 'status');
        status.setAttribute('aria-live', 'polite');
        status.setAttribute('data-quiz-description-copy-status', '');
        document.querySelector('.page-heading')?.insertAdjacentElement('afterend', status);
    }

    let dismissHandle = null;
    let hideFallbackHandle = null;

    const finishHidingStatus = () => {
        if (!status) {
            return;
        }

        window.clearTimeout(hideFallbackHandle);
        status.hidden = true;
        status.classList.remove('message-hidden');
        status.textContent = '';
    };

    const hideStatus = () => {
        if (!status || status.hidden) {
            return;
        }

        status.classList.add('message-hidden');
        status.addEventListener('transitionend', finishHidingStatus, { once: true });
        hideFallbackHandle = window.setTimeout(finishHidingStatus, 350);
    };

    const setStatus = (message, succeeded) => {
        if (!status) {
            return;
        }

        window.clearTimeout(dismissHandle);
        window.clearTimeout(hideFallbackHandle);
        status.classList.remove('message-hidden');
        status.textContent = message;
        status.className = `message ${succeeded ? 'message-success' : 'message-error'}`;
        status.hidden = false;
        dismissHandle = window.setTimeout(hideStatus, 4000);
    };

    const fallbackCopy = value => {
        const textArea = document.createElement('textarea');
        textArea.value = value;
        textArea.setAttribute('readonly', '');
        textArea.style.position = 'fixed';
        textArea.style.opacity = '0';
        document.body.appendChild(textArea);
        textArea.select();

        const copied = document.execCommand('copy');
        textArea.remove();
        if (!copied) {
            throw new Error('Clipboard copy failed.');
        }
    };

    const copyText = async value => {
        if (!value) {
            throw new Error('Description link is missing.');
        }

        if (navigator.clipboard?.writeText) {
            try {
                await navigator.clipboard.writeText(value);
                return;
            } catch {
                // Fall back for browsers/contexts where Clipboard API is unavailable.
            }
        }

        fallbackCopy(value);
    };

    buttons.forEach(button => {
        button.addEventListener('click', async () => {
            button.closest('details')?.removeAttribute('open');
            try {
                await copyText(button.dataset.descriptionUrl ?? '');
                setStatus(button.dataset.copySuccess ?? '', true);
            } catch {
                setStatus(button.dataset.copyFailed ?? '', false);
            }
        });
    });
})();
