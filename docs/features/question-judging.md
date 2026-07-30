# Question Judging

## Purpose

Question judging applies score changes, records answer attempts, resolves questions, and determines which player receives the right to select the next question. The Game Engine owns these rules; host controls only submit commands.

## Regular questions

A regular question may receive attempts from multiple players.

For each judged attempt:

- a correct answer adds the question point value;
- an incorrect answer subtracts the question point value;
- a player cannot be judged more than once for the same question;
- an incorrect attempt leaves the question open for another eligible player;
- a correct attempt resolves the question.

When a player answers correctly, that player becomes the active player. If the current active player answers correctly, the active player remains unchanged.

The host can explicitly resolve a regular question with no correct answer. In that case no additional score is applied and the existing active player keeps the right to select the next question.

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

The current host UI allows the host to choose an eligible player and judge that player's answer. Player score lists receive real-time updates.

A future integration will connect the Runtime Engine to the buzzer winner and render complete immutable question content. Until that integration exists, the host manually identifies the answering player.

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

The **buzzer window timer** is the total time available for eligible players to claim an answer attempt. The host does not start this timer separately: activating the buzzer starts it automatically.

When a player wins the buzzer:

- the buzzer closes;
- the buzzer window timer pauses and preserves its remaining duration;
- the host view highlights the answering player;
- a separate **answer timer** starts for that player.

The host may judge the answer before the answer timer expires. If the answer timer expires before the player gives a correct answer, the Engine records an incorrect answer automatically.

After an incorrect answer:

- the question remains visible;
- the player is excluded from further attempts for that question;
- the buzzer becomes available to the remaining eligible players;
- the buzzer window timer resumes from the exact duration that remained when the previous player buzzed.

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
