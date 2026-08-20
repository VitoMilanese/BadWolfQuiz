(() => {
    const dialog = document.getElementById("exchange-question-dialog");
    const form = dialog?.querySelector("form");
    const slotSelector = ".question-cell-slot";

    if (!dialog || !form) {
        return;
    }

    let submitting = false;
    let buttonStates = [];

    const getFallbackError = () =>
        document.querySelector(".quiz-board-form")?.dataset.saveError ||
        "Question move failed.";

    const disableDialogButtons = () => {
        buttonStates = [...dialog.querySelectorAll("button")]
            .map(button => ({ button, wasDisabled: button.disabled }));
        buttonStates.forEach(({ button }) => {
            button.disabled = true;
        });
    };

    const restoreDialogButtons = () => {
        buttonStates.forEach(({ button, wasDisabled }) => {
            button.disabled = wasDisabled;
        });
        buttonStates = [];
    };

    const showError = error => {
        const message = error?.message || getFallbackError();
        if (typeof showQuizSaveStatus === "function") {
            showQuizSaveStatus(message, false);
            return;
        }

        window.alert(message);
    };

    const bindDeleteButton = button => {
        if (!button) {
            return;
        }

        button.addEventListener("click", () => {
            const deleteDialog = document.getElementById("delete-question-dialog");
            const deleteIdInput = document.getElementById("delete-question-id");
            const deleteTitle = deleteDialog?.querySelector(
                "[data-delete-question-title]");

            if (!deleteDialog || !deleteIdInput || !deleteTitle) {
                return;
            }

            if (typeof pendingDeleteQuestionButton !== "undefined") {
                pendingDeleteQuestionButton = button;
            }

            deleteIdInput.value = button.dataset.questionId || "";
            deleteTitle.textContent = button.dataset.questionTitle || "";
            deleteDialog.showModal();
        });
    };

    const syncExchangeQuestionRounds = (sourceQuestionId, targetQuestionId) => {
        if (typeof exchangeQuestionRounds === "undefined") {
            return;
        }

        let sourceEntry = null;
        let targetEntry = null;

        for (const round of exchangeQuestionRounds) {
            for (const question of round.questions) {
                if (question.id === sourceQuestionId) {
                    sourceEntry = question;
                } else if (question.id === targetQuestionId) {
                    targetEntry = question;
                }
            }
        }

        if (!sourceEntry || !targetEntry) {
            return;
        }

        sourceEntry.id = targetQuestionId;
        targetEntry.id = sourceQuestionId;
    };

    const updateSourceSlot = (sourceQuestionId, targetQuestionId, html) => {
        const parsedDocument = new DOMParser().parseFromString(html, "text/html");
        const sourceSlot = document.querySelector(
            `${slotSelector}[data-question-id="${sourceQuestionId}"]`);
        const replacementSlot = parsedDocument.querySelector(
            `${slotSelector}[data-question-id="${targetQuestionId}"]`);

        if (!sourceSlot || !replacementSlot) {
            throw new Error(getFallbackError());
        }

        sourceSlot.className = replacementSlot.className;
        sourceSlot.dataset.questionId = replacementSlot.dataset.questionId || "";
        sourceSlot.dataset.questionTitle =
            replacementSlot.dataset.questionTitle || "";
        sourceSlot.draggable = replacementSlot.draggable;

        const replacementTitle = replacementSlot.getAttribute("title");
        if (replacementTitle === null) {
            sourceSlot.removeAttribute("title");
        } else {
            sourceSlot.setAttribute("title", replacementTitle);
        }

        sourceSlot.innerHTML = replacementSlot.innerHTML;
        bindDeleteButton(sourceSlot.querySelector(".js-question-delete"));
        syncExchangeQuestionRounds(sourceQuestionId, targetQuestionId);
    };

    form.addEventListener("submit", async event => {
        event.preventDefault();
        event.stopImmediatePropagation();

        if (submitting) {
            return;
        }

        const formData = new FormData(form);
        const sourceQuestionId = Number(
            formData.get("ExchangeQuestions.SourceQuestionId"));
        const targetQuestionId = Number(
            formData.get("ExchangeQuestions.TargetQuestionId"));

        if (!Number.isInteger(sourceQuestionId) || sourceQuestionId <= 0 ||
            !Number.isInteger(targetQuestionId) || targetQuestionId <= 0) {
            return;
        }

        submitting = true;
        disableDialogButtons();
        dialog.close();
        window.BadWolfBusy?.show();

        let succeeded = false;

        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: formData,
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });

            if (!response.ok) {
                throw new Error(
                    `${getFallbackError()} Status: ${response.status}.`);
            }

            const html = await response.text();
            updateSourceSlot(sourceQuestionId, targetQuestionId, html);
            succeeded = true;
        } catch (error) {
            showError(error);
        } finally {
            window.BadWolfBusy?.hide();
            restoreDialogButtons();
            submitting = false;

            if (!succeeded && !dialog.open) {
                dialog.showModal();
            }
        }
    });
})();
