# Host-selected multiple-choice questions

## Purpose

Host-selected multiple-choice questions combine the normal buzzer flow with a fixed list of answer options controlled by the host. Players still buzz from their own devices; the host judges the answer by selecting the option the player gave.

## Authoring contract

- A question contains the normal question content blocks.
- The answer contains between 4 and 10 distinct text options.
- Each option is limited to 20 characters.
- The first option in answer-block order is the correct option and the editor presents that state explicitly.
- This question type cannot be a wager question and is excluded from random wager selection.

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

## Runtime behavior

Before a player owns the buzzer, answer-option controls are disabled and the host may close the question with the existing no-correct-answer action. After a player buzzes, the available options become the host's judgment controls.

Selecting the correct option awards the current value and shows the answer. Selecting an incorrect option subtracts the current value, removes that option, recalculates the value, and reopens the buzzer for players who have not already answered.

When the buzzer timer expires, one random remaining incorrect option is removed. The correct option is never removed. If only two options remain after either an incorrect answer or timer elimination, the question automatically closes and shows the answer without another buzzer attempt.

The set of remaining options is part of the active-game snapshot so recovery does not restore eliminated choices.
