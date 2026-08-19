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
- Wager and random-wager behavior is disabled.

### Multiple choice

- The answer contains two, three, or four options.
- Every option is a Text or Image block.
- The first configured answer block is the correct option.
- Audio, Video, YouTube, and other content types are rejected for both the question and answer sections in this mode.
- Wager and random-wager behavior is disabled.

Opening an existing all-player question establishes a clean editor baseline. Leaving through Back, Next, refresh, page close, or Escape uses the shared unsaved-changes guard without stacking a native browser prompt over the application dialog.

## Gameplay flow

1. The host selects the question.
2. The normal buzzer remains closed.
3. Every current player receives the appropriate answer controls.
4. The host sees submitted/waiting progress without seeing correctness early.
5. Answering closes automatically when every current player has submitted or when the timer expires.
6. The host may close answering early with **Proceed to answer review**.

For multiple choice, closing answering reveals the answer presentation. For text mode, closing answering starts sequential host judging when at least one answer was submitted; with no submissions the question advances directly to its answer state.

## Multiple-choice ordering and presentation

Each player receives a stable shuffled order derived from the question and player identifiers. The host receives a separate stable shuffled order while the question is open.

The host question page renders the shuffled choices on the server so the correct layout is present on the first frame, including image choices. Correctness is not highlighted during answering.

The live answer page and resolved-question answer preview use the configured answer order so the first option can be marked as correct. They render:

- two options as one row and two columns;
- three or four options as a two-column grid;
- the correct option with a green border;
- every distractor with a red border;
- text and images centered inside equal presentation cells.

Image endpoints return inline media responses so they can be displayed by `<img>` elements rather than downloaded as attachments.

## Text judging and scoring

Text submissions are never checked automatically. The host reviews one submitted answer at a time and marks it Correct or Incorrect.

Scoring is the same for both modes:

- correct answer: the full question value;
- incorrect answer: zero points;
- no answer: zero points.

Incorrect submissions remain stored as answer attempts with a zero score delta, allowing statistics to distinguish an incorrect answer from a missing answer.

## Reconnect and page reload behavior

The player client rebuilds controls when the all-player panel or its controls are missing, even when the current question identifier and mode did not change. It polls immediately after player-session join, host reapproval, page show, focus restoration, and network restoration.

The player page keeps a prepared single-use transition token in session storage. A normal reload can therefore reconnect without being treated as a new unapproved join. The token is replaced after every successful join and remains single-use.

`all-player-question.js` is referenced directly by the shared layout with `asp-append-version="true"`. A rebuilt asset receives a new content hash, preventing the browser from reusing an older script after the next page request.

## Validation coverage

The focused regression suite covers:

- editor restrictions and dirty-state behavior;
- automatic and host-forced answering closure;
- shuffled Text/Image choices;
- server-rendered host and preview grids;
- reconnect control rebuilding and transition-token preparation;
- versioned asset loading;
- scoring and question lifecycle behavior.
