(() => {
    const selector = ".question-cell-slot";
    let exchangeInProgress = false;

    const getQuestionSlot = target =>
        target instanceof Element ? target.closest(selector) : null;

    const findSourceSlot = event => {
        const transferredQuestionId = Number(
            event.dataTransfer?.getData("text/plain"));

        if (Number.isInteger(transferredQuestionId) && transferredQuestionId > 0) {
            return document.querySelector(
                `${selector}[data-question-id="${transferredQuestionId}"]`);
        }

        return document.querySelector(`${selector}.question-cell-dragging`);
    };

    const getAntiforgeryToken = () =>
        document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    const clearDragClasses = () => {
        document.querySelectorAll(
            ".question-cell-dragging, .question-cell-drop-target")
            .forEach(element => element.classList.remove(
                "question-cell-dragging",
                "question-cell-drop-target"));
    };

    const updateQuestionTitle = (slot, title) => {
        slot.dataset.questionTitle = title;
        slot.querySelectorAll("[data-question-title]").forEach(element => {
            element.dataset.questionTitle = title;
        });
    };

    const swapQuestionSlots = (sourceSlot, targetSlot) => {
        const sourceTitle = sourceSlot.dataset.questionTitle ?? "";
        const targetTitle = targetSlot.dataset.questionTitle ?? "";
        const sourceMarker = document.createComment("question-source-position");

        sourceSlot.replaceWith(sourceMarker);
        targetSlot.replaceWith(sourceSlot);
        sourceMarker.replaceWith(targetSlot);

        updateQuestionTitle(sourceSlot, targetTitle);
        updateQuestionTitle(targetSlot, sourceTitle);
    };

    const persistExchange = async (sourceQuestionId, targetQuestionId) => {
        const boardForm = document.querySelector(".quiz-board-form");
        const quizId = boardForm?.querySelector(
            '[name="RoundRows.QuizId"]')?.value;

        if (!quizId) {
            throw new Error("Quiz id is unavailable.");
        }

        const formData = new FormData();
        formData.append("ExchangeQuestions.QuizId", quizId);
        formData.append(
            "ExchangeQuestions.SourceQuestionId",
            sourceQuestionId.toString());
        formData.append(
            "ExchangeQuestions.TargetQuestionId",
            targetQuestionId.toString());

        const antiforgeryToken = getAntiforgeryToken();
        if (antiforgeryToken) {
            formData.append("__RequestVerificationToken", antiforgeryToken);
        }

        const response = await fetch("?handler=ExchangeQuestions", {
            method: "POST",
            body: formData,
            headers: { "X-Requested-With": "XMLHttpRequest" }
        });

        if (!response.ok) {
            const responseText = await response.text();
            throw new Error(
                `Question exchange failed. Status: ${response.status}. ${responseText}`);
        }
    };

    document.addEventListener("drop", async event => {
        const targetSlot = getQuestionSlot(event.target);
        if (!targetSlot || exchangeInProgress) {
            return;
        }

        const sourceSlot = findSourceSlot(event);
        if (!sourceSlot || sourceSlot === targetSlot) {
            return;
        }

        const sourceQuestionId = Number(sourceSlot.dataset.questionId);
        const targetQuestionId = Number(targetSlot.dataset.questionId);
        if (!Number.isInteger(sourceQuestionId) || sourceQuestionId <= 0 ||
            !Number.isInteger(targetQuestionId) || targetQuestionId <= 0) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        targetSlot.classList.remove("question-cell-drop-target");

        exchangeInProgress = true;
        const busyShown = window.BadWolfBusy?.show?.() === true;

        try {
            await persistExchange(sourceQuestionId, targetQuestionId);
            swapQuestionSlots(sourceSlot, targetSlot);
        }
        catch (error) {
            console.error("Question exchange error:", error);
            alert(error.message);
        }
        finally {
            exchangeInProgress = false;
            clearDragClasses();
            if (busyShown) {
                window.BadWolfBusy.hide();
            }
        }
    }, true);
})();
