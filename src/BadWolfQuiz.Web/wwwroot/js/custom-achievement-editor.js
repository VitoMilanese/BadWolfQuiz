(() => {
    const form = document.querySelector("form[data-ajax-custom-achievement-editor]");
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    const status = document.querySelector("[data-custom-achievement-save-status]");
    const saveButton = form.querySelector("[data-custom-achievement-save-button]");
    const newButton = form.querySelector("[data-custom-achievement-new-button]");
    const idInput = form.querySelector('input[name="Input.Id"]');
    const nameInput = form.querySelector('input[name="Input.Name"]');
    const descriptionInput = form.querySelector('textarea[name="Input.Description"]');
    const targetInput = form.querySelector('input[name="Input.Target"]');
    const tagEditor = form.querySelector("[data-question-tags-editor]");
    const editorKicker = document.querySelector("[data-custom-achievement-editor-kicker]");
    const editorTitle = document.querySelector("[data-custom-achievement-editor-title]");
    const artworkInput = form.querySelector("[data-custom-achievement-artwork-input]");
    const artworkPreview = form.querySelector("[data-custom-achievement-artwork-preview]");
    const artworkPlaceholder = form.querySelector("[data-custom-achievement-artwork-placeholder]");
    const artworkKeepHint = form.querySelector("[data-custom-achievement-artwork-keep-hint]");
    const deleteDialog = document.querySelector("[data-custom-achievement-delete-dialog]");
    const deleteConfirmButton = deleteDialog?.querySelector("[data-custom-achievement-delete-confirm]");
    let pendingDeleteForm = null;
    let statusTimeout = 0;
    let saveInProgress = false;
    let busyDelayHandle = 0;
    let busyOwned = false;

    const setStatus = (message, success) => {
        if (!status) return;
        window.clearTimeout(statusTimeout);
        status.textContent = message;
        status.hidden = false;
        status.classList.toggle("alert-success", success);
        status.classList.toggle("alert-error", !success);
        if (success) {
            statusTimeout = window.setTimeout(() => {
                status.hidden = true;
            }, 4000);
        }
    };

    const scheduleBusy = () => {
        busyDelayHandle = window.setTimeout(() => {
            busyDelayHandle = 0;
            busyOwned = window.BadWolfBusy?.show?.() === true;
        }, 180);
    };

    const stopBusy = () => {
        if (busyDelayHandle) {
            window.clearTimeout(busyDelayHandle);
            busyDelayHandle = 0;
        }
        if (busyOwned) {
            busyOwned = false;
            window.BadWolfBusy?.hide?.();
        }
    };

    const setTags = tags => {
        tagEditor?.dispatchEvent(new CustomEvent("custom-achievement:set-tags", {
            detail: { tags }
        }));
    };

    const setSelectedCard = id => {
        document.querySelectorAll("[data-custom-achievement-card]").forEach(card => {
            card.classList.toggle("is-selected", Boolean(id) && card.dataset.achievementId === id);
        });
    };

    const applyEditorMode = ({ editing, id = "", name = "", description = "", target = 1, tags = [], artworkUrl = "", editUrl = "" }) => {
        if (idInput) idInput.value = id;
        if (nameInput) nameInput.value = name;
        if (descriptionInput) descriptionInput.value = description;
        if (targetInput) targetInput.value = String(target || 1);
        setTags(tags);
        if (artworkInput) artworkInput.value = "";

        if (editorKicker) {
            editorKicker.textContent = editing ? form.dataset.editKicker : form.dataset.createKicker;
        }
        if (editorTitle) {
            editorTitle.textContent = editing ? form.dataset.editTitle : form.dataset.createTitle;
        }
        if (saveButton) {
            saveButton.textContent = editing ? form.dataset.saveLabel : form.dataset.addLabel;
        }
        if (newButton) {
            newButton.hidden = !editing;
        }
        if (artworkKeepHint) {
            artworkKeepHint.hidden = !editing;
        }
        if (artworkPreview) {
            if (editing && artworkUrl) {
                artworkPreview.src = artworkUrl;
                artworkPreview.hidden = false;
            } else {
                artworkPreview.removeAttribute("src");
                artworkPreview.hidden = true;
            }
        }
        if (artworkPlaceholder) {
            artworkPlaceholder.hidden = editing;
        }

        setSelectedCard(editing ? id : "");
        if (status) status.hidden = true;

        const targetUrl = editing && editUrl
            ? editUrl
            : (newButton?.href || window.location.pathname);
        window.history.replaceState(null, "", targetUrl);
        nameInput?.focus();
    };

    const enterCreateMode = () => {
        applyEditorMode({ editing: false });
    };

    const enterEditMode = editLink => {
        const card = editLink.closest("[data-custom-achievement-card]");
        if (!(card instanceof HTMLElement)) return;
        const tags = [...card.querySelectorAll(".custom-achievement-tag-list > span")]
            .map(item => item.textContent?.trim())
            .filter(Boolean);
        applyEditorMode({
            editing: true,
            id: card.dataset.achievementId || "",
            name: card.querySelector(".custom-achievement-list-title strong")?.textContent?.trim() || "",
            description: card.querySelector(".custom-achievement-list-copy > p")?.textContent?.trim() || "",
            target: Number.parseInt(card.dataset.achievementTarget || "1", 10) || 1,
            tags,
            artworkUrl: card.querySelector(".custom-achievement-list-artwork img")?.src || "",
            editUrl: editLink.href
        });
    };

    const createDeleteForm = result => {
        const deleteForm = document.createElement("form");
        deleteForm.method = "post";
        deleteForm.action = result.deleteUrl;
        deleteForm.dataset.customAchievementDeleteForm = "";
        const token = form.querySelector('input[name="__RequestVerificationToken"]');
        if (token) {
            const tokenCopy = document.createElement("input");
            tokenCopy.type = "hidden";
            tokenCopy.name = token.name;
            tokenCopy.value = token.value;
            deleteForm.appendChild(tokenCopy);
        }
        const button = document.createElement("button");
        button.type = "submit";
        button.className = "button button-danger";
        button.textContent = result.deleteLabel;
        deleteForm.appendChild(button);
        return deleteForm;
    };

    const buildCard = result => {
        const card = document.createElement("article");
        card.className = "custom-achievement-list-card is-selected";
        card.dataset.customAchievementCard = "";
        card.dataset.achievementId = result.id;
        card.dataset.achievementTarget = String(result.target);
        card.innerHTML = `
            <div class="custom-achievement-list-artwork"><img alt="" aria-hidden="true" loading="lazy" decoding="async"></div>
            <div class="custom-achievement-list-copy">
                <div class="custom-achievement-list-title"><strong></strong></div>
                <p></p>
                <div class="custom-achievement-list-meta"><span></span><span></span></div>
                <div class="custom-achievement-tag-list"></div>
            </div>
            <div class="custom-achievement-list-actions"><a class="button button-secondary" data-custom-achievement-edit></a></div>`;
        card.querySelector(".custom-achievement-list-actions")?.appendChild(createDeleteForm(result));
        return card;
    };

    const updateCard = result => {
        const panel = document.querySelector("[data-custom-achievement-list-panel]");
        if (!panel) return;
        let list = panel.querySelector("[data-custom-achievement-list]");
        if (!list) {
            panel.querySelector("[data-custom-achievement-empty]")?.remove();
            list = document.createElement("div");
            list.className = "custom-achievement-list";
            list.dataset.customAchievementList = "";
            panel.appendChild(list);
        }
        for (const item of list.querySelectorAll("[data-custom-achievement-card]")) {
            item.classList.remove("is-selected");
        }
        let card = [...list.querySelectorAll("[data-custom-achievement-card]")]
            .find(item => item.dataset.achievementId === result.id);
        if (!card) {
            card = buildCard(result);
            list.appendChild(card);
        } else {
            card.classList.add("is-selected");
        }
        card.dataset.achievementTarget = String(result.target);
        const image = card.querySelector(".custom-achievement-list-artwork img");
        if (image) image.src = result.artworkUrl;
        const name = card.querySelector(".custom-achievement-list-title strong");
        if (name) name.textContent = result.name;
        const description = card.querySelector(".custom-achievement-list-copy > p");
        if (description) description.textContent = result.description;
        const meta = card.querySelectorAll(".custom-achievement-list-meta > span");
        if (meta[0]) meta[0].textContent = result.correctCountLabel;
        if (meta[1]) meta[1].textContent = result.tagCountLabel;
        const tagList = card.querySelector(".custom-achievement-tag-list");
        if (tagList) {
            tagList.replaceChildren();
            for (const tag of result.tags ?? []) {
                const chip = document.createElement("span");
                chip.textContent = tag;
                tagList.appendChild(chip);
            }
        }
        const edit = card.querySelector("[data-custom-achievement-edit]");
        if (edit) {
            edit.href = result.editUrl;
            edit.textContent = result.editLabel;
        }
        const cards = [...list.querySelectorAll("[data-custom-achievement-card]")]
            .sort((left, right) => {
                const leftName = left.querySelector(".custom-achievement-list-title strong")?.textContent ?? "";
                const rightName = right.querySelector(".custom-achievement-list-title strong")?.textContent ?? "";
                return leftName.localeCompare(rightName, undefined, { sensitivity: "base" });
            });
        for (const item of cards) list.appendChild(item);
    };

    const applySavedState = result => {
        if (idInput) idInput.value = result.id;
        if (editorKicker) editorKicker.textContent = result.editorKicker;
        if (editorTitle) editorTitle.textContent = result.editorTitle;
        if (saveButton) saveButton.textContent = result.saveLabel;
        if (newButton) newButton.hidden = false;
        if (artworkInput) artworkInput.value = "";
        if (artworkPreview) {
            artworkPreview.src = result.artworkUrl;
            artworkPreview.hidden = false;
        }
        if (artworkPlaceholder) artworkPlaceholder.hidden = true;
        if (artworkKeepHint) artworkKeepHint.hidden = false;
        document.querySelectorAll("[data-custom-achievement-total]").forEach(element => {
            element.textContent = String(result.totalCount).padStart(2, "0");
        });
        updateCard(result);
        if (result.editUrl) window.history.replaceState(null, "", result.editUrl);
    };

    newButton?.addEventListener("click", event => {
        event.preventDefault();
        enterCreateMode();
    });

    document.addEventListener("click", event => {
        const editLink = event.target.closest?.("[data-custom-achievement-edit]");
        if (!(editLink instanceof HTMLAnchorElement)) return;
        event.preventDefault();
        enterEditMode(editLink);
    });

    document.addEventListener("submit", event => {
        const deleteForm = event.target.closest?.("form[data-custom-achievement-delete-form]");
        if (!(deleteForm instanceof HTMLFormElement)) return;
        event.preventDefault();
        pendingDeleteForm = deleteForm;
        if (deleteDialog instanceof HTMLDialogElement && !deleteDialog.open) {
            deleteDialog.showModal();
        }
    });

    deleteConfirmButton?.addEventListener("click", () => {
        const deleteForm = pendingDeleteForm;
        pendingDeleteForm = null;
        if (deleteDialog instanceof HTMLDialogElement && deleteDialog.open) {
            deleteDialog.close();
        }
        deleteForm?.submit();
    });

    deleteDialog?.addEventListener("close", () => {
        pendingDeleteForm = null;
    });

    form.addEventListener("submit", async event => {
        event.preventDefault();
        if (saveInProgress) return;
        saveInProgress = true;
        const submitter = event.submitter instanceof HTMLElement ? event.submitter : saveButton;
        submitter?.setAttribute("disabled", "disabled");
        if (status) status.hidden = true;
        scheduleBusy();
        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            const contentType = response.headers.get("content-type") ?? "";
            const result = contentType.includes("application/json") ? await response.json() : null;
            if (!response.ok || !result?.success) {
                throw new Error(result?.error || form.dataset.saveError);
            }
            applySavedState(result);
            setStatus(result.message, true);
        } catch (error) {
            setStatus(error?.message || form.dataset.saveError, false);
        } finally {
            stopBusy();
            submitter?.removeAttribute("disabled");
            saveInProgress = false;
        }
    });
})();
