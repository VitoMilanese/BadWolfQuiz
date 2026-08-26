(() => {
    "use strict";

    const loader = document.currentScript;
    const form = loader?.closest("form[data-ajax-question-editor]");
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    // This controller owns only the Question Editor integration. The existing
    // all-player and host-selected scripts still own their gameplay clients.
    form.dataset.allPlayerEditorInitialized = "true";
    window.badWolfHostMultipleChoiceInitialized = true;

    if (window.badWolfMultipleChoiceAnswerOptionsEditorLoaded) {
        return;
    }
    window.badWolfMultipleChoiceAnswerOptionsEditorLoaded = true;

    const savedQuestionType = loader?.dataset.savedQuestionType ?? "";
    const culture = (document.documentElement.lang || "en").toLowerCase();
    const text = culture.startsWith("uk")
        ? {
            typeText: "Усі гравці — текстова відповідь",
            typeChoice: "Усі гравці — вибір відповіді",
            typeHostChoice: "Вибір відповіді — обирає хост",
            textHint: "Кожен гравець надсилає власну текстову відповідь. Хост перевіряє відповіді по черзі.",
            choiceHint: "Варіанти відповіді зберігаються в обов’язковому блоці «Варіанти відповіді». Перший варіант правильний. Інші блоки відповіді показуються лише після завершення питання.",
            hostChoiceHint: "У блоці «Варіанти відповіді» має бути від 4 до 10 унікальних текстових варіантів до 20 символів. Перший варіант правильний. Інші блоки відповіді показуються лише після завершення питання.",
            correct: "Правильна",
            invalidChoiceQuestion: "Питання з вибором для всіх гравців підтримує лише текст і зображення в самому питанні.",
            invalidChoice: "У блоці «Варіанти відповіді» має бути від 2 до 4 непорожніх унікальних текстових або графічних варіантів.",
            invalidHostCount: "У блоці «Варіанти відповіді» має бути від 4 до 10 варіантів.",
            invalidHostText: "Кожен варіант для хоста має бути непорожнім текстом довжиною не більше 20 символів.",
            invalidHostDuplicate: "Варіанти відповіді для хоста мають бути унікальними.",
            addFailed: "Не вдалося додати варіант відповіді."
        }
        : culture.startsWith("it")
            ? {
                typeText: "Tutti i giocatori — risposta testuale",
                typeChoice: "Tutti i giocatori — scelta multipla",
                typeHostChoice: "Scelta multipla — seleziona il conduttore",
                textHint: "Ogni giocatore invia una risposta testuale privata. Il conduttore le valuta una alla volta.",
                choiceHint: "Le opzioni sono contenute nel blocco obbligatorio «Opzioni di risposta». La prima opzione è corretta. Gli altri blocchi della risposta vengono mostrati solo dopo la chiusura della domanda.",
                hostChoiceHint: "Il blocco «Opzioni di risposta» deve contenere da 4 a 10 opzioni testuali uniche di massimo 20 caratteri. La prima è corretta. Gli altri blocchi vengono mostrati solo dopo la chiusura della domanda.",
                correct: "Corretta",
                invalidChoiceQuestion: "La domanda a scelta per tutti i giocatori supporta solo testo e immagini nella domanda.",
                invalidChoice: "Il blocco «Opzioni di risposta» deve contenere da 2 a 4 opzioni di testo o immagine non vuote e uniche.",
                invalidHostCount: "Il blocco «Opzioni di risposta» deve contenere da 4 a 10 opzioni.",
                invalidHostText: "Ogni opzione selezionata dal conduttore deve essere un testo non vuoto di massimo 20 caratteri.",
                invalidHostDuplicate: "Le opzioni selezionate dal conduttore devono essere uniche.",
                addFailed: "Impossibile aggiungere l’opzione di risposta."
            }
            : culture.startsWith("ru")
                ? {
                    typeText: "Все игроки — текстовый ответ",
                    typeChoice: "Все игроки — выбор ответа",
                    typeHostChoice: "Выбор ответа — выбирает хост",
                    textHint: "Каждый игрок отправляет свой текстовый ответ. Хост проверяет ответы по очереди.",
                    choiceHint: "Варианты находятся в обязательном блоке «Варианты ответа». Первый вариант правильный. Остальные блоки ответа показываются только после завершения вопроса.",
                    hostChoiceHint: "В блоке «Варианты ответа» должно быть от 4 до 10 уникальных текстовых вариантов до 20 символов. Первый вариант правильный. Остальные блоки показываются только после завершения вопроса.",
                    correct: "Правильный",
                    invalidChoiceQuestion: "Вопрос с выбором для всех игроков поддерживает только текст и изображения в самом вопросе.",
                    invalidChoice: "В блоке «Варианты ответа» должно быть от 2 до 4 непустых уникальных текстовых или графических вариантов.",
                    invalidHostCount: "В блоке «Варианты ответа» должно быть от 4 до 10 вариантов.",
                    invalidHostText: "Каждый вариант для хоста должен быть непустым текстом длиной не более 20 символов.",
                    invalidHostDuplicate: "Варианты ответа для хоста должны быть уникальными.",
                    addFailed: "Не удалось добавить вариант ответа."
                }
                : {
                    typeText: "All players — text answer",
                    typeChoice: "All players — choose answer",
                    typeHostChoice: "Multiple choice — host selects",
                    textHint: "Every player submits a private text answer. The host judges submissions one at a time.",
                    choiceHint: "Selectable choices live in the required Answer options block. The first option is correct. Other answer blocks are shown only after the question closes.",
                    hostChoiceHint: "The Answer options block must contain 4 to 10 unique text options of at most 20 characters. The first option is correct. Other answer blocks are shown only after the question closes.",
                    correct: "Correct",
                    invalidChoiceQuestion: "All-player multiple choice supports only Text and Image blocks in the question itself.",
                    invalidChoice: "The Answer options block must contain 2 to 4 non-empty unique Text or Image options.",
                    invalidHostCount: "The Answer options block must contain between 4 and 10 options.",
                    invalidHostText: "Every host-selected option must be non-empty text of at most 20 characters.",
                    invalidHostDuplicate: "Host-selected answer options must be unique.",
                    addFailed: "Could not add answer option."
                };

    const style = document.createElement("style");
    style.id = "multiple-choice-answer-options-editor-styles";
    style.textContent = `
.content-block-card[data-block-type="AnswerOptions"] {
    border-color: color-mix(in srgb, var(--gold) 58%, var(--line));
    background: color-mix(in srgb, var(--panel-2) 88%, var(--gold) 12%);
}
.multiple-choice-answer-options-children {
    border-color: color-mix(in srgb, var(--gold) 58%, var(--line));
}
.multiple-choice-answer-option-child {
    min-width: 0;
    margin-bottom: 0 !important;
    border-left: 3px solid color-mix(in srgb, var(--gold) 76%, var(--line));
}
.multiple-choice-answer-option-child .content-block-drag-handle {
    display: none !important;
}
.multiple-choice-answer-option-correct-badge {
    margin-right: auto;
    padding: .15rem .45rem;
    border: 1px solid color-mix(in srgb, #2e7d32 72%, var(--line));
    border-radius: 999px;
    color: color-mix(in srgb, #66bb6a 82%, var(--text));
    font-size: .78rem;
    font-weight: 800;
}
.multiple-choice-answer-option-invalid {
    border-color: color-mix(in srgb, #c62828 72%, var(--line)) !important;
}
.multiple-choice-answer-options-help {
    display: block;
    margin: 0 0 12px;
    color: var(--muted);
}
`;
    document.head.appendChild(style);

    const isChoiceType = value => value === "3" || value === "4";
    const isAllPlayerChoice = value => value === "3";
    const isHostChoice = value => value === "4";
    const minimumOptions = value => isHostChoice(value) ? 4 : 2;
    const maximumOptions = value => isHostChoice(value) ? 10 : 4;
    const allowedOptionTypes = value => isHostChoice(value)
        ? new Set(["Text"])
        : new Set(["Text", "Image"]);

    const isCard = element =>
        element instanceof HTMLElement &&
        element.classList.contains("content-block-card");

    const directCards = section => {
        const list = section?.querySelector("[data-content-block-list]");
        return Array.from(list?.children ?? []).filter(isCard);
    };

    const escapeRegExp = value =>
        value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

    const reindexSection = section => {
        if (!(section instanceof HTMLElement)) {
            return;
        }
        const fieldPrefix = section.dataset.blockCollection;
        if (!fieldPrefix) {
            return;
        }

        const idPrefix = fieldPrefix.replaceAll(".", "_");
        const namePattern = new RegExp(
            `${escapeRegExp(fieldPrefix)}\\[\\d+\\]`,
            "g");
        const idPattern = new RegExp(
            `${escapeRegExp(idPrefix)}_\\d+__`,
            "g");
        const cards = section.querySelectorAll(".content-block-card");

        cards.forEach((card, index) => {
            card.querySelectorAll("[name]").forEach(element => {
                element.name = element.name.replace(
                    namePattern,
                    `${fieldPrefix}[${index}]`);
            });
            card.querySelectorAll("[id]").forEach(element => {
                element.id = element.id.replace(
                    idPattern,
                    `${idPrefix}_${index}__`);
            });
            card.querySelectorAll("label[for]").forEach(label => {
                label.htmlFor = label.htmlFor.replace(
                    idPattern,
                    `${idPrefix}_${index}__`);
            });
            const sortOrder = card.querySelector(
                `input[name="${fieldPrefix}[${index}].SortOrder"]`);
            if (sortOrder) {
                sortOrder.value = index;
            }
        });
    };

    const fetchBlockCard = async (section, blockType) => {
        const fieldPrefix = section?.dataset.blockCollection;
        if (!fieldPrefix) {
            return null;
        }
        const index = section.querySelectorAll(".content-block-card").length;
        const url = `${window.location.pathname}?handler=ContentBlock` +
            `&fieldPrefix=${encodeURIComponent(fieldPrefix)}` +
            `&blockType=${encodeURIComponent(blockType)}` +
            `&index=${index}`;
        const response = await fetch(url, { credentials: "same-origin" });
        if (!response.ok) {
            return null;
        }
        const template = document.createElement("template");
        template.innerHTML = (await response.text()).trim();
        const card = template.content.firstElementChild;
        return isCard(card) ? card : null;
    };

    const initialize = () => {
        const select = document.getElementById("Input_PresentationType");
        const modeInput = document.getElementById("Input_AllPlayerMode");
        const questionSection = document.getElementById("question-blocks");
        const answerSection = document.getElementById("answer-blocks");
        if (!(select instanceof HTMLSelectElement) ||
            !(answerSection instanceof HTMLElement)) {
            return;
        }

        let hostOption = select.querySelector('option[value="4"]');
        if (!hostOption) {
            hostOption = new Option(text.typeHostChoice, "4");
            select.add(hostOption);
        }
        hostOption.textContent = text.typeHostChoice;
        select.querySelector('option[value="2"]')?.replaceChildren(text.typeText);
        select.querySelector('option[value="3"]')?.replaceChildren(text.typeChoice);
        if (savedQuestionType === "4") {
            select.value = "4";
        }

        const answerList = answerSection.querySelector("[data-content-block-list]");
        if (!(answerList instanceof HTMLElement)) {
            return;
        }

        const answerHeading = answerSection.previousElementSibling;
        if (answerHeading instanceof HTMLHeadingElement) {
            answerHeading.dataset.standardHeading = answerHeading.textContent ?? "";
        }

        const typePanel = select.closest(".question-type-setting");
        const textHelp = document.createElement("small");
        textHelp.className = "multiple-choice-answer-options-help";
        textHelp.textContent = text.textHint;
        textHelp.hidden = true;
        const choiceHelp = document.createElement("small");
        choiceHelp.className = "multiple-choice-answer-options-help";
        choiceHelp.textContent = text.choiceHint;
        choiceHelp.hidden = true;
        const hostHelp = document.createElement("small");
        hostHelp.className = "multiple-choice-answer-options-help";
        hostHelp.textContent = text.hostChoiceHint;
        hostHelp.hidden = true;
        typePanel?.append(textHelp, choiceHelp, hostHelp);

        const buzzSetting = document.getElementById("buzz-mode-setting");
        const buzzSelect = document.getElementById("Input_BuzzModeOverride");
        const specialCheckbox = document.getElementById("Input_IsSpecial");
        const excludeCheckbox = document.getElementById(
            "Input_ExcludeFromRandomWagerSelection");
        const saveStatus = document.querySelector("[data-question-save-status]");
        let standardBuzzMode = buzzSelect instanceof HTMLSelectElement
            ? buzzSelect.value
            : "0";
        let previousAllPlayer = false;
        let syncScheduled = false;
        let structureChangeInProgress = false;

        const getMarker = () => directCards(answerSection)
            .find(card => card.dataset.blockType === "AnswerOptions") ?? null;

        const getOptionHost = marker => marker?.querySelector(
            "[data-answer-options-children]") ?? null;

        const getOptionCards = () => Array.from(
            getOptionHost(getMarker())?.children ?? [])
            .filter(isCard);

        const getAdditionalCards = () => directCards(answerSection)
            .filter(card => card.dataset.blockType !== "AnswerOptions");

        const prepareOptionCard = card => {
            card.classList.add(
                "multiple-choice-answer-option-child",
                "content-block-container-child");
            card.dataset.multipleChoiceAnswerOption = "true";
            const dragHandle = card.querySelector(".content-block-drag-handle");
            if (dragHandle) {
                dragHandle.draggable = false;
                dragHandle.setAttribute("aria-hidden", "true");
            }
        };

        const syncMarkerCount = marker => {
            const countInput = marker?.querySelector(
                ".multiple-choice-answer-options-count");
            if (countInput) {
                countInput.value = String(getOptionCards().length);
            }
        };

        const initializeMarker = marker => {
            if (!isCard(marker) || marker.dataset.answerOptionsInitialized === "true") {
                return;
            }
            const host = getOptionHost(marker);
            const countInput = marker.querySelector(
                ".multiple-choice-answer-options-count");
            if (!(host instanceof HTMLElement) || !countInput) {
                return;
            }

            const expectedCount = Math.max(
                0,
                Number.parseInt(countInput.value || "0", 10) || 0);
            let moved = 0;
            let candidate = marker.nextElementSibling;
            while (candidate && moved < expectedCount) {
                const next = candidate.nextElementSibling;
                if (!isCard(candidate)) {
                    break;
                }
                prepareOptionCard(candidate);
                host.appendChild(candidate);
                moved += 1;
                candidate = next;
            }
            countInput.value = String(moved);
            marker.dataset.answerOptionsInitialized = "true";
        };

        const moveEligibleExistingCards = (marker, type) => {
            const host = getOptionHost(marker);
            if (!(host instanceof HTMLElement)) {
                return;
            }
            const allowed = allowedOptionTypes(type);
            const max = maximumOptions(type);
            for (const card of directCards(answerSection)) {
                if (card === marker || getOptionCards().length >= max) {
                    continue;
                }
                if (!allowed.has(card.dataset.blockType ?? "")) {
                    break;
                }
                prepareOptionCard(card);
                host.appendChild(card);
            }
        };

        const addOption = async blockType => {
            const marker = getMarker();
            const host = getOptionHost(marker);
            const type = select.value;
            if (!(host instanceof HTMLElement) ||
                !isChoiceType(type) ||
                !allowedOptionTypes(type).has(blockType) ||
                getOptionCards().length >= maximumOptions(type)) {
                return;
            }

            const card = await fetchBlockCard(answerSection, blockType);
            if (!card) {
                throw new Error(text.addFailed);
            }
            prepareOptionCard(card);
            host.appendChild(card);
            syncMarkerCount(marker);
            reindexSection(answerSection);
        };

        const ensureMinimum = async () => {
            const type = select.value;
            if (!isChoiceType(type)) {
                return;
            }
            while (getOptionCards().length < minimumOptions(type)) {
                await addOption("Text");
            }
        };

        const ensureStructure = async () => {
            const type = select.value;
            if (!isChoiceType(type) || structureChangeInProgress) {
                return;
            }
            structureChangeInProgress = true;
            try {
                let marker = getMarker();
                if (!marker) {
                    marker = await fetchBlockCard(answerSection, "AnswerOptions");
                    if (!marker) {
                        throw new Error(text.addFailed);
                    }
                    answerList.prepend(marker);
                    marker.dataset.answerOptionsInitialized = "true";
                    moveEligibleExistingCards(marker, type);
                } else {
                    if (marker.parentElement !== answerList) {
                        answerList.prepend(marker);
                    }
                    initializeMarker(marker);
                    if (answerList.firstElementChild !== marker) {
                        answerList.prepend(marker);
                    }
                }
                await ensureMinimum();
                syncMarkerCount(marker);
                reindexSection(answerSection);
            } finally {
                structureChangeInProgress = false;
            }
        };

        const unwrapStructure = () => {
            const marker = getMarker();
            if (!marker) {
                return;
            }
            const host = getOptionHost(marker);
            for (const card of Array.from(host?.children ?? []).filter(isCard)) {
                card.classList.remove(
                    "multiple-choice-answer-option-child",
                    "content-block-container-child",
                    "multiple-choice-answer-option-invalid");
                delete card.dataset.multipleChoiceAnswerOption;
                const badge = card.querySelector(
                    ".multiple-choice-answer-option-correct-badge");
                badge?.remove();
                const dragHandle = card.querySelector(".content-block-drag-handle");
                if (dragHandle) {
                    dragHandle.draggable = true;
                    dragHandle.removeAttribute("aria-hidden");
                }
                answerList.insertBefore(card, marker);
            }
            marker.remove();
            reindexSection(answerSection);
        };

        const setAllowedTopLevelTypes = (section, allowed) => {
            section?.querySelectorAll(".content-block-type-option")
                .forEach(option => {
                    const blockType = option.dataset.blockType;
                    option.hidden = Boolean(allowed) && !allowed.has(blockType);
                });
        };

        const updateOptionUi = () => {
            const type = select.value;
            const marker = getMarker();
            if (!isChoiceType(type) || !marker) {
                return;
            }
            const options = getOptionCards();
            const min = minimumOptions(type);
            const max = maximumOptions(type);
            const allowed = allowedOptionTypes(type);

            marker.querySelectorAll("[data-answer-option-add-block-type]")
                .forEach(button => {
                    const blockType = button.dataset.answerOptionAddBlockType ?? "";
                    button.hidden = !allowed.has(blockType);
                    if (button instanceof HTMLButtonElement) {
                        button.disabled = options.length >= max;
                    }
                });

            options.forEach((card, index) => {
                const isAllowed = allowed.has(card.dataset.blockType ?? "");
                card.classList.toggle(
                    "multiple-choice-answer-option-invalid",
                    !isAllowed);

                const toolbar = card.querySelector(".content-block-toolbar");
                const existingBadge = card.querySelector(
                    ".multiple-choice-answer-option-correct-badge");
                if (index === 0 && toolbar) {
                    if (existingBadge) {
                        existingBadge.textContent = text.correct;
                    } else {
                        const badge = document.createElement("span");
                        badge.className = "multiple-choice-answer-option-correct-badge";
                        badge.textContent = text.correct;
                        toolbar.prepend(badge);
                    }
                } else {
                    existingBadge?.remove();
                }

                const textarea = card.querySelector('textarea[name$=".TextContent"]');
                if (textarea instanceof HTMLTextAreaElement) {
                    if (isHostChoice(type)) {
                        textarea.maxLength = 20;
                    } else {
                        textarea.removeAttribute("maxlength");
                    }
                }

                const remove = card.querySelector(".content-block-remove-button");
                if (remove instanceof HTMLButtonElement) {
                    remove.disabled = options.length <= min && isAllowed;
                }
            });
        };

        const updateQuestionTypeUi = () => {
            const type = select.value;
            const isText = type === "2";
            const allPlayerChoice = type === "3";
            const hostChoice = type === "4";
            const isAllPlayer = isText || allPlayerChoice;

            if (modeInput instanceof HTMLInputElement) {
                modeInput.value = isText
                    ? "text"
                    : allPlayerChoice
                        ? "multipleChoice"
                        : "";
            }

            textHelp.hidden = !isText;
            choiceHelp.hidden = !allPlayerChoice;
            hostHelp.hidden = !hostChoice;

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
            } else if (previousAllPlayer && type === "0" &&
                buzzSelect instanceof HTMLSelectElement) {
                buzzSelect.value = standardBuzzMode;
            }

            document.querySelectorAll(".wager-question-setting")
                .forEach(element => {
                    element.hidden = type === "1" || hostChoice;
                });
            if (hostChoice && specialCheckbox instanceof HTMLInputElement) {
                specialCheckbox.checked = false;
            }
            if (hostChoice && excludeCheckbox instanceof HTMLInputElement) {
                excludeCheckbox.checked = true;
            }

            if (answerHeading instanceof HTMLHeadingElement) {
                answerHeading.textContent =
                    answerHeading.dataset.standardHeading ?? answerHeading.textContent;
            }

            if (allPlayerChoice) {
                setAllowedTopLevelTypes(questionSection, new Set(["Text", "Image"]));
                setAllowedTopLevelTypes(answerSection, null);
            } else {
                setAllowedTopLevelTypes(questionSection, null);
                setAllowedTopLevelTypes(answerSection, null);
            }

            previousAllPlayer = isAllPlayer;
            updateOptionUi();
        };

        const showEditorError = message => {
            if (saveStatus instanceof HTMLElement) {
                saveStatus.textContent = message;
                saveStatus.hidden = false;
                saveStatus.classList.remove("alert-success");
                saveStatus.classList.add("alert-error");
                return;
            }
            window.alert(message);
        };

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
            const type = select.value;
            if (!isChoiceType(type)) {
                return null;
            }

            const marker = getMarker();
            if (!marker || answerList.firstElementChild !== marker) {
                return isHostChoice(type)
                    ? text.invalidHostCount
                    : text.invalidChoice;
            }

            if (isAllPlayerChoice(type)) {
                const questionCards = directCards(questionSection);
                if (!questionCards.every(card =>
                    ["Text", "Image"].includes(card.dataset.blockType ?? ""))) {
                    return text.invalidChoiceQuestion;
                }
            }

            const options = getOptionCards();
            if (options.length < minimumOptions(type) ||
                options.length > maximumOptions(type)) {
                return isHostChoice(type)
                    ? text.invalidHostCount
                    : text.invalidChoice;
            }

            const textValues = [];
            for (const card of options) {
                if (card.dataset.blockType === "Text") {
                    const value = card.querySelector('[name$=".TextContent"]')
                        ?.value?.trim() ?? "";
                    if (!value || (isHostChoice(type) && value.length > 20)) {
                        return isHostChoice(type)
                            ? text.invalidHostText
                            : text.invalidChoice;
                    }
                    textValues.push(value.toLocaleLowerCase(culture));
                    continue;
                }

                if (isHostChoice(type) ||
                    card.dataset.blockType !== "Image" ||
                    !imageCardHasFile(card)) {
                    return isHostChoice(type)
                        ? text.invalidHostText
                        : text.invalidChoice;
                }
            }

            if (new Set(textValues).size !== textValues.length) {
                return isHostChoice(type)
                    ? text.invalidHostDuplicate
                    : text.invalidChoice;
            }
            return null;
        };

        const cardProducesPreview = card => {
            switch (card?.dataset.blockType) {
                case "Text":
                    return Boolean(card.querySelector('[name$=".TextContent"]')
                        ?.value?.trim());
                case "Image": {
                    const preview = card.querySelector(".unified-file-preview");
                    const image = card.querySelector(
                        ".unified-image-preview-element");
                    return Boolean(preview && !preview.hidden && image?.getAttribute("src"));
                }
                case "Audio": {
                    const preview = card.querySelector(".unified-file-preview");
                    const audio = card.querySelector(
                        ".unified-audio-preview-element");
                    return Boolean(preview && !preview.hidden && audio?.getAttribute("src"));
                }
                case "Video":
                case "YouTube":
                    return Boolean(card.querySelector('[name$=".ExternalUrl"]')
                        ?.value?.trim());
                default:
                    return false;
            }
        };

        const rebuildMultipleChoiceAnswerPreview = () => {
            const type = select.value;
            if (!isChoiceType(type)) {
                return;
            }
            const previewRoot = document.getElementById("question-preview-content");
            const marker = getMarker();
            const correctCard = getOptionCards()[0];
            if (!previewRoot || !marker || !correctCard) {
                return;
            }

            const leaves = Array.from(previewRoot.querySelectorAll(
                ".question-preview-text, .question-preview-media"));
            const previewByCard = new Map();
            let leafIndex = 0;
            answerSection.querySelectorAll(".content-block-card").forEach(card => {
                if (card.dataset.blockType === "Container" ||
                    card.dataset.blockType === "AnswerOptions" ||
                    !cardProducesPreview(card)) {
                    return;
                }
                const leaf = leaves[leafIndex++];
                if (leaf) {
                    previewByCard.set(card, leaf);
                }
            });

            leaves.forEach(leaf => leaf.classList.remove(
                "all-player-answer-option",
                "all-player-answer-option-correct",
                "all-player-answer-option-incorrect"));

            const fragment = document.createDocumentFragment();
            const correctPreview = previewByCard.get(correctCard);
            if (correctPreview) {
                if (isAllPlayerChoice(type)) {
                    correctPreview.classList.add(
                        "all-player-answer-option",
                        "all-player-answer-option-correct");
                }
                fragment.appendChild(correctPreview);
            }

            for (const card of getAdditionalCards()) {
                if (card.dataset.blockType === "Container") {
                    const childCards = Array.from(card.querySelector(
                        "[data-content-block-container-children]")?.children ?? [])
                        .filter(isCard);
                    const childPreviews = childCards
                        .map(child => previewByCard.get(child))
                        .filter(Boolean);
                    if (childPreviews.length > 0) {
                        const layout = document.createElement("div");
                        layout.className = "content-block-container-layout";
                        layout.style.setProperty(
                            "--content-block-container-columns",
                            String(childPreviews.length));
                        childPreviews.forEach(preview => layout.appendChild(preview));
                        fragment.appendChild(layout);
                    }
                    continue;
                }

                const preview = previewByCard.get(card);
                if (preview) {
                    fragment.appendChild(preview);
                }
            }

            previewRoot.classList.remove("all-player-answer-grid");
            previewRoot.replaceChildren(fragment);
        };

        const scheduleSync = () => {
            if (syncScheduled) {
                return;
            }
            syncScheduled = true;
            window.queueMicrotask(async () => {
                syncScheduled = false;
                if (isChoiceType(select.value)) {
                    try {
                        await ensureStructure();
                    } catch (error) {
                        console.error(error);
                    }
                }
                updateQuestionTypeUi();
                const marker = getMarker();
                if (marker) {
                    syncMarkerCount(marker);
                }
                reindexSection(answerSection);
            });
        };

        document.addEventListener("click", event => {
            const button = event.target.closest("[data-answer-option-add-block-type]");
            if (!button) {
                return;
            }
            event.preventDefault();
            event.stopImmediatePropagation();
            void addOption(button.dataset.answerOptionAddBlockType ?? "")
                .then(scheduleSync)
                .catch(error => showEditorError(error.message || text.addFailed));
        }, true);

        document.addEventListener("click", event => {
            const button = event.target.closest("[data-open-question-preview]");
            if (!button || button.dataset.openQuestionPreview !== "answer") {
                return;
            }
            window.setTimeout(rebuildMultipleChoiceAnswerPreview, 30);
        }, true);

        select.addEventListener("change", () => {
            if (isChoiceType(select.value)) {
                void ensureStructure().then(scheduleSync).catch(error =>
                    showEditorError(error.message || text.addFailed));
            } else {
                unwrapStructure();
                scheduleSync();
            }
        });

        form.addEventListener("submit", event => {
            const marker = getMarker();
            if (marker) {
                syncMarkerCount(marker);
                if (answerList.firstElementChild !== marker) {
                    answerList.prepend(marker);
                }
            }
            reindexSection(answerSection);
            const error = validate();
            if (!error) {
                return;
            }
            event.preventDefault();
            event.stopImmediatePropagation();
            showEditorError(error);
        }, true);

        new MutationObserver(scheduleSync).observe(answerList, {
            childList: true,
            subtree: true
        });
        answerList.addEventListener("input", scheduleSync);

        window.BadWolfMultipleChoiceAnswerOptions = {
            isChoiceType: () => isChoiceType(select.value),
            getMarker,
            getOptionCards,
            getAdditionalCards,
            ensureMinimum
        };

        if (isChoiceType(select.value)) {
            void ensureStructure().then(scheduleSync).catch(error =>
                showEditorError(error.message || text.addFailed));
        } else {
            unwrapStructure();
            scheduleSync();
        }
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
