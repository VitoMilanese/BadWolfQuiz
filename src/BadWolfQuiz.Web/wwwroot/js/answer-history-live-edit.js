(() => {
    const page = document.querySelector(".answer-history-page");
    if (!page) {
        return;
    }

    const content = page.querySelector(".answer-history-content");
    const deleteDialog = document.getElementById("delete-answer-history-dialog");
    const ajaxHeaders = {
        Accept: "application/json",
        "X-Requested-With": "XMLHttpRequest"
    };

    const showError = message => {
        if (!content || !message) {
            return;
        }

        let alert = content.querySelector(".answer-history-alert");
        if (!alert) {
            alert = document.createElement("div");
            alert.className = "message message-error answer-history-alert";
            alert.setAttribute("role", "alert");
            content.prepend(alert);
        }

        alert.textContent = message;
    };

    const setBusy = (form, busy) => {
        form.toggleAttribute("aria-busy", busy);
        for (const button of form.querySelectorAll("button")) {
            if (busy) {
                button.dataset.answerHistoryWasDisabled = button.disabled ? "true" : "false";
                button.disabled = true;
            } else {
                button.disabled = button.dataset.answerHistoryWasDisabled === "true";
                button.removeAttribute("data-answer-history-was-disabled");
            }
        }
    };

    const postForm = async (form, formData) => {
        const response = await fetch(form.action, {
            method: "POST",
            body: formData,
            credentials: "same-origin",
            headers: ajaxHeaders
        });
        const payload = await response.json().catch(() => null);

        if (!response.ok || payload?.ok !== true) {
            throw new Error(payload?.error ?? "");
        }

        return payload.data ?? {};
    };

    const getAddQuestionOption = sourceQuestionId => {
        const select = document.querySelector(
            '.answer-history-add-form [name="sourceQuestionId"]');
        if (!(select instanceof HTMLSelectElement)) {
            return null;
        }

        return Array.from(select.options)
            .find(option => option.value === String(sourceQuestionId)) ?? null;
    };

    const syncAttemptedPlayers = (sourceQuestionId, removePlayerId, addPlayerId) => {
        const option = getAddQuestionOption(sourceQuestionId);
        if (!option) {
            return;
        }

        const attemptedIds = new Set(
            (option.dataset.attemptedPlayerIds ?? "")
                .split(",")
                .filter(Boolean));

        if (removePlayerId) {
            attemptedIds.delete(String(removePlayerId));
        }
        if (addPlayerId) {
            attemptedIds.add(String(addPlayerId));
        }

        option.dataset.attemptedPlayerIds = Array.from(attemptedIds).join(",");
        if (option.selected) {
            option.parentElement?.dispatchEvent(new Event("change", { bubbles: true }));
        }
    };

    const updateSummaryCounts = data => {
        const stats = document.querySelectorAll(".answer-history-session-stats b");
        if (stats.length >= 2) {
            if (Number.isFinite(Number(data.questionCount))) {
                stats[0].textContent = String(data.questionCount).padStart(2, "0");
            }
            if (Number.isFinite(Number(data.answerCount))) {
                stats[1].textContent = String(data.answerCount).padStart(2, "0");
            }
        }

        const sectionCount = document.querySelector(".answer-history-section-heading > b");
        if (sectionCount && Number.isFinite(Number(data.questionCount))) {
            sectionCount.textContent = String(data.questionCount).padStart(2, "0");
        }
    };

    const renumberQuestionCards = () => {
        document.querySelectorAll(".answer-history-question-number")
            .forEach((number, index) => {
                number.textContent = String(index + 1).padStart(2, "0");
            });
    };

    const createEmptyRow = label => {
        const row = document.createElement("div");
        row.className = "answer-history-empty-row";

        const mark = document.createElement("span");
        mark.setAttribute("aria-hidden", "true");
        mark.textContent = "—";

        const text = document.createElement("p");
        text.textContent = label ?? "";

        row.append(mark, text);
        return row;
    };

    for (const form of document.querySelectorAll(".answer-history-entry-form")) {
        const player = form.querySelector('[name="playerId"]');
        if (player instanceof HTMLSelectElement) {
            form.dataset.savedPlayerId = player.value;
        }
    }

    document.addEventListener("submit", async event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        if (form.matches(".answer-history-entry-form")) {
            event.preventDefault();
            event.stopImmediatePropagation();

            const formData = new FormData(form);
            const previousPlayerId = form.dataset.savedPlayerId ?? "";
            setBusy(form, true);

            try {
                const data = await postForm(form, formData);
                const isCorrect = data.isCorrect === true;
                const scoreDelta = Number(data.scoreDelta ?? 0);
                const resultMark = form.querySelector(".answer-history-result-mark");
                const delta = form.querySelector(".answer-history-delta");
                const player = form.querySelector('[name="playerId"]');
                const deleteButton = form.querySelector("[data-delete-answer-history]");

                form.classList.toggle("is-correct", isCorrect);
                form.classList.toggle("is-incorrect", !isCorrect);

                if (resultMark) {
                    resultMark.textContent = isCorrect ? "✓" : "×";
                }
                if (delta) {
                    delta.textContent = scoreDelta > 0 ? `+${scoreDelta}` : String(scoreDelta);
                    delta.classList.toggle("score-negative", scoreDelta < 0);
                }
                if (player instanceof HTMLSelectElement && data.playerId) {
                    player.value = String(data.playerId);
                }
                if (deleteButton && data.playerName) {
                    deleteButton.dataset.playerName = String(data.playerName);
                }

                const nextPlayerId = data.playerId ? String(data.playerId) : previousPlayerId;
                syncAttemptedPlayers(data.sourceQuestionId, previousPlayerId, nextPlayerId);
                form.dataset.savedPlayerId = nextPlayerId;
            } catch (error) {
                console.error(error);
                showError(error instanceof Error ? error.message : "");
            } finally {
                setBusy(form, false);
            }

            return;
        }

        if (!form.closest("#delete-answer-history-dialog")) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();

        const formData = new FormData(form);
        const attemptId = String(formData.get("attemptId") ?? "");
        const entry = Array.from(document.querySelectorAll(".answer-history-entry-form"))
            .find(candidate =>
                candidate.querySelector('[name="attemptId"]')?.value === attemptId) ?? null;
        const sourceQuestionId = String(formData.get("sourceQuestionId") ?? "");
        const savedPlayerId = entry?.dataset.savedPlayerId ?? "";

        setBusy(form, true);
        try {
            const data = await postForm(form, formData);
            deleteDialog?.close();
            syncAttemptedPlayers(sourceQuestionId, savedPlayerId, null);

            if (entry) {
                const card = entry.closest(".answer-history-question-card");
                const entries = entry.closest(".answer-history-entries");
                entry.remove();

                if (data.questionIsVisible === false) {
                    card?.remove();
                } else if (data.questionHasAttempts === false && entries) {
                    entries.replaceWith(createEmptyRow(data.noEntriesLabel));
                }
            }

            updateSummaryCounts(data);
            renumberQuestionCards();
        } catch (error) {
            console.error(error);
            showError(error instanceof Error ? error.message : "");
        } finally {
            setBusy(form, false);
        }
    }, true);
})();
