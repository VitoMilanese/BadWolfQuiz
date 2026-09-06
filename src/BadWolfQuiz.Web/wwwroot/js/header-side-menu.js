(() => {
    const trigger = document.querySelector('[data-open-header-menu]');
    const dialog = document.querySelector('[data-header-side-menu]');
    const closeButton = dialog?.querySelector('[data-close-header-menu]');

    if (!(trigger instanceof HTMLButtonElement) ||
        !(dialog instanceof HTMLDialogElement) ||
        !(closeButton instanceof HTMLButtonElement)) {
        return;
    }

    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    let returnFocus = null;
    let closeTimer = null;

    const clearCloseTimer = () => {
        if (closeTimer !== null) {
            window.clearTimeout(closeTimer);
            closeTimer = null;
        }
    };

    const finishClose = () => {
        clearCloseTimer();

        if (dialog.open) {
            dialog.close();
        }

        dialog.classList.remove('is-open');
        document.documentElement.classList.remove('header-side-menu-open');
        trigger.setAttribute('aria-expanded', 'false');

        if (returnFocus instanceof HTMLElement && returnFocus.isConnected) {
            returnFocus.focus({ preventScroll: true });
        } else {
            trigger.focus({ preventScroll: true });
        }

        returnFocus = null;
    };

    const closeMenu = () => {
        if (!dialog.open) {
            return;
        }

        dialog.classList.remove('is-open');

        if (reducedMotion.matches) {
            finishClose();
            return;
        }

        clearCloseTimer();
        closeTimer = window.setTimeout(finishClose, 220);
    };

    const openMenu = () => {
        if (dialog.open) {
            return;
        }

        clearCloseTimer();
        returnFocus = document.activeElement;
        document.documentElement.classList.add('header-side-menu-open');
        trigger.setAttribute('aria-expanded', 'true');
        dialog.showModal();

        window.requestAnimationFrame(() => {
            window.requestAnimationFrame(() => {
                dialog.classList.add('is-open');
                closeButton.focus({ preventScroll: true });
            });
        });
    };

    trigger.addEventListener('click', openMenu);
    closeButton.addEventListener('click', closeMenu);

    dialog.addEventListener('cancel', event => {
        event.preventDefault();
        closeMenu();
    });

    dialog.addEventListener('click', event => {
        if (event.target === dialog) {
            closeMenu();
        }
    });

    dialog.querySelectorAll('a[target="_blank"]').forEach(link => {
        link.addEventListener('click', closeMenu);
    });

    dialog.addEventListener('close', () => {
        clearCloseTimer();
        dialog.classList.remove('is-open');
        document.documentElement.classList.remove('header-side-menu-open');
        trigger.setAttribute('aria-expanded', 'false');
    });
})();
