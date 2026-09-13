# Word-rings minigame

BadWolfQuiz includes a standalone three-ring word-classification minigame at `/minigames/word-rings`.

The board contains three overlapping rings, labelled A, B, and C. Each ring follows a hidden condition. The player drags every word into the geometric area that represents all conditions satisfied by that word. A word may therefore belong to exactly one ring, one of the three two-ring intersections, the three-ring intersection, or the area outside all rings.

The first built-in puzzle uses these hidden rules:

- A: the word names an animal;
- B: the word contains the letter `а`;
- C: the word contains exactly five letters.

The player can reset the board, reveal or hide the rules, and validate the completed arrangement. Validation marks correct and incorrect placements and reports the number of errors.

The public `/minigames` catalog exposes the game as the second minigame.

## Editor

Authenticated hosts see a **Word-rings editor** entry in the header menu. The route `/Admin/WordRingsEditor` currently contains a placeholder page only. Puzzle persistence, custom rule definitions, word-set editing, and host-specific game management are intentionally deferred to the next editor implementation step.
