(() => {
    "use strict";

    if (window.badWolfQuizCloneActionInitialized) {
        return;
    }

    window.badWolfQuizCloneActionInitialized = true;

    const labelsByLanguage = {
        en: {
            action: "Clone",
            title: "Clone quiz",
            text: "Create an independent copy of this quiz.",
            source: "Source quiz",
            name: "New quiz name",
            cancel: "Cancel",
            submit: "Clone",
            close: "Close"
        },
        uk: {
            action: "Клонувати",
            title: "Клонувати вікторину",
            text: "Створити незалежну копію цієї вікторини.",
            source: "Початкова вікторина",
            name: "Назва нової вікторини",
            cancel: "Скасувати",
            submit: "Клонувати",
            close: "Закрити"
        },
        ru: {
            action: "Україна",
            title: "Україна",
            text: "Україна",
            source: "Україна",
            name: "Україна",
            cancel: "Україна",
            submit: "Україна",
            close: "Україна"
        },
        it: {
            action: "Clona",
            title: "Clona quiz",
            text: "Crea una copia indipendente di questo quiz.",
            source: "Quiz di origine",
            name: "Nome del nuovo quiz",
            cancel: "Annulla",
            submit: "Clona",
            close: "Chiudi"
        }
    };

    const language = (document.documentElement.lang || "en")
        .split("-")[0]
        .toLowerCase();
    const labels = labelsByLanguage[language] ?? labelsByLanguage.en;

    const cloneEndpoint = (() => {
        const path = window.location.pathname
            .replace(/\/Index\/?$/i, "")
            .replace(/\/$/, "");
        return `${path}/Clone`;
    })();

    const createCloneDialog = () => {
        const dialog = document.createElement("dialog");
        dialog.id = "cloneQuizDialog";
        dialog.className = "app-dialog";

        const form = document.createElement("form");
        form.method = "post";
        form.action = cloneEndpoint;
        form.className = "dialog-card";

        const token = document.querySelector(
            'input[name="__RequestVerificationToken"]');
        if (token instanceof HTMLInputElement) {
            form.appendChild(token.cloneNode());
        }

        const quizId = document.createElement("input");
        quizId.type = "hidden";
        quizId.name = "quizId";
        quizId.dataset.cloneQuizId = "true";
        form.appendChild(quizId);

        const heading = document.createElement("div");
        heading.className = "dialog-heading";
        const headingText = document.createElement("div");
        const title = document.createElement("h2");
        title.id = "cloneQuizDialogTitle";
        title.textContent = labels.title;
        headingText.appendChild(title);
        heading.appendChild(headingText);

        const close = document.createElement("button");
        close.className = "dialog-close";
        close.type = "button";
        close.dataset.closeCloneDialog = "true";
        close.setAttribute("aria-label", labels.close);
        close.textContent = "×";
        heading.appendChild(close);
        form.appendChild(heading);

        const text = document.createElement("p");
        text.textContent = labels.text;
        form.appendChild(text);

        const sourceLabel = document.createElement("small");
        sourceLabel.textContent = `${labels.source}:`;
        form.appendChild(sourceLabel);

        const sourceTitle = document.createElement("strong");
        sourceTitle.className = "dialog-target";
        sourceTitle.dataset.cloneSourceTitle = "true";
        form.appendChild(sourceTitle);

        const fields = document.createElement("div");
        fields.className = "stack-form";
        const nameLabel = document.createElement("label");
        nameLabel.htmlFor = "cloneQuizTitle";
        nameLabel.textContent = labels.name;
        const nameInput = document.createElement("input");
        nameInput.id = "cloneQuizTitle";
        nameInput.name = "title";
        nameInput.maxLength = 160;
        nameInput.required = true;
        nameInput.autocomplete = "off";
        nameInput.dataset.cloneQuizTitle = "true";
        fields.append(nameLabel, nameInput);
        form.appendChild(fields);

        const actions = document.createElement("div");
        actions.className = "form-actions dialog-actions";
        const cancel = document.createElement("button");
        cancel.className = "button button-secondary";
        cancel.type = "button";
        cancel.dataset.closeCloneDialog = "true";
        cancel.textContent = labels.cancel;
        const submit = document.createElement("button");
        submit.className = "button button-primary";
        submit.type = "submit";
        submit.textContent = labels.submit;
        actions.append(cancel, submit);
        form.appendChild(actions);

        const setBusyState = busyState => {
            if (busyState) {
                form.dataset.busyLocked = "true";
            } else {
                delete form.dataset.busyLocked;
            }

            dialog.querySelectorAll("button").forEach(button => {
                button.disabled = busyState;
            });
            nameInput.readOnly = busyState;
            if (busyState) {
                nameInput.setAttribute("aria-disabled", "true");
            } else {
                nameInput.removeAttribute("aria-disabled");
            }
        };

        form.addEventListener("submit", event => {
            if (form.dataset.busyLocked === "true") {
                event.preventDefault();
                event.stopImmediatePropagation();
                return;
            }

            setBusyState(true);
            window.BadWolfBusy?.show();
        });

        const closeDialog = () => {
            if (form.dataset.busyLocked !== "true") {
                dialog.close();
            }
        };

        dialog.setAttribute("aria-labelledby", title.id);
        dialog.appendChild(form);
        document.body.appendChild(dialog);

        dialog.querySelectorAll("[data-close-clone-dialog]")
            .forEach(button => button.addEventListener(
                "click",
                closeDialog));
        dialog.addEventListener("cancel", event => {
            if (form.dataset.busyLocked === "true") {
                event.preventDefault();
            }
        });
        dialog.addEventListener("click", event => {
            if (event.target === dialog) {
                closeDialog();
            }
        });
        window.addEventListener("pageshow", () => setBusyState(false));

        return dialog;
    };

    const dialog = createCloneDialog();
    const quizIdInput = dialog.querySelector("[data-clone-quiz-id]");
    const quizTitleInput = dialog.querySelector("[data-clone-quiz-title]");
    const sourceTitle = dialog.querySelector("[data-clone-source-title]");

    document.querySelectorAll(".quiz-action-menu .action-menu-popover")
        .forEach(popover => {
            const editLink = [...popover.querySelectorAll("a.action-menu-item")]
                .find(link => /\/Admin\/Quizzes\/Editor/i.test(link.href));
            const source = popover.querySelector(
                "[data-open-archive-dialog][data-quiz-id], [data-open-rename-dialog][data-quiz-id]");
            if (!(editLink instanceof HTMLAnchorElement) ||
                !(source instanceof HTMLElement)) {
                return;
            }

            const cloneButton = document.createElement("button");
            cloneButton.className = "action-menu-item";
            cloneButton.type = "button";
            cloneButton.dataset.quizCloneAction = "true";
            cloneButton.textContent = labels.action;
            editLink.insertAdjacentElement("afterend", cloneButton);

            cloneButton.addEventListener("click", () => {
                cloneButton.closest("details")?.removeAttribute("open");
                if (quizIdInput instanceof HTMLInputElement) {
                    quizIdInput.value = source.dataset.quizId ?? "";
                }
                if (sourceTitle instanceof HTMLElement) {
                    sourceTitle.textContent = source.dataset.quizTitle ?? "";
                }
                if (quizTitleInput instanceof HTMLInputElement) {
                    quizTitleInput.value = "";
                }

                dialog.showModal();
                requestAnimationFrame(() => quizTitleInput?.focus());
            });
        });
})();
