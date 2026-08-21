# Product versioning

BadWolfQuiz contains products that are released independently and therefore maintain separate semantic versions.

## BadWolfQuiz.Web

`BadWolfQuiz.Web` uses its own `MAJOR.MINOR.PATCH` version stored in `src/BadWolfQuiz.Web/BadWolfQuiz.Web.csproj`.

The deployed version is available from the About page, the host Settings UI, the logo tooltip, and `/api/version`. The version endpoint also exposes a short commit identifier when one is available from the build environment or assembly informational version.

Web releases should use tags in the form:

`web-vMAJOR.MINOR.PATCH`

Example: `web-v1.0.0`.

The mandatory all-player question feature is the headline backwards-compatible feature for Web `1.16.0`, so it uses a MINOR bump from `1.15.10`.

Question copying and same-quiz cloning from the Quiz Editor board are the headline backwards-compatible feature for Web `1.19.0`, so this release uses a MINOR bump from `1.18.0`.

Host-selected multiple-choice questions are the headline backwards-compatible feature for Web `1.20.0`, so this release uses a MINOR bump from `1.19.0`. The final `1.20.0` implementation includes the host AJAX lifecycle, correct-answer-only presentation, stable randomized option order with **Nobody answered** last, dynamic reward display updates, and automatic closure when no eligible buzzer player remains.

Follow-up fixes made before the first `1.20.0` release remain part of `1.20.0`; a PATCH bump is reserved for compatible fixes released after that version has shipped.

Web `1.20.1` is a PATCH release that fixes Quiz Editor question drag-and-drop so successful swaps and moves update the board in place without a full page reload, while failed moves surface an error without forcing a reload.

Web `1.20.2` is a PATCH release that prevents duplicate round creation from repeated **Add Round** submissions by locking the dialog actions and showing the shared busy indicator while the request is in progress.

Web `1.20.3` is a PATCH release that fixes cross-round question moves in the Quiz Editor by closing and locking the dialog immediately, showing the shared busy indicator, and updating the source question cell in place without a full page reload.

Web `1.20.4` is a PATCH release that fixes **Copy Question** target-category capacity detection by treating truly blank placeholder questions as free slots and reusing those placeholders when copying. It also adds a localized **Exchange category** tooltip and local loading indicators for the Copy Question, cross-round question exchange, and category exchange dialogs while destination lists are prepared.

Web `1.21.0` is a MINOR release that expands Quiz Editor question pricing: manually entered prices may be any positive integer instead of being limited to multiples of 100, while the native up/down spinner continues to change values in 100-point increments. Client-side and server-side validation reject zero and negative prices, and client-side validation also rejects empty or fractional values.

Web `1.21.1` is a PATCH release that automatically advances mandatory all-player questions as soon as every participating player has submitted. Multiple-choice questions immediately reveal the answer and stop the timers; text-answer questions stop accepting submissions, stop the timers, and enter sequential host review. Wager variants wait only for players who submitted wagers, so players who join after wagering has finished do not block completion.

Web `1.21.2` is a PATCH release that fixes low-value wager questions by using a 1-point minimum when the question value is below 10, including for players whose current score is zero, while keeping server-side validation and the host minimum-wager action aligned with the same rule. Player and host wager keypads also clamp digit entry to the maximum allowed wager instead of retaining an oversized value.

Web `1.21.3` is a PATCH release that fixes the private AnswerKey screen for all-player multiple-choice questions so only the configured correct option is rendered. It also removes redundant question metadata and body-level answer chrome, places **Correct answer** in the shared application topbar, hides the portal footer on AnswerKey, and constrains the answer body to the remaining viewport so the screen no longer gains an unnecessary page scrollbar.

Web `1.22.0` is a MINOR release that expands the private AnswerKey presentation for multi-screen hosting. On supporting browsers, the host AnswerKey action reuses a dedicated named window and places it on a display other than the one containing the game window through the Window Management API. The topbar always exposes an eye/eye-slash visibility mode: answers start hidden, the host can reveal them once and keep subsequent answers visible for the current game, or hide them again for later questions. Hidden answers use a clear placeholder. AnswerKey refreshes are tied to answer identity rather than every game-state notification: selecting a different question refreshes the page once, while showing the already-loaded answer, buzzer activity, same-question state changes, reconnect snapshots, and returning to the board do not cause redundant reloads. Entering the final-question flow still refreshes once, and a reload guard prevents duplicate refreshes from overlapping relevant notifications. Single-monitor use, unsupported browsers, permission denial, and placement failures retain the normal separate-window fallback.

## BadWolfQuizLogDownloaderWpf

`BadWolfQuizLogDownloaderWpf` maintains a separate version in its own project file. Its version is displayed in the application title bar and changes independently of the web application.

Downloader releases should use tags in the form:

`log-downloader-vMAJOR.MINOR.PATCH`

Example: `log-downloader-v1.0.0`.

## Semantic versioning

- PATCH: compatible fixes.
- MINOR: backwards-compatible features.
- MAJOR: major or breaking release milestones.

A release of one product does not require a version change in the other product.
