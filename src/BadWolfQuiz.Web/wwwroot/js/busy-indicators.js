(() => {
    const routes = {
        quizzes: "/admin/quizzes",
        publicQuizzes: "/public-quizzes",
        editor: "/admin/quizzes/editor",
        questionEditor: "/admin/quizzes/questioneditor",
        finalQuestionEditor: "/admin/quizzes/finalquestioneditor",
        descriptionEditor: "/admin/quizzes/descriptioneditor"
    };

    const overlayId = "app-busy-overlay";
    const quizExportCompletionCookie = "badwolfquiz-export-complete";
    const quizExportTimeoutMilliseconds = 30 * 60 * 1000;
    let busy = false;
    let lockedForm = null;
    let lockedDialogButtons = [];
    let lockedRenameDialogControls = [];
    let lockedQuizControls = [];
    let lockedExportLink = null;
    let lockedExportAriaDisabled = null;
    let exportPollHandle = 0;
    let exportTimeoutHandle = 0;
    let ajaxObserver = null;
    let navigationScheduled = false;

    const normalisePath = value =>
        (value || "/").replace(/\/$/, "").toLowerCase() || "/";

    const pathMatches = (path, route) =>
        path === route || path.startsWith(`${route}/`);

    const isQuizzesIndex = path =>
        path === routes.quizzes || path === `${routes.quizzes}/index`;

    const isEditorPath = path =>
        pathMatches(path, routes.editor) ||
        pathMatches(path, routes.questionEditor) ||
        pathMatches(path, routes.finalQuestionEditor) ||
        pathMatches(path, routes.descriptionEditor);

    const isEditorNavigationTarget = path =>
        isQuizzesIndex(path) || isEditorPath(path);

    const createOverlay = () => {
        const overlay = document.createElement("dialog");
        overlay.id = overlayId;
        overlay.className = "app-busy-overlay";
        overlay.setAttribute("aria-label", "Loading");
        overlay.innerHTML = `
            <div class="app-busy-visual" aria-hidden="true">
                <span class="app-busy-orbit app-busy-orbit-outer"></span>
                <span class="app-busy-orbit app-busy-orbit-inner"></span>
                <span class="app-busy-core"></span>
            </div>`;
        overlay.addEventListener("cancel", event => event.preventDefault());
        document.body.appendChild(overlay);
        return overlay;
    };

    const getOverlay = () =>
        document.getElementById(overlayId) ?? createOverlay();

    const releaseExportLinkLock = () => {
        if (!lockedExportLink) {
            return;
        }

        delete lockedExportLink.dataset.busyLocked;
        if (lockedExportAriaDisabled === null) {
            lockedExportLink.removeAttribute("aria-disabled");
        } else {
            lockedExportLink.setAttribute("aria-disabled", lockedExportAriaDisabled);
        }
        lockedExportLink = null;
        lockedExportAriaDisabled = null;
    };

    const clearExportTracking = () => {
        if (exportPollHandle !== 0) {
            window.clearInterval(exportPollHandle);
            exportPollHandle = 0;
        }
        if (exportTimeoutHandle !== 0) {
            window.clearTimeout(exportTimeoutHandle);
            exportTimeoutHandle = 0;
        }
        releaseExportLinkLock();
    };

    const releaseFormLock = () => {
        if (lockedForm) {
            delete lockedForm.dataset.busyLocked;
            lockedForm = null;
        }

        lockedDialogButtons.forEach(({ button, wasDisabled }) => {
            button.disabled = wasDisabled;
        });
        lockedDialogButtons = [];

        lockedRenameDialogControls.forEach(({ control, wasDisabled }) => {
            control.disabled = wasDisabled;
        });
        lockedRenameDialogControls = [];

        lockedQuizControls.forEach(({ button, wasDisabled }) => {
            button.disabled = wasDisabled;
        });
        lockedQuizControls = [];
    };

    const hide = () => {
        ajaxObserver?.disconnect();
        ajaxObserver = null;
        releaseFormLock();
        clearExportTracking();
        busy = false;
        navigationScheduled = false;
        document.body.removeAttribute("aria-busy");

        const overlay = document.getElementById(overlayId);
        if (!overlay?.open) {
            return;
        }

        try {
            overlay.close();
        } catch {
            overlay.removeAttribute("open");
        }
    };

    const show = () => {
        if (busy) {
            return false;
        }

        const overlay = getOverlay();
        busy = true;
        document.body.setAttribute("aria-busy", "true");

        if (!overlay.open) {
            try {
                overlay.showModal();
            } catch {
                overlay.setAttribute("open", "");
            }
        }

        return true;
    };

    const runAfterPaint = callback => {
        if (typeof window.requestAnimationFrame !== "function" ||
            document.visibilityState !== "visible") {
            window.setTimeout(callback, 34);
            return;
        }

        window.requestAnimationFrame(() => {
            window.requestAnimationFrame(callback);
        });
    };

    const navigate = url => {
        if (!url || navigationScheduled) {
            return false;
        }

        if (!busy) {
            show();
        }

        navigationScheduled = true;
        runAfterPaint(() => {
            window.location.assign(url);
        });
        return true;
    };

    window.BadWolfBusy = Object.freeze({
        show,
        hide,
        navigate,
        get isBusy() {
            return busy;
        }
    });

    const handlerName = form => {
        try {
            return new URL(form.action || window.location.href, window.location.href)
                .searchParams
                .get("handler")
                ?.toLowerCase() ?? "";
        } catch {
            return "";
        }
    };

    const editorRenameHandler = form => {
        if (!pathMatches(
            normalisePath(window.location.pathname),
            routes.editor)) {
            return null;
        }

        const handler = handlerName(form);
        return handler === "renameround" || handler === "renamecategory"
            ? handler
            : null;
    };

    const lockAddRoundDialogButtons = form => {
        if (handlerName(form) !== "addround") {
            return;
        }

        const dialog = form.closest("dialog");
        if (!dialog) {
            return;
        }

        lockedDialogButtons = [...dialog.querySelectorAll("button")]
            .map(button => ({ button, wasDisabled: button.disabled }));
        lockedDialogButtons.forEach(({ button }) => {
            button.disabled = true;
        });
    };

    const lockEditorRenameDialogControls = form => {
        const dialog = form.closest("dialog");
        if (!dialog) {
            return;
        }

        lockedRenameDialogControls = [
            ...dialog.querySelectorAll("button, input, select, textarea")
        ].map(control => ({ control, wasDisabled: control.disabled }));

        lockedRenameDialogControls.forEach(({ control }) => {
            control.disabled = true;
        });
    };

    const lockQuizImportControl = form => {
        if (handlerName(form) !== "import") {
            return;
        }

        const button = form.querySelector("[data-quiz-import-select]");
        if (!(button instanceof HTMLButtonElement)) {
            return;
        }

        lockedQuizControls = [{ button, wasDisabled: button.disabled }];
        button.disabled = true;
    };

    const renameTitleInput = (form, handler) => {
        const name = handler === "renameround"
            ? "RenameRound.Title"
            : "RenameCategory.Title";
        const input = form.elements.namedItem(name);
        return input instanceof HTMLInputElement ? input : null;
    };

    const renameFallbackError = () =>
        document.querySelector(".quiz-board-form")?.dataset.saveError ||
        "Request failed.";

    const updateRenameCaches = (handler, itemId, title, questionIds) => {
        if (handler === "renameround") {
            if (typeof exchangeCategoryRounds !== "undefined") {
                const categoryRound = exchangeCategoryRounds.find(
                    item => item.id === itemId);
                if (categoryRound) {
                    categoryRound.title = title;
                }
            }

            if (typeof exchangeQuestionRounds !== "undefined") {
                const questionRound = exchangeQuestionRounds.find(
                    item => item.id === itemId);
                if (questionRound) {
                    questionRound.title = title;
                }
            }
            return;
        }

        if (typeof exchangeCategoryRounds !== "undefined") {
            for (const round of exchangeCategoryRounds) {
                const category = round.categories.find(item => item.id === itemId);
                if (category) {
                    category.title = title;
                    break;
                }
            }
        }

        if (typeof exchangeQuestionRounds !== "undefined" &&
            questionIds.size > 0) {
            for (const round of exchangeQuestionRounds) {
                for (const question of round.questions) {
                    if (questionIds.has(question.id)) {
                        question.category = title;
                    }
                }
            }
        }
    };

    const applyEditorRename = (handler, formData, title) => {
        if (handler === "renameround") {
            const roundId = Number(formData.get("RenameRound.RoundId"));
            const tab = document.querySelector(
                `.round-tab-item[data-round-id="${roundId}"]`);
            const link = tab?.querySelector(".round-tab-link");
            if (link) {
                link.textContent = title;
            }

            if (tab?.classList.contains("active")) {
                const deleteTarget = document.querySelector(
                    "#delete-round-dialog .dialog-target");
                if (deleteTarget) {
                    deleteTarget.textContent = title;
                }
            }

            updateRenameCaches(handler, roundId, title, new Set());
            return;
        }

        const categoryId = Number(formData.get("RenameCategory.CategoryId"));
        const column = document.querySelector(
            `.quiz-board-category-column[data-category-id="${categoryId}"]`);
        if (!column) {
            return;
        }

        const heading = column.querySelector(".category-title");
        if (heading) {
            heading.textContent = title;
        }

        column.querySelectorAll(".js-category-rename, .js-category-exchange")
            .forEach(button => {
                button.dataset.categoryTitle = title;
            });

        const questionIds = new Set();
        column.querySelectorAll(".question-cell-slot[data-question-id]")
            .forEach(slot => {
                const questionId = Number(slot.dataset.questionId);
                if (Number.isFinite(questionId)) {
                    questionIds.add(questionId);
                }

                const oldTitle = slot.dataset.questionTitle || "";
                const separatorIndex = oldTitle.indexOf(" — ");
                const questionTitle = separatorIndex >= 0
                    ? `${title}${oldTitle.slice(separatorIndex)}`
                    : title;

                slot.dataset.questionTitle = questionTitle;
                slot.querySelectorAll(
                    ".js-question-exchange, .js-question-delete")
                    .forEach(button => {
                        button.dataset.questionTitle = questionTitle;
                    });
            });

        updateRenameCaches(handler, categoryId, title, questionIds);
    };

    const showEditorRenameError = (input, message) => {
        if (!input) {
            return;
        }

        input.setCustomValidity(message);
        input.reportValidity();
        input.addEventListener("input", () => {
            input.setCustomValidity("");
        }, { once: true });
    };

    const submitEditorRename = async (form, handler, formData, title, input) => {
        let errorMessage = null;

        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: formData,
                redirect: "manual"
            });

            const redirected = response.type === "opaqueredirect" ||
                (response.status >= 300 && response.status < 400);
            if (!redirected && !response.ok) {
                throw new Error(renameFallbackError());
            }

            applyEditorRename(handler, formData, title);
            form.closest("dialog")?.close();
        } catch (error) {
            console.error("Quiz rename error:", error);
            errorMessage = error instanceof Error && error.message
                ? error.message
                : renameFallbackError();
        } finally {
            hide();
        }

        if (errorMessage) {
            showEditorRenameError(input, errorMessage);
        }
    };

    const isAjaxSave = (form, submitter, currentPath) => {
        if (pathMatches(currentPath, routes.editor)) {
            return submitter?.matches("[data-ajax-save-round]") ?? false;
        }

        if (pathMatches(currentPath, routes.questionEditor)) {
            return form.matches("[data-ajax-question-editor]");
        }

        return false;
    };

    const shouldTrackForm = (form, submitter) => {
        const currentPath = normalisePath(window.location.pathname);
        const handler = handlerName(form);

        if (isQuizzesIndex(currentPath)) {
            return handler === "creategame" ||
                handler === "continuegame" ||
                handler === "import";
        }

        if (currentPath === routes.publicQuizzes) {
            return handler === "creategame";
        }

        if (pathMatches(currentPath, routes.editor)) {
            return handler === "addround" ||
                (submitter?.matches("[data-ajax-save-round]") ?? false) ||
                (submitter?.getAttribute("name") === "play" &&
                    submitter?.getAttribute("value") === "true");
        }

        if (pathMatches(currentPath, routes.questionEditor)) {
            return form.matches("[data-ajax-question-editor]");
        }

        if (pathMatches(currentPath, routes.finalQuestionEditor) ||
            pathMatches(currentPath, routes.descriptionEditor)) {
            return form.matches("form.question-editor") &&
                form.method.toLowerCase() === "post";
        }

        return false;
    };

    const isQuizExportLink = link => {
        if (!link || !isQuizzesIndex(normalisePath(window.location.pathname))) {
            return false;
        }

        let target;
        try {
            target = new URL(link.href, window.location.href);
        } catch {
            return false;
        }

        return target.origin === window.location.origin &&
            isQuizzesIndex(normalisePath(target.pathname)) &&
            target.searchParams.get("handler")?.toLowerCase() === "export";
    };

    const readCookie = name => {
        const prefix = `${encodeURIComponent(name)}=`;
        const item = document.cookie
            .split("; ")
            .find(value => value.startsWith(prefix));
        return item ? decodeURIComponent(item.slice(prefix.length)) : null;
    };

    const clearQuizExportCookie = () => {
        document.cookie = `${encodeURIComponent(quizExportCompletionCookie)}=; Max-Age=0; Path=/; SameSite=Lax`;
    };

    const createExportToken = () => {
        if (typeof window.crypto?.randomUUID === "function") {
            return window.crypto.randomUUID();
        }

        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, character => {
            const random = Math.floor(Math.random() * 16);
            const value = character === "x" ? random : (random & 0x3) | 0x8;
            return value.toString(16);
        });
    };

    const startQuizExport = link => {
        const target = new URL(link.href, window.location.href);
        const token = createExportToken();
        target.searchParams.set("exportToken", token);

        lockedExportLink = link;
        lockedExportAriaDisabled = link.getAttribute("aria-disabled");
        link.dataset.busyLocked = "true";
        link.setAttribute("aria-disabled", "true");
        clearQuizExportCookie();
        show();

        exportPollHandle = window.setInterval(() => {
            if (readCookie(quizExportCompletionCookie) !== token) {
                return;
            }

            clearQuizExportCookie();
            hide();
        }, 100);
        exportTimeoutHandle = window.setTimeout(
            hide,
            quizExportTimeoutMilliseconds);

        window.location.assign(target.href);
    };

    const shouldTrackLink = link => {
        if (!link || link.target === "_blank" || link.hasAttribute("download")) {
            return false;
        }

        let target;
        try {
            target = new URL(link.href, window.location.href);
        } catch {
            return false;
        }

        if (target.origin !== window.location.origin) {
            return false;
        }

        const currentPath = normalisePath(window.location.pathname);
        const targetPath = normalisePath(target.pathname);

        if (isQuizzesIndex(currentPath)) {
            return pathMatches(targetPath, routes.editor);
        }

        return isEditorPath(currentPath) &&
            isEditorNavigationTarget(targetPath);
    };

    const getEscapeBackLink = () => {
        const currentPath = normalisePath(window.location.pathname);

        if (pathMatches(currentPath, routes.editor)) {
            return document.getElementById("quiz-editor-my-quizzes");
        }

        if (pathMatches(currentPath, routes.questionEditor)) {
            return document.getElementById("question-editor-back-link");
        }

        if (pathMatches(currentPath, routes.finalQuestionEditor)) {
            return document.getElementById("final-question-editor-back-link");
        }

        if (pathMatches(currentPath, routes.descriptionEditor)) {
            return document.getElementById("description-editor-back");
        }

        return null;
    };

    const hasOpenEditorModal = () => {
        const preview = document.getElementById("question-preview-modal");
        if (preview && !preview.hidden) {
            return true;
        }

        return [...document.querySelectorAll("dialog[open]")]
            .some(dialog => dialog.id !== overlayId);
    };

    const watchAjaxCompletion = submitter => {
        if (!submitter) {
            hide();
            return;
        }

        const finishWhenEnabled = () => {
            if (!submitter.disabled) {
                hide();
                return true;
            }
            return false;
        };

        if (finishWhenEnabled()) {
            return;
        }

        ajaxObserver?.disconnect();
        ajaxObserver = new MutationObserver(() => {
            finishWhenEnabled();
        });
        ajaxObserver.observe(submitter, {
            attributes: true,
            attributeFilter: ["disabled"]
        });
    };

    document.addEventListener("submit", event => {
        const form = event.target instanceof HTMLFormElement ? event.target : null;
        if (!form) {
            return;
        }

        const renameHandler = editorRenameHandler(form);
        if (renameHandler) {
            event.preventDefault();
            event.stopImmediatePropagation();

            if (busy || form.dataset.busyLocked === "true") {
                return;
            }

            const input = renameTitleInput(form, renameHandler);
            const title = input?.value.trim() ?? "";
            if (!input || !title) {
                if (input) {
                    input.value = "";
                    input.reportValidity();
                }
                return;
            }

            input.setCustomValidity("");
            const formData = new FormData(form);
            formData.set(input.name, title);

            lockedForm = form;
            form.dataset.busyLocked = "true";
            lockEditorRenameDialogControls(form);
            show();

            void submitEditorRename(
                form,
                renameHandler,
                formData,
                title,
                input);
            return;
        }

        const submitter = event.submitter ??
            form.querySelector('button[type="submit"], input[type="submit"]');
        if (!shouldTrackForm(form, submitter)) {
            return;
        }

        if (busy || form.dataset.busyLocked === "true") {
            event.preventDefault();
            event.stopImmediatePropagation();
            return;
        }

        lockedForm = form;
        form.dataset.busyLocked = "true";
        lockAddRoundDialogButtons(form);
        lockQuizImportControl(form);
        show();

        const currentPath = normalisePath(window.location.pathname);
        if (!event.defaultPrevented) {
            return;
        }

        if (isAjaxSave(form, submitter, currentPath)) {
            watchAjaxCompletion(submitter);
            return;
        }

        window.setTimeout(hide, 0);
    });

    document.addEventListener("click", event => {
        if (event.defaultPrevented ||
            event.button !== 0 ||
            event.metaKey ||
            event.ctrlKey ||
            event.shiftKey ||
            event.altKey) {
            return;
        }

        const link = event.target instanceof Element
            ? event.target.closest("a[href]")
            : null;
        if (isQuizExportLink(link)) {
            event.preventDefault();
            event.stopImmediatePropagation();

            if (busy || link.dataset.busyLocked === "true") {
                return;
            }

            startQuizExport(link);
            return;
        }

        if (!shouldTrackLink(link)) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();

        if (busy) {
            return;
        }

        navigate(link.href);
    });

    window.addEventListener("keydown", event => {
        if (event.key !== "Escape") {
            return;
        }

        if (busy || navigationScheduled) {
            event.preventDefault();
            event.stopImmediatePropagation();
            return;
        }

        if (hasOpenEditorModal()) {
            return;
        }

        const backLink = getEscapeBackLink();
        if (!backLink?.href) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        backLink.click();
    }, true);

    window.addEventListener("keyup", event => {
        if (event.key !== "Escape" || (!busy && !navigationScheduled)) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
    }, true);

    window.addEventListener("pageshow", hide);
})();
