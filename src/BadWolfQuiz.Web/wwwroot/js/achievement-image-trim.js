(() => {
    const imageSelector = ".player-achievement-image";
    const artworkSize = 168;

    const trimImage = image => {
        if (!(image instanceof HTMLImageElement) || image.dataset.alphaTrimInitialized === "true") {
            return;
        }

        image.dataset.alphaTrimInitialized = "true";

        const applyTrim = () => {
            const width = image.naturalWidth;
            const height = image.naturalHeight;
            if (!width || !height) {
                return;
            }

            const canvas = document.createElement("canvas");
            canvas.width = width;
            canvas.height = height;
            const context = canvas.getContext("2d", { willReadFrequently: true });
            if (!context) {
                return;
            }

            try {
                context.drawImage(image, 0, 0);
                const pixels = context.getImageData(0, 0, width, height).data;
                let minX = width;
                let minY = height;
                let maxX = -1;
                let maxY = -1;

                for (let y = 0; y < height; y += 1) {
                    const rowOffset = y * width * 4;
                    for (let x = 0; x < width; x += 1) {
                        if (pixels[rowOffset + x * 4 + 3] === 0) {
                            continue;
                        }

                        minX = Math.min(minX, x);
                        minY = Math.min(minY, y);
                        maxX = Math.max(maxX, x);
                        maxY = Math.max(maxY, y);
                    }
                }

                if (maxX < minX || maxY < minY) {
                    return;
                }

                const visibleWidth = maxX - minX + 1;
                const visibleHeight = maxY - minY + 1;
                const scale = Math.min(
                    artworkSize / visibleWidth,
                    artworkSize / visibleHeight);
                const scaledWidth = width * scale;
                const scaledHeight = height * scale;
                const visibleCenterX = ((minX + maxX + 1) / 2) * scale;
                const visibleCenterY = ((minY + maxY + 1) / 2) * scale;

                image.style.position = "absolute";
                image.style.left = "50%";
                image.style.top = "50%";
                image.style.width = `${scaledWidth}px`;
                image.style.height = `${scaledHeight}px`;
                image.style.maxWidth = "none";
                image.style.maxHeight = "none";
                image.style.transform = `translate(${-visibleCenterX}px, ${-visibleCenterY}px)`;
                image.dataset.alphaTrimmed = "true";
            } catch (error) {
                console.warn("Could not trim transparent achievement image pixels.", error);
            }
        };

        if (image.complete && image.naturalWidth > 0) {
            applyTrim();
        } else {
            image.addEventListener("load", applyTrim, { once: true });
        }
    };

    const trimAll = root => {
        if (root instanceof HTMLImageElement && root.matches(imageSelector)) {
            trimImage(root);
        }

        if (root instanceof Document || root instanceof Element) {
            for (const image of root.querySelectorAll(imageSelector)) {
                trimImage(image);
            }
        }
    };

    const initialize = () => {
        trimAll(document);

        new MutationObserver(mutations => {
            for (const mutation of mutations) {
                for (const node of mutation.addedNodes) {
                    if (node instanceof Element) {
                        trimAll(node);
                    }
                }
            }
        }).observe(document.body, { childList: true, subtree: true });
    };

    window.BadWolfAchievementImages = { trimAll };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
