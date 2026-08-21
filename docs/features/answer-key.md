# AnswerKey host presentation

## Purpose

`/Admin/Games/AnswerKey/{id}` is the private host answer presentation used to show the correct answer separately from the main game window.

Direct navigation to the AnswerKey route remains a normal server-rendered page. The multi-display feature changes only how the host opens that page from the game UI.

## Multi-display window placement

On the host Lobby/game page, `AnswerKeyWindowAssetsTagHelper` loads `answer-key-window.js`.

For an unmodified primary click on an AnswerKey link, the helper:

1. opens or reuses the named `badwolf-answer-key` browser window immediately from the click gesture;
2. keeps the existing AnswerKey URL unchanged;
3. checks whether the browser exposes the Window Management API;
4. skips the permission lookup when `screen.isExtended` explicitly reports that only one display is in use;
5. calls `getScreenDetails()` when multi-display placement may be available;
6. identifies the display containing the host game window from `currentScreen`;
7. selects a different detected display;
8. moves and resizes the AnswerKey window to that display's available work area using `availLeft`, `availTop`, `availWidth`, and `availHeight`.

Using a stable window name prevents repeated clicks from creating multiple AnswerKey windows. The existing window is navigated to the current AnswerKey URL, focused, and placed on the other display when possible.

## Compatibility and fallback

The implementation uses feature detection rather than browser-name detection.

The original AnswerKey links retain `target="_blank"` and `rel="noopener"`. If JavaScript does not run, normal browser behavior therefore remains available.

When the helper is active, it only prevents the link's default navigation after `window.open()` successfully returns a window handle. If the popup cannot be opened, the native link action is left untouched.

If any of the following applies, the already-open AnswerKey window is simply left where the browser placed it:

- the browser does not implement `getScreenDetails()`;
- only one display is available;
- Window Management permission is denied;
- screen details cannot be read;
- moving or resizing the window is rejected by the browser or operating system.

Modifier-clicks and non-primary mouse activations are not intercepted, so normal browser tab/window shortcuts continue to work.

## Security and permissions

Browsers may prompt the host for Window Management permission before exposing multi-display details. Permission handling remains entirely browser-controlled. BadWolfQuiz does not store display identifiers or screen geometry on the server.

## Validation coverage

Regression coverage verifies that:

- the Lobby model loads the dedicated AnswerKey window helper;
- the existing AnswerKey links keep their native `_blank` fallback;
- the named popup is created before the asynchronous screen-details lookup;
- unsupported/single-display environments return without blocking the popup;
- the helper compares against `currentScreen` and uses the target display's available bounds;
- move/resize failures are handled as a fallback rather than preventing AnswerKey access.
