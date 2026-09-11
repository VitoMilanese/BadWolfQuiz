(() => {
    if (window.badWolfBoardHeaderLayoutInitialized) {
        return;
    }

    window.badWolfBoardHeaderLayoutInitialized = true;

    const baseCategoryHues = [215, 32, 145, 266, 330, 185, 52, 8, 105, 295, 165, 245];
    const automaticCategoryColorProperties = [
        "--board-category-header-bg",
        "--board-category-header-bg-end",
        "--board-category-cell-bg",
        "--board-category-cell-bg-end",
        "--board-category-border",
        "--board-category-accent",
        "--board-category-resolved-bg",
        "--board-category-resolved-bg-end",
        "--board-category-foreground",
        "--board-category-resolved-foreground"
    ];

    const getAutomaticCategoryHue = index => {
        const cycle = Math.floor(index / baseCategoryHues.length);
        return (baseCategoryHues[index % baseCategoryHues.length] + (cycle * 17)) % 360;
    };

    const clearAutomaticCategoryColors = column => {
        automaticCategoryColorProperties.forEach(property => {
            column.style.removeProperty(property);
        });
    };

    const applyCategoryColors = () => {
        const grid = document.querySelector(".host-board-grid");
        if (!(grid instanceof HTMLElement)) {
            return;
        }

        const columns = Array.from(grid.querySelectorAll(".host-board-column"));
        columns.forEach((column, index) => {
            if (!(column instanceof HTMLElement)) {
                return;
            }

            const mode = column.dataset.categoryColorMode || "automatic";
            if (mode === "theme") {
                clearAutomaticCategoryColors(column);
                return;
            }

            const hasCustomColor =
                mode === "custom" &&
                column.style.getPropertyValue("--board-category-header-bg").trim().length > 0;
            if (hasCustomColor) {
                return;
            }

            const hue = getAutomaticCategoryHue(index);
            column.style.setProperty("--board-category-header-bg", `hsl(${hue}, 76%, 27%)`);
            column.style.setProperty("--board-category-header-bg-end", `hsl(${hue}, 72%, 22%)`);
            column.style.setProperty("--board-category-cell-bg", `hsl(${hue}, 66%, 20%)`);
            column.style.setProperty("--board-category-cell-bg-end", `hsl(${hue}, 62%, 15%)`);
            column.style.setProperty("--board-category-border", `hsl(${hue}, 68%, 33%)`);
            column.style.setProperty("--board-category-accent", `hsl(${hue}, 82%, 46%)`);
            column.style.setProperty("--board-category-resolved-bg", `hsl(${hue}, 28%, 14%)`);
            column.style.setProperty("--board-category-resolved-bg-end", `hsl(${hue}, 22%, 11%)`);
            column.style.setProperty("--board-category-foreground", "#ffffff");
            column.style.setProperty("--board-category-resolved-foreground", "#cbd5e1");
        });
    };

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
    window.addEventListener("resize", syncBeforePaint);

    syncBeforePaint();
})();
