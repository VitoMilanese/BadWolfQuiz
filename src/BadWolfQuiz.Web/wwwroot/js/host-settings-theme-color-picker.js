(() => {
    const root = document.querySelector("[data-custom-theme-colors]");
    const form = document.querySelector(".host-settings-form");
    if (!(root instanceof HTMLElement)) return;

    const colorInputs = Array.from(root.querySelectorAll("[data-theme-variable]"))
        .filter(input => input instanceof HTMLInputElement);
    if (colorInputs.length === 0) return;

    const clampColorChannel = value =>
        Math.max(0, Math.min(255, Math.round(value)));

    const parsePickerHex = value => {
        const match = /^#([0-9a-f]{6})$/i.exec(value || "");
        if (!match) return null;
        const number = Number.parseInt(match[1], 16);
        return {
            r: (number >> 16) & 255,
            g: (number >> 8) & 255,
            b: number & 255
        };
    };

    const pickerHex = rgb => `#${[rgb.r, rgb.g, rgb.b]
        .map(channel => clampColorChannel(channel)
            .toString(16)
            .padStart(2, "0"))
        .join("")}`.toUpperCase();

    const rgbToHsv = rgb => {
        const r = rgb.r / 255;
        const g = rgb.g / 255;
        const b = rgb.b / 255;
        const max = Math.max(r, g, b);
        const min = Math.min(r, g, b);
        const delta = max - min;
        let hue = 0;

        if (delta) {
            if (max === r) hue = 60 * (((g - b) / delta) % 6);
            else if (max === g) hue = 60 * (((b - r) / delta) + 2);
            else hue = 60 * (((r - g) / delta) + 4);
        }
        if (hue < 0) hue += 360;

        return {
            h: hue,
            s: max === 0 ? 0 : delta / max,
            v: max
        };
    };

    const hsvToRgb = hsv => {
        const chroma = hsv.v * hsv.s;
        const section = ((hsv.h % 360) + 360) % 360 / 60;
        const x = chroma * (1 - Math.abs((section % 2) - 1));
        const offset = hsv.v - chroma;
        let channels = [0, 0, 0];

        if (section < 1) channels = [chroma, x, 0];
        else if (section < 2) channels = [x, chroma, 0];
        else if (section < 3) channels = [0, chroma, x];
        else if (section < 4) channels = [0, x, chroma];
        else if (section < 5) channels = [x, 0, chroma];
        else channels = [chroma, 0, x];

        return {
            r: clampColorChannel((channels[0] + offset) * 255),
            g: clampColorChannel((channels[1] + offset) * 255),
            b: clampColorChannel((channels[2] + offset) * 255)
        };
    };

    let activePicker = null;

    const positionPicker = picker => {
        if (picker.popover.hidden) return;

        const triggerRect = picker.trigger.getBoundingClientRect();
        const margin = 8;
        const gap = 7;
        const width = Math.min(288, window.innerWidth - (margin * 2));
        picker.popover.style.width = `${width}px`;
        const height = picker.popover.offsetHeight;
        const left = Math.max(
            margin,
            Math.min(triggerRect.left, window.innerWidth - width - margin));
        let top = triggerRect.bottom + gap;

        if (top + height > window.innerHeight - margin &&
            triggerRect.top - height - gap >= margin) {
            top = triggerRect.top - height - gap;
        }

        picker.popover.style.left = `${left}px`;
        picker.popover.style.top = `${Math.max(margin, top)}px`;
    };

    const setPickerOpen = (picker, open) => {
        if (open && activePicker && activePicker !== picker) {
            activePicker.popover.hidden = true;
            activePicker.trigger.setAttribute("aria-expanded", "false");
        }

        picker.popover.hidden = !open;
        picker.trigger.setAttribute("aria-expanded", open ? "true" : "false");
        activePicker = open ? picker : (activePicker === picker ? null : activePicker);

        if (open) {
            requestAnimationFrame(() => positionPicker(picker));
        }
    };

    const createPicker = input => {
        const sourceLabel = input.closest("label");
        const labelText = sourceLabel?.querySelector(":scope > span");
        if (!(sourceLabel instanceof HTMLLabelElement) ||
            !(labelText instanceof HTMLElement)) {
            return null;
        }

        const field = document.createElement("div");
        field.className = "host-theme-color-field";
        sourceLabel.replaceWith(field);
        input.type = "hidden";

        const trigger = document.createElement("button");
        trigger.type = "button";
        trigger.className = "host-theme-color-picker-trigger";
        trigger.setAttribute("aria-haspopup", "true");
        trigger.setAttribute("aria-expanded", "false");
        trigger.setAttribute("aria-label", labelText.textContent?.trim() || "Color");

        const swatch = document.createElement("span");
        swatch.className = "host-theme-color-picker-swatch";
        swatch.setAttribute("aria-hidden", "true");
        const output = document.createElement("output");
        trigger.append(swatch, output);

        const popover = document.createElement("div");
        popover.className = "host-theme-color-picker-popover";
        popover.hidden = true;

        const spectrum = document.createElement("div");
        spectrum.className = "host-theme-color-picker-spectrum";
        spectrum.tabIndex = 0;
        spectrum.setAttribute("aria-label", labelText.textContent?.trim() || "Color");
        const spectrumThumb = document.createElement("span");
        spectrumThumb.className = "host-theme-color-picker-spectrum-thumb";
        spectrumThumb.setAttribute("aria-hidden", "true");
        spectrum.append(spectrumThumb);

        const hueRow = document.createElement("div");
        hueRow.className = "host-theme-color-picker-hue-row";
        const hueLabel = document.createElement("span");
        hueLabel.textContent = "H";
        const hue = document.createElement("input");
        hue.type = "range";
        hue.min = "0";
        hue.max = "360";
        hue.step = "1";
        hue.setAttribute("aria-label", `${labelText.textContent?.trim() || "Color"} hue`);
        hueRow.append(hueLabel, hue);

        const rgbGrid = document.createElement("div");
        rgbGrid.className = "host-theme-color-picker-rgb-grid";
        const channels = ["R", "G", "B"].map(channelName => {
            const channel = document.createElement("div");
            channel.className = "host-theme-color-channel";
            const channelLabel = document.createElement("span");
            channelLabel.textContent = channelName;
            const channelInput = document.createElement("input");
            channelInput.type = "number";
            channelInput.min = "0";
            channelInput.max = "255";
            channelInput.inputMode = "numeric";
            channelInput.setAttribute(
                "aria-label",
                `${labelText.textContent?.trim() || "Color"} ${channelName}`);
            channel.append(channelLabel, channelInput);
            rgbGrid.append(channel);
            return channelInput;
        });

        popover.append(spectrum, hueRow, rgbGrid);
        document.body.append(popover);
        field.append(input, trigger, labelText);

        let state = rgbToHsv(parsePickerHex(input.value) || { r: 0, g: 0, b: 0 });

        const render = (rgb, notify) => {
            state = rgbToHsv(rgb);
            const hex = pickerHex(rgb);
            input.value = hex;
            swatch.style.backgroundColor = hex;
            output.textContent = hex;
            spectrum.style.setProperty("--category-picker-hue", String(state.h));
            spectrumThumb.style.left = `${state.s * 100}%`;
            spectrumThumb.style.top = `${(1 - state.v) * 100}%`;
            hue.value = String(Math.round(state.h));
            channels[0].value = String(rgb.r);
            channels[1].value = String(rgb.g);
            channels[2].value = String(rgb.b);

            if (notify) {
                input.dispatchEvent(new Event("input", { bubbles: true }));
            }
        };

        const renderFromState = notify => render(hsvToRgb(state), notify);
        const updateSpectrumFromPointer = event => {
            const rect = spectrum.getBoundingClientRect();
            state.s = Math.max(0, Math.min(1, (event.clientX - rect.left) / rect.width));
            state.v = 1 - Math.max(0, Math.min(1, (event.clientY - rect.top) / rect.height));
            renderFromState(true);
        };

        const picker = { trigger, popover };

        trigger.addEventListener("click", () => {
            setPickerOpen(picker, popover.hidden);
        });

        spectrum.addEventListener("pointerdown", event => {
            event.preventDefault();
            spectrum.setPointerCapture?.(event.pointerId);
            updateSpectrumFromPointer(event);
        });
        spectrum.addEventListener("pointermove", event => {
            if (spectrum.hasPointerCapture?.(event.pointerId)) {
                updateSpectrumFromPointer(event);
            }
        });

        hue.addEventListener("input", () => {
            state.h = Number(hue.value) || 0;
            renderFromState(true);
        });

        channels.forEach(channel => {
            channel.addEventListener("input", () => {
                const values = channels.map(item => Number(item.value));
                if (values.some(value => !Number.isFinite(value))) return;
                render({
                    r: clampColorChannel(values[0]),
                    g: clampColorChannel(values[1]),
                    b: clampColorChannel(values[2])
                }, true);
            });
        });

        render(parsePickerHex(input.value) || { r: 0, g: 0, b: 0 }, false);
        return picker;
    };

    colorInputs.map(createPicker).filter(Boolean);

    document.addEventListener("pointerdown", event => {
        if (!activePicker ||
            activePicker.popover.contains(event.target) ||
            activePicker.trigger.contains(event.target)) {
            return;
        }
        setPickerOpen(activePicker, false);
    });

    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && activePicker) {
            event.preventDefault();
            event.stopImmediatePropagation();
            const trigger = activePicker.trigger;
            setPickerOpen(activePicker, false);
            trigger.focus();
            return;
        }

        const isSaveKey =
            event.code === "KeyS" ||
            (event.key || "").toLowerCase() === "s";
        const hasSaveModifier = event.ctrlKey || event.metaKey;
        if (!isSaveKey || !hasSaveModifier || event.altKey) return;

        event.preventDefault();
        event.stopImmediatePropagation();
        if (activePicker) setPickerOpen(activePicker, false);
        if (form instanceof HTMLFormElement) form.requestSubmit();
    }, { capture: true });

    window.addEventListener("resize", () => {
        if (activePicker) positionPicker(activePicker);
    });
    window.addEventListener("scroll", () => {
        if (activePicker) positionPicker(activePicker);
    }, true);
})();
