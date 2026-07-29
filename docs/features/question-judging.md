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
