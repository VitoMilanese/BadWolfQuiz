(() => {
    for (const dialog of document.querySelectorAll("[data-avatar-picker]")) {
        const genderStep = dialog.querySelector("[data-avatar-gender-step]");
        const listStep = dialog.querySelector("[data-avatar-list-step]");
        const options = dialog.querySelector("[data-avatar-options]");

        const showGenderStep = () => {
            genderStep.hidden = false;
            listStep.hidden = true;
            options.replaceChildren();
        };

        for (const opener of document.querySelectorAll("[data-open-avatar-picker]")) {
            opener.addEventListener("click", () => {
                showGenderStep();
                dialog.showModal();
            });
        }

        dialog.querySelector("[data-avatar-close]")
            ?.addEventListener("click", () => dialog.close());
        dialog.querySelector("[data-avatar-back]")
            ?.addEventListener("click", showGenderStep);

        for (const genderButton of dialog.querySelectorAll("[data-avatar-gender]")) {
            genderButton.addEventListener("click", () => {
                const gender = genderButton.dataset.avatarGender;
                options.replaceChildren();

                for (let number = 1; number <= 60; number += 1) {
                    const avatarId = `${gender}/${number}.png`;
                    const button = document.createElement("button");
                    button.type = "button";
                    button.className = "avatar-option";
                    button.dataset.avatarId = avatarId;

                    const image = document.createElement("img");
                    image.src = `/avatars/${avatarId}`;
                    image.alt = "";
                    button.append(image);
                    button.addEventListener("click", () => {
                        dialog.dispatchEvent(new CustomEvent("avatarselected", {
                            detail: { avatarId },
                            bubbles: true
                        }));
                        dialog.close();
                    });
                    options.append(button);
                }

                genderStep.hidden = true;
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
