const test = require("node:test");
const assert = require("node:assert/strict");
const DiscordMediaState = require("../../src/BadWolfQuiz.Web/wwwroot/js/discord-media-state.js");

test("mutes when the first media starts and unmutes when the last stops", () => {
    const transitions = [];
    const state = new DiscordMediaState(value => transitions.push(value));

    state.start("audio");
    state.start("video");
    state.stop("audio");
    state.stop("video");

    assert.deepEqual(transitions, [true, false]);
});

test("duplicate events are idempotent", () => {
    const transitions = [];
    const state = new DiscordMediaState(value => transitions.push(value));

    state.start("youtube");
    state.start("youtube");
    state.stop("youtube");
    state.stop("youtube");

    assert.deepEqual(transitions, [true, false]);
});

test("stopping one of two media items does not unmute early", () => {
    const transitions = [];
    const state = new DiscordMediaState(value => transitions.push(value));

    state.start("audio");
    state.start("youtube");
    state.stop("audio");

    assert.equal(state.isActive, true);
    assert.deepEqual(transitions, [true]);
});

test("a stop event for unknown media does not change the state", () => {
    const transitions = [];
    const state = new DiscordMediaState(value => transitions.push(value));

    state.stop("missing");

    assert.equal(state.isActive, false);
    assert.deepEqual(transitions, []);
});

test("media can start again after it stops", () => {
    const transitions = [];
    const state = new DiscordMediaState(value => transitions.push(value));

    state.start("video");
    state.stop("video");
    state.start("video");

    assert.equal(state.isActive, true);
    assert.deepEqual(transitions, [true, false, true]);
});

test("native media events map play and every cleanup event to state changes", () => {
    const handlers = new Map();
    const media = {
        addEventListener(name, handler) {
            handlers.set(name, handler);
        }
    };

    for (const stopEvent of ["pause", "ended", "error", "abort", "emptied"]) {
        const transitions = [];
        const state = new DiscordMediaState(value => transitions.push(value));
        DiscordMediaState.bindNativeMedia(media, state, "native");

        handlers.get("play")();
        handlers.get(stopEvent)();

        assert.deepEqual(transitions, [true, false], stopEvent);
    }
});

test("YouTube player states map playing, paused, ended, and cued", () => {
    assert.equal(DiscordMediaState.getYouTubePlaybackState(1), true);
    assert.equal(DiscordMediaState.getYouTubePlaybackState(2), false);
    assert.equal(DiscordMediaState.getYouTubePlaybackState(0), false);
    assert.equal(DiscordMediaState.getYouTubePlaybackState(5), false);
    assert.equal(DiscordMediaState.getYouTubePlaybackState(3), null);
    assert.equal(DiscordMediaState.getYouTubePlaybackState(-1), null);
});

test("quick-score question sync replaces stale round options and preserves a valid selection", () => {
    const clone = node => ({ ...node });
    const ownerDocument = { importNode: node => clone(node) };
    const currentSelect = {
        ownerDocument,
        value: "round-1-question",
        childNodes: [
            { value: "round-1-question", disabled: false }
        ],
        options: [
            { value: "round-1-question", disabled: false }
        ],
        replaceChildren(...nodes) {
            this.childNodes = nodes;
            this.options = nodes;
        }
    };
    const nextSelect = {
        childNodes: [
            { value: "round-1-question", disabled: false },
            { value: "round-2-question", disabled: false },
            { value: "round-3-question", disabled: false }
        ]
    };

    const synced = DiscordMediaState.syncQuickScoreQuestions(
        currentSelect,
        nextSelect);

    assert.equal(synced, true);
    assert.deepEqual(
        currentSelect.options.map(option => option.value),
        ["round-1-question", "round-2-question", "round-3-question"]);
    assert.equal(currentSelect.value, "round-1-question");
});
