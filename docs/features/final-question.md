# Final Question

## Purpose

The final question is a private, staged conclusion to a game. Each player confirms a wager and an answer from their own device before the host judges the submissions.

## Editor and persistence

The quiz editor provides a dedicated final-question editor. It uses the same ordered content-block model as regular questions, including text, image, audio, video, and YouTube blocks for both the question and correct answer.

Final content is stored separately from round questions and copied into the immutable `FinalQuestionSnapshot` when a game lobby is created. A quiz without final content produces no final snapshot and keeps the existing non-final game flow.

Active-game recovery starts once normal gameplay has meaningful recoverable state. Opening the first regular question still starts persistence as before. Advancing directly to the Final Question also starts persistence when the session reaches `FinalWagering`, even if every regular question is still `Available`. `FinalWagering`, `FinalAnswering`, and `FinalJudging` snapshots are eligible for restoration after a process restart.

## Runtime flow

1. After the last board round is complete, the host enters a localized **Final question** transition page.
2. The transition has no manual controls and automatically continues after 3 seconds.
3. Every participating player submits and confirms a private wager. While a wager is missing for an inactive player, the host can submit the minimum allowed wager on that player's behalf.
4. The Engine locks wagering only after every wager is present.
5. The question is released and players submit private answers. While an answer is missing for an inactive player, the host can submit `-` on that player's behalf.
6. The Engine locks answering only after every answer is present.
7. The host judges each answer.
8. The wager is added for a correct answer and subtracted for an incorrect answer.
9. After every submission is judged, the game is completed and final standings are available.

The host can remove a player during final wagering, answering, or judging. Removal also removes that player's final-question submission so the removed player cannot block phase progression. If the last remaining player is removed during any final-question phase, the final question and game complete immediately with empty final standings.

The same 3-second transition is used when the host forces advancement directly to the final question. A quiz without a final question never shows this transition.

## Initial eligibility and wager rule

By default, every player participates in the final phase, including players with a negative score. The host can disable negative-score participation in global settings or for a specific lobby. The minimum wager is 5 points. The default maximum is 1,000 points. A player whose current score exceeds 1,000 may wager up to that score:

`minimum = 5`

`maximum = max(1000, current score)`.

Eligibility and wager limits remain domain rules and may be refined when the product behavior is finalized.

## Privacy

Other players' wagers and answers are not exposed by player projections. SignalR authenticates a player connection with its reconnect token and accepts wagers and answers only from approved connections. Each player receives only their own submission state; the host receives aggregate progress and sees the submitted values during judging.

## Web presentation

- The last round summary offers the final phase only when the immutable quiz snapshot contains complete final question and answer content.
- Normal and forced entry into the final phase first show the localized, automatic 3-second **Final question** transition.
- When a final question is available, the host action menu provides a shortcut to leave the current regular round and advance directly to the final phase. The host must confirm the action. Unclosed regular-round questions are left unchanged rather than being force-resolved by this navigation action.
- During final wagering, player submission state is displayed as an always-visible vertical panel on the right on desktop host layouts. The panel scrolls independently for long player lists so the waiting message and **Show question** action remain available in the main area.
- During final answering, player submission state moves into a right-side drawer. On pointer-driven desktop layouts the drawer remains collapsed to a narrow player handle until hover or keyboard focus opens it. Narrow/touch layouts keep an inline responsive fallback.
- Player names that do not fit their available width use the shared overflow-only ping-pong marquee in the final submission list, bottom player cards, and the right-side in-game scoreboard. The text moves only by the measured overflow distance, pauses at each visible edge, then reverses direction instead of scrolling into an empty interval.
- During wagering, the host sees submission progress but not the wager amounts.
- For every inactive player whose wager is still missing, the host can submit the minimum allowed wager on the player's behalf.
- Locking wagers releases the final question to participating player devices.
- During answering, the host sees submission progress but not answer text.
- For every inactive player whose answer is still missing, the host can submit `-` on the player's behalf.
- Host fallback wager/answer actions use a dedicated lightweight AJAX endpoint. Rapid clicks are serialized, duplicate requests are idempotent, only the affected host row is updated locally, and no full host or player-page refresh broadcast is triggered by those fallback operations.
- Normal player wager/answer submissions also preserve unrelated live UI state. The submitting player receives their own confirmed state, other players keep any wager or answer they are still typing, and the host updates only submission progress controls instead of rebuilding the gameplay view or player cards.
- The fallback helper is loaded eagerly. If the first fallback click occurs before the helper asset finishes loading, the bootstrap captures and queues that click rather than allowing the legacy form-navigation path to run.
- The host can remove a player throughout final wagering, answering, and judging. The final-question state and host controls refresh immediately after player changes.
- Removing the final remaining player completes the game immediately. The completed host screen keeps **Finish game** available but does not render **Final results** or an empty podium when `FinalStandings` is empty.
- Locking answers starts a sequential presentation of player submissions. The host sees one player name and answer at a time, judges it as correct or incorrect, and then advances automatically to the next submission. Player devices stop rendering the final-question content as soon as the session enters `FinalJudging`; they show only the waiting-for-judging state.
- As soon as the host submits **Correct** or **Incorrect**, both judging buttons are disabled together and the current judging form remains locked until the refreshed view replaces it. A genuine command error releases the lock so the host can retry.
- The host **Tools** menu remains available on the inter-round leaderboard and throughout final wagering, answering, and judging. In these limited states, **Choose random player**, **Next round**, and **Advance to final question** are hidden, while applicable tools such as answer history, answer key, blocked-player management, and game settings remain available.
- Join information is exposed as a dedicated QR button in the game header between **Tools** and the Discord microphone button rather than as an item inside **Tools**. The button opens the existing join-information panel.
- The final-question host panel uses the same viewport-width presentation as an active regular question. During wagering/answering the panel uses compact spacing and gives the question/media area substantially more usable width and height while keeping media viewport-bounded.
- Final-answer judging uses the same wide presentation rules so a long player answer is not artificially constrained to a narrow centered column.
- When answering transitions to judging, any stale submission-list DOM is hidden immediately so the former drawer cannot reappear below the question while the live host view refreshes.
- Answer history and answer-key actions are exposed through **Tools** rather than duplicated as standalone buttons on the final-question page.
- The game-settings dialog remains available during final wagering, answering, and judging.
- The broadcast-facing game screen never reveals the configured correct answer. The host can open a separate live answer-key tab on another display; it follows regular, wager, and final questions automatically.
- The game normally finishes after every participating answer is judged, then shows the authoritative final standings. The zero-player removal case completes immediately with no standings.
- Players excluded by the negative-score setting remain connected as spectators.

### Persistent host navigation

When the host is already inside the running-game shell, the normal and forced Final Question transition is mounted into the existing gameplay region rather than replacing the browser document. Final Wagering, Final Answering, Final Judging, and final standings continue through the same persistent host navigation path, preserving the long-lived SignalR connection, player cards, header controls, and other host state.

The forced Final Question confirmation submits through the asynchronous host flow. The natural unfinished-round warning closes before the host advances; choosing **Return to an unfinished round** performs the actual return in one action. Unsupported or failed transitions retain normal browser navigation as a fallback.

Repeated live refreshes of the same final-results podium do not recreate the existing podium DOM, so its entrance animation is not restarted by duplicate state updates.

## Commands

- `StartFinalQuestion()`
- `ForceAdvanceToFinalQuestion()`
- `SubmitFinalWager()`
- `LockFinalWagers()`
- `SubmitFinalAnswer()`
- `LockFinalAnswers()`
- `JudgeFinalAnswer()`

Each command validates the current phase and rejects duplicate or out-of-order submissions.

## Unfinished round guard

Before entering the Final Question through normal progression, the host is warned if any regular round still contains unclosed questions and can either continue to the Final Question or return to an unfinished round. A forced Final Question action from **Tools** always requires confirmation and, when players are present, shows the current round leaderboard before the Final Question transition. It ignores the current round when deciding whether to offer a return option. The return option considers only unfinished rounds that have already been visited: all earlier visited rounds plus later rounds reached before the host returned backward. Rounds the game has never entered are ignored. When players are present, choosing the return option shows the current round leaderboard before the selected unfinished round intro; without players, the intro opens directly. The dedicated dialog also lets the host stay in the current round.

Regular-round navigation can move backward to the nearest unfinished round and skips fully completed rounds in either direction. Forced **Next round** navigation leaves unopened questions available so the host can return to them later.
