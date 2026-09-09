(() => {
    "use strict";

    if (window.badWolfQuizEditorWorkspaceInteractionsInitialized) {
        return;
    }

    window.badWolfQuizEditorWorkspaceInteractionsInitialized = true;

    const getRoundContextMenu = () =>
        document.querySelector(".quiz-editor-context-menu");

    const getOpenBlockTypeMenus = () =>
        Array.from(document.querySelectorAll(
            ".content-block-type-menu:not([hidden])"));

    const closeTransientEditorMenus = () => {
        let closed = false;

        const roundMenu = getRoundContextMenu();
        if (roundMenu) {
            roundMenu.remove();
            closed = true;
        }

        for (const menu of getOpenBlockTypeMenus()) {
            menu.hidden = true;
            closed = true;
        }

        return closed;
    };

    window.addEventListener("keydown", event => {
        if (event.key !== "Escape" ||
            event.defaultPrevented ||
            event.isComposing ||
            event.repeat) {
            return;
        }

        if (!closeTransientEditorMenus()) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
    }, true);

    window.addEventListener("click", event => {
        if (event.defaultPrevented || event.button !== 0) {
            return;
        }

        const target = event.target instanceof Element
            ? event.target
            : null;
        if (!target) {
            return;
        }

        const roundTrigger = target.closest("[data-round-edit-menu]");
        if (roundTrigger && getRoundContextMenu()) {
            event.preventDefault();
            event.stopImmediatePropagation();
            getRoundContextMenu()?.remove();
            return;
        }

        const addButton = target.closest(".content-block-add-button");
        if (!addButton) {
            return;
        }

        const section = addButton.closest(".content-block-section");
        const typeMenu = section?.querySelector(".content-block-type-menu");
        if (!typeMenu || typeMenu.hidden) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        typeMenu.hidden = true;
    }, true);
})();