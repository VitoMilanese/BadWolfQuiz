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
            textHint: "Every player submits a text answer. The host judges submitted answers manually. Keep exactly one non-empty Text block as the reference correct answer.",
            choiceHint: "Every player chooses one shuffled option. Keep 2–4 Text or Image answer blocks; the first block is the correct option. Audio and video are not supported for this question type.",
            answerOptions: "Answer options (first is correct)",
            invalidText: "All-player text questions require exactly one non-empty Text answer block.",
            invalidChoice: "All-player multiple-choice questions require 2–4 valid Text or Image answer options; the first option is correct.",
            invalidChoiceMedia: "All-player multiple-choice questions can contain only Text or Image blocks.",
            title: "Everyone answers",
            yourAnswer: "Your answer",
            submit: "Submit answer",
            confirmed: "Answer submitted",
            rejected: "The answer could not be submitted.",
            closed: "Answering is closed.",
            waitingForJudgment: "Waiting for the host to judge answers.",
            correct: "Correct",
            incorrect: "Incorrect",
            noAnswer: "No answer — 0 points",
            waiting: "Waiting",
            answered: "Answered",
            judge: "Judge answer",
            progress: (answered, total) => `Answers: ${answered}/${total}`,
            wagersProgress: (submitted, total) => `Wagers: ${submitted}/${total}`,
            judgingProgress: (current, total) => `Answer review: ${current}/${total}`,
            yourWager: "Your wager",
            confirmWager: "Confirm wager",
            wagerConfirmed: "Wager confirmed. Waiting for the host.",
            wagerRejected: "The wager could not be submitted.",
            wagerOutsideLimits: (minimum, maximum) => `Enter a wager from ${minimum} to ${maximum}.`,
            wagerSubmitted: "Wager submitted",
            minimumWager: "Set minimum wager",
            emptyAnswer: "Record an empty answer",
            showQuestion: "Show question",
            waitingForWagers: "Waiting for wagers"
        },
        uk: {
            typeText: "Усі гравці — текстова відповідь",
            typeChoice: "Усі гравці — вибір відповіді",
            textHint: "Кожен гравець вводить текстову відповідь. Відповіді перевіряє хост вручну. Залиште рівно один непорожній текстовий блок як еталон правильної відповіді.",
            choiceHint: "Кожен гравець обирає один перемішаний варіант. Залиште 2–4 текстові блоки або зображення; перший блок є правильним. Аудіо та відео для цього типу питання не підтримуються.",
            answerOptions: "Варіанти відповіді (перший — правильний)",
            invalidText: "Для текстового питання для всіх потрібен рівно один непорожній текстовий блок правильної відповіді.",
            invalidChoice: "Для питання з вибором для всіх потрібно 2–4 коректні текстові варіанти або зображення; перший варіант є правильним.",
            invalidChoiceMedia: "Питання для всіх з вибором відповіді може містити лише текст або зображення.",
            title: "Відповідають усі",
            yourAnswer: "Ваша відповідь",
            submit: "Надіслати відповідь",
            confirmed: "Відповідь зараховано",
            rejected: "Не вдалося зарахувати відповідь.",
            closed: "Прийом відповідей завершено.",
            waitingForJudgment: "Очікування перевірки відповідей хостом.",
            correct: "Правильно",
            incorrect: "Неправильно",
            noAnswer: "Немає відповіді — 0 балів",
            waiting: "Очікує",
            answered: "Відповів",
            judge: "Перевірка відповіді",
            progress: (answered, total) => `Відповіді: ${answered}/${total}`,
            wagersProgress: (submitted, total) => `Ставки: ${submitted}/${total}`,
            judgingProgress: (current, total) => `Перевірка відповідей: ${current}/${total}`,
            yourWager: "Ваша ставка",
            confirmWager: "Підтвердити ставку",
            wagerConfirmed: "Ставку прийнято. Очікування хоста.",
            wagerRejected: "Не вдалося прийняти ставку.",
            wagerOutsideLimits: (minimum, maximum) => `Введіть ставку від ${minimum} до ${maximum}.`,
            wagerSubmitted: "Ставку зроблено",
            minimumWager: "Встановити мінімальну ставку",
            emptyAnswer: "Зафіксувати порожню відповідь",
            showQuestion: "Показати питання",
            waitingForWagers: "Очікування ставок"
        },
        it: {
            typeText: "Tutti i giocatori — risposta testuale",
            typeChoice: "Tutti i giocatori — scelta multipla",
            textHint: "Ogni giocatore invia una risposta testuale. Il conduttore giudica manualmente le risposte. Mantieni esattamente un blocco Testo non vuoto come risposta corretta di riferimento.",
            choiceHint: "Ogni giocatore sceglie un'opzione mescolata. Mantieni 2–4 blocchi Testo o Immagine; il primo blocco è l'opzione corretta. Audio e video non sono supportati per questo tipo di domanda.",
            answerOptions: "Opzioni di risposta (la prima è corretta)",
            invalidText: "Le domande testuali per tutti richiedono esattamente un blocco Testo non vuoto come risposta corretta.",
            invalidChoice: "Le domande a scelta multipla per tutti richiedono 2–4 opzioni Testo o Immagine valide; la prima opzione è corretta.",
            invalidChoiceMedia: "Le domande a scelta multipla per tutti possono contenere solo blocchi Testo o Immagine.",
            title: "Rispondono tutti",
            yourAnswer: "La tua risposta",
            submit: "Invia risposta",
            confirmed: "Risposta inviata",
            rejected: "Non è stato possibile inviare la risposta.",
            closed: "Le risposte sono chiuse.",
            waitingForJudgment: "In attesa che il conduttore giudichi le risposte.",
            correct: "Corretta",
            incorrect: "Errata",
            noAnswer: "Nessuna risposta — 0 punti",
            waiting: "In attesa",
            answered: "Ha risposto",
            judge: "Giudica risposta",
            progress: (answered, total) => `Risposte: ${answered}/${total}`,
            wagersProgress: (submitted, total) => `Puntate: ${submitted}/${total}`,
            judgingProgress: (current, total) => `Valutazione risposte: ${current}/${total}`,
            yourWager: "La tua puntata",
            confirmWager: "Conferma puntata",
            wagerConfirmed: "Puntata confermata. In attesa del conduttore.",
            wagerRejected: "Non è stato possibile inviare la puntata.",
            wagerOutsideLimits: (minimum, maximum) => `Inserisci una puntata da ${minimum} a ${maximum}.`,
            wagerSubmitted: "Puntata inviata",
            minimumWager: "Imposta la puntata minima",
            emptyAnswer: "Registra una risposta vuota",
            showQuestion: "Mostra domanda",
            waitingForWagers: "In attesa delle puntate"
        },
        ru: {
            typeText: "Все игроки — текстовый ответ",
            typeChoice: "Все игроки — выбор ответа",
            textHint: "Каждый игрок вводит текстовый ответ. Ответы проверяет хост вручную. Оставьте ровно один непустой текстовый блок как эталон правильного ответа.",
            choiceHint: "Каждый игрок выбирает один перемешанный вариант. Оставьте 2–4 текстовых блока или изображения; первый блок является правильным. Аудио и видео для этого типа вопроса не поддерживаются.",
            answerOptions: "Варианты ответа (первый — правильный)",
            invalidText: "Для текстового вопроса для всех нужен ровно один непустой текстовый блок правильного ответа.",
            invalidChoice: "Для вопроса с выбором для всех нужны 2–4 корректных текстовых варианта или изображения; первый вариант является правильным.",
            invalidChoiceMedia: "Вопрос для всех с выбором ответа может содержать только текст или изображения.",
            title: "Отвечают все",
            yourAnswer: "Ваш ответ",
            submit: "Отправить ответ",
            confirmed: "Ответ принят",
            rejected: "Не удалось принять ответ.",
            closed: "Приём ответов завершён.",
            waitingForJudgment: "Ожидание проверки ответов хостом.",
            correct: "Правильно",
            incorrect: "Неправильно",
            noAnswer: "Нет ответа — 0 баллов",
            waiting: "Ожидает",
            answered: "Ответил",
            judge: "Проверка ответа",
            progress: (answered, total) => `Ответы: ${answered}/${total}`,
            wagersProgress: (submitted, total) => `Ставки: ${submitted}/${total}`,
            judgingProgress: (current, total) => `Проверка ответов: ${current}/${total}`,
            yourWager: "Ваша ставка",
            confirmWager: "Подтвердить ставку",
            wagerConfirmed: "Ставка принята. Ожидание хоста.",
            wagerRejected: "Не удалось принять ставку.",
            wagerOutsideLimits: (minimum, maximum) => `Введите ставку от ${minimum} до ${maximum}.`,
            wagerSubmitted: "Ставка сделана",
            minimumWager: "Установить минимальную ставку",
            emptyAnswer: "Зафиксировать пустой ответ",
            showQuestion: "Показать вопрос",
            waitingForWagers: "Ожидание ставок"
        }
    };
    const text = stringsByCulture[culture] ?? stringsByCulture.en;
    let playerPollNow = null;
    let playerSessionPending = true;
    let hostPollNow = null;
    let hostControllerCode = null;

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

.all-player-choice-grid,
.all-player-host-choice-grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 0.75rem;
}

.all-player-choice-option,
.all-player-host-choice-option {
    min-height: 3.5rem;
    white-space: normal;
    overflow-wrap: anywhere;
}

.all-player-choice-option img,
.all-player-host-choice-option img {
    display: block;
    width: 100%;
    max-height: min(30vh, 18rem);
    object-fit: contain;
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

.all-player-host-judge {
    display: grid;
    gap: 0.75rem;
    padding: 1rem;
    border: 1px solid var(--line);
    border-radius: 0.8rem;
    background: var(--panel-2);
}

.all-player-host-judge-answer {
    margin: 0;
    white-space: pre-wrap;
    overflow-wrap: anywhere;
    font-size: 1.15rem;
}

.all-player-host-judge-actions {
    display: flex;
    flex-wrap: wrap;
    gap: 0.65rem;
}

.all-player-host-choice-preview {
    display: grid;
    gap: 0.65rem;
    width: min(100%, 84rem);
    margin: 1rem auto 0;
    align-self: stretch;
    justify-self: stretch;
}

.all-player-host-choice-option {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 100%;
    min-height: clamp(5rem, 10vh, 8rem);
    padding: clamp(0.8rem, 1.6vw, 1.35rem);
    font-size: clamp(1.4rem, 2.6vw, 3rem);
    border: 1px solid var(--line);
    border-radius: 0.75rem;
    background: var(--panel-2);
    text-align: center;
}

.host-game-board.all-player-question-answering .question-controls > :not(.all-player-host-progress):not(.all-player-host-close-form):not(.all-player-host-timer-form) {
    display: none !important;
}

html.all-player-multiple-choice-answer-layout .host-game-board .answer-presentation .game-content-blocks,
.host-game-board.all-player-multiple-choice-answer .answer-presentation .game-content-blocks {
    display: grid !important;
    grid-template-columns: repeat(2, minmax(0, 1fr)) !important;
    grid-auto-rows: minmax(4.5rem, auto);
    align-items: stretch;
    justify-items: stretch;
    align-content: center;
    overflow: hidden;
    gap: clamp(0.5rem, 1vw, 0.85rem) !important;
}

html.all-player-multiple-choice-answer-layout .host-game-board .answer-presentation .game-content-block,
.host-game-board.all-player-multiple-choice-answer .answer-presentation .game-content-block {
    min-width: 0;
    width: 100%;
    min-height: clamp(4.5rem, 13vh, 8rem);
    height: auto !important;
    display: flex !important;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    margin: 0 !important;
    padding: clamp(0.45rem, 0.8vw, 0.75rem);
    overflow: hidden;
    border: 3px solid #c62828;
    border-radius: 0.8rem;
    text-align: center;
}

html.all-player-multiple-choice-answer-layout .host-game-board .answer-presentation .game-content-block:first-child,
.host-game-board.all-player-multiple-choice-answer .answer-presentation .game-content-block:first-child {
    border-color: #2e7d32;
}

html.all-player-multiple-choice-answer-layout .host-game-board .answer-presentation .game-content-image,
.host-game-board.all-player-multiple-choice-answer .answer-presentation .game-content-image {
    display: block;
    width: 100%;
    height: min(14vh, 9rem);
    min-height: 0;
    max-height: min(14vh, 9rem);
    object-fit: contain;
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

    const markEditorProgrammaticStateClean = () => {
        const status = document.querySelector("[data-question-save-status]");
        if (!(status instanceof HTMLElement)) {
            return;
        }

        const previous = {
            hidden: status.hidden,
            text: status.textContent,
            className: status.className,
            display: status.style.display
        };

        // The existing editor dirty tracker already marks AJAX saves as clean
        // by observing this status element. Reuse that path without showing UI.
        status.style.display = "none";
        status.classList.add("alert-success");
        status.classList.remove("alert-error");
        status.textContent = "editor-state-synchronized";
        status.hidden = false;

        window.setTimeout(() => {
            status.hidden = previous.hidden;
            status.textContent = previous.text;
            status.className = previous.className;
            status.style.display = previous.display;
        }, 0);
    };

    const initializeEditor = () => {
        const form = document.querySelector("form.question-editor");
        const select = document.getElementById("Input_PresentationType");
        const modeInput = document.getElementById("Input_AllPlayerMode");
        if (!(form instanceof HTMLFormElement) ||
            !(select instanceof HTMLSelectElement) ||
            form.dataset.allPlayerEditorInitialized === "true") {
            return;
        }
        form.dataset.allPlayerEditorInitialized = "true";

        let textOption = select.querySelector('option[value="2"]');
        if (!textOption) {
            textOption = new Option(text.typeText, "2");
            select.add(textOption);
        }
        textOption.textContent = text.typeText;

        let choiceOption = select.querySelector('option[value="3"]');
        if (!choiceOption) {
            choiceOption = new Option(text.typeChoice, "3");
            select.add(choiceOption);
        }
        choiceOption.textContent = text.typeChoice;

        const typePanel = select.closest(".question-type-setting");
        const fourClueHelp = document.getElementById("four-clues-help");
        const textHelp = document.createElement("small");
        textHelp.className = "all-player-editor-help";
        textHelp.textContent = text.textHint;
        textHelp.hidden = true;
        const choiceHelp = document.createElement("small");
        choiceHelp.className = "all-player-editor-help";
        choiceHelp.textContent = text.choiceHint;
        choiceHelp.hidden = true;
        typePanel?.append(textHelp, choiceHelp);

        const questionSection = document.getElementById("question-blocks");
        const answerSection = document.getElementById("answer-blocks");
        const answerHeading = answerSection?.previousElementSibling;
        if (answerHeading instanceof HTMLHeadingElement) {
            answerHeading.dataset.standardHeading = answerHeading.textContent ?? "";
        }

        const buzzSetting = document.getElementById("buzz-mode-setting");
        const buzzSelect = document.getElementById("Input_BuzzModeOverride");
        let standardBuzzMode = buzzSelect instanceof HTMLSelectElement
            ? buzzSelect.value
            : "0";
        let previousAllPlayer = false;

        const setAllowedTypes = (section, allowed) => {
            section?.querySelectorAll(".content-block-type-option")
                .forEach(option => {
                    const type = option.dataset.blockType;
                    option.hidden = Boolean(allowed) && !allowed.has(type);
                });
        };

        const sync = () => {
            const isText = select.value === "2";
            const isChoice = select.value === "3";
            const isAllPlayer = isText || isChoice;

            if (modeInput instanceof HTMLInputElement) {
                modeInput.value = isText
                    ? "text"
                    : isChoice
                        ? "multipleChoice"
                        : "";
            }

            textHelp.hidden = !isText;
            choiceHelp.hidden = !isChoice;
            if (fourClueHelp && isAllPlayer) {
                fourClueHelp.hidden = true;
            }

            if (isAllPlayer && !previousAllPlayer &&
                buzzSelect instanceof HTMLSelectElement) {
                standardBuzzMode = buzzSelect.value;
            }

            if (isAllPlayer) {
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
                if (buzzSelect instanceof HTMLSelectElement) {
                    buzzSelect.value = standardBuzzMode;
                }
                if (answerHeading instanceof HTMLHeadingElement) {
                    answerHeading.textContent =
                        answerHeading.dataset.standardHeading ?? "";
                }
            }

            if (isChoice) {
                const allowed = new Set(["Text", "Image"]);
                setAllowedTypes(questionSection, allowed);
                setAllowedTypes(answerSection, allowed);
            } else if (isText) {
                setAllowedTypes(questionSection, null);
                setAllowedTypes(answerSection, new Set(["Text"]));
            } else {
                setAllowedTypes(questionSection, null);
                setAllowedTypes(answerSection, null);
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

        const directCards = section => Array.from(
            section?.querySelectorAll(
                ":scope > [data-content-block-list] > .content-block-card") ?? []);

        const imageCardHasFile = card => {
            const fileInput = card.querySelector('input[type="file"]');
            if (fileInput instanceof HTMLInputElement &&
                (fileInput.files?.length ?? 0) > 0) {
                return true;
            }

            const preview = card.querySelector(".unified-file-preview");
            const removeFile = card.querySelector('[name$=".RemoveFile"]');
            const removeRequested = removeFile instanceof HTMLInputElement &&
                (removeFile.checked || removeFile.value === "true");
            return preview instanceof HTMLElement &&
                preview.dataset.hasOriginal === "true" &&
                !removeRequested;
        };

        const validate = () => {
            const isText = select.value === "2";
            const isChoice = select.value === "3";
            if (!isText && !isChoice) {
                return null;
            }

            const answerCards = directCards(answerSection);
            if (isText) {
                const value = answerCards[0]?.querySelector('[name$=".TextContent"]')
                    ?.value?.trim() ?? "";
                return answerCards.length === 1 &&
                    answerCards[0]?.dataset.blockType === "Text" &&
                    value
                    ? null
                    : text.invalidText;
            }

            const questionCards = directCards(questionSection);
            const allowedType = card => ["Text", "Image"].includes(
                card.dataset.blockType);
            if (!questionCards.every(allowedType) ||
                !answerCards.every(allowedType)) {
                return text.invalidChoiceMedia;
            }

            if (answerCards.length < 2 || answerCards.length > 4) {
                return text.invalidChoice;
            }

            const textValues = [];
            for (const card of answerCards) {
                if (card.dataset.blockType === "Text") {
                    const value = card.querySelector('[name$=".TextContent"]')
                        ?.value?.trim() ?? "";
                    if (!value) {
                        return text.invalidChoice;
                    }
                    textValues.push(value.toLocaleLowerCase());
                    continue;
                }

                if (!imageCardHasFile(card)) {
                    return text.invalidChoice;
                }
            }

            return new Set(textValues).size === textValues.length
                ? null
                : text.invalidChoice;
        };

        select.addEventListener("change", () => {
            window.queueMicrotask(sync);
        });

        form.addEventListener("submit", event => {
            const validationError = validate();
            if (!validationError) {
                const existing = form.querySelector(".all-player-editor-error");
                if (existing) {
                    existing.hidden = true;
                }
                return;
            }

            event.preventDefault();
            event.stopImmediatePropagation();
            showEditorError(validationError);
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
                    }
                    sync();
                    window.queueMicrotask(markEditorProgrammaticStateClean);
                })
                .catch(() => {
                    sync();
                    window.queueMicrotask(markEditorProgrammaticStateClean);
                });
        } else {
            sync();
        }
    };

    const appendOptionContent = (container, option) => {
        if (option?.kind === "image" && option.imageUrl) {
            const image = document.createElement("img");
            image.src = option.imageUrl;
            image.alt = option.text ?? "";
            container.appendChild(image);
            return;
        }

        const label = document.createElement("span");
        label.textContent = option?.text ?? "";
        container.appendChild(label);
    };

    const initializePlayer = () => {
        const lobby = document.querySelector(".player-lobby");
        if (!(lobby instanceof HTMLElement)) {
            return;
        }

        const existingPanel = lobby.querySelector(".player-all-player-panel");
        if (lobby.dataset.allPlayerClientInitialized === "true" &&
            existingPanel instanceof HTMLElement &&
            existingPanel.isConnected) {
            return;
        }
        delete lobby.dataset.allPlayerClientInitialized;

        const code = lobby.dataset.gameCode;
        const playerId = lobby.dataset.playerId;
        const playerAccessStorageKey = code && playerId
            ? `badwolfquiz:${code}:player:${playerId}`
            : null;
        const accessToken = lobby.dataset.accessToken ||
            (playerAccessStorageKey
                ? localStorage.getItem(playerAccessStorageKey)
                : null);
        if (!code || !playerId || !accessToken) {
            return;
        }
        lobby.dataset.allPlayerClientInitialized = "true";

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
        let currentControlsKey = "";
        let requestInFlight = false;
        let pollHandle = 0;
        let lastState = null;

        const getBuzzerPanel = () => document.querySelector(
            ".player-buzzer-panel");
        const setRuntimeActive = active => {
            lobby.classList.toggle("all-player-runtime-active", active);
        };
        const hideBuzzer = () => {
            const buzzerPanel = getBuzzerPanel();
            if (buzzerPanel) {
                buzzerPanel.dataset.hiddenByAllPlayer = "true";
                buzzerPanel.hidden = true;
            }
        };
        const restoreBuzzer = () => {
            const buzzerPanel = getBuzzerPanel();
            if (buzzerPanel?.dataset.hiddenByAllPlayer === "true") {
                delete buzzerPanel.dataset.hiddenByAllPlayer;
                buzzerPanel.hidden = false;
            }
        };

        const setControlsDisabled = disabled => {
            controls?.querySelectorAll("button, textarea, input")
                .forEach(element => {
                    element.disabled = disabled;
                });
        };

        const postPlayerAction = async (handler, values, fallbackError) => {
            if (currentQuestionId === null || requestInFlight) {
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
            for (const [key, value] of Object.entries(values)) {
                data.set(key, String(value));
            }

            try {
                const response = await fetch(`${apiPath}?handler=${handler}`, {
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
                    throw new Error(result?.error || fallbackError);
                }

                if (result.state) {
                    applyState(result.state);
                }
            } catch (exception) {
                if (error) {
                    error.textContent = exception?.message || fallbackError;
                    error.hidden = false;
                }
                if (lastState?.phase === "wagering" && !lastState.hasWager ||
                    lastState?.phase === "answering" &&
                        !lastState.hasSubmitted &&
                        lastState.isAccepting) {
                    setControlsDisabled(false);
                }
            } finally {
                requestInFlight = false;
            }
        };

        const submitAnswer = answer => {
            const normalized = answer?.trim();
            if (!normalized) {
                return;
            }
            return postPlayerAction(
                "Submit",
                { answer: normalized },
                text.rejected);
        };

        const submitWager = amount => postPlayerAction(
            "Wager",
            { amount },
            text.wagerRejected);

        const buildWagerControls = state => {
            const minimum = Number(state.minimumWager);
            const maximum = Number(state.maximumWager);
            const form = document.createElement("form");
            form.className = "question-wager-form final-wager-form";
            const label = document.createElement("label");
            label.className = "wager-display-label";
            const caption = document.createElement("span");
            caption.textContent = text.yourWager;
            const display = document.createElement("input");
            display.className = "wager-display";
            display.type = "text";
            display.inputMode = "none";
            display.readOnly = true;
            display.required = true;
            label.append(caption, display);

            const keypad = document.createElement("div");
            keypad.className = "wager-keypad";
            const addDigit = digit => {
                if (display.value.length >= String(maximum).length + 1) {
                    return;
                }
                display.value += digit;
                refresh();
            };
            for (const digit of [1, 2, 3, 4, 5, 6, 7, 8, 9]) {
                const button = document.createElement("button");
                button.type = "button";
                button.textContent = String(digit);
                button.addEventListener("click", () => addDigit(digit));
                keypad.appendChild(button);
            }
            const clear = document.createElement("button");
            clear.type = "button";
            clear.textContent = "C";
            clear.addEventListener("click", () => {
                display.value = "";
                refresh();
            });
            keypad.appendChild(clear);
            const zero = document.createElement("button");
            zero.type = "button";
            zero.textContent = "0";
            zero.addEventListener("click", () => addDigit(0));
            keypad.appendChild(zero);
            const backspace = document.createElement("button");
            backspace.type = "button";
            backspace.textContent = "⌫";
            backspace.addEventListener("click", () => {
                display.value = display.value.slice(0, -1);
                refresh();
            });
            keypad.appendChild(backspace);
            const maximumButton = document.createElement("button");
            maximumButton.type = "button";
            maximumButton.className = "wager-keypad-max";
            maximumButton.textContent = "MAX";
            maximumButton.addEventListener("click", () => {
                display.value = String(maximum);
                refresh();
            });
            keypad.appendChild(maximumButton);

            const validation = document.createElement("p");
            validation.className = "wager-validation";
            const submit = document.createElement("button");
            submit.type = "submit";
            submit.className = "button button-primary";
            submit.textContent = text.confirmWager;
            submit.disabled = true;

            function refresh() {
                const amount = Number(display.value);
                const isValid = display.value !== "" &&
                    amount >= minimum &&
                    amount <= maximum;
                validation.textContent = isValid
                    ? "✓"
                    : text.wagerOutsideLimits(minimum, maximum);
                validation.classList.toggle("is-valid", isValid);
                submit.disabled = !isValid;
            }

            form.addEventListener("submit", event => {
                event.preventDefault();
                const amount = Number(display.value);
                if (amount >= minimum && amount <= maximum) {
                    void submitWager(amount);
                }
            });
            form.append(label, keypad, validation, submit);
            controls.appendChild(form);
            refresh();
        };

        const buildAnswerControls = state => {
            if (state.mode === "multipleChoice") {
                const grid = document.createElement("div");
                grid.className = "all-player-choice-grid";
                for (const option of state.options ?? []) {
                    const button = document.createElement("button");
                    button.type = "button";
                    button.className =
                        "button button-secondary all-player-choice-option";
                    appendOptionContent(button, option);
                    button.addEventListener("click", () => {
                        void submitAnswer(String(option.id));
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
                void submitAnswer(textarea.value);
            });
            controls.appendChild(form);
        };

        const getControlsKey = state => JSON.stringify({
            question: state.sourceQuestionId,
            mode: state.mode,
            phase: state.phase,
            hasWager: state.hasWager,
            minimumWager: state.minimumWager,
            maximumWager: state.maximumWager,
            options: (state.options ?? []).map(option => [
                option.id,
                option.kind,
                option.text ?? "",
                option.imageUrl ?? ""
            ])
        });

        const buildControls = state => {
            controls.replaceChildren();
            if (state.phase === "wagering" && !state.hasWager) {
                buildWagerControls(state);
            } else if (state.phase === "answering" &&
                !state.hasSubmitted &&
                state.isAccepting) {
                buildAnswerControls(state);
            }
        };

        function applyState(state) {
            lastState = state;
            if (playerSessionPending) {
                setRuntimeActive(Boolean(state?.active));
                if (state?.active) {
                    hideBuzzer();
                }
                panel.hidden = true;
                return;
            }

            if (!state?.active) {
                setRuntimeActive(false);
                panel.hidden = true;
                currentQuestionId = null;
                currentMode = null;
                currentControlsKey = "";
                controls?.replaceChildren();
                restoreBuzzer();
                return;
            }

            setRuntimeActive(true);
            hideBuzzer();
            panel.hidden = false;
            currentQuestionId = state.sourceQuestionId;
            currentMode = state.mode;
            const controlsKey = getControlsKey(state);
            const controlsMissing = !controls?.firstElementChild;
            if (controlsMissing || currentControlsKey !== controlsKey) {
                buildControls(state);
                currentControlsKey = controlsKey;
            }

            const seconds = Math.max(
                0,
                Math.ceil((state.remainingMilliseconds ?? 0) / 1000));
            if (timer) {
                timer.textContent = state.phase === "answering" &&
                    state.isAccepting
                    ? `${seconds}s`
                    : "";
            }

            if (state.phase === "wagering") {
                if (state.hasWager) {
                    controls.hidden = true;
                    status.textContent = text.wagerConfirmed;
                } else {
                    controls.hidden = false;
                    status.textContent = "";
                }
                return;
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
                status.textContent = state.isJudging
                    ? text.waitingForJudgment
                    : text.confirmed;
            } else if (!state.isAccepting) {
                setControlsDisabled(true);
                controls.hidden = true;
                status.textContent = text.closed;
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
            if (!lobby.isConnected || !panel.isConnected) {
                return;
            }
            pollHandle = window.setTimeout(poll, active ? 300 : 1200);
        };

        const poll = async () => {
            if (!lobby.isConnected || !panel.isConnected) {
                return;
            }
            if (playerSessionPending) {
                panel.hidden = true;
                schedule(false);
                return;
            }
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

        playerPollNow = () => {
            window.clearTimeout(pollHandle);
            if (playerSessionPending) {
                panel.hidden = true;
                return;
            }
            if (!lobby.isConnected || !panel.isConnected) {
                delete lobby.dataset.allPlayerClientInitialized;
                initializePlayer();
                return;
            }
            void poll();
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
        if (!code || hostControllerCode === code) {
            return;
        }
        hostControllerCode = code;

        let pollHandle = 0;
        let lastState = null;
        let refreshKey = null;
        let hostRequestInFlight = false;

        const findBoard = () => document.querySelector(
            `.host-game-board[data-game-code="${CSS.escape(code)}"]`);

        const removeProgress = board => {
            board.querySelector(".all-player-host-progress")?.remove();
        };

        const removeHostChoices = board => {
            board.querySelector(
                "[data-all-player-client-preview]")?.remove();
            board.querySelector(
                "[data-all-player-client-moved-action]")?.remove();
        };

        const removeHostReview = board => {
            board.querySelector(".all-player-host-review")?.remove();
        };

        const clearHostPrimaryAction = board => {
            const target = board.querySelector(
                "[data-all-player-primary-action]");
            if (target instanceof HTMLElement) {
                delete target.dataset.renderKey;
                target.replaceChildren();
            }
        };

        const postHostAction = async (handler, values) => {
            if (hostRequestInFlight) {
                return;
            }
            hostRequestInFlight = true;
            const data = new FormData();
            data.set("code", code);
            for (const [key, value] of Object.entries(values)) {
                data.set(key, String(value));
            }

            try {
                const response = await fetch(`${apiPath}?handler=${handler}`, {
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
                    throw new Error(result?.error || text.rejected);
                }
                if (result.state) {
                    applyState(result.state);
                }
            } catch (error) {
                console.error(error);
            } finally {
                hostRequestInFlight = false;
            }
        };

        const renderHostChoices = (board, state) => {
            removeHostChoices(board);
            if (state.phase !== "answering" ||
                state.mode !== "multipleChoice") {
                return;
            }

            const presentation = board.querySelector(".question-presentation");
            if (!presentation ||
                presentation.querySelector("[data-all-player-server-preview]")) {
                return;
            }

            const preview = document.createElement("section");
            preview.className = "all-player-host-choice-preview";
            preview.dataset.allPlayerClientPreview = "true";
            preview.tabIndex = 0;
            preview.setAttribute("aria-label", text.answerOptions);
            const closeForm = board.querySelector(
                ".all-player-host-close-form");
            if (closeForm && closeForm.parentElement !== presentation) {
                closeForm.dataset.allPlayerClientMovedAction = "true";
                presentation.appendChild(closeForm);
            }
            const grid = document.createElement("div");
            grid.className = "all-player-host-choice-grid";
            for (const option of state.options ?? []) {
                const item = document.createElement("div");
                item.className = "all-player-host-choice-option";
                appendOptionContent(item, option);
                grid.appendChild(item);
            }
            preview.appendChild(grid);
            presentation.appendChild(preview);
        };

        const postJudgment = (state, isCorrect) => postHostAction(
            "Judge",
            {
                sourceQuestionId: state.sourceQuestionId,
                playerId: state.judgeSubmission.id,
                isCorrect
            });

        const createActionButton = (symbol, label, handler) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className =
                "button button-secondary final-player-action-icon";
            button.title = label;
            button.setAttribute("aria-label", label);
            const icon = document.createElement("span");
            icon.setAttribute("aria-hidden", "true");
            icon.textContent = symbol;
            button.appendChild(icon);
            button.addEventListener("pointerdown", event => {
                event.stopPropagation();
            });
            button.addEventListener("click", event => {
                event.preventDefault();
                event.stopPropagation();
                void handler();
            });
            return button;
        };

        const getProgressRenderKey = state => JSON.stringify({
            sourceQuestionId: state.sourceQuestionId,
            phase: state.phase,
            mode: state.mode,
            isClosed: state.isClosed,
            canStartQuestion: state.canStartQuestion,
            players: (state.players ?? []).map(player => [
                player.id,
                player.wagerSubmitted,
                player.submitted,
                player.isJudged,
                player.isCorrect,
                player.scoreDelta
            ])
        });

        const formatScoreDelta = value => {
            const scoreDelta = Number(value);
            if (!Number.isFinite(scoreDelta)) {
                return "";
            }

            return scoreDelta > 0 ? ` +${scoreDelta}` : ` ${scoreDelta}`;
        };

        const renderProgress = (board, state) => {
            let progress = board.querySelector(".all-player-host-progress");
            if (!progress) {
                progress = document.createElement("div");
                progress.className = "all-player-host-progress";
            }

            const renderKey = getProgressRenderKey(state);
            if (progress.dataset.renderKey === renderKey) {
                return;
            }
            progress.dataset.renderKey = renderKey;
            progress.replaceChildren();
            const heading = document.createElement("strong");
            heading.textContent = state.phase === "wagering"
                ? text.wagersProgress(
                    state.wageredCount ?? 0,
                    state.playerCount ?? 0)
                : text.progress(
                    state.answeredCount ?? 0,
                    state.playerCount ?? 0);
            progress.tabIndex = 0;
            progress.setAttribute(
                "aria-label",
                heading.textContent ?? text.title);
            const list = document.createElement("ul");
            list.className = "all-player-answer-progress";

            for (const player of state.players ?? []) {
                const item = document.createElement("li");
                const name = document.createElement("strong");
                name.textContent = player.name;
                name.title = player.name;
                const playerState = document.createElement("span");

                if (state.phase === "wagering") {
                    playerState.textContent = player.wagerSubmitted
                        ? text.wagerSubmitted
                        : text.waiting;
                    item.append(name, playerState);
                    if (!player.wagerSubmitted) {
                        item.appendChild(createActionButton(
                            "⇩",
                            text.minimumWager,
                            () => postHostAction("MinimumWager", {
                                sourceQuestionId: state.sourceQuestionId,
                                playerId: player.id
                            })));
                    }
                } else {
                    if (state.isClosed || player.isJudged) {
                        const result = player.submitted
                            ? player.isCorrect
                                ? text.correct
                                : text.incorrect
                            : text.noAnswer;
                        playerState.textContent =
                            `${result}${formatScoreDelta(player.scoreDelta)}`;
                    } else {
                        playerState.textContent = player.submitted
                            ? text.answered
                            : text.waiting;
                    }
                    item.append(name, playerState);
                    if (!player.submitted &&
                        state.phase === "answering") {
                        item.appendChild(createActionButton(
                            "∅",
                            text.emptyAnswer,
                            () => postHostAction("EmptyAnswer", {
                                sourceQuestionId: state.sourceQuestionId,
                                playerId: player.id
                            })));
                    }
                }
                list.appendChild(item);
            }

            progress.append(heading, list);

            const target = board.querySelector(".current-question-summary") ??
                board.querySelector(".question-presentation");
            if (target && progress.parentElement !== target) {
                target.appendChild(progress);
            }
        };

        const syncReviewActions = (board, state) => {
            board.querySelectorAll("[data-all-player-review-action]")
                .forEach(action => {
                    if (action instanceof HTMLElement) {
                        action.hidden = state.phase !== "answering";
                    }
                });
        };

        const renderPrimaryAction = (board, state) => {
            const target = board.querySelector(
                "[data-all-player-primary-action]");
            if (!(target instanceof HTMLElement)) {
                return;
            }

            const renderKey = [
                state.sourceQuestionId,
                state.phase,
                Boolean(state.canStartQuestion)
            ].join(":");
            if (target.dataset.renderKey === renderKey) {
                return;
            }
            target.dataset.renderKey = renderKey;
            target.replaceChildren();

            if (state.phase !== "wagering" || !state.canStartQuestion) {
                return;
            }

            const button = document.createElement("button");
            button.type = "button";
            button.className = "button button-primary";
            button.textContent = text.showQuestion;
            button.addEventListener("click", event => {
                event.preventDefault();
                button.disabled = true;
                void postHostAction("StartQuestion", {
                    sourceQuestionId: state.sourceQuestionId
                }).finally(() => {
                    if (button.isConnected) {
                        button.disabled = false;
                    }
                });
            });
            target.appendChild(button);
        };

        const getTextReviewRenderKey = state => JSON.stringify({
            sourceQuestionId: state.sourceQuestionId,
            phase: state.phase,
            judgedCount: state.judgedCount,
            answeredCount: state.answeredCount,
            judgeSubmission: state.judgeSubmission
                ? [
                    state.judgeSubmission.id,
                    state.judgeSubmission.name,
                    state.judgeSubmission.answer ?? "-"
                ]
                : null
        });

        const renderTextReview = (board, state) => {
            let review = board.querySelector(".all-player-host-review");
            if (state.phase !== "judging" || !state.judgeSubmission) {
                review?.remove();
                return;
            }

            const renderKey = getTextReviewRenderKey(state);
            if (review instanceof HTMLElement &&
                review.dataset.renderKey === renderKey) {
                return;
            }
            review?.remove();

            const target = board.querySelector(".question-presentation");
            if (!target) {
                return;
            }

            review = document.createElement("section");
            review.className = "all-player-host-review final-judging-list";
            review.dataset.renderKey = renderKey;
            const progress = document.createElement("p");
            progress.className = "dialog-warning";
            progress.textContent = text.judgingProgress(
                (state.judgedCount ?? 0) + 1,
                state.answeredCount ?? 0);
            const answer = document.createElement("section");
            answer.className =
                "game-content-presentation final-player-answer-presentation";
            const name = document.createElement("h2");
            name.textContent = state.judgeSubmission.name;
            const value = document.createElement("p");
            value.className = "game-content-text";
            value.textContent = state.judgeSubmission.answer ?? "-";
            answer.append(name, value);
            const actions = document.createElement("div");
            actions.className =
                "question-judge-actions final-judging-actions all-player-host-judge-actions";
            const correct = document.createElement("button");
            correct.type = "button";
            correct.className = "button judgment-correct-button";
            correct.textContent = text.correct;
            const incorrect = document.createElement("button");
            incorrect.type = "button";
            incorrect.className = "button judgment-incorrect-button";
            incorrect.textContent = text.incorrect;

            const submitJudgment = isCorrect => event => {
                event.preventDefault();
                event.stopPropagation();
                if (hostRequestInFlight) {
                    return;
                }

                correct.disabled = true;
                incorrect.disabled = true;
                void postJudgment(state, isCorrect).finally(() => {
                    if (review.isConnected) {
                        correct.disabled = false;
                        incorrect.disabled = false;
                    }
                });
            };
            for (const button of [correct, incorrect]) {
                button.addEventListener("pointerdown", event => {
                    event.stopPropagation();
                });
            }
            correct.addEventListener("click", submitJudgment(true));
            incorrect.addEventListener("click", submitJudgment(false));
            actions.append(correct, incorrect);
            review.append(progress, answer, actions);
            target.appendChild(review);
        };

        const requestRefresh = (state, expectedSelector) => {
            const board = findBoard();
            if (!board || board.querySelector(expectedSelector)) {
                return false;
            }
            const key = `${state.sourceQuestionId}:${state.phase}`;
            if (refreshKey === key || !window.BadWolfHostGameplay?.refresh) {
                return true;
            }
            refreshKey = key;
            window.BadWolfHostGameplay.refresh()
                .then(() => {
                    refreshKey = null;
                    hostPollNow?.();
                })
                .catch(error => {
                    console.error(error);
                    refreshKey = null;
                });
            return true;
        };

        const applyState = state => {
            lastState = state;
            const isMultipleChoiceAnswer = Boolean(
                state?.active &&
                state.isClosed &&
                state.mode === "multipleChoice");
            document.documentElement.classList.toggle(
                "all-player-multiple-choice-answer-layout",
                isMultipleChoiceAnswer);

            const board = findBoard();
            if (!(board instanceof HTMLElement)) {
                return;
            }

            if (!state?.active) {
                board.classList.remove(
                    "all-player-question-answering",
                    "all-player-question-wagering",
                    "all-player-multiple-choice-answer",
                    "all-player-text-reviewing");
                removeProgress(board);
                removeHostChoices(board);
                removeHostReview(board);
                clearHostPrimaryAction(board);
                refreshKey = null;
                return;
            }

            if (state.phase === "answering" &&
                requestRefresh(state, ".question-presentation")) {
                return;
            }
            if (state.phase === "closed" &&
                requestRefresh(state, ".answer-presentation")) {
                return;
            }

            board.classList.toggle(
                "all-player-question-answering",
                state.phase !== "closed");
            board.classList.toggle(
                "all-player-question-wagering",
                state.phase === "wagering");
            board.classList.toggle(
                "all-player-multiple-choice-answer",
                isMultipleChoiceAnswer);
            board.classList.toggle(
                "all-player-text-reviewing",
                state.phase === "judging");
            renderProgress(board, state);
            renderPrimaryAction(board, state);
            renderHostChoices(board, state);
            syncReviewActions(board, state);
            renderTextReview(board, state);
        };

        const schedule = active => {
            window.clearTimeout(pollHandle);
            pollHandle = window.setTimeout(poll, active ? 180 : 900);
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

        hostPollNow = () => {
            window.clearTimeout(pollHandle);
            void poll();
        };

        document.addEventListener("badwolf:host-gameplay-updated", () => {
            hostPollNow?.();
        });

        void poll();
    };

    const initializeAll = () => {
        initializeEditor();
        initializePlayer();
        initializeHost();
    };

    initializeAll();

    window.addEventListener("pageshow", () => playerPollNow?.());
    window.addEventListener("focus", () => playerPollNow?.());
    window.addEventListener("online", () => playerPollNow?.());

    document.addEventListener("badwolf:player-session-pending", () => {
        playerSessionPending = true;
        const panel = document.querySelector(".player-all-player-panel");
        if (panel instanceof HTMLElement) {
            panel.hidden = true;
        }
    });

    document.addEventListener("badwolf:player-session-ready", () => {
        playerSessionPending = false;
        const lobby = document.querySelector(".player-lobby");
        if (lobby instanceof HTMLElement &&
            lobby.dataset.allPlayerClientInitialized === "true" &&
            !lobby.querySelector(".player-all-player-panel")) {
            delete lobby.dataset.allPlayerClientInitialized;
        }

        initializePlayer();
        playerPollNow?.();
    });

    const observer = new MutationObserver(() => {
        initializeAll();
    });
    observer.observe(document.documentElement, {
        childList: true,
        subtree: true
    });
})();
