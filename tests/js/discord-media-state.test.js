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
