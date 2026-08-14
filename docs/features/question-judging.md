# Question Judging

## Purpose

Question judging applies score changes, records answer attempts, resolves questions, and determines which player receives the right to select the next question. The Game Engine owns these rules; host controls only submit commands.

## Regular questions

A regular question may receive attempts from multiple players.

For each judged attempt:

- a correct answer adds the currently available correct-answer value, including optional answer reward decay;
- an incorrect answer subtracts the question point value;
- a player cannot be judged more than once for the same question;
- an incorrect attempt leaves the question open for another eligible player;
- a correct attempt resolves the question.

When a player answers correctly, that player becomes the active player. If the current active player answers correctly, the active player remains unchanged.

The host can explicitly resolve a regular question with no correct answer while the buzzer phase is available. The control is hidden while a specific player is answering. In either explicit or automatic no-correct-answer resolution, no additional score is applied and the existing active player keeps the right to select the next question. If an incorrect answer or timeout leaves no eligible players who can still buzz, the Engine performs this resolution automatically.

Question selection is submitted asynchronously. If the Engine rejects the
selection because the board state changed or the question is no longer
available, the host remains on the current board and receives a temporary inline
error instead of a full-page reload.

Host gameplay commands that are submitted asynchronously are protected from
SignalR-driven reload races. While a host command is in flight, a live update
that would normally reload the Lobby defers that reload until the request has
finished. This prevents a successful gameplay command from being reported by the
browser as a fetch/network failure merely because the resulting state broadcast
arrived before the HTTP response.

Failed host-owned runtime-session lookups for key gameplay commands are logged
with command, game-session, host, request-path, and request-trace context. This
diagnostic logging is intended to make intermittent `404 Not Found` responses
actionable without changing the normal gameplay rules or exposing participant
data.


## Board context actions

The host can administer unopened board content without entering the normal
question presentation flow. These commands are available only for questions in
`Available` state. Resolved questions do not expose a context menu.

Right-clicking an unopened question shows icon-only actions for:

- **Gift**: create a positive, correct answer-history entry for a selected player;
- **Close**: after confirmation, resolve the question with no answer and no score change.

The Gift dialog shows the selected question as read-only, lets the host choose an
eligible player, and pre-fills the question's nominal value. The reward must be
positive. The spinner changes it in 100-point steps, but manually entered
positive values do not need to be divisible by 100. A checkbox, disabled by
default, controls whether the question is also resolved after the reward is
applied.

Right-clicking a category heading exposes the same close icon when that category
contains at least one unopened question. After confirmation, every still-available
question in that category is resolved with no answer and no score change. A
category with no available questions exposes no context action.

These board context operations are asynchronous. They update scores and board
state without reloading the Lobby page. If one of them resolves the final
available question in the current round, the host view immediately transitions
to the normal round-summary state, preserving the same round-completion behavior
as the standard judging flow.

## Four-clue questions

A four-clue question is a regular buzzer question with a different presentation and scoring rule. Its immutable definition contains exactly four ordered clues. A clue may be text, an image, or audio; video and YouTube blocks are not supported.

The first two clues are revealed when the question opens. The third and fourth clues are revealed one at a time, either manually by the host or automatically when the question timer expires. Every successful clue reveal starts a new full question-timer interval. The number of revealed clues is part of the recoverable runtime state. After the final clue has been revealed, a later timer expiration follows the normal unresolved-question timeout flow.

- A correct answer with two visible clues awards 100% of the question value.
- A correct answer with three visible clues awards 50%.
- A correct answer with four visible clues awards 25%.
- An incorrect answer always deducts 100% of the question value.

When answer reward decay is enabled, the clue-dependent correct-answer value above becomes the base value for decay during an individual player's answer timer. The incorrect-answer penalty remains 100% of the original question value.

Four-clue questions cannot become wager questions, including through random wager selection.

## Wager questions

Only the player who selected the wager question may answer it. The accepted wager is the score value for judging:

- a correct answer adds the wager;
- an incorrect answer subtracts the wager;
- either judgment resolves the question.

The wager player and active player cannot be changed while the wager question is in progress.

## Runtime state

Each judgment creates an immutable `QuestionAnswerAttempt` containing:

- player identifier;
- correct or incorrect result;
- applied score delta;
- UTC judgment timestamp.

Attempts remain attached to the runtime question. Repeated judging of the same player or judging a resolved question is rejected, preventing duplicate score application.

## Current integration boundary

The current host UI presents immutable question content while a question is active and immutable answer content after the question is resolved. The board remains hidden until the host closes the answer presentation. Player score lists remain visible and receive real-time updates.

For regular buzzer questions, the answering player is established by the buzzer winner and the Engine-controlled timed answer phase. The host judges that player's answer while the question remains active.

After correct, incorrect, and timeout outcomes, the host briefly sees the answering player and actual applied score delta over the player-card area. This transient feedback does not delay the authoritative transition to the next gameplay state.

The transient score-result overlay automatically scales the player name and applied
score delta to the available card area. Both values remain fully visible without
overflow, and the fitted text is revealed only after its initial layout pass to
avoid visible font-size jumps.

The near-simultaneous buzzer overlay applies the same fitting behavior to the
winner and later buzzer entries. Later entries remain readable at a size close to
the winner text and shrink only when required to fit the available width.

Both gameplay overlays use a five-second visual lifetime. Because the host gameplay
view is updated through partial navigation, newly inserted overlay cards are
reinitialized for auto-fitting after each host gameplay update instead of relying
only on the initial page-load fitting pass.

## Target question presentation flow

The final host experience replaces the temporary player-selection panel with a full question presentation state.

After a question is selected:

- the board is hidden;
- the question occupies the main presentation area;
- the player scoreboard remains visible at the bottom;
- contextual host controls appear below the question.

The correct answer is always displayed after the question and before the board returns. The host decides when to close the answer presentation and return to the board.

### Regular question timers

A regular question uses two independent timers.

The **buzzer window timer** is the total time available for eligible players to claim an answer attempt. Activating the buzzer starts this timer automatically.

Buzzer activation has its own configurable start mode:

- in `Automatic` mode, revealing a regular question activates the buzzer and starts the buzzer window timer immediately;
- in `Manual` mode, revealing a regular question shows a host **Activate buzzer** control; using it activates the buzzer and starts the buzzer window timer.

This setting controls when the buzzer phase begins. It is independent from the wager-question answer timer start mode.

When a player wins the buzzer:

- the buzzer closes;
- the buzzer window timer pauses and preserves its remaining duration;
- the host view highlights the answering player;
- a separate **answer timer** starts for that player.

The host may judge the answer before the answer timer expires. If the answer timer expires before the player gives a correct answer, the Engine records an incorrect answer automatically.

When answer reward decay is enabled, the available correct-answer reward remains
at its current full value for the configured delay and then decreases linearly
during this individual answer phase. The minimum configured percentage is reached
at 1 displayed second remaining. The rounded value shown to the host is the same
whole-point value used when a correct answer is judged. Incorrect answers and
timeouts continue to apply the full normal penalty.

After an incorrect answer:

- the question remains visible;
- the player is excluded from further attempts for that question;
- the buzzer becomes available to the remaining eligible players;
- the buzzer window timer resumes from the exact duration that remained when the previous player buzzed;
- any decayed correct-answer reward is reset to the normal available value until another player wins the buzzer.

The question moves to answer presentation when any of the following occurs:

- a player answers correctly;
- the host chooses **No correct answer**;
- the buzzer window timer expires without another eligible player buzzing;
- no eligible players remain.

### Wager question flow

A wager question does not use the buzzer. The player who selected the question and submitted the wager is immediately the answering player. The host view highlights that player while the question is visible.

The wager answer timer start mode is configurable:

- in `Automatic` mode, revealing the question starts the answer timer immediately;
- in `Manual` mode, revealing the question shows a host **Start timer** control and the host decides when the answer phase begins.

The **Correct** and **Incorrect** judgment controls become available when the answer phase starts. In automatic mode they appear with the running timer. In manual mode they appear after the host starts the timer.

If the wager answer timer expires before a correct answer is accepted, the Engine records an incorrect answer automatically.

After the wager answer is judged correct or incorrect, the correct answer is displayed. The host then closes the answer presentation to return to the board.


## Round opening selection

The first round starts with the first player who joined the lobby as the active player.

At the beginning of every later round, the Engine transfers the right to select the first question to the weakest player. It applies the standings criteria in reverse: lowest total score, lowest score gain in the completed round, fewest correct answers overall and by round from newest to oldest, then fewest attempts overall and by round from newest to oldest. If every gameplay metric is identical, the player who joined first receives the selection right.

### Completing a round early

When another round exists, the host action menu can complete the current round
without opening every remaining question. Because this action skips all
unresolved questions in the round, the host must confirm it before the action
is submitted.

The Engine force-resolves every unresolved question in the current round,
closes any buzzer state, clears the answering player, and stops both timers.
No score is awarded or deducted for the skipped questions.

With connected players, the normal inter-round standings remain visible before
the host advances. In an empty game, the session advances directly to the next
round because there is no leaderboard to present or active player to select.


## Runtime timer orchestration

The buzzer-window and individual-answer durations come from the effective game settings, which are copied from global defaults when the game is created and may be overridden for that game.

Activating the buzzer starts the buzzer timer. A valid buzzer claim pauses that timer and starts the answer timer. If answer reward decay is enabled, only this player-owned answer interval affects the available correct-answer reward. If the answer timer expires, the Engine records an incorrect answer and resumes the buzzer timer with the exact time that remained when the player claimed the buzzer, unless no eligible players remain; in that case the question moves directly to answer presentation.

If the buzzer timer expires while no player is answering, the Engine resolves the question without a correct answer and moves to the answer presentation.

The Engine exposes timer processing as an explicit command. Real-time scheduling, SignalR broadcasts, and visible countdown controls are connected in the Web layer separately.


### Wager question answer timer

Accepting a wager immediately starts the individual answer timer for the wager player. The buzzer timer is not used because no other player may claim the question.

If the answer timer expires, the Engine records the wager answer as incorrect, subtracts the wager amount, and moves directly to the answer presentation.


## Round and final standings tie-breaking

The leaderboard shown after every completed round, including the final round, is ordered by these criteria:

1. total score;
2. score gain during the last round;
3. total correct answers;
4. correct answers in each round, starting with the latest round and moving backward;
5. total answer attempts;
6. answer attempts in each round, starting with the latest round and moving backward.

Correct answers are therefore exhausted as tie-breakers before total attempts are considered. Players whose metrics remain identical after every criterion share the same position and are co-winners when that position is first.

The same metrics are reversed when the Engine selects the weakest player to open the next round.
