(() => {
    "use strict";

    if (window.badWolfHostMultipleChoiceInitialized) {
        return;
    }
    window.badWolfHostMultipleChoiceInitialized = true;

    const script = document.currentScript ??
        document.querySelector('script[src*="host-multiple-choice.js"]');
    const savedQuestionType = script?.dataset.savedQuestionType ?? "-1";
    const culture = (document.documentElement.lang || "en")
        .slice(0, 2)
        .toLowerCase();
    const strings = {
        en: {
            type: "Multiple choice — host selects answer",
            editorHint: "Use 4–10 unique text options (max 20 characters). The first option is correct; move cards to change the correct answer. Wager mode is disabled.",
            correct: "Correct answer",
            invalidCount: "Multiple choice requires 4–10 answer options.",
            invalidText: "Every option must be non-empty text with at most 20 characters.",
            invalidDuplicate: "Answer options must be unique.",
            title: "Answer options",
            reward: (value, percentage) => `Current value: ${value} (${percentage}%)`,
            waiting: "Waiting for a player to buzz",
            answering: name => `${name} is answering`,
            noAnswer: "Nobody answered",
            rejected: "The answer option could not be applied."
        },
        uk: {
            type: "Вибір відповіді — обирає хост",
            editorHint: "Додайте 4–10 унікальних текстових варіантів (до 20 символів). Перший варіант правильний; змініть порядок блоків, щоб змінити правильну відповідь. Ставка вимкнена.",
            correct: "Правильна відповідь",
            invalidCount: "Для вибору відповіді потрібно 4–10 варіантів.",
            invalidText: "Кожен варіант має бути непорожнім текстом до 20 символів.",
            invalidDuplicate: "Варіанти відповіді мають бути унікальними.",
            title: "Варіанти відповіді",
            reward: (value, percentage) => `Поточна вартість: ${value} (${percentage}%)`,
            waiting: "Очікування натискання кнопки гравцем",
            answering: name => `Відповідає: ${name}`,
            noAnswer: "Ніхто не відповів",
            rejected: "Не вдалося зарахувати вибраний варіант."
        },
        it: {
            type: "Scelta multipla — selezione del conduttore",
            editorHint: "Usa 4–10 opzioni di testo uniche (massimo 20 caratteri). La prima opzione è corretta; riordina i blocchi per cambiarla. Le puntate sono disattivate.",
            correct: "Risposta corretta",
            invalidCount: "La scelta multipla richiede 4–10 opzioni.",
            invalidText: "Ogni opzione deve essere testo non vuoto di massimo 20 caratteri.",
            invalidDuplicate: "Le opzioni devono essere uniche.",
            title: "Opzioni di risposta",
            reward: (value, percentage) => `Valore attuale: ${value} (${percentage}%)`,
            waiting: "In attesa del buzzer di un giocatore",
            answering: name => `Risponde: ${name}`,
            noAnswer: "Nessuno ha risposto",
            rejected: "Non è stato possibile applicare l'opzione selezionata."
        },
        ru: {
            type: "Выбор ответа — выбирает хост",
            editorHint: "Добавьте 4–10 уникальных текстовых вариантов (до 20 символов). Первый вариант правильный; измените порядок блоков, чтобы изменить правильный ответ. Ставка отключена.",
            correct: "Правильный ответ",
            invalidCount: "Для выбора ответа нужно 4–10 вариантов.",
            invalidText: "Каждый вариант должен быть непустым текстом до 20 символов.",
            invalidDuplicate: "Варианты ответа должны быть уникальными.",
            title: "Варианты ответа",
            reward: (value, percentage) => `Текущая стоимость: ${value} (${percentage}%)`,
            waiting: "Ожидание нажатия кнопки игроком",
            answering: name => `Отвечает: ${name}`,
            noAnswer: "Никто не ответил",
            rejected: "Не удалось применить выбранный вариант."
        }
    }[culture] ?? null;
    const text = strings ?? {
        type: "Multiple choice — host selects answer",
        editorHint: "Use 4–10 unique text options (max 20 characters). The first option is correct; move cards to change the correct answer. Wager mode is disabled.",
        correct: "Correct answer",
        invalidCount: "Multiple choice requires 4–10 answer options.",
        invalidText: "Every option must be non-empty text with at most 20 characters.",
        invalidDuplicate: "Answer options must be unique.",
        title: "Answer options",
        reward: (value, percentage) => `Current value: ${value} (${percentage}%)`,
        waiting: "Waiting for a player to buzz",
        answering: name => `${name} is answering`,
        noAnswer: "Nobody answered",
        rejected: "The answer option could not be applied."
    };

    const style = document.createElement("style");
    style.id = "host-multiple-choice-styles";
    style.textContent = `
.host-multiple-choice-editor-hint {
    display: block;
    margin-top: .45rem;
    color: var(--muted);
}
.host-multiple-choice-correct-badge {
    display: inline-flex;
    align-items: center;
    min-height: 1.8rem;
    margin-right: auto;
    padding: .2rem .55rem;
    border: 1px solid var(--accent);
    border-radius: 999px;
    font-size: .78rem;
    font-weight: 700;
}
.host-multiple-choice-invalid {
    outline: 2px solid var(--danger, #c33);
    outline-offset: 2px;
}
.host-multiple-choice-panel {
    position: fixed;
    z-index: 70;
    top: 5.75rem;
    right: 1rem;
    display: grid;
    gap: .65rem;
    width: min(22rem, calc(100vw - 2rem));
    max-height: calc(100vh - 7rem);
    padding: 1rem;
    overflow: auto;
    border: 1px solid var(--line);
    border-radius: .9rem;
    background: var(--panel);
    box-shadow: 0 .75rem 2rem rgb(0 0 0 / 25%);
}
.host-multiple-choice-panel h2,
.host-multiple-choice-panel p {
    margin: 0;
}
.host-multiple-choice-options {
    display: grid;
    gap: .55rem;
}
.host-multiple-choice-option {
    width: 100%;
    min-height: 3rem;
    white-space: normal;
    overflow-wrap: anywhere;
}
.host-multiple-choice-option[disabled] {
    opacity: .55;
    cursor: not-allowed;
}
.host-multiple-choice-panel-status {
    min-height: 1.25rem;
    color: var(--muted);
}
@media (max-width: 800px) {
    .host-multiple-choice-panel {
        top: auto;
        bottom: 1rem;
        max-height: min(55vh, 30rem);
    }
}
`;
    document.head.appendChild(style);

    const initializeEditor = () => {
        const form = document.querySelector("[data-ajax-question-editor]");
        const presentationType = document.getElementById("Input_PresentationType");
        const answerSection = document.getElementById("answer-blocks");
        if (!form || !(presentationType instanceof HTMLSelectElement) || !answerSection) {
            return;
        }

        if (!presentationType.querySelector('option[value="4"]')) {
            const option = document.createElement("option");
            option.value = "4";
            option.textContent = text.type;
            presentationType.appendChild(option);
        }
        if (savedQuestionType === "4") {
            presentationType.value = "4";
        }

        const hint = document.createElement("small");
        hint.className = "host-multiple-choice-editor-hint";
        hint.textContent = text.editorHint;
        hint.hidden = true;
        answerSection.querySelector(".content-block-section-header")?.after(hint);

        const answerList = answerSection.querySelector("[data-content-block-list]");
        const specialCheckbox = document.getElementById("Input_IsSpecial");
        const excludeCheckbox = document.getElementById(
            "Input_ExcludeFromRandomWagerSelection");
        const saveStatus = document.querySelector("[data-question-save-status]");
        let ensuringMinimum = false;

        const isHostChoice = () => presentationType.value === "4";
        const answerCards = () => [...answerSection.querySelectorAll(
            ":scope > [data-content-block-list] > .content-block-card")];

        const setSaveError = message => {
            if (saveStatus) {
                saveStatus.textContent = message;
                saveStatus.hidden = false;
                saveStatus.classList.remove("alert-success");
                saveStatus.classList.add("alert-error");
            } else {
                window.alert(message);
            }
        };

        const validate = () => {
            if (!isHostChoice()) {
                return null;
            }

            const cards = answerCards();
            if (cards.length < 4 || cards.length > 10) {
                return text.invalidCount;
            }

            const values = [];
            for (const card of cards) {
                const textarea = card.querySelector('textarea[name$=".TextContent"]');
                const value = textarea?.value.trim() ?? "";
                if (card.dataset.blockType !== "Text" ||
                    !value || value.length > 20) {
                    return text.invalidText;
                }
                values.push(value.toLocaleLowerCase(culture));
            }

            if (new Set(values).size !== values.length) {
                return text.invalidDuplicate;
            }

            return null;
        };

        const refresh = () => {
            const hostChoice = isHostChoice();
            hint.hidden = !hostChoice;

            document.querySelectorAll(".wager-question-setting").forEach(element => {
                const fourClues = presentationType.value === "1";
                element.hidden = hostChoice || fourClues;
            });
            if (hostChoice && specialCheckbox) {
                specialCheckbox.checked = false;
            }
            if (hostChoice && excludeCheckbox) {
                excludeCheckbox.checked = true;
            }

            answerSection.querySelectorAll(".content-block-type-option").forEach(button => {
                if (!(button instanceof HTMLElement)) {
                    return;
                }
                button.hidden = hostChoice && button.dataset.blockType !== "Text";
            });

            const cards = answerCards();
            const addButton = answerSection.querySelector(".content-block-add-button");
            if (addButton instanceof HTMLButtonElement) {
                addButton.disabled = hostChoice && cards.length >= 10;
            }

            cards.forEach((card, index) => {
                card.querySelectorAll(".host-multiple-choice-correct-badge")
                    .forEach(item => item.remove());
                card.classList.toggle(
                    "host-multiple-choice-invalid",
                    hostChoice && card.dataset.blockType !== "Text");

                const textarea = card.querySelector('textarea[name$=".TextContent"]');
                if (textarea instanceof HTMLTextAreaElement) {
                    if (hostChoice) {
                        textarea.maxLength = 20;
                    } else {
                        textarea.removeAttribute("maxlength");
                    }
                }

                const removeButton = card.querySelector(".content-block-remove-button");
                if (removeButton instanceof HTMLButtonElement) {
                    removeButton.disabled = hostChoice && cards.length <= 4;
                }

                if (hostChoice && index === 0) {
                    const badge = document.createElement("span");
                    badge.className = "host-multiple-choice-correct-badge";
                    badge.textContent = text.correct;
                    card.querySelector(".content-block-toolbar")?.prepend(badge);
                }
            });
        };

        const ensureMinimum = async () => {
            if (!isHostChoice() || ensuringMinimum) {
                return;
            }
            ensuringMinimum = true;
            try {
                while (answerCards().length < 4 &&
                    typeof window.addContentBlock === "function") {
                    await window.addContentBlock(answerSection, "Text");
                }
            } finally {
                ensuringMinimum = false;
                refresh();
            }
        };

        presentationType.addEventListener("change", () => {
            refresh();
            ensureMinimum().catch(console.error);
        });

        form.addEventListener("submit", event => {
            const error = validate();
            if (!error) {
                return;
            }
            event.preventDefault();
            event.stopImmediatePropagation();
            setSaveError(error);
        }, true);

        if (answerList) {
            new MutationObserver(refresh).observe(answerList, {
                childList: true,
                subtree: false
            });
            answerList.addEventListener("input", refresh);
        }

        refresh();
        ensureMinimum().catch(console.error);
    };

    const initializeHostGameplay = () => {
        if (!window.location.pathname.includes("/Admin/Games/Lobby/")) {
            return;
        }

        const getBoard = () => document.querySelector(
            ".host-game-board[data-game-id]");
        const initialBoard = getBoard();
        const gameId = initialBoard?.dataset.gameId;
        if (!gameId) {
            return;
        }

        const endpoint = `/Admin/Games/HostMultipleChoice/${encodeURIComponent(gameId)}`;
        let panel = null;
        let pollInProgress = false;
        let selectionInProgress = false;
        let lastState = null;

        const antiforgeryToken = () =>
            document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? "";

        const hideDefaultJudgment = active => {
            document.querySelectorAll(".question-judge-actions").forEach(form => {
                form.hidden = active;
            });
            document.querySelectorAll('form[action*="handler=ResolveQuestion"]').forEach(form => {
                form.hidden = active;
            });
        };

        const updateQuestionHeading = state => {
            const heading = document.querySelector("[data-question-heading]");
            if (!heading || !state?.active) {
                return;
            }
            heading.dataset.currentReward = String(state.rewardValue);
            const template = heading.dataset.rewardTemplate;
            if (template) {
                heading.textContent = template.replace("__REWARD__", state.rewardValue);
            }
        };

        const removePanel = () => {
            panel?.remove();
            panel = null;
        };

        const createNoAnswerForm = state => {
            const form = document.createElement("form");
            form.method = "post";
            form.action = `/Admin/Games/Lobby/${encodeURIComponent(gameId)}?handler=ResolveQuestion`;

            const token = antiforgeryToken();
            if (token) {
                const tokenInput = document.createElement("input");
                tokenInput.type = "hidden";
                tokenInput.name = "__RequestVerificationToken";
                tokenInput.value = token;
                form.appendChild(tokenInput);
            }

            const questionInput = document.createElement("input");
            questionInput.type = "hidden";
            questionInput.name = "sourceQuestionId";
            questionInput.value = state.sourceQuestionId;
            form.appendChild(questionInput);

            const button = document.createElement("button");
            button.type = "submit";
            button.className = "button button-secondary";
            button.textContent = text.noAnswer;
            button.disabled = state.buzzerStatus === "claimed";
            form.appendChild(button);
            return form;
        };

        const selectOption = async (state, optionId, statusElement) => {
            if (selectionInProgress ||
                state.buzzerStatus !== "claimed" ||
                !state.answeringPlayerId) {
                return;
            }

            selectionInProgress = true;
            panel?.querySelectorAll("button").forEach(button => {
                button.disabled = true;
            });

            const data = new FormData();
            const token = antiforgeryToken();
            if (token) {
                data.set("__RequestVerificationToken", token);
            }
            data.set("sourceQuestionId", state.sourceQuestionId);
            data.set("playerId", state.answeringPlayerId);
            data.set("sourceContentBlockId", optionId);

            try {
                const response = await fetch(`${endpoint}?handler=Select`, {
                    method: "POST",
                    body: data,
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                const result = await response.json();
                if (!response.ok || !result.success) {
                    throw new Error(result.error || text.rejected);
                }

                if (result.questionClosed) {
                    window.location.reload();
                    return;
                }

                render(result.state);
            } catch (error) {
                statusElement.textContent = error.message || text.rejected;
            } finally {
                selectionInProgress = false;
            }
        };

        const render = state => {
            lastState = state;
            if (!state?.active) {
                hideDefaultJudgment(false);
                removePanel();
                return;
            }

            hideDefaultJudgment(true);
            updateQuestionHeading(state);

            if (state.status === "showinganswer") {
                removePanel();
                const closeAnswerForm = document.querySelector(
                    'form[action*="handler=CloseAnswer"]');
                if (!closeAnswerForm) {
                    window.location.reload();
                } else {
                    document.querySelector(
                        ".answer-presentation .game-content-block")
                        ?.classList.add("all-player-answer-option-correct");
                }
                return;
            }

            removePanel();
            panel = document.createElement("aside");
            panel.className = "host-multiple-choice-panel";
            panel.setAttribute("aria-live", "polite");

            const title = document.createElement("h2");
            title.textContent = text.title;
            panel.appendChild(title);

            const reward = document.createElement("strong");
            reward.textContent = text.reward(
                state.rewardValue,
                state.rewardPercentage);
            panel.appendChild(reward);

            const player = document.createElement("p");
            player.className = "host-multiple-choice-panel-status";
            player.textContent = state.buzzerStatus === "claimed" &&
                state.answeringPlayerName
                    ? text.answering(state.answeringPlayerName)
                    : text.waiting;
            panel.appendChild(player);

            const options = document.createElement("div");
            options.className = "host-multiple-choice-options";
            const canSelect = state.buzzerStatus === "claimed" &&
                Boolean(state.answeringPlayerId) &&
                !selectionInProgress;
            for (const option of state.options ?? []) {
                const button = document.createElement("button");
                button.type = "button";
                button.className = "button button-secondary host-multiple-choice-option";
                button.textContent = option.text;
                button.disabled = !canSelect;
                button.addEventListener("click", () =>
                    selectOption(state, option.id, player));
                options.appendChild(button);
            }
            panel.appendChild(options);
            panel.appendChild(createNoAnswerForm(state));
            document.body.appendChild(panel);
        };

        const poll = async () => {
            if (pollInProgress || selectionInProgress) {
                return;
            }
            pollInProgress = true;
            try {
                const response = await fetch(`${endpoint}?handler=State`, {
                    headers: { Accept: "application/json" },
                    cache: "no-store"
                });
                if (!response.ok) {
                    if (response.status === 404) {
                        removePanel();
                        hideDefaultJudgment(false);
                    }
                    return;
                }
                render(await response.json());
            } catch (error) {
                console.debug("Host multiple-choice state poll failed.", error);
            } finally {
                pollInProgress = false;
            }
        };

        poll();
        window.setInterval(poll, 750);

        document.addEventListener("visibilitychange", () => {
            if (!document.hidden) {
                poll();
            }
        });

        window.addEventListener("beforeunload", () => {
            lastState = null;
        });
    };

    initializeEditor();
    initializeHostGameplay();
})();
