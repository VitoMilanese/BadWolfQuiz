(() => {
    const trigger = document.querySelector("[data-open-player-lobby-menu]");
    const menu = document.querySelector("[data-player-lobby-action-menu]");
    const panel = menu?.querySelector(".player-lobby-action-menu-panel");
    const closeButton = menu?.querySelector("[data-close-player-lobby-menu]");

    if (!(trigger instanceof HTMLButtonElement) ||
        !(menu instanceof HTMLDialogElement) ||
        !(panel instanceof HTMLElement) ||
        !(closeButton instanceof HTMLButtonElement)) {
        return;
    }

    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    const focusGuard = document.createElement("span");
    focusGuard.className = "player-lobby-action-menu-focus-guard";
    focusGuard.tabIndex = -1;
    focusGuard.setAttribute("autofocus", "");
    menu.prepend(focusGuard);

    let returnFocus = null;
    let closeTimer = null;
    let openFocusTimer = null;
    let pendingDialog = null;

    const clearTimers = () => {
        if (closeTimer !== null) {
            window.clearTimeout(closeTimer);
            closeTimer = null;
        }

        if (openFocusTimer !== null) {
            window.clearTimeout(openFocusTimer);
            openFocusTimer = null;
        }
    };

    const focusCloseButton = () => {
        if (menu.open && menu.classList.contains("is-open")) {
            closeButton.focus({ preventScroll: true });
        }
    };

    const openTargetDialog = target => {
        if (!(target instanceof HTMLDialogElement) || target.open) {
            return;
        }

        target.addEventListener("close", () => {
            trigger.focus({ preventScroll: true });
        }, { once: true });
        target.showModal();
    };

    const finishClose = () => {
        clearTimers();

        if (menu.open) {
            menu.close();
        }

        menu.classList.remove("is-open");
        document.documentElement.classList.remove("player-lobby-action-menu-open");
        trigger.setAttribute("aria-expanded", "false");

        const target = pendingDialog;
        pendingDialog = null;

        if (target) {
            openTargetDialog(target);
        } else if (returnFocus instanceof HTMLElement && returnFocus.isConnected) {
            returnFocus.focus({ preventScroll: true });
        } else {
            trigger.focus({ preventScroll: true });
        }

        returnFocus = null;
    };

    const closeMenu = () => {
        if (!menu.open) {
            if (pendingDialog) {
                const target = pendingDialog;
                pendingDialog = null;
                openTargetDialog(target);
            }
            return;
        }

        menu.classList.remove("is-open");

        if (reducedMotion.matches) {
            finishClose();
            return;
        }

        clearTimers();
        closeTimer = window.setTimeout(finishClose, 220);
    };

    const openMenu = () => {
        if (menu.open) {
            return;
        }

        clearTimers();
        pendingDialog = null;
        returnFocus = document.activeElement;
        document.documentElement.classList.add("player-lobby-action-menu-open");
        trigger.setAttribute("aria-expanded", "true");
        menu.showModal();
        focusGuard.focus({ preventScroll: true });

        window.requestAnimationFrame(() => {
            window.requestAnimationFrame(() => {
                menu.classList.add("is-open");

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

    trigger.addEventListener("click", openMenu);
    closeButton.addEventListener("click", closeMenu);

    menu.addEventListener("cancel", event => {
        event.preventDefault();
        closeMenu();
    });

    menu.addEventListener("click", event => {
        if (event.target === menu) {
            closeMenu();
            return;
        }

        const item = event.target.closest("[data-player-dialog-target]");
        if (!(item instanceof HTMLButtonElement) || item.hidden) {
            return;
        }

        const targetId = item.dataset.playerDialogTarget;
        const target = targetId ? document.getElementById(targetId) : null;
        if (!(target instanceof HTMLDialogElement)) {
            return;
        }

        pendingDialog = target;
        closeMenu();
    });

    menu.addEventListener("close", () => {
        clearTimers();
        menu.classList.remove("is-open");
        document.documentElement.classList.remove("player-lobby-action-menu-open");
        trigger.setAttribute("aria-expanded", "false");
    });

    for (const dialog of document.querySelectorAll("[data-player-settings-dialog]")) {
        if (!(dialog instanceof HTMLDialogElement)) {
            continue;
        }

        dialog.querySelector("[data-close-player-settings-dialog]")
            ?.addEventListener("click", () => dialog.close());

        dialog.addEventListener("click", event => {
            if (event.target === dialog) {
                dialog.close();
            }
        });
    }
})();
