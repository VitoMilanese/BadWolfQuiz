(() => {
    "use strict";

    if (window.badWolfQuestionCopyActionInitialized) {
        return;
    }

    window.badWolfQuestionCopyActionInitialized = true;

    const quizBoardForm = document.querySelector("form.quiz-board-form");
    const questionSlots = Array.from(document.querySelectorAll(
        ".question-cell-slot[data-question-id]"));
    if (!(quizBoardForm instanceof HTMLFormElement) || questionSlots.length === 0) {
        return;
    }

    const language = (document.documentElement.lang || "en")
        .split("-")[0]
        .toLowerCase();
    const labelsByLanguage = {
        en: {
            action: "Copy or clone question",
            title: "Copy question",
            text: "Copy this question to a selected quiz and category.",
            quiz: "Target quiz",
            category: "Target category",
            selectQuiz: "Choose a quiz",
            selectCategory: "Choose a category",
            full: "full",
            loading: "Loading available destinations…",
            noDestination: "No active quiz category has room for an additional question.",
            success: "Question copied.",
            sourceMissing: "The source question is no longer available.",
            targetMissing: "The selected destination is no longer available.",
            noCapacity: "The selected category no longer has room for another question.",
            invalid: "The question could not be copied.",
            cancel: "Cancel",
            submit: "Copy",
            close: "Close"
        },
        uk: {
            action: "Копіювати або клонувати питання",
            title: "Копіювати питання",
            text: "Скопіювати це питання у вибрану вікторину та категорію.",
            quiz: "Цільова вікторина",
            category: "Цільова категорія",
            selectQuiz: "Оберіть вікторину",
            selectCategory: "Оберіть категорію",
            full: "заповнено",
            loading: "Завантаження доступних місць…",
            noDestination: "В активних вікторинах немає категорії з місцем для додаткового питання.",
            success: "Питання скопійовано.",
            sourceMissing: "Початкове питання більше недоступне.",
            targetMissing: "Вибране місце більше недоступне.",
            noCapacity: "У вибраній категорії більше немає місця для нового питання.",
            invalid: "Не вдалося скопіювати питання.",
            cancel: "Скасувати",
            submit: "Копіювати",
            close: "Закрити"
        },
        it: {
            action: "Copia o clona la domanda",
            title: "Copia domanda",
            text: "Copia questa domanda nel quiz e nella categoria selezionati.",
            quiz: "Quiz di destinazione",
            category: "Categoria di destinazione",
            selectQuiz: "Scegli un quiz",
            selectCategory: "Scegli una categoria",
            full: "completa",
            loading: "Caricamento destinazioni disponibili…",
            noDestination: "Nessuna categoria nei quiz attivi ha spazio per una domanda aggiuntiva.",
            success: "Domanda copiata.",
            sourceMissing: "La domanda di origine non è più disponibile.",
            targetMissing: "La destinazione selezionata non è più disponibile.",
            noCapacity: "La categoria selezionata non ha più spazio per un'altra domanda.",
            invalid: "Non è stato possibile copiare la domanda.",
            cancel: "Annulla",
            submit: "Copia",
            close: "Chiudi"
        }
    };
    const labels = language === "ru"
        ? labelsByLanguage.uk
        : labelsByLanguage[language] ?? labelsByLanguage.en;

    const layoutStyle = document.createElement("style");
    layoutStyle.id = "question-copy-action-layout";
    layoutStyle.textContent = `
.question-card-actions.has-question-copy {
    top: 0;
    bottom: 0;
    gap: 0;
    justify-content: space-evenly;
}

.question-card-actions.has-question-copy .icon-button {
    width: 30px;
    height: 30px;
    min-height: 30px;
}`;
    document.head.appendChild(layoutStyle);

    const copyEndpoint = (() => {
        const match = window.location.pathname.match(
            /^(.*\/Admin\/Quizzes)\/Editor(?:\/.*)?$/i);
        return match ? `${match[1]}/QuestionCopy` : "/Admin/Quizzes/QuestionCopy";
    })();

    const quizSaveStatus = document.querySelector("[data-quiz-save-status]");

    const dialog = document.createElement("dialog");
    dialog.className = "app-dialog";
    dialog.dataset.questionCopyDialog = "true";

    const form = document.createElement("form");
    form.className = "dialog-card";
    form.method = "post";

    const heading = document.createElement("div");
    heading.className = "dialog-heading";
    const headingText = document.createElement("div");
    const title = document.createElement("h2");
    title.id = "questionCopyDialogTitle";
    title.textContent = labels.title;
    headingText.appendChild(title);
    const closeButton = document.createElement("button");
    closeButton.className = "dialog-close";
    closeButton.type = "button";
    closeButton.dataset.closeQuestionCopy = "true";
    closeButton.setAttribute("aria-label", labels.close);
    closeButton.textContent = "×";
    heading.append(headingText, closeButton);
    form.appendChild(heading);

    const description = document.createElement("p");
    description.textContent = labels.text;
    form.appendChild(description);

    const fields = document.createElement("div");
    fields.className = "stack-form";

    const quizLabel = document.createElement("label");
    quizLabel.htmlFor = "questionCopyQuiz";
    quizLabel.textContent = labels.quiz;
    const quizSelect = document.createElement("select");
    quizSelect.id = "questionCopyQuiz";
    quizSelect.required = true;
    quizSelect.dataset.questionCopyQuiz = "true";

    const categoryLabel = document.createElement("label");
    categoryLabel.htmlFor = "questionCopyCategory";
    categoryLabel.textContent = labels.category;
    const categorySelect = document.createElement("select");
    categorySelect.id = "questionCopyCategory";
    categorySelect.required = true;
    categorySelect.dataset.questionCopyCategory = "true";

    fields.append(quizLabel, quizSelect, categoryLabel, categorySelect);
    form.appendChild(fields);

    const status = document.createElement("div");
    status.className = "alert";
    status.setAttribute("role", "status");
    status.hidden = true;
    form.appendChild(status);

    const actions = document.createElement("div");
    actions.className = "form-actions dialog-actions";
    const cancelButton = document.createElement("button");
    cancelButton.className = "button button-secondary";
    cancelButton.type = "button";
    cancelButton.dataset.closeQuestionCopy = "true";
    cancelButton.textContent = labels.cancel;
    const submitButton = document.createElement("button");
    submitButton.className = "button button-primary";
    submitButton.type = "submit";
    submitButton.textContent = labels.submit;
    actions.append(cancelButton, submitButton);
    form.appendChild(actions);

    dialog.setAttribute("aria-labelledby", title.id);
    dialog.appendChild(form);
    document.body.appendChild(dialog);

    let quizzes = [];
    let sourceQuestionId = "";

    const setStatus = (message, kind = "") => {
        status.textContent = message;
        status.classList.remove("alert-success", "alert-error");
        if (kind === "error") {
            status.classList.add("alert-error");
        }
        status.hidden = !message;
    };

    const showQuizOverlay = message => {
        if (!(quizSaveStatus instanceof HTMLElement)) {
            return;
        }

        quizSaveStatus.textContent = message;
        quizSaveStatus.classList.remove(
            "alert-error",
            "message-hidden",
            "editor-save-overlay-hiding");
        quizSaveStatus.classList.add("alert-success");
        quizSaveStatus.hidden = false;
    };

    const addPlaceholder = (select, text) => {
        const option = document.createElement("option");
        option.value = "";
        option.textContent = text;
        option.selected = true;
        option.disabled = true;
        select.appendChild(option);
    };

    const selectedQuiz = () => quizzes.find(
        quiz => String(quiz.id) === quizSelect.value);

    const syncCategories = () => {
        categorySelect.replaceChildren();
        addPlaceholder(categorySelect, labels.selectCategory);
        const quiz = selectedQuiz();
        if (!quiz) {
            categorySelect.disabled = true;
            submitButton.disabled = true;
            return;
        }

        for (const category of quiz.categories ?? []) {
            const option = document.createElement("option");
            option.value = String(category.id);
            option.disabled = !category.hasCapacity;
            option.textContent = `${category.roundTitle} — ${category.title}${
                category.hasCapacity ? "" : ` (${labels.full})`}`;
            categorySelect.appendChild(option);
        }

        const firstAvailable = [...categorySelect.options]
            .find(option => option.value && !option.disabled);
        if (firstAvailable) {
            categorySelect.value = firstAvailable.value;
            categorySelect.disabled = false;
            submitButton.disabled = false;
        } else {
            categorySelect.disabled = true;
            submitButton.disabled = true;
        }
    };

    const populateDestinations = data => {
        quizzes = Array.isArray(data?.quizzes) ? data.quizzes : [];
        quizSelect.replaceChildren();
        addPlaceholder(quizSelect, labels.selectQuiz);

        for (const quiz of quizzes) {
            const option = document.createElement("option");
            option.value = String(quiz.id);
            option.textContent = quiz.title;
            option.disabled = !(quiz.categories ?? []).some(
                category => category.hasCapacity);
            quizSelect.appendChild(option);
        }

        const firstAvailableQuiz = [...quizSelect.options]
            .find(option => option.value && !option.disabled);
        if (firstAvailableQuiz) {
            quizSelect.value = firstAvailableQuiz.value;
            quizSelect.disabled = false;
            setStatus("");
        } else {
            quizSelect.disabled = true;
            setStatus(labels.noDestination, "error");
        }
        syncCategories();
    };

    const errorMessage = error => {
        switch (error) {
            case "source-not-found":
                return labels.sourceMissing;
            case "target-not-found":
                return labels.targetMissing;
            case "no-capacity":
                return labels.noCapacity;
            default:
                return labels.invalid;
        }
    };

    const loadDestinations = async () => {
        submitButton.disabled = true;
        quizSelect.disabled = true;
        categorySelect.disabled = true;
        setStatus(labels.loading);

        try {
            const url = new URL(copyEndpoint, window.location.origin);
            url.searchParams.set("handler", "Targets");
            url.searchParams.set("questionId", sourceQuestionId);
            const response = await fetch(url, {
                headers: { "Accept": "application/json" },
                credentials: "same-origin"
            });
            const data = await response.json().catch(() => null);
            if (!response.ok || !data?.success) {
                setStatus(errorMessage(data?.error), "error");
                return;
            }

            populateDestinations(data);
        } catch {
            setStatus(labels.invalid, "error");
        }
    };

    quizSelect.addEventListener("change", () => {
        setStatus("");
        syncCategories();
    });
    categorySelect.addEventListener("change", () => {
        submitButton.disabled = !categorySelect.value;
        setStatus("");
    });

    const openCopyDialog = async questionId => {
        sourceQuestionId = questionId;
        form.reset();
        quizzes = [];
        setStatus("");
        dialog.showModal();
        await loadDestinations();
    };

    for (const slot of questionSlots) {
        const deleteButton = slot.querySelector(".js-question-delete");
        if (!(deleteButton instanceof HTMLButtonElement)) {
            continue;
        }

        const questionId = slot.dataset.questionId;
        if (!questionId) {
            continue;
        }

        const actionColumn = deleteButton.closest(".question-card-actions");
        actionColumn?.classList.add("has-question-copy");

        const copyButton = document.createElement("button");
        copyButton.className = "js-question-copy button button-secondary icon-button";
        copyButton.type = "button";
        copyButton.draggable = false;
        copyButton.dataset.questionCopyAction = "true";
        copyButton.dataset.questionId = questionId;
        copyButton.title = labels.action;
        copyButton.setAttribute("aria-label", labels.action);
        copyButton.textContent = "⧉";
        deleteButton.insertAdjacentElement("beforebegin", copyButton);

        copyButton.addEventListener("click", async event => {
            event.preventDefault();
            event.stopPropagation();
            await openCopyDialog(questionId);
        });
    }

    form.addEventListener("submit", async event => {
        event.preventDefault();
        if (!sourceQuestionId || !categorySelect.value) {
            return;
        }

        submitButton.disabled = true;
        setStatus("");
        const body = new FormData();
        body.append("questionId", sourceQuestionId);
        body.append("targetCategoryId", categorySelect.value);
        const antiforgery = quizBoardForm.querySelector(
            'input[name="__RequestVerificationToken"]');
        if (antiforgery instanceof HTMLInputElement) {
            body.append(antiforgery.name, antiforgery.value);
        }

        try {
            const response = await fetch(copyEndpoint, {
                method: "POST",
                body,
                headers: { "Accept": "application/json" },
                credentials: "same-origin"
            });
            const data = await response.json().catch(() => null);
            if (!response.ok || !data?.success) {
                setStatus(errorMessage(data?.error), "error");
                submitButton.disabled = false;
                return;
            }

            dialog.close();
            setStatus("");
            showQuizOverlay(labels.success);
        } catch {
            setStatus(labels.invalid, "error");
            submitButton.disabled = false;
        }
    });

    dialog.querySelectorAll("[data-close-question-copy]")
        .forEach(button => button.addEventListener("click", () => dialog.close()));
    dialog.addEventListener("click", event => {
        if (event.target === dialog) {
            dialog.close();
        }
    });
})();
