(() => {
    const script = document.currentScript;

    const language = document.documentElement.lang?.toLowerCase() || "en";
    const labels = language.startsWith("uk")
        ? {
            editRound: "Редагувати раунд",
            renameRound: "Перейменувати раунд",
            editRoundDescription: "Змінити опис",
            deleteRound: "Видалити раунд",
            renameCategory: "Перейменувати категорію",
            editCategoryDescription: "Редагувати опис"
        }
        : language.startsWith("it")
            ? {
                editRound: "Modifica round",
                renameRound: "Rinomina round",
                editRoundDescription: "Modifica descrizione",
                deleteRound: "Elimina round",
                renameCategory: "Rinomina categoria",
                editCategoryDescription: "Modifica descrizione"
            }
            : {
                editRound: "Edit round",
                renameRound: "Rename round",
                editRoundDescription: "Edit description",
                deleteRound: "Delete round",
                renameCategory: "Rename category",
                editCategoryDescription: "Edit description"
            };

    let openMenu = null;

    function ensureStyles() {
        if (document.getElementById("quiz-editor-context-menu-styles")) return;

        const style = document.createElement("style");
        style.id = "quiz-editor-context-menu-styles";
        style.textContent = `
            .quiz-editor-context-menu {
                position: fixed;
                z-index: 10000;
                min-width: 220px;
                display: flex;
                flex-direction: column;
                gap: 4px;
                padding: 8px;
                border: 1px solid var(--border-color, rgba(255,255,255,.16));
                border-radius: 10px;
                background: var(--panel-bg, var(--surface, #181818));
                box-shadow: 0 12px 32px rgba(0,0,0,.28);
            }

            .quiz-editor-context-menu-item {
                width: 100%;
                justify-content: flex-start;
                text-align: left;
                white-space: nowrap;
            }
        `;
        document.head.appendChild(style);
    }

    function closeMenu() {
        openMenu?.remove();
        openMenu = null;
    }

    function positionMenu(menu, trigger) {
        const triggerRect = trigger.getBoundingClientRect();
        const menuRect = menu.getBoundingClientRect();
        const margin = 8;

        let left = triggerRect.left;
        let top = triggerRect.bottom + 6;

        if (left + menuRect.width > window.innerWidth - margin) {
            left = Math.max(margin, triggerRect.right - menuRect.width);
        }

        if (top + menuRect.height > window.innerHeight - margin) {
            top = Math.max(margin, triggerRect.top - menuRect.height - 6);
        }

        menu.style.left = `${left}px`;
        menu.style.top = `${top}px`;
    }

    function createMenuItem(label, action, danger = false) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = `button ${danger ? "button-danger-outline" : "button-secondary"} quiz-editor-context-menu-item`;
        button.textContent = label;
        button.addEventListener("click", event => {
            event.preventDefault();
            event.stopPropagation();
            closeMenu();
            action();
        });
        return button;
    }

    function openContextMenu(trigger, items) {
        closeMenu();
        ensureStyles();

        const menu = document.createElement("div");
        menu.className = "quiz-editor-context-menu";
        menu.setAttribute("role", "menu");
        for (const item of items) {
            menu.appendChild(createMenuItem(item.label, item.action, item.danger));
        }

        document.body.appendChild(menu);
        openMenu = menu;
        positionMenu(menu, trigger);
    }

    function initializeRoundMenu(roundId) {
        const roundSummary = document.querySelector(".round-settings-disclosure > summary");
        const renameButton = roundSummary?.querySelector('[onclick*="openRenameRoundDialog"]');
        if (!roundSummary || !renameButton) return;

        const deleteButton = document.querySelector('.round-tab-item')
            ?.parentElement
            ?.querySelector('[onclick*="openDeleteRoundDialog"]');

        renameButton.textContent = labels.editRound;
        renameButton.removeAttribute("onclick");
        renameButton.dataset.roundEditMenu = "";

        deleteButton?.remove();
        roundSummary.querySelector("[data-edit-round-description]")?.remove();

        renameButton.addEventListener("click", event => {
            event.preventDefault();
            event.stopPropagation();

            const items = [
                {
                    label: labels.renameRound,
                    action: () => window.openRenameRoundDialog?.()
                },
                {
                    label: labels.editRoundDescription,
                    action: () => {
                        window.location.href = `/Admin/Quizzes/DescriptionEditor?roundId=${encodeURIComponent(roundId)}`;
                    }
                }
            ];

            if (deleteButton) {
                items.push({
                    label: labels.deleteRound,
                    danger: true,
                    action: () => window.openDeleteRoundDialog?.()
                });
            }

            openContextMenu(renameButton, items);
        });
    }

    function triggerOriginalCategoryRename(renameButton) {
        renameButton.dataset.allowOriginalRename = "true";
        renameButton.dispatchEvent(new MouseEvent("click", {
            bubbles: true,
            cancelable: true,
            view: window
        }));
        delete renameButton.dataset.allowOriginalRename;
    }

    function initializeCategoryMenus() {
        document.querySelectorAll(".quiz-board-category-column[data-category-id]").forEach(column => {
            const categoryId = column.dataset.categoryId;
            const actions = column.querySelector(".category-actions");
            const renameButton = actions?.querySelector(".js-category-rename");
            if (!categoryId || !actions || !renameButton || renameButton.dataset.categoryEditMenu !== undefined) return;

            actions.querySelector("[data-edit-category-description]")?.remove();
            renameButton.dataset.categoryEditMenu = "";
            renameButton.title = labels.renameCategory;
            renameButton.setAttribute("aria-label", labels.renameCategory);

            renameButton.addEventListener("click", event => {
                if (renameButton.dataset.allowOriginalRename === "true") {
                    return;
                }

                event.preventDefault();
                event.stopImmediatePropagation();

                openContextMenu(renameButton, [
                    {
                        label: labels.renameCategory,
                        action: () => triggerOriginalCategoryRename(renameButton)
                    },
                    {
                        label: labels.editCategoryDescription,
                        action: () => {
                            window.location.href = `/Admin/Quizzes/DescriptionEditor?categoryId=${encodeURIComponent(categoryId)}`;
                        }
                    }
                ]);
            }, true);
        });
    }

    function initialize() {
        const roundIdInput = document.querySelector('input[name="RoundRows.RoundId"]');
        if (!roundIdInput) return;

        initializeRoundMenu(roundIdInput.value);
        initializeCategoryMenus();
    }

    document.addEventListener("click", event => {
        if (openMenu && !openMenu.contains(event.target)) {
            closeMenu();
        }
    });

    document.addEventListener("keydown", event => {
        if (event.key === "Escape") {
            closeMenu();
        }
    });

    window.addEventListener("resize", closeMenu);
    window.addEventListener("scroll", closeMenu, true);

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
