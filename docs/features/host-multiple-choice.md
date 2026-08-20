# Host-selected multiple-choice questions

## Purpose

Host-selected multiple-choice questions combine the normal buzzer flow with a fixed list of answer options controlled by the host. Players still buzz from their own devices; the host judges the answer by selecting the option the player gave.

## Authoring contract

- A question contains the normal question content blocks.
- The answer contains between 4 and 10 distinct text options.
- Each option is limited to 20 characters.
- The first option in answer-block order is the correct option and the editor presents that state explicitly.
- Reordering answer options changes which option is correct.
- This question type cannot be a wager question and is excluded from random wager selection.

## Answer presentation

Only the correct option is presented as the answer for this question type.

- The Question Editor answer preview shows only the first/correct option.
- Previewing the answer of an already closed question also shows only the correct option.
- During live gameplay, when the question enters `ShowingAnswer`, only the correct answer block is rendered; incorrect answer-option blocks are not rendered as part of the answer.

## Host answer-option panel

During an active question, the host sees a vertical answer-option panel on the right side of the gameplay screen.

- Available answer options are displayed in a stable randomized order for the current game/question rather than editor order.
- Eliminating an incorrect option does not reshuffle the remaining options.
- **Nobody answered** is always rendered after all answer-option buttons and is never part of the shuffle.
- Before a player owns the buzzer, answer-option buttons are disabled and **Nobody answered** is enabled.
- After a player owns the buzzer, answer-option buttons become the judgment controls and **Nobody answered** is disabled until the buzzer becomes free again.
- The standard **Correct / Incorrect** judgment controls and the generic resolve control are not rendered for this presentation type.
- The header action menu remains above the answer-option panel in the stacking order.

The panel participates in the normal AJAX host-gameplay lifecycle. The host-selected multiple-choice assets are loaded when the Lobby shell is mounted, including the initial `RoundIntro` → `Lobby` transition, so opening the first question does not require a browser refresh before the panel appears.

## Dynamic value

The current reward and penalty use an equal-step percentage from 100% with all original options present to 50% with three options remaining. The percentage is calculated as:

`50 + ceil(50 * (remaining - 3) / (original - 3))`

Values at or below three remaining options are clamped to 50%. The question value is the base point value multiplied by the current percentage and rounded to the nearest integer, away from zero.

Examples:

- 4 options: 100%, 50%
- 5 options: 100%, 75%, 50%
- 6 options: 100%, 84%, 67%, 50%
- 7 options: 100%, 88%, 75%, 63%, 50%

The 7-option series intentionally follows the equal-step formula above; it replaces the earlier inconsistent 100/90/80/70/50 example from issue #259.

Whenever the current value changes after an incorrect answer or timer elimination, both the host panel and the reward displayed beside the question/category heading are updated to the new value.

## Runtime behavior

Selecting the correct option awards the current value, closes the question, and shows the correct answer through the normal AJAX gameplay refresh without a full-page reload.

Selecting an incorrect option subtracts the current value, removes that option, recalculates the value, and reopens the buzzer for players who have not already answered. A player keeps the normal one-attempt-per-question restriction.

If an incorrect answer leaves no player who is still eligible to buzz, the question closes automatically and shows the correct answer.

**Nobody answered** closes the question through the existing no-correct-answer flow and immediately shows the correct answer.

When the buzzer timer expires, one random remaining incorrect option is removed. The correct option is never removed. The current value is recalculated and, while more than two options remain, the buzzer timer restarts for the next attempt.

If only two options remain after either an incorrect answer or timer elimination, the question automatically closes and shows the correct answer without another buzzer attempt.

## Recovery

The set of remaining options is part of the active-game snapshot, so recovery does not restore eliminated choices. The current reward is derived from the recovered remaining-option state, and the stable host display order is reconstructed from the game and question identifiers.

## Release

Host-selected multiple-choice questions are introduced in BadWolfQuiz Web `1.20.0` (`web-v1.20.0`).
