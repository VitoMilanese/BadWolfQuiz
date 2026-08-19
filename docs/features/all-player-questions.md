# Mandatory all-player questions

## Purpose

Mandatory all-player questions replace the normal first-to-buzz flow with a phase in which every current player may submit one answer from the player page.

The runtime exposes two presentation modes:

- `AllPlayerText` for private free-text submissions judged by the host;
- `AllPlayerMultipleChoice` for one choice from two to four shuffled Text or Image options.

These modes use the existing `QuestionPresentationType` field, so they do not require a database migration.

## Editor rules

### Text answer

- The answer contains exactly one non-empty Text block. It is shown to the host as the reference answer but is not used for automatic judging.
- Question content may use the normal content-block editor.
- The question may be marked as a wager question and may participate in random wager selection.

### Multiple choice

- The answer contains two, three, or four options.
- Every option is a Text or Image block.
- The first configured answer block is the correct option.
- Audio, Video, YouTube, and other content types are rejected for both the question and answer sections in this mode.
- The question may be marked as a wager question and may participate in random wager selection.

Opening an existing all-player question establishes a clean editor baseline. Leaving through Back, Next, refresh, page close, or Escape uses the shared unsaved-changes guard without stacking a native browser prompt over the application dialog.

The type selector is rendered by Razor and also posts a hidden all-player mode marker, so Text/Image uploads cannot accidentally fall back to the Standard presentation value. Existing image-only questions that carry the earlier all-player editor signature (disabled buzzer, excluded from random wagers, and two to four stored Image answers) are normalized to `AllPlayerMultipleChoice` when a game snapshot is created and when the editor is reopened.

## Gameplay flow

1. The host selects the question.
2. The normal buzzer remains closed.
3. Every current player receives the appropriate answer controls.
4. The host sees submitted/waiting progress without seeing correctness early.
5. Answering closes automatically when every current player has submitted.
6. When the timer expires, player input closes but the question remains visible until the host selects **Proceed to answer review**.
7. The host may close answering early with **Proceed to answer review**.

For multiple choice, closing answering reveals the answer presentation. For text mode, closing answering starts sequential host judging when at least one answer was submitted; with no submissions the question advances directly to its answer state.

## Multiple-choice ordering and presentation

Each player receives a stable shuffled order derived from the question and player identifiers. The host receives a separate stable shuffled order while the question is open.

The host question page renders the shuffled choices on the server so the correct layout is present on the first frame, including image choices. Correctness is not highlighted during answering. On hover-capable desktop host displays, those choices stay in a horizontally centered collapsed bottom drawer and expand on hover or keyboard focus, while the submitted/waiting player list uses a matching right-side drawer. The host's **Proceed to answer review** button stays in a dedicated lower-left area beside the centered drawer, so the expanded drawer cannot cover it and hovering the button does not open the drawer. Drawer headers are clipped to the same rounded corners as their outer frames. Touch layouts keep the information directly visible because hover is unavailable.

The editor answer preview, live answer page, and resolved-question answer preview use the configured answer order so the first option can be marked as correct. They render:

- two options as one row and two columns;
- three or four options as a two-column grid;
- the correct option with a green border;
- every distractor with a red border;
- text and images centered inside equal presentation cells.

Image endpoints return inline media responses so they can be displayed by `<img>` elements rather than downloaded as attachments.

## Text judging and scoring

Text submissions are never checked automatically. The right-side player drawer shows only submitted/waiting status and never displays answer text. After answering is locked and every player has a recorded submission, the host reviews one answer at a time in the main question area, matching the Final Question judging flow. The current review card and its Correct/Incorrect controls keep a stable DOM identity between polling updates so a normal click cannot be lost. For an AFK player, the host can record `-` with the empty-answer action in the drawer before judging begins.

For a normal all-player question, scoring is the same for both modes:

- correct answer: the full question value;
- incorrect answer: zero points;
- no answer: zero points.

All-player questions may also be explicit or randomly selected wager questions. Every participating player privately submits an individual wager from the player screen before the question is shown. The host sees only submitted/not-submitted status, may assign the minimum wager for an AFK player, and reveals the question after every wager exists. The host wager screen uses the full gameplay width, and **Show question** stays in a stable bottom action area outside the right status drawer. Each correct answer adds that player's own wager, each incorrect answer subtracts that player's own wager, and a player with no recorded answer has no score change. Wager all-player questions use the configured wager-answer timer start mode and duration.

Incorrect submissions remain stored as answer attempts. Their score delta is zero for normal all-player questions and the negative wager amount for wager all-player questions, allowing statistics to distinguish an incorrect answer from a missing answer.

## Reconnect and page reload behavior

The player client rebuilds controls when the all-player panel or its controls are missing, even when the current question identifier and mode did not change. It polls immediately after player-session join, host reapproval, page show, focus restoration, and network restoration.

A normal disconnect or manual reconnect during a running game still requires host approval. A short-lived single-use transition token is created only immediately before an intentional game-phase reload, so internal page transitions do not look like an unrelated reconnect.

The all-player client can recover its access token from the same local-storage record used by the player SignalR client. Controls remain hidden while rejoin approval is pending and are rebuilt after approval even when the server-rendered bootstrap token is empty. While the all-player wager, answer controls, or confirmation status are active, the normal buzzer is force-hidden and the player page remains vertically scrollable.

`all-player-question.js` is referenced directly by the shared layout with `asp-append-version="true"`. A rebuilt asset receives a new content hash, preventing the browser from reusing an older script after the next page request.

## Validation coverage

The focused regression suite covers:

- editor restrictions and dirty-state behavior;
- automatic and host-forced answering closure;
- shuffled Text/Image choices;
- server-rendered host and preview grids;
- reconnect approval, local-storage token recovery, and control rebuilding;
- versioned asset loading;
- scoring and question lifecycle behavior.
