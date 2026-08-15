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
    let busy = false;
    let lockedForm = null;
    let ajaxObserver = null;

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

    const releaseFormLock = () => {
        if (lockedForm) {
            delete lockedForm.dataset.busyLocked;
            lockedForm = null;
        }
    };

    const hide = () => {
        ajaxObserver?.disconnect();
        ajaxObserver = null;
        releaseFormLock();
        busy = false;
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

    window.BadWolfBusy = Object.freeze({
        show,
        hide,
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
            return handler === "creategame" || handler === "continuegame";
        }

        if (currentPath === routes.publicQuizzes) {
            return handler === "creategame";
        }

        if (pathMatches(currentPath, routes.editor)) {
            return (submitter?.matches("[data-ajax-save-round]") ?? false) ||
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
        if (!shouldTrackLink(link)) {
            return;
        }

        if (busy) {
            event.preventDefault();
            event.stopImmediatePropagation();
            return;
        }

        show();
        window.setTimeout(() => {
            if (event.defaultPrevented) {
                hide();
            }
        }, 0);
    });

    window.addEventListener("pageshow", hide);
})();
