(() => {
    const script = document.currentScript;
    const editLabel = script?.dataset.editLabel || "Edit";
    const roundLabel = script?.dataset.roundLabel || "Round";
    const categoryLabel = script?.dataset.categoryLabel || "Category";

    function createLink(href, label, iconOnly = false) {
        const link = document.createElement("a");
        link.href = href;
        link.className = iconOnly ? "button button-secondary icon-button" : "button button-secondary";
        link.textContent = iconOnly ? "📝" : `${editLabel} 📝`;
        link.title = `${editLabel}: ${label}`;
        link.setAttribute("aria-label", `${editLabel}: ${label}`);
        link.addEventListener("click", event => event.stopPropagation());
        return link;
    }

    function initialize() {
        const roundIdInput = document.querySelector('input[name="RoundRows.RoundId"]');
        if (!roundIdInput) return;

        const roundId = roundIdInput.value;
        if (roundId) {
            const roundSummary = document.querySelector(".round-settings-disclosure > summary");
            const renameButton = roundSummary?.querySelector('[onclick*="openRenameRoundDialog"]');
            if (roundSummary && renameButton && !roundSummary.querySelector("[data-edit-round-description]")) {
                const link = createLink(`/Admin/Quizzes/DescriptionEditor?roundId=${encodeURIComponent(roundId)}`, roundLabel);
                link.dataset.editRoundDescription = "";
                roundSummary.insertBefore(link, renameButton.nextSibling);
            }
        }

        document.querySelectorAll(".quiz-board-category-column[data-category-id]").forEach(column => {
            const categoryId = column.dataset.categoryId;
            const actions = column.querySelector(".category-actions");
            if (!categoryId || !actions || actions.querySelector("[data-edit-category-description]")) return;

            const link = createLink(`/Admin/Quizzes/DescriptionEditor?categoryId=${encodeURIComponent(categoryId)}`, categoryLabel, true);
            link.dataset.editCategoryDescription = "";
            actions.appendChild(link);
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
