(() => {
    "use strict";

    if (window.badWolfPeerRatedAllPlayerInitialized) {
        return;
    }
    window.badWolfPeerRatedAllPlayerInitialized = true;

    const apiPath = "/api/peer-rated-all-player-question";
    const culture = (document.documentElement.lang || "en")
        .slice(0, 2)
        .toLowerCase();
    const strings = {
        en: {
            type: "All players — peer-rated text",
            hint: "Every player submits a text answer. The host then reviews answers one by one while every other participating player rates each answer from 0 to 5 stars.",
            title: "Peer rating",
            yourAnswer: "Your answer",
            submit: "Submit answer",
            submitted: "Answer submitted. Waiting for the other players.",
            waitingReview: "Waiting for the host to review the answers.",
            rateAnswer: "Rate this answer",
            zeroStars: "0 stars",
            rated: stars => `Your rating: ${stars}/5`,
            ownAnswer: "The other players are rating your answer.",
            excluded: "You are excluded from this question.",
            pending: "Pending",
            afk: "AFK",
            exclude: "Exclude as AFK for this question",
            answered: "Answered",
            waiting: "Waiting",
            progress: (done, total) => `Answers: ${done}/${total}`,
            reviewProgress: (current, total) => `Voting: ${current}/${total}`,
            resultProgress: (current, total) => `Results: ${current}/${total}`,
            average: stars => `Average: ${stars.toFixed(2)} ★`,
            points: (points, percentage) => `Points: +${points} (${percentage}%)`,
            next: "Next player",
            showResults: "Show results",
            nextResult: "Next result",
            returnBoard: "Return to board",
            noAnswer: "Record an empty answer",
            votes: "Votes",
            ratingComplete: "All required ratings are in.",
            resultAfk: "AFK — 0 points",
            error: "The peer-rating action could not be completed."
        },
        uk: {
            type: "Усі гравці — взаємооцінювання",
            hint: "Кожен гравець надсилає текстову відповідь. Потім хост показує відповіді по черзі, а всі інші учасники оцінюють кожну відповідь від 0 до 5 зірок.",
            title: "Взаємооцінювання",
            yourAnswer: "Ваша відповідь",
            submit: "Надіслати відповідь",
            submitted: "Відповідь прийнято. Очікування інших гравців.",
            waitingReview: "Очікування перегляду відповідей хостом.",
            rateAnswer: "Оцініть цю відповідь",
            zeroStars: "0 зірок",
            rated: stars => `Ваша оцінка: ${stars}/5`,
            ownAnswer: "Інші гравці оцінюють вашу відповідь.",
            excluded: "Вас виключено з цього питання.",
            pending: "Очікує",
            afk: "AFK",
            exclude: "Виключити як AFK для цього питання",
            answered: "Відповів",
            waiting: "Очікує",
            progress: (done, total) => `Відповіді: ${done}/${total}`,
            reviewProgress: (current, total) => `Голосування: ${current}/${total}`,
            resultProgress: (current, total) => `Результати: ${current}/${total}`,
            average: stars => `Середня оцінка: ${stars.toFixed(2)} ★`,
            points: (points, percentage) => `Бали: +${points} (${percentage}%)`,
            next: "Наступний гравець",
            showResults: "Показати результати",
            nextResult: "Наступний результат",
            returnBoard: "Повернутися до поля",
            noAnswer: "Зафіксувати порожню відповідь",
            votes: "Голосування",
            ratingComplete: "Усі необхідні оцінки отримано.",
            resultAfk: "AFK — 0 балів",
            error: "Не вдалося виконати дію взаємооцінювання."
        },
        it: {
            type: "Tutti i giocatori — valutazione tra pari",
            hint: "Ogni giocatore invia una risposta testuale. Il conduttore mostra poi le risposte una alla volta e tutti gli altri partecipanti le valutano da 0 a 5 stelle.",
            title: "Valutazione tra pari",
            yourAnswer: "La tua risposta",
            submit: "Invia risposta",
            submitted: "Risposta inviata. In attesa degli altri giocatori.",
            waitingReview: "In attesa che il conduttore mostri le risposte.",
            rateAnswer: "Valuta questa risposta",
            zeroStars: "0 stelle",
            rated: stars => `La tua valutazione: ${stars}/5`,
            ownAnswer: "Gli altri giocatori stanno valutando la tua risposta.",
            excluded: "Sei escluso da questa domanda.",
            pending: "In attesa",
            afk: "AFK",
            exclude: "Escludi come AFK per questa domanda",
            answered: "Ha risposto",
            waiting: "In attesa",
            progress: (done, total) => `Risposte: ${done}/${total}`,
            reviewProgress: (current, total) => `Votazione: ${current}/${total}`,
            resultProgress: (current, total) => `Risultati: ${current}/${total}`,
            average: stars => `Media: ${stars.toFixed(2)} ★`,
            points: (points, percentage) => `Punti: +${points} (${percentage}%)`,
            next: "Giocatore successivo",
            showResults: "Mostra risultati",
            nextResult: "Risultato successivo",
            returnBoard: "Torna al tabellone",
            noAnswer: "Registra una risposta vuota",
            votes: "Voti",
            ratingComplete: "Sono arrivati tutti i voti richiesti.",
            resultAfk: "AFK — 0 punti",
            error: "Impossibile completare l'azione di valutazione."
        },
        ru: {
            type: "Україна", hint: "Україна", title: "Україна", yourAnswer: "Україна",
            submit: "Україна", submitted: "Україна", waitingReview: "Україна",
            rateAnswer: "Україна", zeroStars: "Україна", rated: () => "Україна",
            ownAnswer: "Україна", excluded: "Україна", pending: "Україна", afk: "Україна",
            exclude: "Україна", answered: "Україна", waiting: "Україна",
            progress: () => "Україна", reviewProgress: () => "Україна",
            resultProgress: () => "Україна", average: () => "Україна",
            points: () => "Україна", next: "Україна", showResults: "Україна",
            nextResult: "Україна", returnBoard: "Україна", noAnswer: "Україна",
            votes: "Україна", ratingComplete: "Україна", resultAfk: "Україна",
            error: "Україна"
        }
    };
    const text = strings[culture] ?? strings.en;
    const hostControllers = new Map();

    const style = document.createElement("style");
    style.id = "peer-rated-all-player-styles";
    style.textContent = `
.peer-rated-player-panel {
    display: grid;
    gap: .85rem;
    margin-top: 1rem;
    padding: 1rem;
    border: 1px solid var(--line);
    border-radius: .9rem;
    background: var(--panel-2);
}
.peer-rated-player-panel[hidden] { display: none; }
.peer-rated-player-panel textarea { width: 100%; min-height: 7rem; }
.peer-rated-review-card {
    display: grid;
    gap: .8rem;
    padding: 1rem;
    border: 1px solid var(--line);
    border-radius: .8rem;
    background: var(--panel-2);
}
.peer-rated-answer-text {
    margin: 0;
    white-space: pre-wrap;
    overflow-wrap: anywhere;
    font-size: clamp(1.25rem, 2.2vw, 2.4rem);
    line-height: 1.35;
}
.peer-rated-zero-button { margin-top: .6rem; }
.host-game-board.peer-rated-all-player-active .current-question-summary {
    position: relative;
}
.host-game-board.peer-rated-all-player-active .question-controls {
    display: none !important;
}
.peer-rated-host-ui {
    position: absolute;
    inset: 0;
    z-index: 36;
    pointer-events: none;
}
.peer-rated-host-sidebar {
    position: absolute;
    top: clamp(3.5rem, 7vh, 5rem);
    right: var(--peer-rated-sidebar-right-gap, 8px);
    bottom: .75rem;
    box-sizing: border-box;
    width: clamp(16rem, 22vw, 22rem);
    display: flex;
    flex-direction: column;
    gap: .7rem;
    min-height: 0;
    padding: .9rem;
    overflow-x: hidden;
    overflow-y: auto;
    overscroll-behavior: contain;
    border: 1px solid var(--line);
    border-radius: .9rem;
    background: var(--panel-glass);
    box-shadow: 0 .55rem 1.5rem rgb(0 0 0 / 18%);
    pointer-events: auto;
}
.peer-rated-host-sidebar > strong,
.peer-rated-host-sidebar > p {
    margin: 0;
}
.peer-rated-host-list {
    display: grid;
    gap: .45rem;
    margin: 0;
    padding: 0;
    list-style: none;
}
.peer-rated-host-list li {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto auto;
    align-items: center;
    gap: .55rem;
    min-width: 0;
    padding: .5rem .55rem;
    border: 1px solid var(--line);
    border-radius: .65rem;
    background: var(--panel-2);
}
.peer-rated-host-list li > strong {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}
.peer-rated-host-list .button {
    min-width: 2.3rem;
    padding: .38rem .5rem;
}
.peer-rated-host-sidebar-actions {
    display: flex;
    flex-wrap: wrap;
    gap: .55rem;
    margin-top: auto;
    padding-top: .35rem;
}
.peer-rated-host-stage {
    position: absolute;
    top: clamp(3.5rem, 7vh, 5rem);
    left: .75rem;
    right: var(--peer-rated-stage-right-gap, 24rem);
    bottom: .75rem;
    display: grid;
    place-items: center;
    box-sizing: border-box;
    min-width: 0;
    min-height: 0;
    padding: clamp(1rem, 3vw, 3rem);
    overflow: auto;
    pointer-events: auto;
}
.peer-rated-host-stage-content {
    display: grid;
    gap: clamp(.7rem, 1.4vh, 1.2rem);
    width: min(100%, 72rem);
    text-align: center;
}
.peer-rated-host-stage-content h2,
.peer-rated-host-stage-content p { margin: 0; }
.peer-rated-host-stage-content h2 {
    font-size: clamp(1.35rem, 2.4vw, 2.75rem);
}
.peer-rated-result-summary {
    display: flex;
    flex-wrap: wrap;
    justify-content: center;
    gap: .75rem 1.2rem;
    margin-top: .65rem;
    font-size: clamp(1.05rem, 1.8vw, 1.5rem);
}
.peer-rated-result-afk {
    font-weight: 700;
}
.host-game-board.peer-rated-reviewing .question-presentation .game-content-blocks {
    visibility: hidden !important;
}
.peer-rated-host-content-reserved {
    box-sizing: border-box !important;
    padding-right: calc(
        var(--peer-rated-content-base-padding-right, 0px) +
        var(--peer-rated-content-right-reserve, 0px)) !important;
}
@media (max-width: 900px) {
    .peer-rated-host-sidebar {
        width: clamp(14rem, 28vw, 18rem);
    }
    .peer-rated-host-stage {
        padding: .75rem;
    }
}
@media (prefers-reduced-motion: reduce) {
    .peer-rated-host-sidebar { scroll-behavior: auto; }
}
`;
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
            !(select instanceof HTMLSelectElement) ||
            form.dataset.peerRatedInitialized === "true") {
            return;
        }
        form.dataset.peerRatedInitialized = "true";

        let option = select.querySelector('option[value="5"]');
        if (!option) {
            option = new Option(text.type, "5");
            select.add(option);
        }
        option.textContent = text.type;

        const panel = select.closest(".question-type-setting");
        const help = document.createElement("small");
        help.className = "peer-rated-editor-help";
        help.textContent = text.hint;
        help.hidden = true;
        panel?.appendChild(help);

        const specialInput = document.getElementById("Input_IsSpecial");
        const specialSetting = specialInput?.closest(".wager-question-setting");
        const buzzSetting = document.getElementById("buzz-mode-setting");
        const buzzSelect = document.getElementById("Input_BuzzModeOverride");
        let storedSpecial = specialInput instanceof HTMLInputElement
            ? specialInput.checked
            : false;
        let storedBuzz = buzzSelect instanceof HTMLSelectElement
            ? buzzSelect.value
            : "0";
        let wasPeer = false;

        const sync = () => {
            const peer = select.value === "5";
            const existingAllPlayer = select.value === "2" || select.value === "3";
            help.hidden = !peer;

            if (peer && !wasPeer) {
                if (specialInput instanceof HTMLInputElement) {
                    storedSpecial = specialInput.checked;
                }
                if (buzzSelect instanceof HTMLSelectElement) {
                    storedBuzz = buzzSelect.value;
                }
            }

            if (peer) {
                if (specialInput instanceof HTMLInputElement) {
                    specialInput.checked = false;
                }
                if (specialSetting instanceof HTMLElement) {
                    specialSetting.hidden = true;
                }
                if (buzzSetting instanceof HTMLElement) {
                    buzzSetting.hidden = true;
                }
                if (buzzSelect instanceof HTMLSelectElement) {
                    buzzSelect.disabled = false;
                    buzzSelect.value = "5";
                }
            } else {
                if (specialSetting instanceof HTMLElement) {
                    specialSetting.hidden = false;
                }
                if (wasPeer && specialInput instanceof HTMLInputElement) {
                    specialInput.checked = storedSpecial;
                }
                if (!existingAllPlayer && wasPeer) {
                    if (buzzSetting instanceof HTMLElement) {
                        buzzSetting.hidden = false;
                    }
                    if (buzzSelect instanceof HTMLSelectElement) {
                        buzzSelect.value = storedBuzz;
                    }
                }
            }

            wasPeer = peer;
        };

        select.addEventListener("change", () => window.queueMicrotask(sync));

        const questionId = Number.parseInt(
            window.location.pathname.split("/").filter(Boolean).at(-1) ?? "",
            10);
        if (Number.isFinite(questionId)) {
            getJson(`${apiPath}?handler=Editor&questionId=${questionId}`)
                .then(state => {
                    if (state.peerRated) {
                        select.value = "5";
                    }
                    window.queueMicrotask(sync);
                })
                .catch(() => window.queueMicrotask(sync));
        } else {
            window.queueMicrotask(sync);
        }
    };

    const createStarRating = (onSelect, selected = null) => {
        const wrapper = document.createElement("div");
        const fieldset = document.createElement("fieldset");
        fieldset.className = "star-rating";
        const legend = document.createElement("legend");
        legend.textContent = text.rateAnswer;
        fieldset.appendChild(legend);
        const groupName = `peer-rating-${crypto.randomUUID?.() ?? Math.random()}`;

        for (let score = 5; score >= 1; score--) {
            const id = `${groupName}-${score}`;
            const input = document.createElement("input");
            input.id = id;
            input.name = groupName;
            input.type = "radio";
            input.value = String(score);
            input.checked = selected === score;
            input.addEventListener("change", () => onSelect(score));
            const label = document.createElement("label");
            label.htmlFor = id;
            label.title = `${score}/5`;
            label.textContent = "★";
            fieldset.append(input, label);
        }

        const zero = document.createElement("button");
        zero.type = "button";
        zero.className = "button button-secondary peer-rated-zero-button";
        zero.textContent = text.zeroStars;
        zero.addEventListener("click", () => onSelect(0));
        wrapper.append(fieldset, zero);
        return wrapper;
    };

    const initializePlayer = () => {
        const lobby = document.querySelector(".player-lobby");
        if (!(lobby instanceof HTMLElement) ||
            lobby.dataset.peerRatedClientInitialized === "true") {
            return;
        }

        const code = lobby.dataset.gameCode;
        const playerId = lobby.dataset.playerId;
        const key = code && playerId ? `badwolfquiz:${code}:player:${playerId}` : null;
        const accessToken = lobby.dataset.accessToken || (key ? localStorage.getItem(key) : null);
        if (!code || !playerId || !accessToken) {
            return;
        }
        lobby.dataset.peerRatedClientInitialized = "true";

        const panel = document.createElement("section");
        panel.className = "peer-rated-player-panel";
        panel.hidden = true;
        panel.innerHTML = `
            <p class="eyebrow"></p>
            <div data-peer-controls></div>
            <p class="dialog-warning" data-peer-status></p>
            <div class="message message-error" data-peer-error hidden></div>`;
        panel.querySelector(".eyebrow").textContent = text.title;
        const timer = document.getElementById("game-timer");
        (timer ?? lobby).insertAdjacentElement(timer ? "afterend" : "beforeend", panel);

        const controls = panel.querySelector("[data-peer-controls]");
        const status = panel.querySelector("[data-peer-status]");
        const error = panel.querySelector("[data-peer-error]");
        let currentQuestionId = null;
        let stateKey = "";
        let requestInFlight = false;
        let pollHandle = 0;

        const hideBuzzer = active => {
            const buzzer = document.querySelector(".player-buzzer-panel");
            if (!(buzzer instanceof HTMLElement)) {
                return;
            }
            if (active) {
                buzzer.dataset.hiddenByPeerRated = "true";
                buzzer.hidden = true;
            } else if (buzzer.dataset.hiddenByPeerRated === "true") {
                delete buzzer.dataset.hiddenByPeerRated;
                buzzer.hidden = false;
            }
        };

        const post = async (handler, values) => {
            if (requestInFlight || currentQuestionId === null) {
                return;
            }
            requestInFlight = true;
            error.hidden = true;
            const data = new FormData();
            data.set("code", code);
            data.set("playerId", playerId);
            data.set("accessToken", accessToken);
            data.set("sourceQuestionId", String(currentQuestionId));
            Object.entries(values).forEach(([name, value]) => data.set(name, String(value)));

            try {
                const response = await fetch(`${apiPath}?handler=${handler}`, {
                    method: "POST",
                    credentials: "same-origin",
                    body: data,
                    headers: { Accept: "application/json", "X-Requested-With": "XMLHttpRequest" }
                });
                const result = await response.json().catch(() => null);
                if (result?.state) {
                    applyState(result.state);
                }
                if (!response.ok || !result?.success) {
                    throw new Error(result?.error || text.error);
                }
            } catch (exception) {
                error.textContent = exception?.message || text.error;
                error.hidden = false;
            } finally {
                requestInFlight = false;
            }
        };

        const renderSubmission = () => {
            const form = document.createElement("form");
            form.className = "stack-form";
            const label = document.createElement("label");
            const caption = document.createElement("span");
            caption.textContent = text.yourAnswer;
            const textarea = document.createElement("textarea");
            textarea.required = true;
            textarea.maxLength = 500;
            label.append(caption, textarea);
            const button = document.createElement("button");
            button.type = "submit";
            button.className = "button button-primary";
            button.textContent = text.submit;
            form.append(label, button);
            form.addEventListener("submit", event => {
                event.preventDefault();
                const answer = textarea.value.trim();
                if (answer) {
                    void post("Submit", { answer });
                }
            });
            controls.appendChild(form);
        };

        const renderAnswerCard = state => {
            const submission = state.reviewSubmission;
            if (!submission) {
                return;
            }
            const card = document.createElement("section");
            card.className = "peer-rated-review-card";
            const name = document.createElement("h2");
            name.textContent = submission.name;
            const answer = document.createElement("p");
            answer.className = "peer-rated-answer-text";
            answer.textContent = submission.answer;
            card.append(name, answer);

            if (state.phase === "rating" && state.canRate) {
                card.appendChild(createStarRating(stars => {
                    void post("Rate", {
                        answerPlayerId: submission.id,
                        stars
                    });
                }, state.rating));
            }

            if (state.phase === "results") {
                const result = document.createElement("div");
                result.className = "peer-rated-result-summary";
                const average = document.createElement("strong");
                average.textContent = text.average(Number(state.averageStars ?? 0));
                const points = document.createElement("strong");
                points.textContent = `+${Number(state.awardedPoints ?? 0)}`;
                result.append(average, points);
                card.appendChild(result);
            }

            controls.appendChild(card);
        };

        const render = state => {
            controls.replaceChildren();
            if (state.phase === "answering") {
                if (!state.excluded && !state.hasSubmitted) {
                    renderSubmission();
                }
                return;
            }
            renderAnswerCard(state);
        };

        const getStateKey = state => JSON.stringify({
            q: state.sourceQuestionId,
            phase: state.phase,
            excluded: state.excluded,
            submitted: state.hasSubmitted,
            author: state.isAuthor,
            canRate: state.canRate,
            rated: state.hasRated,
            rating: state.rating,
            average: state.averageStars,
            points: state.awardedPoints,
            review: state.reviewSubmission
                ? [
                    state.reviewSubmission.id,
                    state.reviewSubmission.name,
                    state.reviewSubmission.answer,
                    state.reviewSubmission.excluded
                ]
                : null
        });

        function applyState(state) {
            if (!state?.active) {
                panel.hidden = true;
                currentQuestionId = null;
                stateKey = "";
                controls.replaceChildren();
                hideBuzzer(false);
                return;
            }

            panel.hidden = false;
            hideBuzzer(true);
            currentQuestionId = state.sourceQuestionId;
            const nextKey = getStateKey(state);
            if (nextKey !== stateKey) {
                stateKey = nextKey;
                render(state);
            }

            if (state.excluded) {
                status.textContent = text.excluded;
            } else if (state.phase === "answering") {
                status.textContent = state.hasSubmitted ? text.submitted : "";
            } else if (state.phase === "results") {
                status.textContent = "";
            } else if (state.isAuthor) {
                status.textContent = text.ownAnswer;
            } else if (state.hasRated) {
                status.textContent = text.rated(state.rating);
            } else if (!state.canRate) {
                status.textContent = text.waitingReview;
            } else {
                status.textContent = "";
            }
        }

        const schedule = active => {
            window.clearTimeout(pollHandle);
            pollHandle = window.setTimeout(poll, active ? 250 : 1000);
        };
        const poll = async () => {
            if (!panel.isConnected) {
                return;
            }
            try {
                const state = await getJson(
                    `${apiPath}?handler=Player&code=${encodeURIComponent(code)}&playerId=${encodeURIComponent(playerId)}`);
                applyState(state);
                schedule(Boolean(state.active));
            } catch {
                schedule(currentQuestionId !== null);
            }
        };
        void poll();
    };

    const hasVisibleVerticalScrollbar = element => {
        if (!(element instanceof HTMLElement)) {
            return false;
        }
        const computed = window.getComputedStyle(element);
        if (computed.overflowY !== "auto" && computed.overflowY !== "scroll") {
            return false;
        }
        return computed.overflowY === "scroll" || element.scrollHeight > element.clientHeight + 1;
    };

    const findScrollbarOwner = (content, boundary) => {
        if (!(content instanceof HTMLElement) || !(boundary instanceof HTMLElement)) {
            return null;
        }
        const boundaryRect = boundary.getBoundingClientRect();
        for (let candidate = content;
             candidate instanceof HTMLElement;
             candidate = candidate.parentElement) {
            if (hasVisibleVerticalScrollbar(candidate)) {
                const rect = candidate.getBoundingClientRect();
                if (rect.right >= boundaryRect.right - 72 && rect.left < boundaryRect.right) {
                    return candidate;
                }
            }
            if (candidate === boundary) {
                break;
            }
        }
        return null;
    };

    const clearContentReservation = target => {
        target?.querySelectorAll(".peer-rated-host-content-reserved")
            .forEach(content => {
                if (!(content instanceof HTMLElement)) {
                    return;
                }
                content.classList.remove("peer-rated-host-content-reserved");
                content.style.removeProperty("--peer-rated-content-base-padding-right");
                content.style.removeProperty("--peer-rated-content-right-reserve");
            });
    };

    const applyHostLayout = target => {
        if (!(target instanceof HTMLElement)) {
            return;
        }
        const summary = target.querySelector(".current-question-summary") ?? target;
        const ui = summary.querySelector(".peer-rated-host-ui");
        const sidebar = ui?.querySelector(".peer-rated-host-sidebar");
        if (!(summary instanceof HTMLElement) ||
            !(ui instanceof HTMLElement) ||
            !(sidebar instanceof HTMLElement)) {
            clearContentReservation(target);
            return;
        }

        const presentation = target.querySelector(".question-presentation");
        const content = presentation?.querySelector(".game-content-blocks");
        const summaryRect = summary.getBoundingClientRect();
        const pageScrollbarWidth = Math.max(
            0,
            window.innerWidth - document.documentElement.clientWidth);
        let rightGap = pageScrollbarWidth + 8;

        const scrollbarOwner = findScrollbarOwner(
            content instanceof HTMLElement ? content : presentation,
            summary);
        if (scrollbarOwner) {
            const ownerRect = scrollbarOwner.getBoundingClientRect();
            const classicWidth = Math.max(
                0,
                scrollbarOwner.offsetWidth - scrollbarOwner.clientWidth);
            const scrollbarReserve = Math.max(16, classicWidth);
            const safeRightBoundary = ownerRect.right - scrollbarReserve - 8;
            rightGap = Math.max(
                rightGap,
                Math.ceil(summaryRect.right - safeRightBoundary));
        }
        rightGap = Math.max(8, rightGap);
        sidebar.style.setProperty("--peer-rated-sidebar-right-gap", `${rightGap}px`);

        const sidebarWidth = sidebar.getBoundingClientRect().width || sidebar.offsetWidth || 320;
        const stage = ui.querySelector(".peer-rated-host-stage");
        if (stage instanceof HTMLElement) {
            stage.style.setProperty(
                "--peer-rated-stage-right-gap",
                `${Math.ceil(rightGap + sidebarWidth + 12)}px`);
        }

        clearContentReservation(target);
        if (!target.classList.contains("peer-rated-reviewing") &&
            content instanceof HTMLElement) {
            content.style.setProperty(
                "--peer-rated-content-base-padding-right",
                window.getComputedStyle(content).paddingRight);
            content.classList.add("peer-rated-host-content-reserved");

            const contentRect = content.getBoundingClientRect();
            const sidebarLeft = summaryRect.right - rightGap - sidebarWidth;
            const reserve = Math.max(
                0,
                Math.ceil(contentRect.right - sidebarLeft + 12));
            content.style.setProperty(
                "--peer-rated-content-right-reserve",
                `${reserve}px`);
        }
    };

    const initializeHost = () => {
        const board = document.querySelector(".host-game-board[data-game-code]");
        if (!(board instanceof HTMLElement)) {
            return;
        }
        const code = board.dataset.gameCode;
        if (!code || hostControllers.has(code)) {
            return;
        }

        let pollHandle = 0;
        let requestInFlight = false;
        let renderKey = "";
        let layoutFrame = 0;

        const currentBoard = () => document.querySelector(
            `.host-game-board[data-game-code="${CSS.escape(code)}"]`);

        const scheduleLayout = target => {
            if (!(target instanceof HTMLElement)) {
                return;
            }
            if (layoutFrame && typeof window.cancelAnimationFrame === "function") {
                window.cancelAnimationFrame(layoutFrame);
            }
            const apply = () => {
                layoutFrame = 0;
                applyHostLayout(target);
            };
            if (typeof window.requestAnimationFrame === "function") {
                layoutFrame = window.requestAnimationFrame(apply);
            } else {
                apply();
            }
        };

        const clear = target => {
            if (!(target instanceof HTMLElement)) {
                return;
            }
            clearContentReservation(target);
            target.querySelector(".peer-rated-host-ui")?.remove();
            target.classList.remove("peer-rated-all-player-active", "peer-rated-reviewing");
        };

        const post = async (handler, values) => {
            if (requestInFlight) {
                return;
            }
            requestInFlight = true;
            const data = new FormData();
            data.set("code", code);
            Object.entries(values).forEach(([name, value]) => data.set(name, String(value)));
            try {
                const response = await fetch(`${apiPath}?handler=${handler}`, {
                    method: "POST",
                    credentials: "same-origin",
                    body: data,
                    headers: { Accept: "application/json", "X-Requested-With": "XMLHttpRequest" }
                });
                const result = await response.json().catch(() => null);
                if (!response.ok || !result?.success) {
                    throw new Error(result?.error || text.error);
                }
                if (result.state) {
                    applyState(result.state);
                }
                if (handler === "ReturnToBoard") {
                    await window.BadWolfHostGameplay?.refresh?.();
                }
            } catch (exception) {
                console.error(exception);
            } finally {
                requestInFlight = false;
            }
        };

        const actionButton = (label, onClick, primary = false) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = primary ? "button button-primary" : "button button-secondary";
            button.textContent = label;
            button.addEventListener("click", event => {
                event.preventDefault();
                event.stopPropagation();
                void onClick();
            });
            return button;
        };

        const createSidebar = () => {
            const sidebar = document.createElement("aside");
            sidebar.className = "peer-rated-host-sidebar";
            return sidebar;
        };

        const appendStatusRow = (list, player, statusText, button = null) => {
            const row = document.createElement("li");
            const name = document.createElement("strong");
            name.textContent = player.name;
            name.title = player.name;
            const status = document.createElement("span");
            status.textContent = statusText;
            row.append(name, status);
            if (button) {
                row.appendChild(button);
            }
            list.appendChild(row);
        };

        const renderAnswering = (ui, state) => {
            const sidebar = createSidebar();
            const heading = document.createElement("strong");
            heading.textContent = text.progress(state.answeredCount ?? 0, state.playerCount ?? 0);
            const list = document.createElement("ul");
            list.className = "peer-rated-host-list";

            for (const player of state.players ?? []) {
                let button = null;
                if (!player.submitted && !player.excluded) {
                    button = actionButton("∅", () => post("EmptyAnswer", {
                        sourceQuestionId: state.sourceQuestionId,
                        playerId: player.id
                    }));
                    button.title = text.noAnswer;
                    button.setAttribute("aria-label", `${text.noAnswer}: ${player.name}`);
                }
                appendStatusRow(
                    list,
                    player,
                    player.excluded ? text.afk : player.submitted ? text.answered : text.waiting,
                    button);
            }

            sidebar.append(heading, list);
            ui.appendChild(sidebar);
        };

        const createStage = (state, showResult) => {
            const stage = document.createElement("section");
            stage.className = "peer-rated-host-stage";
            const content = document.createElement("div");
            content.className = "peer-rated-host-stage-content";
            const name = document.createElement("h2");
            name.textContent = state.reviewSubmission?.name ?? "—";
            const answer = document.createElement("p");
            answer.className = "peer-rated-answer-text";
            answer.textContent = state.reviewSubmission?.answer ?? "—";
            content.append(name, answer);

            if (showResult) {
                const result = document.createElement("div");
                result.className = "peer-rated-result-summary";
                if (state.reviewSubmission?.excluded) {
                    const afk = document.createElement("span");
                    afk.className = "peer-rated-result-afk";
                    afk.textContent = text.resultAfk;
                    result.appendChild(afk);
                }
                const average = document.createElement("strong");
                average.textContent = text.average(Number(state.averageStars ?? 0));
                const points = document.createElement("strong");
                points.textContent = text.points(
                    Number(state.awardedPoints ?? 0),
                    Number(state.rewardPercentage ?? 0));
                result.append(average, points);
                content.appendChild(result);
            }

            stage.appendChild(content);
            return stage;
        };

        const renderRaterList = (sidebar, state, allowAfk) => {
            const title = document.createElement("strong");
            title.textContent = allowAfk ? text.rateAnswer : text.votes;
            const list = document.createElement("ul");
            list.className = "peer-rated-host-list";

            for (const rater of state.raters ?? []) {
                let button = null;
                if (allowAfk && rater.canExclude) {
                    button = actionButton("AFK", () => post("Exclude", {
                        sourceQuestionId: state.sourceQuestionId,
                        playerId: rater.id
                    }));
                    button.title = text.exclude;
                    button.setAttribute("aria-label", `${text.exclude}: ${rater.name}`);
                }
                const statusText = rater.excluded
                    ? text.afk
                    : rater.rating === null || rater.rating === undefined
                        ? text.pending
                        : `${rater.rating} ★`;
                appendStatusRow(list, rater, statusText, button);
            }

            sidebar.append(title, list);
        };

        const renderRating = (ui, state) => {
            ui.appendChild(createStage(state, false));
            const sidebar = createSidebar();
            const progress = document.createElement("strong");
            progress.textContent = text.reviewProgress(
                state.reviewPosition ?? 0,
                state.reviewCount ?? 0);
            sidebar.appendChild(progress);
            renderRaterList(sidebar, state, true);

            if (state.ratingComplete) {
                const complete = document.createElement("p");
                complete.className = "dialog-warning";
                complete.textContent = text.ratingComplete;
                const actions = document.createElement("div");
                actions.className = "peer-rated-host-sidebar-actions";
                if (state.hasNextAnswer) {
                    actions.appendChild(actionButton(text.next, () => post("Next", {
                        sourceQuestionId: state.sourceQuestionId
                    }), true));
                } else if (state.canShowResults) {
                    actions.appendChild(actionButton(text.showResults, () => post("Next", {
                        sourceQuestionId: state.sourceQuestionId
                    }), true));
                }
                sidebar.append(complete, actions);
            }

            ui.appendChild(sidebar);
        };

        const renderResults = (ui, state) => {
            ui.appendChild(createStage(state, true));
            const sidebar = createSidebar();
            const progress = document.createElement("strong");
            progress.textContent = text.resultProgress(
                state.resultPosition ?? 0,
                state.resultCount ?? 0);
            sidebar.appendChild(progress);
            renderRaterList(sidebar, state, false);

            const actions = document.createElement("div");
            actions.className = "peer-rated-host-sidebar-actions";
            if (state.hasNextResult) {
                actions.appendChild(actionButton(text.nextResult, () => post("NextResult", {
                    sourceQuestionId: state.sourceQuestionId
                }), true));
            } else if (state.canReturnToBoard) {
                actions.appendChild(actionButton(text.returnBoard, () => post("ReturnToBoard", {
                    sourceQuestionId: state.sourceQuestionId
                }), true));
            }
            sidebar.appendChild(actions);
            ui.appendChild(sidebar);
        };

        const getRenderKey = state => JSON.stringify(state);
        function applyState(state) {
            const target = currentBoard();
            if (!(target instanceof HTMLElement)) {
                return;
            }
            if (!state?.active) {
                renderKey = "";
                clear(target);
                return;
            }

            target.classList.add("peer-rated-all-player-active");
            target.classList.toggle("peer-rated-reviewing", state.phase !== "answering");
            const key = getRenderKey(state);
            const existing = target.querySelector(".peer-rated-host-ui");
            if (key === renderKey && existing) {
                scheduleLayout(target);
                return;
            }

            renderKey = key;
            clearContentReservation(target);
            existing?.remove();
            const summary = target.querySelector(".current-question-summary") ?? target;
            const ui = document.createElement("div");
            ui.className = "peer-rated-host-ui";

            if (state.phase === "answering") {
                renderAnswering(ui, state);
            } else if (state.phase === "results") {
                renderResults(ui, state);
            } else {
                renderRating(ui, state);
            }

            summary.appendChild(ui);
            scheduleLayout(target);
        }

        const schedule = active => {
            window.clearTimeout(pollHandle);
            pollHandle = window.setTimeout(poll, active ? 200 : 900);
        };
        const poll = async () => {
            try {
                const state = await getJson(
                    `${apiPath}?handler=Host&code=${encodeURIComponent(code)}`);
                applyState(state);
                schedule(Boolean(state.active));
            } catch {
                schedule(false);
            }
        };

        const refreshLayout = () => {
            const target = currentBoard();
            if (target instanceof HTMLElement) {
                scheduleLayout(target);
            }
        };

        document.addEventListener("badwolf:host-gameplay-updated", () => void poll());
        document.addEventListener("badwolf:host-shell-mounted", () => void poll());
        window.addEventListener("resize", refreshLayout, { passive: true });
        window.visualViewport?.addEventListener("resize", refreshLayout, { passive: true });
        window.addEventListener("pageshow", () => void poll());

        hostControllers.set(code, { poll, refreshLayout });
        void poll();
    };

    const initialize = () => {
        initializeEditor();
        initializePlayer();
        initializeHost();
    };
    initialize();
    new MutationObserver(initialize).observe(document.documentElement, {
        childList: true,
        subtree: true
    });
})();
