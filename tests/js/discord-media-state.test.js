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
