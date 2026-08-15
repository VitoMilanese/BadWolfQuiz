((root, factory) => {
    const MediaState = factory();
    if (typeof module === "object" && module.exports) {
        module.exports = MediaState;
    } else {
        root.BadWolfDiscordMediaState = MediaState;
        MediaState.bindQuickScoreQuestionRefresh(root);
    }
})(typeof globalThis !== "undefined" ? globalThis : window, () =>
    class DiscordMediaState {
        constructor(onChange) {
            this.active = new Set();
            this.onChange = onChange;
        }

        start(key) {
            const wasActive = this.active.size > 0;
            this.active.add(key);
            if (!wasActive) {
                this.onChange(true);
            }
        }

        stop(key) {
            const wasActive = this.active.size > 0;
            this.active.delete(key);
            if (wasActive && this.active.size === 0) {
                this.onChange(false);
            }
        }

        get isActive() {
            return this.active.size > 0;
        }

        static bindNativeMedia(media, state, key = media) {
            media.addEventListener("play", () => state.start(key));
            ["pause", "ended", "error", "abort", "emptied"]
                .forEach(name => media.addEventListener(name, () => state.stop(key)));
        }

        static getYouTubePlaybackState(playerState) {
            if (playerState === 1) {
                return true;
            }
            if (playerState === 0 || playerState === 2 || playerState === 5) {
                return false;
            }
            return null;
        }

        static syncQuickScoreQuestions(currentSelect, nextSelect) {
            if (!currentSelect || !nextSelect) {
                return false;
            }

            const selectedValue = currentSelect.value;
            currentSelect.replaceChildren(
                ...Array.from(nextSelect.childNodes, node =>
                    currentSelect.ownerDocument.importNode(node, true)));

            const selectedOption = Array.from(currentSelect.options)
                .find(option => option.value === selectedValue && !option.disabled);
            if (selectedOption) {
                currentSelect.value = selectedValue;
            }

            return true;
        }

        static bindQuickScoreQuestionRefresh(root) {
            const document = root?.document;
            if (!document || typeof root.fetch !== "function" || typeof root.DOMParser !== "function") {
                return;
            }

            let refreshSequence = 0;
            document.addEventListener("badwolf:host-gameplay-updated", async () => {
                const currentSelect = document.querySelector("[data-quick-score-question]");
                if (!currentSelect) {
                    return;
                }

                const requestId = ++refreshSequence;
                try {
                    const response = await root.fetch(root.location.href, {
                        method: "GET",
                        headers: { "X-Requested-With": "XMLHttpRequest" },
                        cache: "no-store"
                    });
                    if (!response.ok) {
                        return;
                    }

                    const markup = await response.text();
                    if (requestId !== refreshSequence) {
                        return;
                    }

                    const parsed = new root.DOMParser().parseFromString(markup, "text/html");
                    const nextSelect = parsed.querySelector("[data-quick-score-question]");
                    DiscordMediaState.syncQuickScoreQuestions(currentSelect, nextSelect);
                } catch (error) {
                    console.error("Quick-score question refresh failed.", error);
                }
            });
        }
    });
