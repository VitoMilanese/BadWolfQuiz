(() => {
    if (window.badWolfEditorSaveShortcutInitialized) {
        return;
    }

    window.badWolfEditorSaveShortcutInitialized = true;

    const isSaveShortcut = event => {
        if (!(event.ctrlKey || event.metaKey) || event.altKey) {
            return false;
        }

        return event.code === "KeyS" ||
            event.key?.toLowerCase() === "s";
    };

    const resolveSaveTarget = () => {
        const quizEditorForm =
            document.querySelector("form.quiz-board-form");
        if (quizEditorForm) {
            return {
                form: quizEditorForm,
                submitter: quizEditorForm.querySelector(
                    "button[data-ajax-save-round]")
            };
        }

        const questionEditorForm =
            document.querySelector("form[data-ajax-question-editor]");
        if (questionEditorForm) {
            return {
                form: questionEditorForm,
                submitter: questionEditorForm.querySelector(
                    'button[type="submit"].button-primary')
            };
        }

        const metadataEditorForm =
            document.querySelector("form.quiz-metadata-editor");
        if (metadataEditorForm) {
            return {
                form: metadataEditorForm,
                submitter: metadataEditorForm.querySelector(
                    'button[type="submit"].button-primary')
            };
        }

        const descriptionEditorForm =
            document.querySelector("form.description-editor");
        if (descriptionEditorForm) {
            return {
                form: descriptionEditorForm,
                submitter: descriptionEditorForm.querySelector(
                    'button[type="submit"].button-primary')
            };
        }

        const finalQuestionEditorForm =
            document.querySelector("form.question-editor");
        if (finalQuestionEditorForm) {
            return {
                form: finalQuestionEditorForm,
                submitter: finalQuestionEditorForm.querySelector(
                    'button[type="submit"].button-primary')
            };
        }

        return null;
    };

    window.addEventListener("keydown", event => {
        if (!isSaveShortcut(event)) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();

        if (event.repeat || document.querySelector("dialog[open]")) {
            return;
        }

        const target = resolveSaveTarget();
        if (!target?.form || !target.submitter) {
            return;
        }

        if (target.submitter.disabled ||
            target.submitter.getAttribute("aria-disabled") === "true") {
            return;
        }

        target.form.requestSubmit(target.submitter);
    }, { capture: true });
})();
