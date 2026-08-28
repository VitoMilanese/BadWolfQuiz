(() => {
    "use strict";

    if (window.badWolfPeerRatedRatingConfirmationInitialized) {
        return;
    }
    window.badWolfPeerRatedRatingConfirmationInitialized = true;

    const culture = (document.documentElement.lang || "en")
        .slice(0, 2)
        .toLowerCase();
    const strings = {
        en: {
            confirmRating: "Confirm rating",
            ratedStatus: "Rated"
        },
        uk: {
            confirmRating: "Підтвердити оцінку",
            ratedStatus: "Оцінив"
        },
        it: {
            confirmRating: "Conferma valutazione",
            ratedStatus: "Ha votato"
        },
        ru: {
            confirmRating: "Україна",
            ratedStatus: "Україна"
        }
    };
    const text = strings[culture] ?? strings.en;
    let submitBypass = false;

    const style = document.createElement("style");
    style.id = "peer-rated-rating-confirmation-styles";
    style.textContent = `
.star-rating label:not(.zero-rating) {
    position: relative;
    z-index: 1;
    display: grid;
    flex: 0 0 auto;
    min-inline-size: 44px;
    min-block-size: 44px;
    place-items: center;
    padding: .1rem;
    touch-action: manipulation;
    -webkit-tap-highlight-color: transparent;
}
.star-rating input[type="radio"] {
    pointer-events: none;
}
.peer-rated-rating-editor {
    display: grid;
    justify-items: center;
    gap: .65rem;
}
.peer-rated-rating-editor > .star-rating {
    justify-self: stretch;
}
.peer-rated-rating-editor .peer-rated-zero-button {
    display: block;
    margin: .6rem auto 0 !important;
}
.peer-rated-rating-editor .peer-rated-zero-button[aria-pressed="true"] {
    outline: 2px solid currentColor;
    outline-offset: 2px;
}
.peer-rated-confirm-rating-button {
    min-width: min(100%, 14rem);
}
`;
    document.head.appendChild(style);

    const activateStarLabel = event => {
        if (!(event.target instanceof Element)) {
            return;
        }
        const label = event.target.closest(".star-rating label:not(.zero-rating)");
        if (!(label instanceof HTMLLabelElement) || !label.htmlFor) {
            return;
        }
        const input = document.getElementById(label.htmlFor);
        if (!(input instanceof HTMLInputElement) || input.type !== "radio") {
            return;
        }

        event.preventDefault();
        input.checked = true;
        input.dispatchEvent(new Event("change", { bubbles: true }));
    };

    const getEditor = element => {
        if (!(element instanceof Element)) {
            return null;
        }
        const fieldset = element.matches(".star-rating")
            ? element
            : element.closest(".peer-rated-review-card")?.querySelector(".star-rating");
        if (!(fieldset instanceof HTMLFieldSetElement)) {
            return null;
        }
        const wrapper = fieldset.parentElement;
        const zero = wrapper?.querySelector(".peer-rated-zero-button");
        if (!(wrapper instanceof HTMLElement) || !(zero instanceof HTMLButtonElement)) {
            return null;
        }
        return { wrapper, fieldset, zero };
    };

    const setDraft = (editor, score) => {
        editor.wrapper.dataset.peerRatedDraftRating = String(score);
        editor.zero.setAttribute("aria-pressed", score === 0 ? "true" : "false");
        if (score === 0) {
            editor.fieldset.querySelectorAll('input[type="radio"]')
                .forEach(input => {
                    if (input instanceof HTMLInputElement) {
                        input.checked = false;
                    }
                });
        }
        const confirm = editor.wrapper.querySelector(".peer-rated-confirm-rating-button");
        if (confirm instanceof HTMLButtonElement) {
            confirm.disabled = false;
        }
    };

    const submitDraft = editor => {
        const draft = Number.parseInt(
            editor.wrapper.dataset.peerRatedDraftRating ?? "",
            10);
        if (!Number.isInteger(draft) || draft < 0 || draft > 5) {
            return;
        }

        submitBypass = true;
        try {
            if (draft === 0) {
                editor.zero.click();
                return;
            }

            const input = Array.from(
                editor.fieldset.querySelectorAll('input[type="radio"]'))
                .find(candidate =>
                    candidate instanceof HTMLInputElement &&
                    Number.parseInt(candidate.value, 10) === draft);
            if (input instanceof HTMLInputElement) {
                input.checked = true;
                input.dispatchEvent(new Event("change", { bubbles: true }));
            }
        } finally {
            submitBypass = false;
        }
    };

    const enhanceEditor = fieldset => {
        const editor = getEditor(fieldset);
        if (!editor || editor.wrapper.dataset.peerRatedConfirmationEnhanced === "true") {
            return;
        }

        editor.wrapper.dataset.peerRatedConfirmationEnhanced = "true";
        editor.wrapper.classList.add("peer-rated-rating-editor");
        editor.zero.setAttribute("aria-pressed", "false");

        const confirm = document.createElement("button");
        confirm.type = "button";
        confirm.className = "button button-primary peer-rated-confirm-rating-button";
        confirm.textContent = text.confirmRating;
        confirm.disabled = true;
        confirm.addEventListener("click", () => submitDraft(editor));
        editor.wrapper.appendChild(confirm);
    };

    const enhanceEditors = root => {
        if (!(root instanceof Document || root instanceof Element)) {
            return;
        }
        if (root instanceof Element && root.matches(".star-rating")) {
            enhanceEditor(root);
        }
        root.querySelectorAll?.(".peer-rated-review-card .star-rating")
            .forEach(enhanceEditor);
    };

    const interceptRatingChange = event => {
        if (submitBypass || !(event.target instanceof HTMLInputElement)) {
            return;
        }
        if (!event.target.matches(
            '.peer-rated-review-card .star-rating input[type="radio"]')) {
            return;
        }

        const editor = getEditor(event.target);
        if (!editor) {
            return;
        }

        event.stopImmediatePropagation();
        const score = Number.parseInt(event.target.value, 10);
        if (Number.isInteger(score)) {
            setDraft(editor, score);
        }
    };

    const interceptZeroClick = event => {
        if (submitBypass || !(event.target instanceof Element)) {
            return;
        }
        const zero = event.target.closest(
            ".peer-rated-review-card .peer-rated-zero-button");
        if (!(zero instanceof HTMLButtonElement)) {
            return;
        }

        const editor = getEditor(zero);
        if (!editor) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        setDraft(editor, 0);
    };

    const maskVotingRatings = root => {
        if (!(root instanceof Document || root instanceof Element)) {
            return;
        }
        const uiElements = [];
        if (root instanceof Element && root.matches(".peer-rated-host-ui")) {
            uiElements.push(root);
        }
        root.querySelectorAll?.(".peer-rated-host-ui").forEach(ui => uiElements.push(ui));

        for (const ui of uiElements) {
            if (ui.querySelector(".peer-rated-result-summary")) {
                continue;
            }
            ui.querySelectorAll(".peer-rated-host-sidebar .peer-rated-host-list li > span")
                .forEach(status => {
                    if (/^\s*[0-5]\s*★\s*$/.test(status.textContent ?? "")) {
                        status.textContent = text.ratedStatus;
                    }
                });
        }
    };

    document.addEventListener("click", activateStarLabel);
    document.addEventListener("change", interceptRatingChange, true);
    document.addEventListener("click", interceptZeroClick, true);

    const refresh = root => {
        enhanceEditors(root);
        maskVotingRatings(root);
    };

    refresh(document);
    new MutationObserver(mutations => {
        for (const mutation of mutations) {
            mutation.addedNodes.forEach(node => {
                if (node instanceof Element) {
                    refresh(node);
                }
            });
        }
        maskVotingRatings(document);
    }).observe(document.documentElement, {
        childList: true,
        subtree: true
    });
})();
