(() => {
    "use strict";

    if (window.badWolfQuestionCopyBoardRefreshInitialized) {
        return;
    }

    const quizBoardForm = document.querySelector("form.quiz-board-form");
    if (!(quizBoardForm instanceof HTMLFormElement)) {
        return;
    }

    window.badWolfQuestionCopyBoardRefreshInitialized = true;

    const currentQuizId = Number(
        document.getElementById("RoundRows_QuizId")?.value);
    const currentRoundId = Number(
        document.getElementById("RoundRows_RoundId")?.value);
    const quizSaveStatus = document.querySelector("[data-quiz-save-status]");
    const nativeFetch = window.fetch.bind(window);
    let dynamicPendingDeleteButton = null;
    let activeDraggedSlot = null;
    let copyScriptReloadPending = false;

    const isQuestionCopyPost = (input, init) => {
        const method = String(init?.method ?? input?.method ?? "GET")
            .toUpperCase();
        if (method !== "POST") {
            return false;
        }

        try {
            const rawUrl = typeof input === "string" || input instanceof URL
                ? input
                : input?.url;
            const url = new URL(rawUrl, window.location.origin);
            return /\/Admin\/Quizzes\/QuestionCopy$/i.test(url.pathname);
        } catch {
            return false;
        }
    };

    window.fetch = async (...args) => {
        const [input, init] = args;
        const response = await nativeFetch(...args);
        if (!isQuestionCopyPost(input, init)) {
            return response;
        }

        try {
            const data = await response.clone().json();
            if (response.ok && data?.success) {
                window.setTimeout(() => {
                    window.dispatchEvent(new CustomEvent(
                        "badwolf:question-copy-succeeded",
                        { detail: data }));
                }, 0);
            }
        } catch {
            // The normal copy action owns request error handling.
        }

        return response;
    };

    const showQuizOverlay = (message, success) => {
        if (!(quizSaveStatus instanceof HTMLElement)) {
            return;
        }

        quizSaveStatus.textContent = message;
        quizSaveStatus.classList.remove(
            "alert-success",
            "alert-error",
            "message-hidden",
            "editor-save-overlay-hiding");
        quizSaveStatus.classList.add(
            success ? "alert-success" : "alert-error");
        quizSaveStatus.hidden = false;
    };

    const getRowCells = column =>
        Array.from(column?.children ?? []).slice(1);

    const markDynamicSlot = slot => {
        if (slot?.classList?.contains("question-cell-slot")) {
            slot.dataset.questionCopyRefreshDynamic = "true";
        }
    };

    const cloneRemoteCell = remoteCell => {
        const clone = remoteCell.cloneNode(true);
        markDynamicSlot(clone);
        return clone;
    };

    const syncFieldState = (nextDocument, id, attributes = []) => {
        const current = document.getElementById(id);
        const next = nextDocument.getElementById(id);
        if (!(current instanceof HTMLElement) || !(next instanceof HTMLElement)) {
            return;
        }

        if ("value" in current && "value" in next) {
            current.value = next.value;
        }

        for (const attribute of attributes) {
            const value = next.getAttribute(attribute);
            if (value === null) {
                current.removeAttribute(attribute);
            } else {
                current.setAttribute(attribute, value);
            }
        }
    };

    const syncRoundSettings = nextDocument => {
        syncFieldState(nextDocument, "RoundRows_QuestionCount", [
            "data-initial-count",
            "data-filled-items"
        ]);
        syncFieldState(nextDocument, "RoundRows_CategoryCount", [
            "data-initial-count",
            "data-filled-items"
        ]);
        syncFieldState(nextDocument, "RoundRows_RandomWagerQuestionCount", [
            "max"
        ]);
        syncFieldState(
            nextDocument,
            "RoundRows_RandomAnonymousSharedWagerQuestionCount",
            ["max"]);

        for (const selector of [
            "#random-wager-count-setting small",
            "#random-anonymous-shared-wager-count-setting small"
        ]) {
            const current = document.querySelector(selector);
            const next = nextDocument.querySelector(selector);
            if (current && next) {
                current.textContent = next.textContent;
            }
        }
    };

    const reloadQuestionCopyAction = () => {
        if (copyScriptReloadPending) {
            return;
        }
        copyScriptReloadPending = true;

        document.querySelector("[data-question-copy-dialog]")?.remove();
        document.getElementById("question-copy-action-layout")?.remove();
        document.querySelectorAll(".js-question-copy").forEach(button =>
            button.remove());
        document.querySelectorAll(".question-card-actions.has-question-copy")
            .forEach(actions => actions.classList.remove("has-question-copy"));

        window.badWolfQuestionCopyActionInitialized = false;
        const script = document.createElement("script");
        script.src = new URL(
            "/js/question-copy-action.js",
            window.location.origin).href;
        script.async = false;
        script.dataset.questionCopyReload = "true";
        script.addEventListener("load", () => {
            copyScriptReloadPending = false;
        }, { once: true });
        script.addEventListener("error", () => {
            copyScriptReloadPending = false;
        }, { once: true });
        document.head.appendChild(script);
    };

    const refreshCurrentRoundBoard = async copyResult => {
        if (Number(copyResult?.quizId) !== currentQuizId ||
            Number(copyResult?.roundId) !== currentRoundId) {
            return;
        }

        const response = await nativeFetch(window.location.href, {
            method: "GET",
            headers: {
                "Accept": "text/html",
                "X-Requested-With": "XMLHttpRequest"
            },
            credentials: "same-origin",
            cache: "no-store"
        });
        if (!response.ok) {
            throw new Error(`Editor refresh failed with ${response.status}.`);
        }

        const html = await response.text();
        const nextDocument = new DOMParser().parseFromString(html, "text/html");
        const currentBoard = document.querySelector(".quiz-board");
        const nextBoard = nextDocument.querySelector(".quiz-board");
        const currentTargetColumn = document.getElementById(
            `category-${copyResult.categoryId}`);
        const nextTargetColumn = nextDocument.getElementById(
            `category-${copyResult.categoryId}`);
        const nextCopiedSlot = nextTargetColumn?.querySelector(
            `.question-cell-slot[data-question-id="${copyResult.questionId}"]`);

        if (!currentBoard || !nextBoard || !currentTargetColumn ||
            !nextTargetColumn || !nextCopiedSlot) {
            throw new Error(
                "Could not locate the copied question in the refreshed editor.");
        }

        const nextTargetRows = getRowCells(nextTargetColumn);
        const copiedRowPosition = nextTargetRows.indexOf(nextCopiedSlot);
        if (copiedRowPosition < 0) {
            throw new Error("Could not locate the copied question row.");
        }

        const currentPoints = document.querySelector(
            ".quiz-board-points-column");
        const nextPoints = nextDocument.querySelector(
            ".quiz-board-points-column");
        const currentPointRows = getRowCells(currentPoints);
        const nextPointRows = getRowCells(nextPoints);
        const rowWasAdded = nextPointRows.length > currentPointRows.length;

        if (rowWasAdded) {
            if (!currentPoints || !nextPoints) {
                throw new Error("Could not refresh the added question row.");
            }

            currentPoints.replaceChildren(
                ...Array.from(nextPoints.children).map(child =>
                    child.cloneNode(true)));

            document.querySelectorAll(
                ".quiz-board-category-column[data-category-id]").forEach(
                currentColumn => {
                    const categoryId = currentColumn.dataset.categoryId;
                    const nextColumn = nextDocument.getElementById(
                        `category-${categoryId}`);
                    const nextCell = getRowCells(nextColumn)[copiedRowPosition];
                    if (!nextCell) {
                        throw new Error(
                            `Could not refresh category ${categoryId}.`);
                    }

                    const currentRows = getRowCells(currentColumn);
                    const reference = currentRows[copiedRowPosition] ?? null;
                    currentColumn.insertBefore(
                        cloneRemoteCell(nextCell),
                        reference);
                });
        } else {
            const currentTargetRows = getRowCells(currentTargetColumn);
            const currentCell = currentTargetRows[copiedRowPosition];
            if (!currentCell) {
                throw new Error("Could not refresh the copied question cell.");
            }

            if (currentCell.classList.contains("question-cell-slot") &&
                currentCell.dataset.questionId === String(copyResult.questionId)) {
                currentCell.replaceChildren(
                    ...Array.from(nextCopiedSlot.children).map(child =>
                        child.cloneNode(true)));
                for (const attribute of nextCopiedSlot.attributes) {
                    currentCell.setAttribute(attribute.name, attribute.value);
                }
                markDynamicSlot(currentCell);
            } else {
                currentCell.replaceWith(cloneRemoteCell(nextCopiedSlot));
            }
        }

        currentBoard.style.setProperty(
            "--row-count",
            nextBoard.style.getPropertyValue("--row-count"));
        syncRoundSettings(nextDocument);
        reloadQuestionCopyAction();
    };

    window.addEventListener("badwolf:question-copy-succeeded", event => {
        refreshCurrentRoundBoard(event.detail).catch(error => {
            console.error("Question copy board refresh failed:", error);
        });
    });

    const deleteQuestionDialog = document.getElementById("delete-question-dialog");
    const deleteQuestionForm = document.querySelector(
        "[data-delete-question-form]");

    document.addEventListener("click", event => {
        const deleteButton = event.target.closest?.(".js-question-delete");
        const slot = deleteButton?.closest(".question-cell-slot");
        if (!deleteButton ||
            slot?.dataset.questionCopyRefreshDynamic !== "true") {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        dynamicPendingDeleteButton = deleteButton;

        const questionIdInput = document.getElementById("delete-question-id");
        if (questionIdInput) {
            questionIdInput.value = deleteButton.dataset.questionId ?? "";
        }
        const titleTarget = deleteQuestionDialog?.querySelector(
            "[data-delete-question-title]");
        if (titleTarget) {
            titleTarget.textContent = deleteButton.dataset.questionTitle ?? "";
        }
        deleteQuestionDialog?.showModal();
    }, true);

    deleteQuestionDialog?.addEventListener("close", () => {
        dynamicPendingDeleteButton = null;
    });

    deleteQuestionForm?.addEventListener("submit", async event => {
        if (!dynamicPendingDeleteButton) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        const submitter = event.submitter;
        if (submitter instanceof HTMLButtonElement) {
            submitter.disabled = true;
        }

        try {
            const response = await nativeFetch(deleteQuestionForm.action, {
                method: "POST",
                body: new FormData(deleteQuestionForm),
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            const result = await response.json();
            if (!response.ok || !result.success) {
                throw new Error(result.error || "Question deletion failed.");
            }

            const slot = dynamicPendingDeleteButton.closest(
                ".question-cell-slot");
            const questionCard = slot?.querySelector(".question-cell");
            questionCard?.classList.remove("filled");
            questionCard?.querySelector(
                ".question-editor-preview")?.replaceChildren();
            questionCard?.querySelectorAll(
                ".question-completion-item").forEach(item => {
                item.classList.remove("complete");
                item.textContent = `${item.textContent.trim()[0]} —`;
            });
            questionCard?.querySelector(".visually-hidden")?.remove();
            slot?.querySelector(".js-question-copy")?.remove();
            const actionColumn = dynamicPendingDeleteButton.closest(
                ".question-card-actions");
            dynamicPendingDeleteButton.remove();
            actionColumn?.classList.remove("has-question-copy");
            dynamicPendingDeleteButton = null;
            deleteQuestionDialog?.close();
            showQuizOverlay(result.message ?? "", true);
        } catch (error) {
            deleteQuestionDialog?.close();
            showQuizOverlay(
                error.message || "Question deletion failed.",
                false);
        } finally {
            if (submitter instanceof HTMLButtonElement) {
                submitter.disabled = false;
            }
        }
    }, true);

    document.addEventListener("dragstart", event => {
        const slot = event.target.closest?.(".question-cell-slot");
        if (slot) {
            activeDraggedSlot = slot;
        }
    }, true);

    document.addEventListener("dragover", event => {
        const targetSlot = event.target.closest?.(".question-cell-slot");
        if (!activeDraggedSlot || !targetSlot ||
            activeDraggedSlot === targetSlot) {
            return;
        }

        const involvesDynamicSlot =
            activeDraggedSlot.dataset.questionCopyRefreshDynamic === "true" ||
            targetSlot.dataset.questionCopyRefreshDynamic === "true";
        if (!involvesDynamicSlot) {
            return;
        }

        event.preventDefault();
        targetSlot.classList.add("question-cell-drop-target");
    }, true);

    document.addEventListener("drop", async event => {
        const targetSlot = event.target.closest?.(".question-cell-slot");
        if (!activeDraggedSlot || !targetSlot ||
            activeDraggedSlot === targetSlot) {
            return;
        }

        const involvesDynamicSlot =
            activeDraggedSlot.dataset.questionCopyRefreshDynamic === "true" ||
            targetSlot.dataset.questionCopyRefreshDynamic === "true";
        if (!involvesDynamicSlot ||
            typeof window.exchangeQuestions !== "function") {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        targetSlot.classList.remove("question-cell-drop-target");
        const antiforgery = quizBoardForm.querySelector(
            'input[name="__RequestVerificationToken"]');
        await window.exchangeQuestions(
            Number(activeDraggedSlot.dataset.questionId),
            Number(targetSlot.dataset.questionId),
            antiforgery);
    }, true);

    document.addEventListener("dragend", () => {
        activeDraggedSlot = null;
        document.querySelectorAll(".question-cell-drop-target")
            .forEach(element => element.classList.remove(
                "question-cell-drop-target"));
    }, true);
})();
