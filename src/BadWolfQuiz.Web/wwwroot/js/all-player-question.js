(() => {
    "use strict";

    if (window.badWolfAllPlayerQuestionInitialized) {
        return;
    }

    window.badWolfAllPlayerQuestionInitialized = true;

    const apiPath = "/api/all-player-question";
    const culture = (document.documentElement.lang || "en")
        .slice(0, 2)
        .toLowerCase();
    const stringsByCulture = {
        en: {
            typeText: "All players — text answer",
            typeChoice: "All players — multiple choice",
            textHint: "Every player submits a text answer. Keep exactly one non-empty Text block under Correct answer. Matching ignores surrounding spaces and letter case.",
            choiceHint: "Every player chooses one option. Keep 2–4 distinct non-empty Text blocks under Correct answer; the first block is the correct option.",
            answerOptions: "Answer options (first is correct)",
            invalidText: "All-player text questions require exactly one non-empty Text answer block.",
            invalidChoice: "All-player multiple-choice questions require 2–4 distinct non-empty Text answer blocks; the first option is correct.",
            title: "Everyone answers",
            yourAnswer: "Your answer",
            submit: "Submit answer",
            confirmed: "Answer submitted",
            rejected: "The answer could not be submitted.",
            closed: "Answering is closed.",
            correct: "Correct",
            incorrect: "Incorrect",
            noAnswer: "No answer — 0 points",
            waiting: "Waiting",
            answered: "Answered",
            progress: (answered, total) => `Answers: ${answered}/${total}`
        },
        uk: {
            typeText: "Усі гравці — текстова відповідь",
            typeChoice: "Усі гравці — вибір відповіді",
            textHint: "Кожен гравець вводить текстову відповідь. У правильній відповіді залиште рівно один непорожній текстовий блок. Пробіли по краях і регістр літер ігноруються.",
            choiceHint: "Кожен гравець обирає один варіант. У правильній відповіді залиште 2–4 різні непорожні текстові блоки; перший блок є правильним варіантом.",
            answerOptions: "Варіанти відповіді (перший — правильний)",
            invalidText: "Для текстового питання для всіх потрібен рівно один непорожній текстовий блок правильної відповіді.",
            invalidChoice: "Для питання з вибором для всіх потрібно 2–4 різні непорожні текстові блоки; перший варіант є правильним.",
            title: "Відповідають усі",
            yourAnswer: "Ваша відповідь",
            submit: "Надіслати відповідь",
            confirmed: "Відповідь зараховано",
            rejected: "Не вдалося зарахувати відповідь.",
            closed: "Прийом відповідей завершено.",
            correct: "Правильно",
            incorrect: "Неправильно",
            noAnswer: "Немає відповіді — 0 балів",
            waiting: "Очікує",
            answered: "Відповів",
            progress: (answered, total) => `Відповіді: ${answered}/${total}`
        },
        it: {
            typeText: "Tutti i giocatori — risposta testuale",
            typeChoice: "Tutti i giocatori — scelta multipla",
            textHint: "Ogni giocatore invia una risposta testuale. Mantieni esattamente un blocco Testo non vuoto nella risposta corretta. Spazi iniziali/finali e maiuscole/minuscole vengono ignorati.",
            choiceHint: "Ogni giocatore sceglie un'opzione. Mantieni 2–4 blocchi Testo distinti e non vuoti nella risposta corretta; il primo blocco è l'opzione corretta.",
            answerOptions: "Opzioni di risposta (la prima è corretta)",
            invalidText: "Le domande testuali per tutti richiedono esattamente un blocco Testo non vuoto come risposta corretta.",
            invalidChoice: "Le domande a scelta multipla per tutti richiedono 2–4 blocchi Testo distinti e non vuoti; la prima opzione è corretta.",
            title: "Rispondono tutti",
            yourAnswer: "La tua risposta",
            submit: "Invia risposta",
            confirmed: "Risposta inviata",
            rejected: "Non è stato possibile inviare la risposta.",
            closed: "Le risposte sono chiuse.",
            correct: "Corretta",
            incorrect: "Errata",
            noAnswer: "Nessuna risposta — 0 punti",
            waiting: "In attesa",
            answered: "Ha risposto",
            progress: (answered, total) => `Risposte: ${answered}/${total}`
        },
        ru: {
            typeText: "Все игроки — текстовый ответ",
            typeChoice: "Все игроки — выбор ответа",
            textHint: "Каждый игрок вводит текстовый ответ. В правильном ответе оставьте ровно один непустой текстовый блок. Пробелы по краям и регистр букв игнорируются.",
            choiceHint: "Каждый игрок выбирает один вариант. В правильном ответе оставьте 2–4 разных непустых текстовых блока; первый блок является правильным вариантом.",
            answerOptions: "Варианты ответа (первый — правильный)",
            invalidText: "Для текстового вопроса для всех нужен ровно один непустой текстовый блок правильного ответа.",
            invalidChoice: "Для вопроса с выбором для всех нужны 2–4 разных непустых текстовых блока; первый вариант является правильным.",
            title: "Отвечают все",
            yourAnswer: "Ваш ответ",
            submit: "Отправить ответ",
            confirmed: "Ответ принят",
            rejected: "Не удалось принять ответ.",
            closed: "Приём ответов завершён.",
            correct: "Правильно",
            incorrect: "Неправильно",
            noAnswer: "Нет ответа — 0 баллов",
            waiting: "Ожидает",
            answered: "Ответил",
            progress: (answered, total) => `Ответы: ${answered}/${total}`
        }
    };
    const text = stringsByCulture[culture] ?? stringsByCulture.en;

    const style = document.createElement("style");
    style.id = "all-player-question-styles";
    style.textContent = `
.all-player-editor-help {
    display: block;
    margin-top: 0.45rem;
    color: var(--muted);
}

.all-player-editor-error {
    margin-top: 0.75rem;
}

.player-all-player-panel {
    display: grid;
    gap: 0.85rem;
    margin-top: 1rem;
    padding: 1rem;
    border: 1px solid var(--line);
    border-radius: 0.9rem;
    background: var(--panel-2);
}

.player-all-player-panel[hidden] {
    display: none;
}

.player-all-player-panel textarea {
    width: 100%;
    min-height: 7rem;
}

.all-player-choice-grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 0.75rem;
}

.all-player-choice-option {
    min-height: 3.5rem;
    white-space: normal;
    overflow-wrap: anywhere;
}

.all-player-question-timer {
    margin: 0;
    font-variant-numeric: tabular-nums;
}

.all-player-host-progress {
    display: grid;
    gap: 0.6rem;
    width: min(100%, 58rem);
    margin-inline: auto;
}

.all-player-answer-progress {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr));
    gap: 0.45rem;
    margin: 0;
    padding: 0;
    list-style: none;
}

.all-player-answer-progress li {
    min-width: 0;
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 0.75rem;
    padding: 0.55rem 0.7rem;
    border: 1px solid var(--line);
    border-radius: 0.65rem;
    background: var(--panel-2);
}

.all-player-answer-progress li > strong {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.host-game-board.all-player-question-answering .question-controls > :not(.all-player-host-progress) {
    display: none !important;
}

.host-game-board.all-player-multiple-choice-answer .answer-presentation .game-content-blocks > .game-content-block:not(:first-child) {
    display: none !important;
}

@media (max-width: 640px) {
    .all-player-choice-grid {
        grid-template-columns: 1fr;
    }

    .all-player-answer-progress {
        grid-template-columns: 1fr;
    }
}`;
    document.head.appendChild(style);

    const getJson = async url => {
        const response = await fetch(url, {
            credentials: "same-origin",
            cache: "no-store",
            headers: { Accept: "application/json" }
        });
        if (!response.ok) {
            throw new Error(response.statusText);
        }
        return response.json();
    };

    const initializeEditor = () => {
        const form = document.querySelector("form.question-editor");
        const select = document.getElementById("Input_PresentationType");
        if (!(form instanceof HTMLFormElement) ||
            !(select instanceof HTMLSelectElement)) {
            return;
        }

        if (!select.querySelector('option[value="2"]')) {
            select.add(new Option(text.typeText, "2"));
        }
        if (!select.querySelector('option[value="3"]')) {
            select.add(new Option(text.typeChoice, "3"));
        }

        const typePanel = select.closest(".question-type-setting");
        const fourClueHelp = document.getElementById("four-clues-help");
        const textHelp = document.createElement("small");
        textHelp.className = "all-player-editor-help";
        textHelp.dataset.allPlayerTextHelp = "true";
        textHelp.textContent = text.textHint;
        textHelp.hidden = true;
        const choiceHelp = document.createElement("small");
        choiceHelp.className = "all-player-editor-help";
        choiceHelp.dataset.allPlayerChoiceHelp = "true";
        choiceHelp.textContent = text.choiceHint;
        choiceHelp.hidden = true;
        typePanel?.append(textHelp, choiceHelp);

        const answerSection = document.getElementById("answer-blocks");
        const answerHeading = answerSection?.previousElementSibling;
        if (answerHeading instanceof HTMLHeadingElement) {
            answerHeading.dataset.standardHeading = answerHeading.textContent ?? "";
        }

        const special = document.getElementById("Input_IsSpecial");
        const excludeRandom = document.getElementById(
            "Input_ExcludeFromRandomWagerSelection");
        const buzzSetting = document.getElementById("buzz-mode-setting");
        const buzzSelect = document.getElementById("Input_BuzzModeOverride");
        let standardSpecial = special instanceof HTMLInputElement
            ? special.checked
            : false;
        let standardExclude = excludeRandom instanceof HTMLInputElement
            ? excludeRandom.checked
            : false;
        let standardBuzzMode = buzzSelect instanceof HTMLSelectElement
            ? buzzSelect.value
            : "0";
        let previousAllPlayer = false;

        const sync = () => {
            const isText = select.value === "2";
            const isChoice = select.value === "3";
            const isAllPlayer = isText || isChoice;

            textHelp.hidden = !isText;
            choiceHelp.hidden = !isChoice;
            if (fourClueHelp && isAllPlayer) {
                fourClueHelp.hidden = true;
            }

            if (isAllPlayer && !previousAllPlayer) {
                if (special instanceof HTMLInputElement) {
                    standardSpecial = special.checked;
                }
                if (excludeRandom instanceof HTMLInputElement) {
                    standardExclude = excludeRandom.checked;
                }
                if (buzzSelect instanceof HTMLSelectElement) {
                    standardBuzzMode = buzzSelect.value;
                }
            }

            document.querySelectorAll(".wager-question-setting")
                .forEach(element => {
                    if (isAllPlayer) {
                        element.hidden = true;
                    }
                });

            if (isAllPlayer) {
                if (special instanceof HTMLInputElement) {
                    special.checked = false;
                }
                if (excludeRandom instanceof HTMLInputElement) {
                    excludeRandom.checked = true;
                }
                if (buzzSetting) {
                    buzzSetting.hidden = true;
                }
                if (buzzSelect instanceof HTMLSelectElement) {
                    buzzSelect.disabled = false;
                    buzzSelect.value = "5";
                }
                if (answerHeading instanceof HTMLHeadingElement) {
                    answerHeading.textContent = isChoice
                        ? text.answerOptions
                        : text.yourAnswer;
                }
            } else if (previousAllPlayer && select.value === "0") {
                if (special instanceof HTMLInputElement) {
                    special.checked = standardSpecial;
                }
                if (excludeRandom instanceof HTMLInputElement) {
                    excludeRandom.checked = standardExclude;
                }
                if (buzzSelect instanceof HTMLSelectElement) {
                    buzzSelect.value = standardBuzzMode;
                }
                if (answerHeading instanceof HTMLHeadingElement) {
                    answerHeading.textContent =
                        answerHeading.dataset.standardHeading ?? "";
                }
            }

            previousAllPlayer = isAllPlayer;
        };

        const showEditorError = message => {
            let error = form.querySelector(".all-player-editor-error");
            if (!error) {
                error = document.createElement("div");
                error.className = "message message-error all-player-editor-error";
                const answerValidation = answerSection?.nextElementSibling;
                (answerValidation ?? answerSection)?.insertAdjacentElement(
                    "afterend",
                    error);
            }
            error.textContent = message;
            error.hidden = false;
        };

        const validate = () => {
            const isText = select.value === "2";
            const isChoice = select.value === "3";
            if (!isText && !isChoice) {
                return null;
            }

            const cards = Array.from(
                answerSection?.querySelectorAll(
                    ":scope > [data-content-block-list] > .content-block-card") ?? []);
            const values = cards.map(card => ({
                type: card.dataset.blockType,
                text: card.querySelector('[name$=".TextContent"]')
                    ?.value?.trim() ?? ""
            }));

            if (isText) {
                return values.length === 1 &&
                    values[0].type === "Text" &&
                    values[0].text
                    ? null
                    : text.invalidText;
            }

            const normalized = values.map(item => item.text.toLocaleLowerCase());
            return values.length >= 2 &&
                values.length <= 4 &&
                values.every(item => item.type === "Text" && item.text) &&
                new Set(normalized).size === normalized.length
                ? null
                : text.invalidChoice;
        };

        select.addEventListener("change", () => {
            window.queueMicrotask(sync);
        });

        form.addEventListener("submit", event => {
            const error = validate();
            if (!error) {
                const existing = form.querySelector(".all-player-editor-error");
                if (existing) {
                    existing.hidden = true;
                }
                return;
            }

            event.preventDefault();
            event.stopImmediatePropagation();
            showEditorError(error);
        }, true);

        const questionId = Number.parseInt(
            window.location.pathname.split("/").filter(Boolean).at(-1) ?? "",
            10);
        if (Number.isFinite(questionId)) {
            const url = `${apiPath}?handler=Editor&questionId=${questionId}`;
            getJson(url)
                .then(state => {
                    if (state.presentationType === 2 || state.presentationType === 3) {
                        select.value = String(state.presentationType);
                        select.dispatchEvent(new Event("change", { bubbles: true }));
                    }
                    window.queueMicrotask(sync);
                })
                .catch(() => sync());
        } else {
            sync();
        }
    };

    const initializePlayer = () => {
        const lobby = document.querySelector(".player-lobby");
        if (!(lobby instanceof HTMLElement)) {
            return;
        }

        const code = lobby.dataset.gameCode;
        const playerId = lobby.dataset.playerId;
        const accessToken = lobby.dataset.accessToken;
        if (!code || !playerId || !accessToken) {
            return;
        }

        const panel = document.createElement("section");
        panel.className = "player-all-player-panel";
        panel.hidden = true;
        panel.innerHTML = `
            <p class="eyebrow"></p>
            <p class="all-player-question-timer" data-all-player-timer></p>
            <div data-all-player-controls></div>
            <p class="dialog-warning" data-all-player-status></p>
            <div class="message message-error" data-all-player-error hidden></div>`;
        panel.querySelector(".eyebrow").textContent = text.title;

        const timerPanel = document.getElementById("game-timer");
        if (timerPanel) {
            timerPanel.insertAdjacentElement("afterend", panel);
        } else {
            lobby.appendChild(panel);
        }

        const controls = panel.querySelector("[data-all-player-controls]");
        const status = panel.querySelector("[data-all-player-status]");
        const error = panel.querySelector("[data-all-player-error]");
        const timer = panel.querySelector("[data-all-player-timer]");
        let currentQuestionId = null;
        let currentMode = null;
        let requestInFlight = false;
        let pollHandle = 0;
        let lastState = null;

        const buzzerPanel = document.querySelector(".player-buzzer-panel");
        const hideBuzzer = () => {
            if (buzzerPanel && !buzzerPanel.hidden) {
                buzzerPanel.dataset.hiddenByAllPlayer = "true";
                buzzerPanel.hidden = true;
            }
        };
        const restoreBuzzer = () => {
            if (buzzerPanel?.dataset.hiddenByAllPlayer === "true") {
                delete buzzerPanel.dataset.hiddenByAllPlayer;
                buzzerPanel.hidden = false;
            }
        };

        const setControlsDisabled = disabled => {
            controls?.querySelectorAll("button, textarea")
                .forEach(element => {
                    element.disabled = disabled;
                });
        };

        const submit = async answer => {
            const normalized = answer?.trim();
            if (!normalized || currentQuestionId === null || requestInFlight) {
                return;
            }

            requestInFlight = true;
            setControlsDisabled(true);
            if (error) {
                error.hidden = true;
            }

            const data = new FormData();
            data.set("code", code);
            data.set("playerId", playerId);
            data.set("accessToken", accessToken);
            data.set("sourceQuestionId", String(currentQuestionId));
            data.set("answer", normalized);

            try {
                const response = await fetch(`${apiPath}?handler=Submit`, {
                    method: "POST",
                    credentials: "same-origin",
                    body: data,
                    headers: {
                        Accept: "application/json",
                        "X-Requested-With": "XMLHttpRequest"
                    }
                });
                const result = await response.json().catch(() => null);
                if (!response.ok || !result?.success) {
                    if (result?.state) {
                        applyState(result.state);
                    }
                    throw new Error(result?.error || text.rejected);
                }

                if (result.state) {
                    applyState(result.state);
                }
            } catch (exception) {
                if (error) {
                    error.textContent = exception?.message || text.rejected;
                    error.hidden = false;
                }
                if (!lastState?.hasSubmitted && !lastState?.isClosed) {
                    setControlsDisabled(false);
                }
            } finally {
                requestInFlight = false;
            }
        };

        const buildControls = state => {
            controls.replaceChildren();
            if (state.mode === "multipleChoice") {
                const grid = document.createElement("div");
                grid.className = "all-player-choice-grid";
                for (const option of state.options ?? []) {
                    const button = document.createElement("button");
                    button.type = "button";
                    button.className =
                        "button button-secondary all-player-choice-option";
                    button.textContent = option;
                    button.addEventListener("click", () => {
                        void submit(option);
                    });
                    grid.appendChild(button);
                }
                controls.appendChild(grid);
                return;
            }

            const form = document.createElement("form");
            form.className = "stack-form";
            const label = document.createElement("label");
            const caption = document.createElement("span");
            caption.textContent = text.yourAnswer;
            const textarea = document.createElement("textarea");
            textarea.maxLength = 500;
            textarea.required = true;
            const button = document.createElement("button");
            button.type = "submit";
            button.className = "button button-primary";
            button.textContent = text.submit;
            label.append(caption, textarea);
            form.append(label, button);
            form.addEventListener("submit", event => {
                event.preventDefault();
                void submit(textarea.value);
            });
            controls.appendChild(form);
        };

        function applyState(state) {
            lastState = state;
            if (!state?.active) {
                panel.hidden = true;
                currentQuestionId = null;
                currentMode = null;
                controls?.replaceChildren();
                restoreBuzzer();
                return;
            }

            hideBuzzer();
            panel.hidden = false;
            const questionChanged = currentQuestionId !== state.sourceQuestionId ||
                currentMode !== state.mode;
            currentQuestionId = state.sourceQuestionId;
            currentMode = state.mode;

            if (questionChanged) {
                buildControls(state);
            }

            const seconds = Math.max(
                0,
                Math.ceil((state.remainingMilliseconds ?? 0) / 1000));
            if (timer) {
                timer.textContent = state.isClosed ? "" : `${seconds}s`;
            }

            if (state.isClosed) {
                setControlsDisabled(true);
                controls.hidden = true;
                status.textContent = state.hasSubmitted
                    ? state.isCorrect
                        ? text.correct
                        : text.incorrect
                    : text.noAnswer;
            } else if (state.hasSubmitted) {
                setControlsDisabled(true);
                controls.hidden = true;
                status.textContent = text.confirmed;
            } else {
                controls.hidden = false;
                if (!requestInFlight) {
                    setControlsDisabled(false);
                }
                status.textContent = "";
            }
        }

        const schedule = active => {
            window.clearTimeout(pollHandle);
            pollHandle = window.setTimeout(
                poll,
                active ? 700 : 1500);
        };

        const poll = async () => {
            try {
                const url = `${apiPath}?handler=Player` +
                    `&code=${encodeURIComponent(code)}` +
                    `&playerId=${encodeURIComponent(playerId)}`;
                const state = await getJson(url);
                applyState(state);
                schedule(Boolean(state.active));
            } catch {
                schedule(Boolean(lastState?.active));
            }
        };

        void poll();
    };

    const initializeHost = () => {
        const initialBoard = document.querySelector(
            ".host-game-board[data-game-code]");
        if (!(initialBoard instanceof HTMLElement)) {
            return;
        }

        const code = initialBoard.dataset.gameCode;
        if (!code) {
            return;
        }

        let pollHandle = 0;
        let lastState = null;
        let refreshQuestionId = null;

        const removeProgress = board => {
            board.querySelector(".all-player-host-progress")?.remove();
        };

        const renderProgress = (board, state) => {
            let progress = board.querySelector(".all-player-host-progress");
            if (!progress) {
                progress = document.createElement("div");
                progress.className = "all-player-host-progress";
            }

            progress.replaceChildren();
            const heading = document.createElement("strong");
            heading.textContent = text.progress(
                state.answeredCount ?? 0,
                state.playerCount ?? 0);
            const list = document.createElement("ul");
            list.className = "all-player-answer-progress";

            for (const player of state.players ?? []) {
                const item = document.createElement("li");
                const name = document.createElement("strong");
                name.textContent = player.name;
                name.title = player.name;
                const playerState = document.createElement("span");
                playerState.textContent = state.isClosed
                    ? player.submitted
                        ? player.isCorrect
                            ? text.correct
                            : text.incorrect
                        : text.noAnswer
                    : player.submitted
                        ? text.answered
                        : text.waiting;
                item.append(name, playerState);
                list.appendChild(item);
            }

            progress.append(heading, list);
            const target = board.querySelector(".question-controls") ??
                board.querySelector(".current-question-summary");
            if (target && progress.parentElement !== target) {
                target.appendChild(progress);
            }
        };

        const applyState = state => {
            lastState = state;
            const board = document.querySelector(
                `.host-game-board[data-game-code="${CSS.escape(code)}"]`);
            if (!(board instanceof HTMLElement)) {
                return;
            }

            if (!state?.active) {
                board.classList.remove(
                    "all-player-question-answering",
                    "all-player-multiple-choice-answer");
                removeProgress(board);
                refreshQuestionId = null;
                return;
            }

            board.classList.toggle(
                "all-player-question-answering",
                !state.isClosed);
            board.classList.toggle(
                "all-player-multiple-choice-answer",
                state.isClosed && state.mode === "multipleChoice");
            renderProgress(board, state);

            if (!state.isClosed) {
                refreshQuestionId = null;
                return;
            }

            const hasAnswerPresentation = Boolean(
                board.querySelector(".answer-presentation"));
            if (!hasAnswerPresentation &&
                refreshQuestionId !== state.sourceQuestionId &&
                window.BadWolfHostGameplay?.refresh) {
                refreshQuestionId = state.sourceQuestionId;
                window.BadWolfHostGameplay.refresh()
                    .then(() => applyState(state))
                    .catch(error => {
                        console.error(error);
                        refreshQuestionId = null;
                    });
            }
        };

        const schedule = active => {
            window.clearTimeout(pollHandle);
            pollHandle = window.setTimeout(
                poll,
                active ? 450 : 1000);
        };

        const poll = async () => {
            try {
                const url = `${apiPath}?handler=Host` +
                    `&code=${encodeURIComponent(code)}`;
                const state = await getJson(url);
                applyState(state);
                schedule(Boolean(state.active));
            } catch {
                schedule(Boolean(lastState?.active));
            }
        };

        document.addEventListener("badwolf:host-gameplay-updated", () => {
            if (lastState?.active) {
                applyState(lastState);
            }
        });

        void poll();
    };

    initializeEditor();
    initializePlayer();
    initializeHost();
})();
