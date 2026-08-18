(() => {
    "use strict";

    if (window.badWolfEditorResetInitialized) {
        return;
    }

    window.badWolfEditorResetInitialized = true;

    const resetTokenParameter = "_editorReset";
    const backLinkIds = [
        "question-editor-back-link",
        "final-question-editor-back-link",
        "description-editor-back"
    ];
    const editorForm = document.querySelector("form.question-editor");

    const labels = {
        en: {
            reset: "Reset",
            title: "Unsaved changes",
            message: "You have unsaved changes. If you leave this editor now, those changes will be lost.",
            stay: "Stay",
            discard: "Discard changes"
        },
        uk: {
            reset: "Скинути",
            title: "Незбережені зміни",
            message: "Є незбережені зміни. Якщо вийти з редактора зараз, ці зміни буде втрачено.",
            stay: "Залишитися",
            discard: "Вийти без збереження"
        },
        it: {
            reset: "Reimposta",
            title: "Modifiche non salvate",
            message: "Ci sono modifiche non salvate. Se esci ora dall'editor, verranno perse.",
            stay: "Rimani",
            discard: "Esci senza salvare"
        },
        ru: {
            reset: "Україна",
            title: "Несохранённые изменения",
            message: "Есть несохранённые изменения. Если выйти из редактора сейчас, они будут потеряны.",
            stay: "Остаться",
            discard: "Выйти без сохранения"
        }
    };

    const getLabels = () => {
        const language = (document.documentElement.lang || "en")
            .split("-")[0]
            .toLowerCase();
        return labels[language] ?? labels.en;
    };

    const clearResetToken = () => {
        const currentUrl = new URL(window.location.href);
        if (!currentUrl.searchParams.has(resetTokenParameter)) {
            return;
        }

        currentUrl.searchParams.delete(resetTokenParameter);
        window.history.replaceState(
            window.history.state,
            "",
            currentUrl.href);
    };

    const createResetButton = backLink => {
        if (!(backLink instanceof HTMLAnchorElement) ||
            backLink.nextElementSibling?.matches("[data-editor-reset]")) {
            return;
        }

        const localized = getLabels();
        const button = document.createElement("button");
        const icon = document.createElement("span");

        button.type = "button";
        button.className = "button button-secondary editor-reset-button";
        button.dataset.editorReset = "true";
        button.title = localized.reset;
        button.setAttribute("aria-label", localized.reset);

        icon.textContent = "↻";
        icon.setAttribute("aria-hidden", "true");
        button.appendChild(icon);

        backLink.insertAdjacentElement("afterend", button);
    };

    const serializeControl = element => {
        if (!(element instanceof HTMLInputElement ||
            element instanceof HTMLSelectElement ||
            element instanceof HTMLTextAreaElement) ||
            !element.name) {
            return null;
        }

        if (element instanceof HTMLInputElement && element.type === "file") {
            return {
                name: element.name,
                type: element.type,
                files: Array.from(element.files ?? []).map(file => ({
                    name: file.name,
                    size: file.size,
                    type: file.type,
                    lastModified: file.lastModified
                }))
            };
        }

        if (element instanceof HTMLInputElement &&
            (element.type === "checkbox" || element.type === "radio")) {
            return {
                name: element.name,
                type: element.type,
                value: element.value,
                checked: element.checked
            };
        }

        if (element instanceof HTMLSelectElement && element.multiple) {
            return {
                name: element.name,
                type: "select-multiple",
                values: Array.from(element.selectedOptions).map(option => option.value)
            };
        }

        return {
            name: element.name,
            type: element instanceof HTMLInputElement ? element.type : element.tagName,
            value: element.value
        };
    };

    const serializeEditorState = () => {
        if (!(editorForm instanceof HTMLFormElement)) {
            return "";
        }

        return JSON.stringify(
            Array.from(editorForm.elements)
                .map(serializeControl)
                .filter(Boolean));
    };

    let baselineState = "";
    let dirtyCandidate = false;
    let forceDirtyOnLoad = false;
    let suppressBeforeUnload = false;
    let pendingNavigationUrl = null;
    let unsavedDialog = null;

    const hasRenderedValidationErrors = () => {
        if (!(editorForm instanceof HTMLFormElement)) {
            return false;
        }

        return Array.from(editorForm.querySelectorAll(
            ".validation-summary li, .field-validation-error, .text-danger"))
            .some(element => Boolean(element.textContent?.trim()));
    };

    const markClean = () => {
        baselineState = serializeEditorState();
        dirtyCandidate = false;
        forceDirtyOnLoad = false;
    };

    const hasUnsavedChanges = () => {
        if (!(editorForm instanceof HTMLFormElement)) {
            return false;
        }

        if (forceDirtyOnLoad) {
            return true;
        }

        return dirtyCandidate && serializeEditorState() !== baselineState;
    };

    const createUnsavedDialog = () => {
        if (unsavedDialog instanceof HTMLDialogElement) {
            return unsavedDialog;
        }

        const localized = getLabels();
        const dialog = document.createElement("dialog");
        const card = document.createElement("div");
        const heading = document.createElement("div");
        const headingText = document.createElement("div");
        const title = document.createElement("h2");
        const closeButton = document.createElement("button");
        const message = document.createElement("p");
        const actions = document.createElement("div");
        const stayButton = document.createElement("button");
        const discardButton = document.createElement("button");

        dialog.className = "app-dialog editor-unsaved-dialog";
        dialog.dataset.editorUnsavedDialog = "true";

        card.className = "dialog-card";
        heading.className = "dialog-heading";
        title.textContent = localized.title;
        closeButton.type = "button";
        closeButton.className = "dialog-close";
        closeButton.textContent = "×";
        closeButton.setAttribute("aria-label", localized.stay);
        message.className = "editor-unsaved-dialog-message";
        message.textContent = localized.message;
        actions.className = "form-actions dialog-actions";

        stayButton.type = "button";
        stayButton.className = "button button-secondary";
        stayButton.dataset.editorUnsavedStay = "true";
        stayButton.textContent = localized.stay;

        discardButton.type = "button";
        discardButton.className = "button button-danger";
        discardButton.dataset.editorUnsavedDiscard = "true";
        discardButton.textContent = localized.discard;

        closeButton.dataset.editorUnsavedStay = "true";
        headingText.appendChild(title);
        heading.append(headingText, closeButton);
        actions.append(stayButton, discardButton);
        card.append(heading, message, actions);
        dialog.appendChild(card);
        document.body.appendChild(dialog);

        dialog.addEventListener("cancel", event => {
            event.preventDefault();
            pendingNavigationUrl = null;
            dialog.close();
        });

        unsavedDialog = dialog;
        return dialog;
    };

    const showUnsavedDialog = targetUrl => {
        pendingNavigationUrl = targetUrl;
        const dialog = createUnsavedDialog();
        if (!dialog.open) {
            dialog.showModal();
        }
    };

    const resetEditor = () => {
        suppressBeforeUnload = true;
        const targetUrl = new URL(window.location.href);
        targetUrl.searchParams.delete("saved");
        targetUrl.searchParams.set(
            resetTokenParameter,
            Date.now().toString());
        window.location.replace(targetUrl.href);
    };

    const initializeDirtyTracking = () => {
        if (!(editorForm instanceof HTMLFormElement)) {
            return;
        }

        markClean();
        forceDirtyOnLoad = hasRenderedValidationErrors();

        editorForm.addEventListener("input", () => {
            dirtyCandidate = true;
        }, true);

        editorForm.addEventListener("change", () => {
            dirtyCandidate = true;
        }, true);

        editorForm.addEventListener("submit", () => {
            if (!editorForm.hasAttribute("data-ajax-question-editor")) {
                suppressBeforeUnload = true;
            }
        }, true);

        const formObserver = new MutationObserver(mutations => {
            if (mutations.some(mutation => mutation.type === "childList")) {
                dirtyCandidate = true;
            }
        });
        formObserver.observe(editorForm, {
            childList: true,
            subtree: true
        });

        const questionSaveStatus = document.querySelector(
            "[data-question-save-status]");
        if (questionSaveStatus instanceof HTMLElement) {
            const syncSavedState = () => {
                if (!questionSaveStatus.hidden &&
                    questionSaveStatus.classList.contains("alert-success") &&
                    questionSaveStatus.textContent?.trim()) {
                    window.queueMicrotask(markClean);
                }
            };

            const saveObserver = new MutationObserver(syncSavedState);
            saveObserver.observe(questionSaveStatus, {
                attributes: true,
                attributeFilter: ["hidden", "class"],
                childList: true,
                characterData: true,
                subtree: true
            });
            syncSavedState();
        }

        window.addEventListener("beforeunload", event => {
            if (suppressBeforeUnload || !hasUnsavedChanges()) {
                return;
            }

            event.preventDefault();
            event.returnValue = "";
        });
    };

    const initialize = () => {
        clearResetToken();
        backLinkIds.forEach(id => {
            createResetButton(document.getElementById(id));
        });
        initializeDirtyTracking();
    };

    document.addEventListener("click", event => {
        const resetButton = event.target.closest("[data-editor-reset]");
        if (resetButton instanceof HTMLButtonElement) {
            event.preventDefault();
            resetEditor();
            return;
        }

        const stayButton = event.target.closest("[data-editor-unsaved-stay]");
        if (stayButton instanceof HTMLButtonElement) {
            event.preventDefault();
            pendingNavigationUrl = null;
            unsavedDialog?.close();
            return;
        }

        const discardButton = event.target.closest("[data-editor-unsaved-discard]");
        if (discardButton instanceof HTMLButtonElement) {
            event.preventDefault();
            const targetUrl = pendingNavigationUrl;
            pendingNavigationUrl = null;
            unsavedDialog?.close();
            if (targetUrl) {
                suppressBeforeUnload = true;
                window.location.assign(targetUrl);
            }
            return;
        }

        const navigationLink = event.target.closest(".editor-actions a[href]");
        if (!(navigationLink instanceof HTMLAnchorElement) ||
            !(editorForm instanceof HTMLFormElement) ||
            !editorForm.contains(navigationLink) ||
            !hasUnsavedChanges()) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        showUnsavedDialog(navigationLink.href);
    }, true);

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, {
            once: true
        });
    } else {
        initialize();
    }
})();
