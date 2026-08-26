(() => {
    "use strict";

    if (window.badWolfContentBlockReorderButtonsInitialized) {
        return;
    }

    window.badWolfContentBlockReorderButtonsInitialized = true;

    const isContentBlockCard = element =>
        element instanceof HTMLElement &&
        element.classList.contains("content-block-card");

    const directCards = host =>
        Array.from(host?.children ?? [])
            .filter(isContentBlockCard)
            .filter(card => card.dataset.blockType !== "AnswerOptions");

    const getToolbar = card =>
        Array.from(card.children).find(element =>
            element.classList?.contains("content-block-toolbar"));

    const updateMoveButtons = () => {
        document.querySelectorAll(
            "[data-content-block-list], [data-content-block-container-children]")
            .forEach(host => {
                const cards = directCards(host);
                cards.forEach((card, index) => {
                    const toolbar = getToolbar(card);
                    const upButton = toolbar?.querySelector(
                        '[data-content-block-move="up"]');
                    const downButton = toolbar?.querySelector(
                        '[data-content-block-move="down"]');

                    if (upButton instanceof HTMLButtonElement) {
                        upButton.disabled = index === 0;
                    }

                    if (downButton instanceof HTMLButtonElement) {
                        downButton.disabled = index === cards.length - 1;
                    }
                });
            });
    };

    const escapeRegExp = value =>
        value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

    const reindexSection = section => {
        if (!(section instanceof HTMLElement)) {
            return;
        }

        const fieldPrefix = section.dataset.blockCollection;
        if (!fieldPrefix) {
            return;
        }

        const idPrefix = fieldPrefix.replaceAll(".", "_");
        const namePattern = new RegExp(
            `${escapeRegExp(fieldPrefix)}\\[\\d+\\]`,
            "g");
        const idPattern = new RegExp(
            `${escapeRegExp(idPrefix)}_\\d+__`,
            "g");
        const cards = section.querySelectorAll(".content-block-card");

        cards.forEach((card, index) => {
            card.querySelectorAll("[name]").forEach(element => {
                element.name = element.name.replace(
                    namePattern,
                    `${fieldPrefix}[${index}]`);
            });

            card.querySelectorAll("[id]").forEach(element => {
                element.id = element.id.replace(
                    idPattern,
                    `${idPrefix}_${index}__`);
            });

            card.querySelectorAll("label[for]").forEach(label => {
                label.htmlFor = label.htmlFor.replace(
                    idPattern,
                    `${idPrefix}_${index}__`);
            });

            const sortOrderInput = card.querySelector(
                `input[name="${fieldPrefix}[${index}].SortOrder"]`);
            if (sortOrderInput) {
                sortOrderInput.value = index;
            }
        });
    };

    const moveCard = (card, direction) => {
        const host = card.parentElement;
        if (!(host instanceof HTMLElement)) {
            return;
        }

        const cards = directCards(host);
        const currentIndex = cards.indexOf(card);
        const targetIndex = direction === "up"
            ? currentIndex - 1
            : currentIndex + 1;

        if (currentIndex < 0 ||
            targetIndex < 0 ||
            targetIndex >= cards.length) {
            updateMoveButtons();
            return;
        }

        const target = cards[targetIndex];
        if (direction === "up") {
            host.insertBefore(card, target);
        } else {
            host.insertBefore(target, card);
        }

        reindexSection(card.closest(".content-block-section"));
        updateMoveButtons();
    };

    document.addEventListener("click", event => {
        const button = event.target.closest("[data-content-block-move]");
        if (!(button instanceof HTMLButtonElement) || button.disabled) {
            return;
        }

        const direction = button.dataset.contentBlockMove;
        if (direction !== "up" && direction !== "down") {
            return;
        }

        const card = button.closest(".content-block-card");
        if (!isContentBlockCard(card)) {
            return;
        }

        event.preventDefault();
        moveCard(card, direction);
    }, true);

    let updateScheduled = false;
    const scheduleMoveButtonUpdate = () => {
        if (updateScheduled) {
            return;
        }

        updateScheduled = true;
        window.queueMicrotask(() => {
            updateScheduled = false;
            updateMoveButtons();
        });
    };

    const observer = new MutationObserver(mutations => {
        if (mutations.some(mutation => mutation.type === "childList")) {
            scheduleMoveButtonUpdate();
        }
    });

    const initialize = () => {
        updateMoveButtons();
        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, {
            once: true
        });
    } else {
        initialize();
    }
})();
