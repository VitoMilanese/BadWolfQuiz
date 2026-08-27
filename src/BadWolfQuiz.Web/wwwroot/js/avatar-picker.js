(() => {
    for (const dialog of document.querySelectorAll("[data-avatar-picker]")) {
        const categoryStep = dialog.querySelector("[data-avatar-category-step]");
        const listStep = dialog.querySelector("[data-avatar-list-step]");
        const options = dialog.querySelector("[data-avatar-options]");

        const showCategoryStep = () => {
            categoryStep.hidden = false;
            listStep.hidden = true;
            options.replaceChildren();
        };

        for (const opener of document.querySelectorAll("[data-open-avatar-picker]")) {
            opener.addEventListener("click", () => {
                showCategoryStep();
                dialog.showModal();
            });
        }

        dialog.querySelector("[data-avatar-close]")
            ?.addEventListener("click", () => dialog.close());
        dialog.querySelector("[data-avatar-back]")
            ?.addEventListener("click", showCategoryStep);

        for (const categoryButton of dialog.querySelectorAll("[data-avatar-category]")) {
            categoryButton.addEventListener("click", () => {
                const template = dialog.querySelector(
                    `[data-avatar-options-template="${categoryButton.dataset.avatarTemplate}"]`
                );
                options.replaceChildren();

                if (template) {
                    options.append(template.content.cloneNode(true));
                }

                for (const button of options.querySelectorAll("[data-avatar-id]")) {
                    button.addEventListener("click", () => {
                        dialog.dispatchEvent(new CustomEvent("avatarselected", {
                            detail: {
                                avatarId: button.dataset.avatarId,
                                avatarUrl: button.dataset.avatarUrl,
                                avatarFrameEligible:
                                    button.dataset.avatarFrameEligible === "true"
                            },
                            bubbles: true
                        }));
                        dialog.close();
                    });
                }

                categoryStep.hidden = true;
                listStep.hidden = false;
            });
        }

        dialog.addEventListener("click", event => {
            if (event.target === dialog) {
                dialog.close();
            }
        });
    }
})();
