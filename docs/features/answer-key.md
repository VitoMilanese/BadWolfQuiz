# AnswerKey host presentation

## Purpose

`/Admin/Games/AnswerKey/{id}` is the private host answer presentation used to show the correct answer separately from the main game window.

Direct navigation to the AnswerKey route remains a normal server-rendered page. The multi-display feature changes only how the host opens that page from the game UI.

## Multi-display window placement

On the host Lobby/game page, `AnswerKeyWindowAssetsTagHelper` loads `answer-key-window.js`.

For an unmodified primary click on an AnswerKey link, the helper:

1. keeps the existing AnswerKey URL unchanged;
2. uses feature detection rather than browser-name checks;
3. keeps unsupported browsers and known-denied Window Management permission on the native `target="_blank"` path;
4. obtains `ScreenDetails` before opening a managed multi-display popup;
5. identifies the display containing the host game window from `currentScreen`;
6. selects a different detected display;
7. converts that display's `availLeft`, `availTop`, `availWidth`, and `availHeight` values into `left`, `top`, `width`, and `height` `window.open()` features;
8. opens or reuses the named `badwolf-answer-key` window with those coordinates so the browser performs the cross-display placement at popup creation time;
9. also applies `moveTo()` and `resizeTo()` as a secondary reinforcement for a reused named window.

When Window Management permission is already granted, screen details are preloaded so later host clicks can resolve placement with minimal delay. When permission is still in the prompt state, the browser may request it from the host before the popup is created.

Using a stable window name prevents repeated clicks from creating multiple AnswerKey windows. The existing window is navigated to the current AnswerKey URL and focused.

## Compatibility and fallback

The original AnswerKey links retain `target="_blank"` and `rel="noopener"`. If JavaScript does not run, the Window Management API is unavailable, or permission is already denied, normal browser behavior remains available.

If Window Management is attempted but no second display can be resolved, the helper opens the same named AnswerKey window without display coordinates. If coordinate placement is accepted, the target display bounds are passed directly to `window.open()`; `moveTo()` and `resizeTo()` are not the primary placement mechanism.

Modifier-clicks and non-primary mouse activations are not intercepted, so normal browser tab/window shortcuts continue to work.

## Security and permissions

Browsers may prompt the host for Window Management permission before exposing multi-display details. Permission handling remains entirely browser-controlled. BadWolfQuiz does not store display identifiers or screen geometry on the server.

## Validation coverage

Regression coverage verifies that:

- the Lobby model loads the dedicated AnswerKey window helper with a cache-busted asset URL;
- the existing AnswerKey links keep their native `_blank` fallback;
- screen details and the target display are resolved before the managed popup is opened;
- target display coordinates are included in the `window.open()` feature string;
- the helper compares against `currentScreen` and uses the target display's available bounds;
- move/resize remains a secondary reinforcement rather than the primary placement mechanism;
- unsupported and denied-permission environments keep native browser behavior.
