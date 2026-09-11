(() => {
    if (window.badWolfBoardHeaderLayoutInitialized) {
        return;
    }

    window.badWolfBoardHeaderLayoutInitialized = true;

    const baseCategoryHues = [215, 32, 145, 266, 330, 185, 52, 8, 105, 295, 165, 245];
    const categoryColorProperties = [
        "--board-category-header-bg",
        "--board-category-header-bg-end",
        "--board-category-cell-bg",
        "--board-category-cell-bg-end",
        "--board-category-border",
        "--board-category-accent",
        "--board-category-resolved-bg",
        "--board-category-resolved-bg-end",
        "--board-category-foreground",
        "--board-category-header-foreground",
        "--board-category-cell-foreground",
        "--board-category-resolved-foreground"
    ];

    const getAutomaticCategoryHue = index => {
        const cycle = Math.floor(index / baseCategoryHues.length);
        return (baseCategoryHues[index % baseCategoryHues.length] + (cycle * 17)) % 360;
    };

    const clearCategoryColors = column => {
        categoryColorProperties.forEach(property => column.style.removeProperty(property));
    };

    const parseHexColor = value => {
        const match = /^#([0-9a-f]{6})$/i.exec(value || "");
        if (!match) return null;
        const number = Number.parseInt(match[1], 16);
        return {
            r: (number >> 16) & 255,
            g: (number >> 8) & 255,
            b: number & 255
        };
    };

    const mixColor = (source, target, amount) => ({
        r: Math.round(source.r + ((target.r - source.r) * amount)),
        g: Math.round(source.g + ((target.g - source.g) * amount)),
        b: Math.round(source.b + ((target.b - source.b) * amount))
    });

    const rgbCss = color => `rgb(${color.r} ${color.g} ${color.b})`;

    const relativeLuminance = color => {
        const linear = channel => {
            const value = channel / 255;
            return value <= 0.04045
                ? value / 12.92
                : Math.pow((value + 0.055) / 1.055, 2.4);
        };
        return (0.2126 * linear(color.r)) +
            (0.7152 * linear(color.g)) +
            (0.0722 * linear(color.b));
    };

    const contrastRatio = (first, second) => {
        const high = Math.max(relativeLuminance(first), relativeLuminance(second));
        const low = Math.min(relativeLuminance(first), relativeLuminance(second));
        return (high + 0.05) / (low + 0.05);
    };

    const chooseForeground = background => {
        const white = { r: 255, g: 255, b: 255 };
        return contrastRatio(background, white) >= 3
            ? "#ffffff"
            : "#000000";
    };

    const gradientEndForForeground = (background, foreground, amount) =>
        mixColor(
            background,
            foreground === "#ffffff"
                ? { r: 0, g: 0, b: 0 }
                : { r: 255, g: 255, b: 255 },
            amount);

    const applyCustomCategoryColor = (column, value) => {
        const base = parseHexColor(value);
        if (!base) return false;

        const black = { r: 0, g: 0, b: 0 };
        const white = { r: 255, g: 255, b: 255 };
        const foreground = chooseForeground(base);
        const contrastTarget = foreground === "#ffffff" ? black : white;
        const headerEnd = gradientEndForForeground(base, foreground, 0.12);
        const cell = mixColor(base, contrastTarget, 0.22);
        const cellEnd = gradientEndForForeground(cell, foreground, 0.12);
        const border = mixColor(base, white, 0.18);
        const accent = mixColor(base, white, 0.10);
        const resolved = mixColor(base, contrastTarget, 0.60);
        const resolvedEnd = gradientEndForForeground(resolved, foreground, 0.10);

        column.style.setProperty("--board-category-header-bg", rgbCss(base));
        column.style.setProperty("--board-category-header-bg-end", rgbCss(headerEnd));
        column.style.setProperty("--board-category-cell-bg", rgbCss(cell));
        column.style.setProperty("--board-category-cell-bg-end", rgbCss(cellEnd));
        column.style.setProperty("--board-category-border", rgbCss(border));
        column.style.setProperty("--board-category-accent", rgbCss(accent));
        column.style.setProperty("--board-category-resolved-bg", rgbCss(resolved));
        column.style.setProperty("--board-category-resolved-bg-end", rgbCss(resolvedEnd));
        column.style.setProperty("--board-category-header-foreground", foreground);
        column.style.setProperty("--board-category-cell-foreground", foreground);
        column.style.setProperty("--board-category-resolved-foreground", foreground);
        return true;
    };

    const applyAutomaticCategoryColor = (column, index) => {
        const hue = getAutomaticCategoryHue(index);
        column.style.setProperty("--board-category-header-bg", `hsl(${hue}, 76%, 27%)`);
        column.style.setProperty("--board-category-header-bg-end", `hsl(${hue}, 72%, 22%)`);
        column.style.setProperty("--board-category-cell-bg", `hsl(${hue}, 66%, 20%)`);
        column.style.setProperty("--board-category-cell-bg-end", `hsl(${hue}, 62%, 15%)`);
        column.style.setProperty("--board-category-border", `hsl(${hue}, 68%, 33%)`);
        column.style.setProperty("--board-category-accent", `hsl(${hue}, 82%, 46%)`);
        column.style.setProperty("--board-category-resolved-bg", `hsl(${hue}, 28%, 14%)`);
        column.style.setProperty("--board-category-resolved-bg-end", `hsl(${hue}, 22%, 11%)`);
        column.style.setProperty("--board-category-header-foreground", "#ffffff");
        column.style.setProperty("--board-category-cell-foreground", "#ffffff");
        column.style.setProperty("--board-category-resolved-foreground", "#cbd5e1");
    };

    const applyCategoryColors = () => {
        const grid = document.querySelector(".host-board-grid");
        if (!(grid instanceof HTMLElement)) return;

        const columns = Array.from(grid.querySelectorAll(".host-board-column"));
        const colorsEnabled = (
            grid.dataset.categoryColorsPreview ??
            grid.dataset.categoryColorsEnabled ??
            "true") !== "false";

        if (!colorsEnabled) {
            columns.forEach(column => {
                if (column instanceof HTMLElement) clearCategoryColors(column);
            });
            return;
        }

        columns.forEach((column, index) => {
            if (!(column instanceof HTMLElement)) return;

            const mode = column.dataset.categoryColorMode || "automatic";
            if (mode === "theme") {
                clearCategoryColors(column);
                return;
            }

            if (mode === "custom") {
                clearCategoryColors(column);
                if (applyCustomCategoryColor(column, column.dataset.categoryCustomColor)) return;
            }

            clearCategoryColors(column);
            applyAutomaticCategoryColor(column, index);
        });
    };

    const categoryColorsToggleSelector =
        "[data-category-colors-enabled-toggle]";
    let categoryColorsSavePending = false;

    const getCategoryColorsGrid = () => {
        const grid = document.querySelector(".host-board-grid");
        return grid instanceof HTMLElement ? grid : null;
    };

    const getCategoryColorsToggle = scope => {
        const toggle = (scope ?? document).querySelector(
            categoryColorsToggleSelector);
        return toggle instanceof HTMLInputElement ? toggle : null;
    };

    const notifyCategoryColorsChanged = () => {
        document.dispatchEvent(new CustomEvent(
            "badwolf:category-colors-enabled-changed"));
    };

    const previewCategoryColors = toggle => {
        const grid = getCategoryColorsGrid();
        if (!grid || !toggle) return;

        grid.dataset.categoryColorsPreview = toggle.checked ? "true" : "false";
        notifyCategoryColorsChanged();
    };

    const syncCategoryColorsToggleFromPersisted = toggle => {
        const grid = getCategoryColorsGrid();
        if (!grid || !toggle) return;

        toggle.checked =
            (grid.dataset.categoryColorsEnabled ?? "true") !== "false";
    };

    const clearCategoryColorsPreview = toggle => {
        const grid = getCategoryColorsGrid();
        if (!grid) return;

        grid.removeAttribute("data-category-colors-preview");
        syncCategoryColorsToggleFromPersisted(toggle);
        notifyCategoryColorsChanged();
    };

    const commitCategoryColorsPreview = toggle => {
        const grid = getCategoryColorsGrid();
        if (!grid || !toggle) return;

        grid.dataset.categoryColorsEnabled = toggle.checked ? "true" : "false";
        grid.removeAttribute("data-category-colors-preview");
        notifyCategoryColorsChanged();
    };

    document.addEventListener("change", event => {
        const toggle = event.target instanceof HTMLInputElement &&
            event.target.matches(categoryColorsToggleSelector)
            ? event.target
            : null;
        if (toggle) {
            previewCategoryColors(toggle);
        }
    });

    document.addEventListener("click", event => {
        const target = event.target instanceof Element ? event.target : null;
        if (!target) return;

        if (target.closest("[data-open-game-settings]")) {
            categoryColorsSavePending = false;
            const dialog = document.getElementById("game-settings-dialog");
            syncCategoryColorsToggleFromPersisted(
                getCategoryColorsToggle(dialog));
            return;
        }

        const dialog = target.closest("#game-settings-dialog");
        if (!dialog) return;

        if (target.closest("[data-close-game-settings]") || target === dialog) {
            categoryColorsSavePending = false;
            clearCategoryColorsPreview(getCategoryColorsToggle(dialog));
        }
    }, true);

    document.addEventListener("submit", event => {
        const form = event.target instanceof HTMLFormElement
            ? event.target
            : null;
        if (!form?.closest("#game-settings-dialog") ||
            !form.querySelector(categoryColorsToggleSelector)) {
            return;
        }

        categoryColorsSavePending = true;
    }, true);

    document.addEventListener("cancel", event => {
        const dialog = event.target instanceof HTMLDialogElement &&
            event.target.id === "game-settings-dialog"
            ? event.target
            : null;
        if (!dialog) return;

        categoryColorsSavePending = false;
        clearCategoryColorsPreview(getCategoryColorsToggle(dialog));
    }, true);

    document.addEventListener("close", event => {
        const dialog = event.target instanceof HTMLDialogElement &&
            event.target.id === "game-settings-dialog"
            ? event.target
            : null;
        if (!dialog) return;

        const toggle = getCategoryColorsToggle(dialog);
        if (categoryColorsSavePending) {
            categoryColorsSavePending = false;
            commitCategoryColorsPreview(toggle);
            return;
        }

        clearCategoryColorsPreview(toggle);
    }, true);

    if (!document.querySelector("script[data-host-gameplay-submit-guard]")) {
        const submitGuard = document.createElement("script");
        submitGuard.src = "/js/host-gameplay-submit-guard.js";
        submitGuard.async = false;
        submitGuard.dataset.hostGameplaySubmitGuard = "";
        document.head.append(submitGuard);
    }

    const syncHeaderHeights = () => {
        applyCategoryColors();

        const grid = document.querySelector(".host-board-grid");
        if (!(grid instanceof HTMLElement) ||
            grid.getClientRects().length === 0) {
            return false;
        }

        const headers = Array.from(grid.querySelectorAll(
            ".host-board-column > h3"));
        if (headers.length === 0) {
            return false;
        }

        headers.forEach(header => {
            header.style.height = "";
        });

        const maximumHeight = Math.max(
            ...headers.map(header => header.offsetHeight));
        if (maximumHeight <= 0) {
            return false;
        }

        headers.forEach(header => {
            header.style.height = `${maximumHeight}px`;
        });
        return true;
    };

    let followUpFrame = 0;
    const syncBeforePaint = () => {
        syncHeaderHeights();

        if (followUpFrame !== 0) {
            window.cancelAnimationFrame(followUpFrame);
        }
        followUpFrame = window.requestAnimationFrame(() => {
            followUpFrame = 0;
            syncHeaderHeights();
        });
    };

    document.addEventListener(
        "badwolf:host-gameplay-updated",
        syncBeforePaint);
    document.addEventListener(
        "badwolf:category-colors-enabled-changed",
        syncBeforePaint);
    window.addEventListener("resize", syncBeforePaint);

    syncBeforePaint();
})();
