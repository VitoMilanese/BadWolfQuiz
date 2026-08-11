(() => {
    const trigger = document.querySelector("[data-open-description-preview]");
    const modal = document.getElementById("question-preview-modal");
    const title = document.getElementById("question-preview-title");
    const content = document.getElementById("question-preview-content");
    const closeButton = modal?.querySelector(".question-preview-close-button");
    const section = document.getElementById("description-blocks");

    if (!trigger || !modal || !title || !content || !section) return;

    const value = (card, property) =>
        card.querySelector(`[name$=".${property}"]`)?.value?.trim() ?? "";

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

        if (!content.childElementCount) {
            const empty = document.createElement("div");
            empty.className = "question-preview-empty";
            empty.textContent = "—";
            content.appendChild(empty);
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
})();
