((root, factory) => {
    const MediaState = factory();
    if (typeof module === "object" && module.exports) {
        module.exports = MediaState;
    } else {
        root.BadWolfDiscordMediaState = MediaState;
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
    });
