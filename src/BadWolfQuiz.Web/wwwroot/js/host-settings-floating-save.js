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
            saved: "Global settings saved",
            saveFailed: "Global settings could not be saved.",
            title: "Unsaved changes",
            message: "You have unsaved global settings. If you leave this page now, those changes will be lost.",
            stay: "Stay",
            discard: "Leave without saving"
        },
        uk: {
            unsaved: "Є незбережені зміни",
            saved: "Глобальні налаштування збережено",
            saveFailed: "Не вдалося зберегти глобальні налаштування.",
            title: "Незбережені зміни",
            message: "Є незбережені глобальні налаштування. Якщо залишити сторінку зараз, ці зміни буде втрачено.",
            stay: "Залишитися",
            discard: "Вийти без збереження"
        },
        it: {
            unsaved: "Modifiche non salvate",
            saved: "Impostazioni globali salvate",
            saveFailed: "Impossibile salvare le impostazioni globali.",
            title: "Modifiche non salvate",
            message: "Ci sono modifiche non salvate nelle impostazioni globali. Se lasci questa pagina ora, verranno perse.",
            stay: "Rimani",
            discard: "Esci senza salvare"
        },
        ru: {
            unsaved: "Україна",
            saved: "Україна",
            saveFailed: "Україна",
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
    const pageShell = form.closest(".page-shell") ?? document.querySelector(".page-shell");
    const footer = document.querySelector(".portal-footer");
    const validationSummary = form.querySelector(".host-settings-validation");
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
    let saveInProgress = false;
    let savedFeedbackTimer = 0;

    const hasUnsavedChanges = () =>
        dirtyCandidate && serializeFormState() !== baselineState;

    const updateFooterOffset = () => {
        let visibleFooterHeight = 0;
        if (footer instanceof HTMLElement) {
            const rect = footer.getBoundingClientRect();
            const viewportHeight = window.visualViewport?.height ?? window.innerHeight;
            visibleFooterHeight = Math.max(
                0,
                Math.min(rect.bottom, viewportHeight) - Math.max(rect.top, 0));
        }

        actions.style.setProperty(
            "--host-settings-footer-offset",
            `${Math.ceil(visibleFooterHeight)}px`);
    };

    const clearSavedFeedback = () => {
        if (savedFeedbackTimer !== 0) {
            window.clearTimeout(savedFeedbackTimer);
            savedFeedbackTimer = 0;
        }
        delete actions.dataset.saveState;
    };

    const syncSaveBar = () => {
        const dirty = hasUnsavedChanges();
        if (dirty) {
            clearSavedFeedback();
            if (status instanceof HTMLElement) {
                status.textContent = localized.unsaved;
            }
            if (saveButton instanceof HTMLButtonElement) {
                saveButton.hidden = false;
                saveButton.disabled = saveInProgress;
            }
        }

        if (!dirty && actions.dataset.saveState !== "saved") {
            actions.hidden = true;
        } else {
            actions.hidden = false;
            updateFooterOffset();
        }

        page?.classList.toggle(
            "host-settings-dirty",
            dirty || actions.dataset.saveState === "saved");
    };

    const markDirtyCandidate = () => {
        dirtyCandidate = true;
        syncSaveBar();
    };

    const setValidationErrors = errors => {
        if (!(validationSummary instanceof HTMLElement)) {
            return;
        }

        validationSummary.replaceChildren();
        validationSummary.classList.toggle(
            "validation-summary-errors",
            errors.length > 0);
        validationSummary.classList.toggle(
            "validation-summary-valid",
            errors.length === 0);

        if (errors.length === 0) {
            return;
        }

        const list = document.createElement("ul");
        for (const error of errors) {
            const item = document.createElement("li");
            item.textContent = error;
            list.appendChild(item);
        }
        validationSummary.appendChild(list);
    };

    const extractValidationErrors = documentRoot => {
        const messages = Array.from(documentRoot.querySelectorAll(
            ".host-settings-validation li, .field-validation-error"))
            .map(element => element.textContent?.trim() ?? "")
            .filter(Boolean);
        return [...new Set(messages)];
    };

    const syncReturnedBrandUi = returnedDocument => {
        const currentBrand = document.querySelector("a.brand");
        const returnedBrand = returnedDocument.querySelector("a.brand");
        if (currentBrand instanceof HTMLAnchorElement &&
            returnedBrand instanceof HTMLAnchorElement) {
            currentBrand.innerHTML = returnedBrand.innerHTML;
        }

        const currentGrid = form.querySelector(".host-settings-brand-grid");
        const returnedGrid = returnedDocument.querySelector(
            "form.host-settings-form .host-settings-brand-grid");
        if (!(currentGrid instanceof HTMLElement) ||
            !(returnedGrid instanceof HTMLElement)) {
            return;
        }

        const currentRemoveLabel = currentGrid
            .querySelector('input[name="RemoveBrandLogo"]')
            ?.closest("label");
        const returnedRemoveLabel = returnedGrid
            .querySelector('input[name="RemoveBrandLogo"]')
            ?.closest("label");

        if (currentRemoveLabel && !returnedRemoveLabel) {
            currentRemoveLabel.remove();
        } else if (!currentRemoveLabel && returnedRemoveLabel) {
            currentGrid.appendChild(returnedRemoveLabel.cloneNode(true));
        } else if (currentRemoveLabel) {
            const removeInput = currentRemoveLabel.querySelector(
                'input[name="RemoveBrandLogo"]');
            if (removeInput instanceof HTMLInputElement) {
                removeInput.checked = false;
            }
        }

        const currentPreview = currentGrid.querySelector("[data-brand-logo-preview]");
        const returnedPreview = returnedGrid.querySelector("[data-brand-logo-preview]");
        if (!(currentPreview instanceof HTMLImageElement)) {
            return;
        }

        if (!(returnedPreview instanceof HTMLImageElement) || returnedPreview.hidden) {
            currentPreview.hidden = true;
            currentPreview.removeAttribute("src");
            return;
        }

        if (returnedPreview.src) {
            const savedLogoUrl = new URL(returnedPreview.src, window.location.href);
            savedLogoUrl.searchParams.set("v", Date.now().toString());
            currentPreview.src = savedLogoUrl.href;
            currentPreview.hidden = false;
        }
    };

    const showSavedFeedback = message => {
        clearSavedFeedback();
        actions.dataset.saveState = "saved";
        if (status instanceof HTMLElement) {
            status.textContent = message || localized.saved;
        }
        if (saveButton instanceof HTMLButtonElement) {
            saveButton.hidden = true;
            saveButton.disabled = true;
        }
        actions.hidden = false;
        page?.classList.add("host-settings-dirty");
        updateFooterOffset();

        savedFeedbackTimer = window.setTimeout(() => {
            savedFeedbackTimer = 0;
            delete actions.dataset.saveState;
            if (saveButton instanceof HTMLButtonElement) {
                saveButton.hidden = false;
            }
            syncSaveBar();
        }, 1400);
    };

    const saveSettings = async () => {
        if (saveInProgress || !hasUnsavedChanges()) {
            return;
        }

        const submittedData = new FormData(form);
        const hadInert = form.hasAttribute("inert");
        saveInProgress = true;
        form.setAttribute("inert", "");
        if (saveButton instanceof HTMLButtonElement) {
            saveButton.disabled = true;
            saveButton.setAttribute("aria-busy", "true");
        }
        window.BadWolfBusy?.show();

        try {
            const response = await fetch(form.action || window.location.href, {
                method: "POST",
                body: submittedData,
                credentials: "same-origin",
                headers: {
                    Accept: "text/html",
                    "X-Requested-With": "XMLHttpRequest"
                }
            });
            const html = await response.text();
            const returnedDocument = new DOMParser().parseFromString(
                html,
                "text/html");
            const returnedForm = returnedDocument.querySelector(
                "form.host-settings-form");

            if (!response.ok ||
                !response.redirected ||
                !(returnedForm instanceof HTMLFormElement)) {
                const errors = extractValidationErrors(returnedDocument);
                setValidationErrors(
                    errors.length > 0 ? errors : [localized.saveFailed]);
                return;
            }

            setValidationErrors([]);
            syncReturnedBrandUi(returnedDocument);
            form.querySelectorAll('input[type="file"]')
                .forEach(input => {
                    if (input instanceof HTMLInputElement) {
                        input.value = "";
                    }
                });

            baselineState = serializeFormState();
            dirtyCandidate = false;
            const message = returnedDocument
                .querySelector(".host-settings-message strong")
                ?.textContent
                ?.trim();
            showSavedFeedback(message || localized.saved);
        } catch (error) {
            console.error("Global settings save failed:", error);
            setValidationErrors([localized.saveFailed]);
        } finally {
            saveInProgress = false;
            if (!hadInert) {
                form.removeAttribute("inert");
            }
            if (saveButton instanceof HTMLButtonElement) {
                saveButton.removeAttribute("aria-busy");
                saveButton.disabled = !hasUnsavedChanges();
            }
            window.BadWolfBusy?.hide();
            updateFooterOffset();
        }
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
    form.addEventListener("submit", event => {
        event.preventDefault();
        void saveSettings();
    }, true);

    document.addEventListener("keydown", event => {
        if (event.key.toLowerCase() !== "s" ||
            (!event.ctrlKey && !event.metaKey) ||
            event.altKey) {
            return;
        }

        event.preventDefault();
        if (hasUnsavedChanges() && !saveInProgress) {
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

    window.addEventListener("resize", updateFooterOffset);
    window.addEventListener("scroll", updateFooterOffset, { passive: true });
    pageShell?.addEventListener("scroll", updateFooterOffset, { passive: true });
    window.visualViewport?.addEventListener("resize", updateFooterOffset);
    window.visualViewport?.addEventListener("scroll", updateFooterOffset);
    if (footer instanceof HTMLElement && "ResizeObserver" in window) {
        new ResizeObserver(updateFooterOffset).observe(footer);
    }
    updateFooterOffset();

    window.BadWolfHostSettingsFloatingSave = Object.freeze({
        hasUnsavedChanges,
        markClean: () => {
            baselineState = serializeFormState();
            dirtyCandidate = false;
            syncSaveBar();
        }
    });
})();
