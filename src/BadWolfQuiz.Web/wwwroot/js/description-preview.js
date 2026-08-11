(() => {
    const trigger = document.querySelector("[data-open-description-preview]");
    const modal = document.getElementById("question-preview-modal");
    const title = document.getElementById("question-preview-title");
    const content = document.getElementById("question-preview-content");
    const closeButton = modal?.querySelector(".question-preview-close-button");
    const section = document.getElementById("description-blocks");

    if (!trigger || !modal || !title || !content || !section) return;

    modal.classList.add("description-preview-modal");

    const style = document.createElement("style");
    style.textContent = `
        .description-preview-modal .question-preview-title {
            margin: 0 0 clamp(20px, 3vh, 36px);
            font-size: clamp(2.4rem, 6vw, 6.5rem);
            line-height: 1;
            letter-spacing: 0.06em;
            font-weight: 950;
        }
    `;
    document.head.appendChild(style);

    const value = (card, property) =>
        card.querySelector(`[name$=".${property}"]`)?.value?.trim() ?? "";

    const revokeTemporaryImageUrl = preview => {
        const objectUrl = preview?.dataset.objectUrl;
        if (!objectUrl) return;
        URL.revokeObjectURL(objectUrl);
        delete preview.dataset.objectUrl;
    };

    const updateImageEditorPreview = input => {
        const editor = input.closest(".image-block-editor");
        const preview = editor?.querySelector(".unified-file-preview");
        const image = editor?.querySelector(".unified-image-preview-element");
        const fileLabel = editor?.querySelector(".file-preview-label");
        const fileName = editor?.querySelector(".file-preview-name");
        const removeFile = editor?.querySelector(".remove-file-value");
        const removeButton = editor?.querySelector(".remove-stored-file-button");
        const cancelButton = editor?.querySelector(".cancel-file-change-button");
        const file = input.files?.[0];

        if (!editor || !preview || !image) return;

        revokeTemporaryImageUrl(preview);

        if (!file) {
            if (preview.dataset.hasOriginal === "true") {
                image.src = preview.dataset.originalSrc ?? "";
                image.alt = preview.dataset.originalName ?? "";
                if (fileLabel) fileLabel.textContent = preview.dataset.originalLabel ?? "";
                if (fileName) fileName.textContent = preview.dataset.originalName ?? "";
                preview.hidden = false;
                if (removeButton) removeButton.hidden = false;
            } else {
                image.removeAttribute("src");
                image.alt = "";
                preview.hidden = true;
                if (removeButton) removeButton.hidden = true;
            }
            if (cancelButton) cancelButton.style.setProperty("display", "none", "important");
            return;
        }

        const objectUrl = URL.createObjectURL(file);
        preview.dataset.objectUrl = objectUrl;
        image.src = objectUrl;
        image.alt = file.name;
        preview.hidden = false;
        if (fileLabel) fileLabel.textContent = preview.dataset.pendingLabel ?? "";
        if (fileName) fileName.textContent = file.name;
        if (removeFile) removeFile.value = "false";
        if (removeButton) removeButton.hidden = true;
        if (cancelButton) cancelButton.style.setProperty("display", "inline-flex", "important");
    };

    document.addEventListener("change", event => {
        if (event.target.matches(".image-block-editor .uploaded-file-input")) {
            updateImageEditorPreview(event.target);
        }
    });

    const caption = (container, text) => {
        if (!text) return;
        const element = document.createElement("div");
        element.className = "question-preview-caption";
        element.textContent = text;
        container.appendChild(element);
    };

    const textPreview = card => {
        const text = value(card, "TextContent");
        if (!text) return null;
        const element = document.createElement("div");
        element.className = "question-preview-text game-content-block game-content-text";
        element.textContent = text;
        return element;
    };

    const imagePreview = card => {
        const source = card.querySelector(".unified-image-preview-element");
        const wrapperPreview = card.querySelector(".unified-file-preview");
        if (!source?.src || wrapperPreview?.hidden) return null;

        const wrapper = document.createElement("figure");
        wrapper.className = "question-preview-media game-content-block";
        caption(wrapper, value(card, "TopCaption"));
        const image = document.createElement("img");
        image.className = "question-preview-image";
        image.src = source.src;
        image.alt = source.alt || value(card, "FileName");
        wrapper.appendChild(image);
        caption(wrapper, value(card, "BottomCaption"));
        return wrapper;
    };

    const audioPreview = card => {
        const source = card.querySelector(".unified-audio-preview-element");
        const wrapperPreview = card.querySelector(".unified-file-preview");
        if (!source?.src || wrapperPreview?.hidden) return null;

        const wrapper = document.createElement("div");
        wrapper.className = "question-preview-media question-preview-audio game-content-block";
        caption(wrapper, value(card, "TopCaption"));
        const audio = document.createElement("audio");
        audio.className = "question-preview-audio-player";
        audio.controls = true;
        audio.src = source.src;
        wrapper.appendChild(audio);
        caption(wrapper, value(card, "BottomCaption"));
        return wrapper;
    };

    const youtubeEmbedUrl = rawUrl => {
        try {
            const url = new URL(rawUrl);
            let id = "";
            if (url.hostname === "youtu.be") id = url.pathname.slice(1).split("/")[0];
            else if (url.pathname.startsWith("/shorts/")) id = url.pathname.split("/")[2];
            else if (url.pathname.startsWith("/embed/")) id = url.pathname.split("/")[2];
            else id = url.searchParams.get("v") ?? "";
            return id ? `https://www.youtube.com/embed/${encodeURIComponent(id)}` : "";
        } catch {
            return "";
        }
    };

    const videoPreview = card => {
        const rawUrl = value(card, "ExternalUrl");
        if (!rawUrl) return null;

        const wrapper = document.createElement("div");
        wrapper.className = "question-preview-media game-content-block";
        caption(wrapper, value(card, "TopCaption"));

        const blockType = card.dataset.blockType?.toLowerCase();
        if (blockType === "youtube") {
            const embedUrl = youtubeEmbedUrl(rawUrl);
            if (!embedUrl) return null;
            const frameWrapper = document.createElement("div");
            frameWrapper.className = "question-preview-video-frame";
            const frame = document.createElement("iframe");
            frame.src = embedUrl;
            frame.allow = "accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share";
            frame.allowFullscreen = true;
            frameWrapper.appendChild(frame);
            wrapper.appendChild(frameWrapper);
        } else {
            const video = document.createElement("video");
            video.className = "question-preview-video";
            video.controls = true;
            video.src = rawUrl;
            wrapper.appendChild(video);
        }

        caption(wrapper, value(card, "BottomCaption"));
        return wrapper;
    };

    const render = () => {
        content.replaceChildren();
        title.textContent = trigger.dataset.previewTitle ?? "";

        for (const card of section.querySelectorAll(".content-block-card")) {
            const type = card.dataset.blockType?.toLowerCase();
            const element = type === "text"
                ? textPreview(card)
                : type === "image"
                    ? imagePreview(card)
                    : type === "audio"
                        ? audioPreview(card)
                        : (type === "video" || type === "youtube")
                            ? videoPreview(card)
                            : null;
            if (element) content.appendChild(element);
        }
    };

    const open = () => {
        render();
        modal.hidden = false;
        modal.setAttribute("aria-hidden", "false");
        document.body.classList.add("question-preview-open");
        closeButton?.focus();
    };

    const close = () => {
        modal.hidden = true;
        modal.setAttribute("aria-hidden", "true");
        document.body.classList.remove("question-preview-open");
        content.querySelectorAll("audio, video").forEach(media => media.pause());
        content.querySelectorAll("iframe").forEach(frame => frame.src = "about:blank");
        trigger.focus();
    };

    trigger.addEventListener("click", open);
    closeButton?.addEventListener("click", close);
    modal.addEventListener("click", event => {
        if (event.target === modal) close();
    });
    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && !modal.hidden) {
            event.preventDefault();
            event.stopImmediatePropagation();
            close();
        }
    }, true);

    window.addEventListener("beforeunload", () => {
        section.querySelectorAll(".unified-file-preview").forEach(revokeTemporaryImageUrl);
    });
})();
