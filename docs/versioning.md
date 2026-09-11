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

Web `1.22.1` is a PATCH release that fixes CRLF-sensitive AnswerKey regression tests without changing production AnswerKey behavior.

Web `1.22.2` is a PATCH release that prevents the Question Editor from persisting phantom empty content blocks with an undefined `ContentBlockType` value, repairs already-affected empty blocks when an existing question is opened, and prevents malformed undefined block types from being saved again.

Web `1.22.3` is a PATCH release that makes authored buzzer activation modes effective during gameplay for Standard, Four Clues, and Host-selected multiple-choice questions. It supports Manual, Immediately, After media, After delay, and round-default inheritance; removes `Disabled` from the user-facing authoring choices while preserving it internally for wager/all-player flows; treats legacy authored `Disabled` values as Manual; and fixes After-media activation for both manually started and autoplay media.

Web `1.22.4` is a PATCH release that preserves authored YouTube start times when supported YouTube links are converted to privacy-enhanced embeds. `t` and `start` parameters are accepted from the query string or fragment, numeric seconds and `h` / `m` / `s` timestamp forms are converted to the embed `start` parameter, and links without a valid positive timestamp continue to start from the beginning. The behavior is shared by host gameplay, player gameplay, AnswerKey, and game-content previews without changing the existing autoplay, placeholder, or fullscreen flow.

Web `1.23.0` is a MINOR release that introduces the standalone **Minigames** catalog and the first two-player minigame, **Guess what I'm playing / Вгадай, у що я граю**. It adds private two-player rooms with join codes and one-hour inactivity expiry, configurable game-card tables and exclusions, private secret cards, timed alternating turns, guesses, optional YES/NO Question cards with independent shuffled decks and shared history, theme synchronization, in-place game restart, shareable room links with social previews, and a responsive square-card layout.

Web `1.23.1` is a PATCH release that fixes same-round **Copy Question** updates in the Quiz Editor. After a successful copy into the currently open round, the editor refreshes the affected board state from the persisted server-rendered markup without a full page reload, including newly added rows, while copies to other rounds or quizzes keep their previous behavior.

Web `1.23.2` is a PATCH release that fixes the host answer reveal for all-player multiple-choice wager questions. Player wager results stay in a bounded summary area, the answer uses the remaining viewport space, runtime content-container markers are consumed instead of appearing as text, container children are reconstructed in their intended layout, and legacy all-player option styles no longer leak onto reveal-only content. Regression coverage is also updated so configurable Minigames card counts are not mistaken for an immutable local setting.

Web `1.23.3` is a PATCH release that finalizes the post-game quiz rating phase when the host explicitly finishes the game. Connected player clients are notified immediately, stale rating submissions are rejected server-side after finalization, ratings already saved before finalization remain intact, and refresh/reconnect no longer restores an editable rating screen for that finished runtime session.

Web `1.24.0` is a MINOR release that adds filterable Question-card history to **Guess what I'm playing**. Players can switch between the full chronological history and directional `1 → 2` / `2 → 1` views that combine each completed question and response into one color-coded row, while filtering remains entirely client-side and does not modify room state.

Web `1.25.0` is a MINOR release that moves the **Guess what I'm playing** game-card/question catalog into the main SQLite database and adds a MasterHost-only catalog editor. The editor manages game names and card images, the ordered shared question list, and per-game YES/NO answer matrices with autosave and line-for-line TXT import/export. Clean databases are seeded once from the bundled legacy Minigames resources, while live gameplay subsequently reads card metadata, images, and questions from the database. Potentially slow editor operations use the shared busy indicator, and the Games view uses compact horizontal cards with square icon actions.

Web `1.26.2` is a PATCH release that speeds up **Copy Question** destination loading in the Quiz Editor. The destination lookup now projects only the metadata needed to list quizzes, rounds, categories, and slot capacity instead of materializing full question/content graphs and stored media bytes. Full rounds use bounded lightweight capacity checks, while the dialog now cancels obsolete requests, applies a 15-second client timeout, and exposes an explicit Retry action after loading failures.

Web `1.27.0` is a MINOR release that introduces persistent player achievements. It ships an initial catalog of 47 localized milestones, server-authoritative metric and gameplay-event evaluation, account/nickname history adoption, active-game recovery for pending achievement state, player achievement dialogs, host-lobby per-player achievement inspection, secret achievements, and migration compatibility for development databases created during the feature work. The Big Game milestone unlocks at a final score of at least 15,000 points. See [`docs/features/player-achievements.md`](features/player-achievements.md) for the full behavior and catalog rules.

Web `1.28.0` is a MINOR release that adds first-class question tags to quiz authoring and persistence. Tags are normalized case-insensitively and preserved through question editing, cloning, import/export flows, and quiz APIs so authored topic metadata can be reused reliably.

Web `1.29.0` is a MINOR release that expands persistent player achievements from 47 to 65 with 18 topic and media milestones. All new milestones require correct answers. Topic achievements use normalized question tags, while **By Ear / На слух** detects `Audio` content blocks and **On Screen / На екрані** detects both `Video` and `YouTube` content blocks without relying on media tags. Four one-answer topic milestones remain secret until unlocked. See [`docs/features/player-achievements.md`](features/player-achievements.md) for the detailed rules.

Web `1.29.1` is a PATCH release that fixes host question-card overlap when browser zoom changes. Running question tiles are constrained to their fractional grid rows, hover/focus no longer shifts a tile vertically into a neighbouring row, and narrow viewport styling no longer reintroduces width-based minimum heights that can exceed the assigned row.

Web `1.29.2` is a PATCH release that fixes the quiz title/description dialog on My quizzes so its header uses existing localized resources instead of rendering missing localization key names. Regression coverage verifies the dialog uses valid shared-resource keys across the supported localization files.

Web `1.30.0` is a MINOR release that adds a dedicated lightweight quiz-metadata editor for title, description, and up to 20 normalized quiz-level tags. The editor uses the shared Save/Reset/Back, keyboard-shortcut, save-overlay, and unsaved-change behavior; host-scoped suggestions reuse quiz tags and fall back to question tags. Quiz tags are preserved through cloning and `.bwquiz` export/import, appear on shareable quiz-description pages, and are summarized in one bounded row on social-preview images. The shareable page also moves the quiz rating into the announcement-card rail.

Web `1.31.0` is a MINOR release that adds configurable colored category columns to the host game board. Quiz authors can use Automatic, Theme, or Custom category colors with contrast-aware text; preferences persist through cloning and `.bwquiz` import/export. Hosts can enable or disable colored categories globally or per game, with immediate running-game preview and reliable cancel/reset behavior even directly after the lobby-to-game transition. This release also includes the shared custom-theme color-picker UX and avoids loading stored media blobs unnecessarily during question/category description saves. See [`docs/features/category-colors.md`](features/category-colors.md) for the full behavior.

Web `1.32.0` is a MINOR release that expands persistent player achievements from 65 to 80. It adds Star Trek, anime, Counter-Strike, Dota, Ukraine, geography, and history tag milestones; explicit double/half answer-reward achievements; consecutive-win and previous-winner competition milestones; a ten-times maximum peer-rating milestone; and one-month, six-month, and one-year participation milestones. Reward achievements are driven by an explicit authoritative runtime modifier instead of inferred score deltas, while rating progress is de-duplicated and preserved across nickname-to-account history adoption. See [`docs/features/player-achievements.md`](features/player-achievements.md) for the detailed rules.

Web `1.32.1` is a PATCH release that replaces achievement-card emoji with dedicated PNG artwork for all 80 achievements in both player and host achievement dialogs. Secret locked achievements keep the shared hidden placeholder until they are unlocked.

Web `1.32.2` is a PATCH release that fixes contributor-framed gameplay player media resizing. Framed avatars, uploaded images, webcam feeds, and webcam URL media now re-measure against the player card's current available media area so they can grow as well as shrink when the card is resized, while preserving the existing frame inset and preview-parity behavior.

Web `1.33.0` is a MINOR release that adds optional per-question `x2` and `1/2` correct-answer scoring to the standard host judgment flow. Quiz authors explicitly enable the two extra actions in the Question Editor; the option is off by default and persists through normal snapshot, copy, clone, and `.bwquiz` import/export paths. Both actions reuse the authoritative answer-reward modifier pipeline, preserve normal correct-answer side effects and duplicate-submission protection, record the actual awarded delta, and provide the gameplay path for the `DoubleReward` and `HalfReward` achievements. See [`docs/features/question-judging.md`](features/question-judging.md) for the scoring and integration rules.

Web `1.33.1` is a PATCH release that hardens player-achievement history and account adoption. Gameplay-derived progress is finalized from eligible players in `Finished` sessions only; kicked or removed players remain in stored audit history but do not contribute to achievement progress or nickname-history adoption. Nickname-to-account adoption is deferred until an eligible finished-game participation, preserves prior fallback unlock timestamps and source-game metadata including direct event unlocks, and avoids reassigning history already claimed by another account. Built-in account progress continues to aggregate eligible finished-game history across hosts, while future host-defined custom achievements remain host-scoped. See [`docs/features/player-achievements.md`](features/player-achievements.md) for the lifecycle and identity rules.

Web `1.34.0` is a MINOR release that adds the host-side **Player achievements** history page to the active-game **Tools** menu. Hosts can review persisted unlocks for the current game or switch to a current player's complete persisted achievement history without triggering evaluation, unlocks, or account adoption. The page enforces host ownership, uses account or host/nickname identity consistently, supports responsive player side-rail navigation, larger achievement artwork, localized timestamps and empty states, future-code fallback rendering, and the Answer History visual design with the same compact current-game summary card. Filter/player switching updates the history in place without a full-page refresh or scroll jump, keeps browser Back/Forward state, supports `Escape` return-to-game navigation, and uses the shared busy indicator for slower transitions. See [`docs/features/player-achievements.md`](features/player-achievements.md) for the history and identity rules.

Web `1.35.0` is a MINOR release that adds explicit host confirmation for pending nickname-to-account achievement-history adoption during an active game. A conditional **Pending confirmation** tab appears only when a current signed-in player has safely adoptable older `HostId + normalized nickname` history. The host can confirm that identity immediately instead of waiting for the current game to finish; eligible prior finished-game appearances and fallback unlocks are adopted without changing the unfinished current game's eligibility, preserving authoritative unlock timestamps/source-game metadata and conflict protection. The pending view reuses the existing responsive player side rail and achievement-card layout, updates in place without a full-page refresh, and immediately refreshes the other history modes after synchronization. Regression coverage includes both Linux and Windows line-ending behavior for the history UI. See [`docs/features/player-achievements.md`](features/player-achievements.md) for the identity and adoption rules.

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