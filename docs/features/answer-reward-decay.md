# Answer Reward Decay

## Purpose

Answer reward decay rewards faster correct answers during the individual answer
timer without changing the penalty for an incorrect answer. The feature is
host-configurable and applies only to regular buzzer questions, including
four-clue questions.

## Settings

The effective game settings contain three values:

- `AnswerRewardDecayEnabled` enables or disables the feature;
- `AnswerRewardDecayStartAfterSeconds` controls how long the full reward remains
  available after the individual answer timer starts;
- `AnswerRewardDecayMinimumPercent` controls the minimum percentage of the
  currently available correct-answer value.

The host defaults are:

- decay disabled unless explicitly enabled;
- start after 10 seconds;
- minimum reward 25%.

`AnswerRewardDecayStartAfterSeconds` is limited to 5-45 seconds.
`AnswerRewardDecayMinimumPercent` is limited to 10-90%. Global settings are
persisted per host and copied into the game-specific settings snapshot when a
game is created, so each lobby may override the values independently.

## Reward calculation

The full currently available correct-answer value remains available through the
configured initial delay. On the first displayed second after that delay, the
reward begins decreasing. From there it decreases linearly in equal steps until
the configured minimum is reached at 1 displayed second remaining.

The displayed countdown uses ceiling semantics, and reward calculation follows
the same visible-second model. At 1 second remaining the minimum reward is used.
If timer processing is delayed and the remaining time reaches zero or becomes
negative, the reward remains clamped to the same minimum.

The calculated reward is rounded to the nearest whole point for both display and
score application. The Web UI displays the current rounded reward in real time,
while the Game Engine remains authoritative for the value actually awarded.

## Scope

Decay applies only while one specific player owns the regular-question answer
timer. It does not consume or permanently reduce the question value.

If that player answers incorrectly or times out and another player may still
buzz, the individual answer phase ends and the available reward returns to the
normal full value for the buzzer phase. A later buzzer winner starts a new decay
window for that player's answer timer.

Incorrect answers always use the existing full-value penalty rule. Reward decay
changes only the value of a correct answer.

Decay does not apply to wager questions or the final question.

## Four-clue questions

For a four-clue question, decay starts from the correct-answer value currently
available for the revealed clue count:

- two clues: 100% of the question value;
- three clues: 50%;
- four clues: 25%.

The decay percentage is then applied to that current value. Revealing another
clue while the buzzer phase is active updates the displayed base reward in real
time. Incorrect answers still deduct 100% of the original question value.

## Host feedback and question flow

Changes to the visible reward use a short emphasis animation so the new value is
noticeable without interrupting play. Four-clue base-value changes use the same
visual treatment.

After a correct answer, incorrect answer, or answer timeout, the host briefly
sees an overlay over the player-card area containing the answering player's name
and the actual score delta. The overlay is transient presentation state; normal
game-state transitions and page refreshes are not delayed for it.

While a player is answering, controls that belong to the buzzer phase, including
**Reveal clue** and **No correct answer**, are hidden. After an incorrect answer
or timeout, if no eligible players remain, the Engine resolves the question
without a correct answer automatically and moves to answer presentation.

## Testing expectations

At minimum, regression coverage should verify:

- disabled decay preserves the normal correct-answer value;
- the full reward remains available during the configured delay;
- decay begins on the first displayed second after the delay;
- the configured minimum is reached at 1 second remaining and is clamped after it;
- displayed and awarded values use the same whole-point rounding;
- incorrect answers always deduct the full applicable penalty;
- returning to the buzzer phase restores the normal available reward;
- each later buzzer winner starts an independent decay window;
- wager and final questions do not use decay;
- four-clue decay uses the currently revealed clue value as its base;
- host controls are hidden while a player is answering;
- a question resolves automatically when no eligible buzzer players remain;
- correct, incorrect, and timeout outcomes expose the actual score delta in the
  transient host feedback overlay.
