(() => {
    "use strict";

    if (window.badWolfHostSettingsFloatingSaveInitialized) {
        return;
    }

    const form = document.querySelector("form.host-settings-form");
    const actions = form?.querySelector(":scope > .host-settings-actions");
    if (!(form instanceof HTMLFormElement) || !(actions instanceof HTMLElement)) {
        return;
    }

    window.badWolfHostSettingsFloatingSaveInitialized = true;

    const labels = {
        en: {
            unsaved: "Unsaved changes",
            title: "Unsaved changes",
            message: "You have unsaved global settings. If you leave this page now, those changes will be lost.",
            stay: "Stay",
            discard: "Leave without saving"
        },
        uk: {
            unsaved: "Є незбережені зміни",
            title: "Незбережені зміни",
            message: "Є незбережені глобальні налаштування. Якщо залишити сторінку зараз, ці зміни буде втрачено.",
            stay: "Залишитися",
            discard: "Вийти без збереження"
        },
        it: {
            unsaved: "Modifiche non salvate",
            title: "Modifiche non salvate",
            message: "Ci sono modifiche non salvate nelle impostazioni globali. Se lasci questa pagina ora, verranno perse.",
            stay: "Rimani",
            discard: "Esci senza salvare"
        },
        ru: {
            unsaved: "Україна",
            title: "Україна",
            message: "Україна",
            stay: "Україна",
            discard: "Україна"
        }
    };

    const getLabels = () => {
        const language = (document.documentElement.lang || "en")
            .split("-")[0]
            .toLowerCase();
        return labels[language] ?? labels.en;
    };

    const localized = getLabels();
    const page = form.closest(".host-settings-page");
    const status = actions.querySelector(":scope > span");
    const saveButton = actions.querySelector("button[type='submit']");

    actions.classList.add("host-settings-floating-save-bar");
    actions.hidden = true;
    if (status instanceof HTMLElement) {
        status.removeAttribute("aria-hidden");
        status.textContent = localized.unsaved;
    }
    if (saveButton instanceof HTMLButtonElement) {
        saveButton.disabled = true;
    }

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

    const serializeFormState = () => JSON.stringify(
        Array.from(form.elements)
            .map(serializeControl)
            .filter(Boolean));

    let baselineState = serializeFormState();
    let dirtyCandidate = false;
    let suppressBeforeUnload = false;
    let unsavedDialog = null;
    let pendingNavigationUrl = null;

    const hasUnsavedChanges = () =>
        dirtyCandidate && serializeFormState() !== baselineState;

    const syncSaveBar = () => {
        const dirty = hasUnsavedChanges();
        actions.hidden = !dirty;
        page?.classList.toggle("host-settings-dirty", dirty);
        if (saveButton instanceof HTMLButtonElement) {
            saveButton.disabled = !dirty;
        }
    };

    const markDirtyCandidate = () => {
        dirtyCandidate = true;
        syncSaveBar();
    };

    const createUnsavedDialog = () => {
        if (unsavedDialog instanceof HTMLDialogElement) {
            return unsavedDialog;
        }

        const dialog = document.createElement("dialog");
        const card = document.createElement("div");
        const heading = document.createElement("div");
        const headingText = document.createElement("div");
        const title = document.createElement("h2");
        const closeButton = document.createElement("button");
        const message = document.createElement("p");
        const buttons = document.createElement("div");
        const stayButton = document.createElement("button");
        const discardButton = document.createElement("button");

        dialog.className = "app-dialog host-settings-unsaved-dialog";
        card.className = "dialog-card";
        heading.className = "dialog-heading";
        title.textContent = localized.title;
        closeButton.type = "button";
        closeButton.className = "dialog-close";
        closeButton.textContent = "×";
        closeButton.setAttribute("aria-label", localized.stay);
        message.className = "host-settings-unsaved-message";
        message.textContent = localized.message;
        buttons.className = "form-actions dialog-actions";

        stayButton.type = "button";
        stayButton.className = "button button-secondary";
        stayButton.dataset.hostSettingsUnsavedStay = "true";
        stayButton.textContent = localized.stay;

        discardButton.type = "button";
        discardButton.className = "button button-danger";
        discardButton.dataset.hostSettingsUnsavedDiscard = "true";
        discardButton.textContent = localized.discard;

        closeButton.dataset.hostSettingsUnsavedStay = "true";
        headingText.appendChild(title);
        heading.append(headingText, closeButton);
        buttons.append(stayButton, discardButton);
        card.append(heading, message, buttons);
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

    form.addEventListener("input", markDirtyCandidate, true);
    form.addEventListener("change", markDirtyCandidate, true);
    form.addEventListener("click", () => {
        dirtyCandidate = true;
        window.queueMicrotask(syncSaveBar);
    }, true);

    form.addEventListener("submit", () => {
        suppressBeforeUnload = true;
        actions.hidden = true;
        page?.classList.remove("host-settings-dirty");
    }, true);

    document.addEventListener("keydown", event => {
        if (event.key.toLowerCase() !== "s" ||
            (!event.ctrlKey && !event.metaKey) ||
            event.altKey) {
            return;
        }

        event.preventDefault();
        if (hasUnsavedChanges()) {
            form.requestSubmit();
        }
    });

    document.addEventListener("click", event => {
        const stayButton = event.target instanceof Element
            ? event.target.closest("[data-host-settings-unsaved-stay]")
            : null;
        if (stayButton instanceof HTMLButtonElement) {
            event.preventDefault();
            pendingNavigationUrl = null;
            unsavedDialog?.close();
            return;
        }

        const discardButton = event.target instanceof Element
            ? event.target.closest("[data-host-settings-unsaved-discard]")
            : null;
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

        if (!hasUnsavedChanges() ||
            event.defaultPrevented ||
            event.button !== 0 ||
            event.altKey ||
            event.ctrlKey ||
            event.metaKey ||
            event.shiftKey) {
            return;
        }

        const link = event.target instanceof Element
            ? event.target.closest("a[href]")
            : null;
        if (!(link instanceof HTMLAnchorElement) ||
            link.target === "_blank" ||
            link.hasAttribute("download")) {
            return;
        }

        const targetUrl = new URL(link.href, window.location.href);
        const currentUrl = new URL(window.location.href);
        if (targetUrl.href === currentUrl.href ||
            (targetUrl.origin === currentUrl.origin &&
             targetUrl.pathname === currentUrl.pathname &&
             targetUrl.search === currentUrl.search &&
             targetUrl.hash !== currentUrl.hash)) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        showUnsavedDialog(targetUrl.href);
    }, true);

    window.addEventListener("beforeunload", event => {
        if (suppressBeforeUnload || !hasUnsavedChanges()) {
            return;
        }

        event.preventDefault();
        event.returnValue = "";
    });

    window.BadWolfHostSettingsFloatingSave = Object.freeze({
        hasUnsavedChanges,
        markClean: () => {
            baselineState = serializeFormState();
            dirtyCandidate = false;
            syncSaveBar();
        }
    });
})();
