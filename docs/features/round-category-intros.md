# Round and Category Intros

## Purpose

Rounds and categories can contain optional presentation descriptions that are shown before a round board opens. The descriptions let a quiz author introduce the round and each category without turning that content into a playable question.

## Editor

Round and category descriptions use ordered content blocks. These description editors support **Text** and **Image** blocks only; Audio and YouTube blocks are not offered.

Round actions are grouped under **Edit round**. From that menu the author can rename the round, edit its description, or delete it. Category renaming remains a direct action, while clicking the category header cell opens the category description editor.

Both description editors provide a preview. Escape returns to the quiz board editor. Uploaded image blocks are rendered in both the editor and preview. Description previews use the available preview-dialog width with normal responsive padding instead of the old narrow desktop content cap.

## Heading rules

The intro and editor preview use the same heading rules.

For rounds:

- a normal non-numeric title is shown as-is;
- an untitled round uses localized **Round N**, where `N` is its 1-based position;
- a numeric-only title uses localized **Round {title}**;
- a title that already begins with the localized Round label is not prefixed again.

For categories:

- a normal non-numeric title uses localized **Category: {title}**;
- an untitled category uses localized **Category N**, based on the editor/game ordering;
- a numeric-only title uses localized **Category {title}**;
- a title that already begins with the localized Category label is not prefixed again.

When non-empty description blocks exist, the heading and blocks form one centered composition. When the description is empty, only the heading is shown and it is centered vertically.

## Gameplay flow

Every round starts with the same intro sequence:

1. round intro;
2. category intros in category order;
3. round board.

The sequence is used when the first round starts and when later rounds are reached through normal completion, the inter-round leaderboard, forced advancement, or no-player advancement.

When an already visited unfinished round is entered again through normal completion, a return action, or a forced **Next round** transition, category intro pages are filtered to categories that still contain `Available` questions. Categories whose playable questions are already fully closed are skipped. Untouched rounds still show every category because all of their questions are available.

Returning to an earlier unfinished round first shows the current round leaderboard when players are present, then restarts the target round intro.

Every category receives an intro page even when its description is empty. **Next** advances through the sequence, **Skip** opens the round board immediately, and the final category replaces those actions with **Start game**.

During an active intro, the host can press **Escape** to perform the same completion action as the current UI control: **Skip** where it is available, or **Start game**/**Start round** on the final intro. When a category intro is reopened from the round board, Escape uses the existing **Return to board** action and leaves game state unchanged. Open dialogs and menus take priority over this gameplay shortcut.

During regular round-board play, the host can click the full category header cell to reopen only that category's intro presentation. Closing that presentation returns directly to the current round board without changing game state. The category header uses the same hover emphasis as playable question cells.

When no players are present, the game does not show an empty inter-round leaderboard before the next intro.

Intro pages use short slide/fade/scale transitions. The first intro also animates in. `prefers-reduced-motion` disables these animations.

### Presentation width

Round and category intro descriptions use the available gameplay viewport width rather than fixed `1100px`/`980px` group and block caps. The standalone first-round intro also opts out of the generic centered `page-shell` maximum. Existing responsive page padding, media height constraints, centered alignment, and `object-fit` behavior remain in place.

### Host navigation behavior

Later-round intros are rendered inside the persistent host gameplay shell when the surrounding game state can remain mounted. Entering an intro invalidates stale in-flight Lobby refreshes so a previous response cannot immediately overwrite the presentation.

The first round is a special bootstrap case because the running-game host shell does not exist yet. `RoundIntro.cshtml` remains the standalone server-rendered source, but its category frames replace `[data-game-intro-page]` asynchronously. **Skip** and **Start game** also submit through `fetch`; after the server redirects to the running Lobby, that Lobby is mounted into the existing browser document and initializes the persistent host navigation without a second browser navigation.

If an asynchronous intro transition fails or returns an unsupported route, normal browser navigation remains the fallback.

## Persistence

Round and category description blocks are persisted with the quiz definition and copied into the immutable quiz snapshot used by a running game. `GameSessionLauncher` explicitly eager-loads both round and category `DescriptionBlocks` before `QuizSnapshotFactory` creates that snapshot; otherwise the no-tracking game query would leave the intro description collections empty. Starting or skipping an intro does not modify quiz data.
