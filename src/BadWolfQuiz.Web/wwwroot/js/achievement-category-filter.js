(() => {
    const initialize = () => {
        const select = document.querySelector("[data-achievement-category-filter]");
        const grid = document.querySelector(".self-achievements-panel .player-achievements-grid");
        const count = document.querySelector("[data-achievement-visible-count]");
        if (!(select instanceof HTMLSelectElement) || !(grid instanceof HTMLElement)) {
            return;
        }

        const updateCount = (unlocked, total) => {
            if (!(count instanceof HTMLElement)) {
                return;
            }

            const template = count.dataset.achievementCountTemplate || "{0} / {1}";
            count.textContent = template
                .replace("{0}", String(unlocked))
                .replace("{1}", String(total));
        };

        const applyFilter = () => {
            const selectedCategory = select.value || "all";
            let visibleCount = 0;
            let unlockedCount = 0;

            for (const card of grid.querySelectorAll(".player-achievement-card[data-achievement-category]")) {
                if (!(card instanceof HTMLElement)) {
                    continue;
                }

                const isVisible = selectedCategory === "all" ||
                    card.dataset.achievementCategory === selectedCategory;
                card.hidden = !isVisible;

                if (isVisible) {
                    visibleCount++;
                    if (card.classList.contains("is-unlocked")) {
                        unlockedCount++;
                    }
                }
            }

            updateCount(unlockedCount, visibleCount);
        };

        select.addEventListener("change", applyFilter);

        const observer = new MutationObserver(mutations => {
            if (mutations.some(mutation =>
                mutation.type === "childList" ||
                mutation.type === "attributes" && mutation.attributeName === "class")) {
                applyFilter();
            }
        });
        observer.observe(grid, {
            subtree: true,
            childList: true,
            attributes: true,
            attributeFilter: ["class"]
        });

        applyFilter();
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
