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

## Answer visibility mode

The shared topbar always shows an answer-visibility toggle beside **Correct answer**, including before any question has been opened.

The toggle represents a presentation mode for the whole AnswerKey window rather than a one-question-only state:

- the normal eye means answers are currently hidden;
- clicking the eye enables visible-answer mode and changes the icon to a crossed-out eye;
- the selected mode is stored in `sessionStorage` for the current game code;
- SignalR-driven AnswerKey reloads restore the stored mode, so later answers remain visible after the host has revealed answers once;
- clicking the crossed-out eye disables visible-answer mode again, and subsequent answers return to hidden presentation;
- `aria-pressed` mirrors the current visible/hidden mode for assistive technology.

When a current answer exists and the mode is hidden, the answer body shows a centered placeholder with a crossed-out eye, the localized **Correct answer** label, and the existing localized **Show answer** text. This keeps the presentation intentional rather than leaving an unexplained blank screen.

When there is no current answer, the existing waiting-state message remains visible, but the eye/eye-slash toggle is still available so the host can choose the mode before the first question or between questions.

The eye icons are switched through the toggle's explicit `data-answer-visible="true|false"` state and CSS selectors rather than relying on the SVG `hidden` attribute.

## SignalR refresh behavior

The AnswerKey page does not reload for every game-state event. Its initial `GameStatusChanged` and `BuzzerStateChanged` snapshots only seed the current session status and source-question ID.

After that:

- selecting a different regular question produces a new non-null `sourceQuestionId` and reloads AnswerKey once so the new answer is rendered;
- buzzer activity and other updates for the same question do not reload the page;
- changing the current question into its **showing answer** state does not reload AnswerKey again, because no new question ID has appeared;
- returning to the board does not reload AnswerKey merely because the buzzer closes;
- entering the final-question flow from regular play reloads once so the final answer definition is rendered;
- reconnect snapshots that describe the same state do not create another reload.

This keeps the private answer window synchronized with the actual answer identity while avoiding the visible second refresh that previously occurred when the host revealed an answer that AnswerKey had already loaded.

## Compatibility and fallback

The original AnswerKey links retain `target="_blank"` and `rel="noopener"`. If JavaScript does not run, the Window Management API is unavailable, or permission is already denied, normal browser behavior remains available.

If Window Management is attempted but no second display can be resolved, the helper opens the same named AnswerKey window without display coordinates. If coordinate placement is accepted, the target display bounds are passed directly to `window.open()`; `moveTo()` and `resizeTo()` are not the primary placement mechanism.

Modifier-clicks and non-primary mouse activations are not intercepted, so normal browser tab/window shortcuts continue to work.

## Security and permissions

Browsers may prompt the host for Window Management permission before exposing multi-display details. Permission handling remains entirely browser-controlled. BadWolfQuiz does not store display identifiers or screen geometry on the server.

Answer visibility is stored only in the AnswerKey window's browser `sessionStorage`, scoped by the current game code. It is not persisted to the server as a host setting.

## Validation coverage

Regression coverage verifies that:

- the Lobby model loads the dedicated AnswerKey window helper with a cache-busted asset URL;
- the existing AnswerKey links keep their native `_blank` fallback;
- screen details and the target display are resolved before the managed popup is opened;
- target display coordinates are included in the `window.open()` feature string;
- the helper compares against `currentScreen` and uses the target display's available bounds;
- move/resize remains a secondary reinforcement rather than the primary placement mechanism;
- unsupported and denied-permission environments keep native browser behavior;
- the answer visibility control is rendered even while no question is open;
- eye and crossed-out-eye icons follow `data-answer-visible` reliably;
- the selected visibility mode is restored from `sessionStorage` after AnswerKey reloads;
- hidden answer content uses the dedicated placeholder until the host enables visible-answer mode;
- regular AnswerKey refreshes happen only when the source-question ID changes;
- revealing the already-loaded answer does not cause a redundant second reload;
- entering the final-question flow still refreshes AnswerKey once.
