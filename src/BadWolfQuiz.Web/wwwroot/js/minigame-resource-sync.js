(() => {
    const root = document.querySelector("[data-minigame-resource-sync]");
    if (!root) return;

    const syncButton = root.querySelector("[data-minigame-resource-sync-start]");
    const status = root.querySelector("[data-minigame-resource-sync-status]");
    const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value ?? "";
    const dialog = document.querySelector("[data-minigame-resource-cleanup-dialog]");
    const list = dialog?.querySelector("[data-minigame-resource-cleanup-list]");
    const previousButton = dialog?.querySelector("[data-minigame-resource-cleanup-previous]");
    const nextButton = dialog?.querySelector("[data-minigame-resource-cleanup-next]");
    const pageLabel = dialog?.querySelector("[data-minigame-resource-cleanup-page]");
    const deleteButton = dialog?.querySelector("[data-minigame-resource-cleanup-delete]");
    const keepButton = dialog?.querySelector("[data-minigame-resource-cleanup-keep]");
    const pageSize = 10;
    let missingGames = [];
    let currentPage = 0;
    const selectedIds = new Set();

    const setStatus = (message, isError = false) => {
        if (!status) return;
        status.textContent = message ?? "";
        status.classList.toggle("is-error", isError);
    };

    const setBusy = (busy) => {
        if (syncButton) syncButton.disabled = busy;
        if (deleteButton) deleteButton.disabled = busy || selectedIds.size === 0;
        if (keepButton) keepButton.disabled = busy;
        if (previousButton) previousButton.disabled = busy || currentPage <= 0;
        if (nextButton) {
            const pageCount = Math.max(1, Math.ceil(missingGames.length / pageSize));
            nextButton.disabled = busy || currentPage >= pageCount - 1;
        }
    };

    const post = async (url, values = []) => {
        const body = new FormData();
        body.append("__RequestVerificationToken", token);
        values.forEach(([name, value]) => body.append(name, value));

        window.BadWolfBusy?.show?.();
        try {
            const response = await fetch(url, {
                method: "POST",
                body,
                headers: { Accept: "application/json" }
            });
            const payload = await response.json().catch(() => ({}));
            if (!response.ok || payload.success === false) {
                throw new Error(payload.message || root.dataset.unexpectedError || "Request failed.");
            }
            return payload;
        } finally {
            window.BadWolfBusy?.hide?.();
        }
    };

    const renderPage = () => {
        if (!dialog || !list) return;
        const pageCount = Math.max(1, Math.ceil(missingGames.length / pageSize));
        currentPage = Math.min(Math.max(currentPage, 0), pageCount - 1);
        const start = currentPage * pageSize;
        const pageItems = missingGames.slice(start, start + pageSize);

        list.replaceChildren();
        pageItems.forEach((game) => {
            const row = document.createElement("label");
            row.className = "minigame-resource-cleanup-row";

            const checkbox = document.createElement("input");
            checkbox.type = "checkbox";
            checkbox.checked = selectedIds.has(game.id);
            checkbox.addEventListener("change", () => {
                if (checkbox.checked) selectedIds.add(game.id);
                else selectedIds.delete(game.id);
                setBusy(false);
            });

            const name = document.createElement("span");
            name.textContent = game.name;
            row.append(checkbox, name);
            list.append(row);
        });

        if (pageLabel) pageLabel.textContent = `${currentPage + 1} / ${pageCount}`;
        if (previousButton) previousButton.disabled = currentPage <= 0;
        if (nextButton) nextButton.disabled = currentPage >= pageCount - 1;
        if (deleteButton) deleteButton.disabled = selectedIds.size === 0;
    };

    syncButton?.addEventListener("click", async () => {
        setBusy(true);
        setStatus(root.dataset.syncing ?? "");
        try {
            const payload = await post(root.dataset.syncUrl);
            setStatus(payload.message ?? "");
            missingGames = Array.isArray(payload.missingGames) ? payload.missingGames : [];
            selectedIds.clear();
            currentPage = 0;

            if (missingGames.length === 0 || !dialog) {
                window.location.reload();
                return;
            }

            renderPage();
            dialog.showModal();
        } catch (error) {
            setStatus(error instanceof Error ? error.message : String(error), true);
        } finally {
            setBusy(false);
        }
    });

    previousButton?.addEventListener("click", () => {
        currentPage--;
        renderPage();
    });

    nextButton?.addEventListener("click", () => {
        currentPage++;
        renderPage();
    });

    keepButton?.addEventListener("click", () => {
        dialog?.close();
        window.location.reload();
    });

    deleteButton?.addEventListener("click", async () => {
        if (selectedIds.size === 0) return;
        if (!window.confirm(root.dataset.deleteConfirm ?? "")) return;

        setBusy(true);
        try {
            const values = Array.from(selectedIds, (id) => ["gameIds", String(id)]);
            const payload = await post(root.dataset.deleteUrl, values);
            setStatus(payload.message ?? "");
            dialog?.close();
            window.location.reload();
        } catch (error) {
            setStatus(error instanceof Error ? error.message : String(error), true);
            setBusy(false);
        }
    });

    dialog?.addEventListener("cancel", (event) => {
        event.preventDefault();
        dialog.close();
        window.location.reload();
    });
})();
