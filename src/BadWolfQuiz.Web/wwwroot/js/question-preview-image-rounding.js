(() => {
    if (window.badWolfQuestionPreviewImageRoundingInitialized) {
        return;
    }

    window.badWolfQuestionPreviewImageRoundingInitialized = true;

    const previewImageSelector = "img.question-preview-image";
    const previewImageRadius = "clamp(12px, 1.3vw, 20px)";
    const modal = document.getElementById("question-preview-modal");
    let frameHandle = 0;

    if (!(modal instanceof HTMLElement)) {
        return;
    }

    const applyPreviewImageCorners = image => {
        if (!(image instanceof HTMLImageElement)) {
            return;
        }

        image.style.setProperty(
            "border-radius",
            previewImageRadius,
            "important");

        if (!image.complete ||
            image.naturalWidth <= 0 ||
            image.naturalHeight <= 0) {
            image.style.removeProperty("clip-path");
            return;
        }

        const bounds = image.getBoundingClientRect();
        if (bounds.width <= 0 || bounds.height <= 0) {
            return;
        }

        const naturalRatio = image.naturalWidth / image.naturalHeight;
        const boxRatio = bounds.width / bounds.height;
        let horizontalInset = 0;
        let verticalInset = 0;

        if (boxRatio > naturalRatio) {
            const paintedWidth = bounds.height * naturalRatio;
            horizontalInset = Math.max(
                0,
                (bounds.width - paintedWidth) / 2);
        } else if (boxRatio < naturalRatio) {
            const paintedHeight = bounds.width / naturalRatio;
            verticalInset = Math.max(
                0,
                (bounds.height - paintedHeight) / 2);
        }

        image.style.setProperty(
            "clip-path",
            `inset(${verticalInset}px ${horizontalInset}px ${verticalInset}px ${horizontalInset}px round ${previewImageRadius})`,
            "important");
    };

    const applyAll = () => {
        frameHandle = 0;

        if (modal.hidden) {
            return;
        }

        modal.querySelectorAll(previewImageSelector)
            .forEach(applyPreviewImageCorners);
    };

    const scheduleApply = () => {
        if (frameHandle !== 0) {
            return;
        }

        frameHandle = window.requestAnimationFrame(() => {
            frameHandle = window.requestAnimationFrame(applyAll);
        });
    };

    modal.addEventListener("load", event => {
        if (event.target instanceof HTMLImageElement &&
            event.target.matches(previewImageSelector)) {
            applyPreviewImageCorners(event.target);
        }
    }, true);

    const observer = new MutationObserver(scheduleApply);
    observer.observe(modal, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ["hidden"]
    });

    window.addEventListener("resize", scheduleApply);
    scheduleApply();
})();
