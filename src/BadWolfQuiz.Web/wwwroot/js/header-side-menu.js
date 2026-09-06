(() => {
    const trigger = document.querySelector('[data-open-header-menu]');
    const dialog = document.querySelector('[data-header-side-menu]');
    const panel = dialog?.querySelector('.header-side-menu-panel');
    const closeButton = dialog?.querySelector('[data-close-header-menu]');

    if (!(trigger instanceof HTMLButtonElement) ||
        !(dialog instanceof HTMLDialogElement) ||
        !(panel instanceof HTMLElement) ||
        !(closeButton instanceof HTMLButtonElement)) {
        return;
    }

    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    const focusGuard = document.createElement('span');
    focusGuard.className = 'header-side-menu-focus-guard';
    focusGuard.tabIndex = -1;
    focusGuard.setAttribute('autofocus', '');
    dialog.prepend(focusGuard);

    let returnFocus = null;
    let closeTimer = null;
    let openFocusTimer = null;

    const clearCloseTimer = () => {
        if (closeTimer !== null) {
            window.clearTimeout(closeTimer);
            closeTimer = null;
        }
    };

    const clearOpenFocusTimer = () => {
        if (openFocusTimer !== null) {
            window.clearTimeout(openFocusTimer);
            openFocusTimer = null;
        }
    };

    const focusCloseButton = () => {
        if (dialog.open && dialog.classList.contains('is-open')) {
            closeButton.focus({ preventScroll: true });
        }
    };

    const finishClose = () => {
        clearCloseTimer();
        clearOpenFocusTimer();

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

        clearOpenFocusTimer();
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
        clearOpenFocusTimer();
        returnFocus = document.activeElement;
        document.documentElement.classList.add('header-side-menu-open');
        trigger.setAttribute('aria-expanded', 'true');
        dialog.showModal();

        // Keep focus on a stationary element while the panel slides in. Mobile
        // Safari can otherwise pan the visual viewport toward the transformed
        // close button and briefly show the drawer on the wrong side.
        focusGuard.focus({ preventScroll: true });

        window.requestAnimationFrame(() => {
            window.requestAnimationFrame(() => {
                dialog.classList.add('is-open');

                if (reducedMotion.matches) {
                    focusCloseButton();
                    return;
                }

                openFocusTimer = window.setTimeout(() => {
                    openFocusTimer = null;
                    focusCloseButton();
                }, 240);
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
        clearOpenFocusTimer();
        dialog.classList.remove('is-open');
        document.documentElement.classList.remove('header-side-menu-open');
        trigger.setAttribute('aria-expanded', 'false');
    });
})();
