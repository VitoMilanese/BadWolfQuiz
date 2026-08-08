# BadWolfQuiz Product Vision

## Purpose

BadWolfQuiz is a host-controlled multiplayer quiz application. A host runs the game, players join from their own devices, and a separate presentation view can be used for the shared screen. The product also provides a lightweight feedback channel through which users can contact the developer without requiring a host account.

The current gameplay model supports individual players only. Teams are intentionally out of scope.

## Roles

### Host

The host controls the game flow, selects questions, starts and pauses timers, enters wagers for special questions, judges answers, and advances between phases.

### Player

A player joins the game from a web page on a personal device. Depending on the current phase, the page may show a waiting screen, wager keypad, answer input, or status confirmation.

### Presentation

The presentation view is read-only. It displays the board, question content, timer, revealed answer, and results without exposing host controls.

## Core gameplay

1. Players join the lobby.
2. The host starts the game.
3. The game shows the board for the current round.
4. The host selects a question.
5. The question is presented and the timer may be started.
6. The host can pause and resume the timer at any time while it is active.
7. For a four-clue question, another clue may be revealed manually or automatically when the timer expires; each reveal starts a new full timer interval.
8. The host judges the answer and updates the player's score.
9. The game returns to the board until all regular rounds are complete.
10. An optional final question may follow the regular rounds.
11. Final results are shown after all final answers are judged.

## Special wager questions

Some regular questions require the answering player to choose a wager. The player announces the amount verbally and the host enters it using a dedicated numeric keypad. The wager is validated and confirmed before the question begins.


## Four-clue questions

A regular question may use a four-clue presentation. It contains exactly four text, image, or audio clues. Two clues are visible initially and the remaining clues are revealed one at a time, either by the host or automatically when the question timer expires.

Each successful clue reveal starts a new full question-timer interval. Correct answers are worth 100%, 50%, or 25% of the question value depending on how many clues have been revealed; an incorrect answer always deducts the full question value.

## Final question

After all regular rounds, the quiz may contain one final question.

The final phase consists of two private submissions from every eligible player:

1. Each player enters a wager on the player web page.
2. After wagers are locked, each player submits a textual answer on the same page.

The host sees submission status while input is open. Submitted amounts and answers should remain hidden until the relevant phase has been locked, unless a future product decision explicitly changes that behavior.


## User feedback

Users can contact the developer to ask a question, suggest an idea, report a problem, or leave other feedback. A submission starts a persistent conversation rather than a one-time question/answer exchange.

The same browser can keep a local history of submitted conversations so the user can return to them without creating an account. The developer can review and reply to conversations through the application and receive notifications through the configured Discord bot.

This feedback workflow is separate from gameplay and does not change the authoritative game-state model.

## Product principles

- The server is the source of truth.
- Every game action is validated against the current game state.
- The original quiz definition is never modified during gameplay.
- Runtime game state is separate from editor and persistence models.
- Host, player, and presentation clients receive only the data appropriate to their role.
- Reconnection must restore the current view from server state.
