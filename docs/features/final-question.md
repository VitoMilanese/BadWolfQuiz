# Final Question

## Purpose

The final question is a private, staged conclusion to a game. Each player confirms a wager and an answer from their own device before the host judges the submissions.

## Editor and persistence

The quiz editor provides a dedicated final-question editor. It uses the same ordered content-block model as regular questions, including text, image, audio, video, and YouTube blocks for both the question and correct answer.

Final content is stored separately from round questions and copied into the immutable `FinalQuestionSnapshot` when a game lobby is created. A quiz without final content produces no final snapshot and keeps the existing non-final game flow.

## Runtime flow

1. The host starts the final phase after the last board round is complete.
2. Every participating player submits and confirms a private wager.
3. The Engine locks wagering only after every wager is present.
4. The question is released and players submit private answers.
5. The Engine locks answering only after every answer is present.
6. The host judges each answer.
7. The wager is added for a correct answer and subtracted for an incorrect answer.
8. After every submission is judged, the game is completed and final standings are available.

## Initial eligibility and wager rule

By default, every player participates in the final phase, including players with a negative score. The host can disable negative-score participation in global settings or for a specific lobby. The minimum wager is 5 points. The default maximum is 1,000 points. A player whose current score exceeds 1,000 may wager up to that score:

`minimum = 5`

`maximum = max(1000, current score)`.

Eligibility and wager limits remain domain rules and may be refined when the product behavior is finalized.

## Privacy

Other players' wagers and answers are not exposed by player projections. SignalR authenticates a player connection with its reconnect token and accepts wagers and answers only from approved connections. Each player receives only their own submission state; the host receives aggregate progress and sees the submitted values during judging.

## Web presentation

- The last round summary offers the final phase only when the immutable quiz snapshot contains complete final question and answer content.
- During wagering, the host sees submission progress but not the wager amounts.
- Locking wagers releases the final question to participating player devices.
- During answering, the host sees submission progress but not answer text.
- Locking answers starts a sequential presentation of player submissions. The host sees one player name and answer at a time, judges it as correct or incorrect, and then advances automatically to the next submission.
- The broadcast-facing game screen never reveals the configured correct answer. The host can open a separate live answer-key tab on another display; it follows regular, wager, and final questions automatically.
- The game finishes only after every participating answer is judged, then shows the authoritative final standings.
- Players excluded by the negative-score setting remain connected as spectators.

## Commands

- `StartFinalQuestion()`
- `SubmitFinalWager()`
- `LockFinalWagers()`
- `SubmitFinalAnswer()`
- `LockFinalAnswers()`
- `JudgeFinalAnswer()`

Each command validates the current phase and rejects duplicate or out-of-order submissions.
