(() => {
    "use strict";

    if (window.badWolfContentBlockContainersInitialized) {
        return;
    }

    window.badWolfContentBlockContainersInitialized = true;

    const containerType = "Container";
    const supportedChildTypes = new Set([
        "Image",
        "Audio",
        "Video",
        "YouTube"
    ]);
    const runtimeMarkerPattern = /^__badwolf_container:(\d+)__$/;
    let syncScheduled = false;

    const style = document.createElement("style");
    style.id = "content-block-container-styles";
    style.textContent = `
.content-block-card[data-block-type="Container"] {
    border-color: color-mix(in srgb, var(--red) 42%, var(--line));
    background: color-mix(in srgb, var(--panel-2) 88%, var(--red) 12%);
}

.content-block-container-editor {
    display: grid;
    gap: 12px;
}

.content-block-container-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    flex-wrap: wrap;
}

.content-block-container-toolbar > span {
    color: var(--muted);
    font-size: 0.92rem;
}

.content-block-container-actions {
    display: flex;
    gap: 8px;
    flex-wrap: wrap;
}

.content-block-container-children {
    min-width: 0;
    display: grid;
    gap: 12px;
    padding: 14px;
    border: 1px dashed color-mix(in srgb, var(--red) 42%, var(--line));
    border-radius: 12px;
    background: color-mix(in srgb, var(--panel) 92%, transparent);
}

.content-block-container-children:empty::before {
    content: "Add image, video, or audio blocks";
    color: var(--muted);
    font-size: 0.9rem;
    text-align: center;
}

.content-block-container-child {
    min-width: 0;
    margin-bottom: 0 !important;
    border-left: 3px solid color-mix(in srgb, var(--red-bright) 72%, var(--line));
}

.content-block-container-child .content-block-drag-handle {
    display: none !important;
}

.content-block-container-layout {
    --content-block-container-columns: 1;
    width: 100%;
    max-width: 100%;
    min-width: 0;
    display: grid;
    grid-template-columns: repeat(
        var(--content-block-container-columns),
        minmax(0, 1fr));
    align-items: start;
    gap: clamp(10px, 1.8vw, 24px);
}

.content-block-container-host {
    width: 100%;
    max-width: none !important;
    min-width: 0;
}

.content-block-container-layout > .game-content-block {
    width: 100%;
    max-width: 100%;
    min-width: 0;
    margin: 0;
}

.content-block-container-layout .game-content-image,
.content-block-container-layout .game-content-video,
.content-block-container-layout .question-preview-image,
.content-block-container-layout .question-preview-video,
.content-block-container-layout iframe,
.content-block-container-layout video,
.content-block-container-layout img {
    width: 100%;
    max-width: 100%;
    height: auto;
    object-fit: contain;
}

.content-block-container-layout .game-content-audio-shell,
.content-block-container-layout .game-content-audio,
.content-block-container-layout .question-preview-audio,
.content-block-container-layout .question-preview-audio-player,
.content-block-container-layout audio {
    width: 100%;
    max-width: 100%;
    min-width: 0;
}

.content-block-container-layout .youtube-placeholder,
.content-block-container-layout .question-preview-video-frame {
    width: 100%;
    max-width: 100%;
    min-width: 0;
}

@media (max-width: 700px) {
    .content-block-container-layout {
        gap: 8px;
    }

    .content-block-container-children {
        padding: 10px;
    }
}`;
    document.head.appendChild(style);

    const parseChildCount = value => {
        const parsed = Number.parseInt(value ?? "", 10);
        return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
    };

    const isContentBlockCard = element =>
        element instanceof HTMLElement &&
        element.classList.contains("content-block-card");

    const isContainerCard = card =>
        isContentBlockCard(card) &&
        card.dataset.blockType === containerType;

    const isSupportedChildCard = card =>
        isContentBlockCard(card) &&
        supportedChildTypes.has(card.dataset.blockType ?? "");

    const prepareChildCard = card => {
        card.classList.add("content-block-container-child");
        card.dataset.contentBlockContainerChild = "true";
        const dragHandle = card.querySelector(".content-block-drag-handle");
        if (dragHandle) {
            dragHandle.draggable = false;
            dragHandle.setAttribute("aria-hidden", "true");
        }
    };

    const syncContainerCount = containerCard => {
        const countInput = containerCard.querySelector(
            ".content-block-container-count");
        const childrenHost = containerCard.querySelector(
            "[data-content-block-container-children]");
        if (!countInput || !childrenHost) {
            return;
        }

        const childCount = Array.from(childrenHost.children)
            .filter(isSupportedChildCard)
            .length;
        countInput.value = String(childCount);
    };

    const initializeEditorContainer = containerCard => {
        if (!isContainerCard(containerCard) ||
            containerCard.dataset.contentBlockContainerInitialized === "true") {
            return;
        }

        const countInput = containerCard.querySelector(
            ".content-block-container-count");
        const childrenHost = containerCard.querySelector(
            "[data-content-block-container-children]");
        if (!countInput || !childrenHost) {
            return;
        }

        const expectedCount = parseChildCount(countInput.value);
        let movedCount = 0;
        let candidate = containerCard.nextElementSibling;

        while (candidate && movedCount < expectedCount) {
            const nextCandidate = candidate.nextElementSibling;
            if (!isSupportedChildCard(candidate)) {
                break;
            }

            prepareChildCard(candidate);
            childrenHost.appendChild(candidate);
            movedCount += 1;
            candidate = nextCandidate;
        }

        countInput.value = String(movedCount);
        containerCard.dataset.contentBlockContainerInitialized = "true";
    };

    const initializeEditorContainers = root => {
        root.querySelectorAll?.(
            '.content-block-card[data-block-type="Container"]')
            .forEach(initializeEditorContainer);
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

            const sortOrderInput = card.querySelector(
                `input[name="${fieldPrefix}[${index}].SortOrder"]`);
            if (sortOrderInput) {
                sortOrderInput.value = index;
            }
        });
    };

    const syncEditorContainers = () => {
        document.querySelectorAll(
            '.content-block-card[data-block-type="Container"]')
            .forEach(syncContainerCount);
        document.querySelectorAll(".content-block-section")
            .forEach(reindexSection);
    };

    const requestSync = () => {
        if (syncScheduled) {
            return;
        }

        syncScheduled = true;
        window.queueMicrotask(() => {
            syncScheduled = false;
            initializeEditorContainers(document);
            syncEditorContainers();
            processRuntimeContainers(document);
        });
    };

    const fetchBlockCard = async (section, blockType) => {
        const list = section.querySelector("[data-content-block-list]");
        const fieldPrefix = section.dataset.blockCollection;
        if (!list || !fieldPrefix) {
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
        return isContentBlockCard(card) ? card : null;
    };

    const addContainer = async (section, errorMessage) => {
        const list = section.querySelector("[data-content-block-list]");
        if (!list) {
            return;
        }

        const card = await fetchBlockCard(section, containerType);
        if (!card) {
            window.alert(errorMessage || "Could not add container.");
            return;
        }

        list.appendChild(card);
        initializeEditorContainer(card);
        reindexSection(section);
    };

    const addContainerChild = async (button, blockType) => {
        const containerCard = button.closest(
            '.content-block-card[data-block-type="Container"]');
        const section = button.closest(".content-block-section");
        const childrenHost = containerCard?.querySelector(
            "[data-content-block-container-children]");
        if (!containerCard || !section || !childrenHost ||
            !supportedChildTypes.has(blockType)) {
            return;
        }

        const card = await fetchBlockCard(section, blockType);
        if (!card) {
            window.alert("Could not add media block.");
            return;
        }

        prepareChildCard(card);
        childrenHost.appendChild(card);
        syncContainerCount(containerCard);
        reindexSection(section);
    };

    const getTopLevelCards = section => {
        const list = section.querySelector("[data-content-block-list]");
        if (!list) {
            return [];
        }

        return Array.from(list.children).filter(isContentBlockCard);
    };

    const cardProducesEditorPreview = card => {
        switch (card.dataset.blockType) {
            case "Text":
                return Boolean(card.querySelector('[name$=".TextContent"]')
                    ?.value?.trim());

            case "Image": {
                const preview = card.querySelector(".unified-file-preview");
                const image = card.querySelector(
                    ".unified-image-preview-element");
                return Boolean(preview && !preview.hidden &&
                    image?.getAttribute("src"));
            }

            case "Audio": {
                const preview = card.querySelector(".unified-file-preview");
                const audio = card.querySelector(
                    ".unified-audio-preview-element");
                return Boolean(preview && !preview.hidden &&
                    audio?.getAttribute("src"));
            }

            case "Video":
            case "YouTube":
                return Boolean(card.querySelector('[name$=".ExternalUrl"]')
                    ?.value?.trim());

            default:
                return false;
        }
    };

    const createHorizontalLayout = childCount => {
        const layout = document.createElement("div");
        layout.className = "content-block-container-layout";
        layout.style.setProperty(
            "--content-block-container-columns",
            String(Math.max(1, childCount)));
        return layout;
    };

    const rebuildEditorPreview = section => {
        const previewRoot = document.getElementById(
            "question-preview-content");
        if (!section || !previewRoot) {
            return;
        }

        const rendered = Array.from(previewRoot.children);
        const rebuilt = document.createDocumentFragment();
        let renderedIndex = 0;

        for (const card of getTopLevelCards(section)) {
            if (isContainerCard(card)) {
                const childCards = Array.from(
                    card.querySelector(
                        "[data-content-block-container-children]")
                        ?.children ?? [])
                    .filter(isSupportedChildCard);
                const childPreviews = [];

                for (const childCard of childCards) {
                    if (!cardProducesEditorPreview(childCard)) {
                        continue;
                    }

                    const preview = rendered[renderedIndex++];
                    if (preview) {
                        childPreviews.push(preview);
                    }
                }

                if (childPreviews.length > 0) {
                    const layout = createHorizontalLayout(
                        childPreviews.length);
                    childPreviews.forEach(preview => layout.appendChild(preview));
                    rebuilt.appendChild(layout);
                }
                continue;
            }

            if (!cardProducesEditorPreview(card)) {
                continue;
            }

            const preview = rendered[renderedIndex++];
            if (preview) {
                rebuilt.appendChild(preview);
            }
        }

        while (renderedIndex < rendered.length) {
            rebuilt.appendChild(rendered[renderedIndex++]);
        }

        previewRoot.replaceChildren(rebuilt);
    };

    const parseRuntimeMarker = value => {
        const match = value?.trim().match(runtimeMarkerPattern);
        if (!match) {
            return null;
        }

        return parseChildCount(match[1]);
    };

    function processRuntimeContainers(root) {
        const blockLists = [];
        if (root instanceof Element && root.matches(".game-content-blocks")) {
            blockLists.push(root);
        }
        root.querySelectorAll?.(".game-content-blocks")
            .forEach(list => blockLists.push(list));

        for (const list of blockLists) {
            const directBlocks = Array.from(list.children)
                .filter(element => element.classList.contains(
                    "game-content-block"));

            for (const host of directBlocks) {
                if (host.dataset.contentBlockContainerRuntime === "true") {
                    continue;
                }

                const marker = host.querySelector(":scope > .game-content-text");
                const childCount = parseRuntimeMarker(marker?.textContent);
                if (childCount === null) {
                    continue;
                }

                host.dataset.contentBlockContainerRuntime = "true";
                host.classList.add("content-block-container-host");
                marker.remove();

                const children = [];
                let candidate = host.nextElementSibling;
                while (candidate && children.length < childCount) {
                    const nextCandidate = candidate.nextElementSibling;
                    if (candidate.classList.contains("game-content-block")) {
                        children.push(candidate);
                    }
                    candidate = nextCandidate;
                }

                if (children.length === 0) {
                    host.hidden = true;
                    continue;
                }

                const layout = createHorizontalLayout(children.length);
                children.forEach(child => layout.appendChild(child));
                host.appendChild(layout);
            }
        }
    }

    const updateFourClueAvailability = () => {
        const presentationType = document.getElementById(
            "Input_PresentationType");
        const questionSection = document.getElementById("question-blocks");
        const option = questionSection?.querySelector(
            "[data-content-block-container-option]");
        if (!presentationType || !option) {
            return;
        }

        option.hidden = presentationType.value === "1";
    };

    document.addEventListener("click", event => {
        const option = event.target.closest(
            "[data-content-block-container-option]");
        if (!option) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        const section = option.closest(".content-block-section");
        if (!section) {
            return;
        }

        const presentationType = document.getElementById(
            "Input_PresentationType");
        if (section.id === "question-blocks" &&
            presentationType?.value === "1") {
            return;
        }

        const typeMenu = option.closest(".content-block-type-menu");
        typeMenu?.setAttribute("hidden", "hidden");
        void addContainer(section, option.dataset.containerAddError);
    }, true);

    document.addEventListener("click", event => {
        const button = event.target.closest("[data-container-add-block-type]");
        if (!button) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        void addContainerChild(
            button,
            button.dataset.containerAddBlockType ?? "");
    }, true);

    document.addEventListener("click", event => {
        const button = event.target.closest("[data-open-question-preview]");
        if (!button) {
            return;
        }

        const sectionId = button.dataset.openQuestionPreview === "answer"
            ? "answer-blocks"
            : "question-blocks";
        window.setTimeout(() => {
            rebuildEditorPreview(document.getElementById(sectionId));
        }, 0);
    }, true);

    document.addEventListener("change", event => {
        if (event.target?.id !== "Input_PresentationType") {
            return;
        }

        const questionSection = document.getElementById("question-blocks");
        const hasContainer = questionSection
            ?.querySelector('.content-block-card[data-block-type="Container"]');
        if (event.target.value === "1" && hasContainer) {
            event.target.value = "0";
            window.alert(
                "Container blocks are not available for four-clue questions.");
        }
        updateFourClueAvailability();
    }, true);

    document.addEventListener("dragover", event => {
        if (!event.target.closest(".content-block-container-children")) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
    }, true);

    document.addEventListener("drop", event => {
        if (!event.target.closest(".content-block-container-children")) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
    }, true);

    const observer = new MutationObserver(requestSync);
    if (document.body) {
        observer.observe(document.body, { childList: true, subtree: true });
    }

    initializeEditorContainers(document);
    syncEditorContainers();
    updateFourClueAvailability();
    processRuntimeContainers(document);
})();
