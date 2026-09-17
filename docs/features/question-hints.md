# Question hints

Standard questions can optionally contain from one to four hint blocks. Hint blocks are independent from question and answer content and support Text and Image content only.

## Editor

- Hints are available only for the Standard question presentation type.
- The `Add hints` toggle controls the hint collection.
- A question can contain at most four hint blocks.
- Empty hint blocks are ignored by gameplay.
- Text hints are limited to 30 characters in the editor.
- Hint blocks are preserved by question copy, quiz clone, package import/export, and media archive/restore flows.

## Host gameplay

For an active ordinary Standard question with at least one non-empty hint, the host controls next to `No correct answer` include `Show hint`. After the first hint has been revealed, the action changes to `Show another hint` while unrevealed hints remain.

Revealed hints are shown in a persistent panel at the bottom of the question presentation. The panel stays open, is part of the normal layout rather than covering the question, and can contain both text and images.

Only hints that contain actual content count toward gameplay. A text hint must contain non-whitespace text; an image hint must contain stored image data.

## Reward reduction

Hint penalties are always calculated from the question's original point value, never from the already reduced value.

| Total non-empty hints | Revealed | Reduction |
| ---: | ---: | ---: |
| 4 | 1 | 12% |
| 4 | 2 | 25% |
| 4 | 3 | 37% |
| 4 | 4 | 50% |
| 3 | 1 | 16% |
| 3 | 2 | 33% |
| 3 | 3 | 50% |
| 2 | 1 | 25% |
| 2 | 2 | 50% |
| 1 | 1 | 50% |

The point reduction is rounded to the nearest whole point using midpoint-away-from-zero rounding. The remaining reward is never lower than 1 point.

Incorrect regular answers continue to use the question's original point value for the normal incorrect-answer penalty; hints reduce only the reward for a correct answer.

## Runtime state

The runtime stores both the total hint count used for the current question and the number of hints already revealed. Active-game persistence therefore keeps the visible-hint count and reduced correct-answer value across host refreshes and game recovery.

Wager questions and other non-Standard presentation types do not expose the hint reveal action.
