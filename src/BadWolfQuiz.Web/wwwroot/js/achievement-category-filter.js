(() => {
    const initializedScopes = new WeakSet();

    const initializeScope = scope => {
        if (!(scope instanceof Element) || initializedScopes.has(scope)) {
            return;
        }

        const select = scope.querySelector("[data-achievement-category-filter]");
        const grid = scope.querySelector(".player-achievements-grid");
        const count = scope.querySelector("[data-achievement-visible-count]");
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

        initializedScopes.add(scope);
        applyFilter();
    };

    const initialize = root => {
        if (root instanceof Element && root.matches("[data-achievement-category-filter-scope]")) {
            initializeScope(root);
        }

        if (root && typeof root.querySelectorAll === "function") {
            for (const scope of root.querySelectorAll("[data-achievement-category-filter-scope]")) {
                initializeScope(scope);
            }
        }
    };

    const start = () => {
        initialize(document);

        const observer = new MutationObserver(mutations => {
            for (const mutation of mutations) {
                for (const node of mutation.addedNodes) {
                    if (node instanceof Element) {
                        initialize(node);
                    }
                }
            }
        });
        observer.observe(document.body, { childList: true, subtree: true });
    };

    window.BadWolfAchievementCategoryFilter = { initialize };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", start, { once: true });
    } else {
        start();
    }
})();
