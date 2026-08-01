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
                const avatarOptions = JSON.parse(
                    categoryButton.dataset.avatarOptions || "[]"
                );
                options.replaceChildren();

                for (const avatarOption of avatarOptions) {
                    const avatarId = avatarOption.Id;
                    const avatarUrl =
                        `/avatars/${avatarId}?v=${encodeURIComponent(avatarOption.Version)}`;
                    const button = document.createElement("button");
                    button.type = "button";
                    button.className = "avatar-option";
                    button.dataset.avatarId = avatarId;

                    const image = document.createElement("img");
                    image.src = avatarUrl;
                    image.alt = "";
                    button.append(image);
                    button.addEventListener("click", () => {
                        dialog.dispatchEvent(new CustomEvent("avatarselected", {
                            detail: { avatarId, avatarUrl },
                            bubbles: true
                        }));
                        dialog.close();
                    });
                    options.append(button);
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
