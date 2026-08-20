# Mandatory all-player questions

## Purpose

Mandatory all-player questions replace the normal first-to-buzz flow with a phase in which every current player may submit one answer from the player page.

This feature is released with `BadWolfQuiz.Web` **1.16.0**.

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
3. Every current participating player receives the appropriate answer controls.
4. The host sees submitted/waiting progress without seeing correctness early.
5. The timer is informational for the host and never blocks player submissions while required answers are still missing.
6. Players may still answer after the timer reaches zero if at least one required participant has not submitted yet.
7. As soon as every required participant submits, the server advances automatically: multiple choice reveals the answer, while text mode stops accepting submissions and enters host review.
8. **Proceed to answer review** remains available while answering is open. If the host starts review before everyone submits, every missing participant receives an automatic empty response before the question advances to reveal/review.

For multiple choice, automatic completion or the host's early close action reveals the answer presentation and stops the question timers. For text mode, automatic completion stops both timers and starts sequential host judging without resolving the question first. An early host close does the same after recording empty responses for missing participants.

For wager all-player questions, the participants after wagering are the players who submitted wagers. A player who joins after the wager phase has finished does not become a required respondent and therefore does not block automatic completion.

## Multiple-choice ordering and presentation

Each player receives a stable shuffled order derived from the question and player identifiers. The host receives a separate stable shuffled order while the question is open.

The host question page renders the shuffled choices on the server so the correct layout is present on the first frame, including image choices. Correctness is not highlighted during answering. On hover-capable desktop host displays, those choices stay in a horizontally centered collapsed bottom drawer and expand on hover or keyboard focus, while the submitted/waiting player list uses a matching right-side drawer. The host's **Proceed to answer review** button stays in a dedicated lower-left area beside the centered drawer, so the expanded drawer cannot cover it and hovering the button does not open the drawer. Drawer headers are clipped to the same rounded corners as their outer frames. Touch layouts keep the information directly visible because hover is unavailable.

The editor answer preview, live answer page, and resolved-question answer preview use the configured answer order so the first option can be marked as correct. They render:

- two options as one row and two columns;
- three or four options as a two-column grid;
- the correct option with a green border;
- every distractor with a red border;
- text and images centered inside equal presentation cells.

The private host `/Admin/Games/AnswerKey/` screen intentionally uses a different reveal presentation for `AllPlayerMultipleChoice`: it renders only the first configured answer block, which is the correct option, and omits every distractor. Other question types continue to render their normal answer blocks. The AnswerKey screen uses a compact centered **Correct answer** header and a full-width answer area so the answer content can use the available viewport without the previous category/question metadata or excessive horizontal margins.

Image endpoints return inline media responses so they can be displayed by `<img>` elements rather than downloaded as attachments.

## Text judging and scoring

Text submissions are never checked automatically. The right-side player drawer shows only submitted/waiting status and never displays answer text. After a result is known, the host progress row also shows the recorded score delta next to Correct/Incorrect (for example `+5`, `-5`, or `0`). The timer never closes input by itself.

When every current participant has submitted a text answer, answering closes automatically, both timers stop, and the host enters sequential review immediately. The current review card and its Correct/Incorrect controls keep a stable DOM identity between polling updates so a normal click cannot be lost. If the host starts review early, every participant who is still missing is first recorded automatically as `-`. The individual empty-response action remains available in the drawer when the host wants to mark an AFK participant before review starts.

Multiple-choice early review uses the same missing-player behavior: participants without a submitted option receive incorrect empty attempts without selecting an option on their behalf. Normal questions keep a zero delta; wager questions deduct that participant's own wager.

For a normal all-player question, scoring is the same for both modes:

- correct answer: the full question value;
- incorrect answer: zero points;
- no answer: zero points.

All-player questions may also be explicit or randomly selected wager questions. Every participating player privately submits an individual wager from the player screen before the question is shown. The host sees only submitted/not-submitted status, may assign the minimum wager for an AFK player, and reveals the question after every wager exists. The host wager screen uses the full gameplay width, and **Show question** stays in a stable bottom action area outside the right status drawer. Each correct answer adds that player's own wager and each incorrect answer subtracts that player's own wager. If the host closes answering while a player is still missing, the automatically recorded empty response is incorrect and also subtracts that player's own wager. Wager all-player questions use the configured wager-answer timer start mode and duration.

Incorrect submissions remain stored as answer attempts. Their score delta is zero for normal all-player questions and the negative wager amount for wager all-player questions, allowing statistics to distinguish an incorrect answer from a missing answer.

## Reconnect and page reload behavior

The player client rebuilds controls when the all-player panel or its controls are missing, even when the current question identifier and mode did not change. It polls immediately after player-session join, host reapproval, page show, focus restoration, and network restoration.

A normal disconnect or manual reconnect during a running game still requires host approval. A short-lived single-use transition token is created only immediately before an intentional game-phase reload, so internal page transitions do not look like an unrelated reconnect.

The all-player client can recover its access token from the same local-storage record used by the player SignalR client. Controls remain hidden while rejoin approval is pending and are rebuilt after approval even when the server-rendered bootstrap token is empty. While the all-player wager, answer controls, or confirmation status are active, the normal buzzer is force-hidden and the player page remains vertically scrollable.

`all-player-question.js` is referenced directly by the shared layout with `asp-append-version="true"`. A rebuilt asset receives a new content hash, preventing the browser from reusing an older script after the next page request.

## Validation coverage

The focused regression suite covers:

- editor restrictions and dirty-state behavior;
- automatic completion after every participating player submits, for both multiple-choice reveal and text-answer review;
- host-triggered early review and automatic empty-response fill for missing players;
- wager participation rules, including late players who did not submit wagers;
- shuffled Text/Image choices;
- server-rendered host and preview grids;
- AnswerKey correct-option filtering and compact full-width presentation;
- reconnect approval, local-storage token recovery, and control rebuilding;
- versioned asset loading;
- normal and per-player-wager scoring behavior.
