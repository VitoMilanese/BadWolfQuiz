# Word-rings minigame

BadWolfQuiz includes a standalone three-ring word-classification minigame at `/minigames/word-rings`.

The board contains three overlapping rings, labelled A, B, and C. Each ring follows a hidden condition. The player drags every word into the geometric area that represents all conditions satisfied by that word. A word may therefore belong to exactly one ring, one of the three two-ring intersections, the three-ring intersection, or the area outside all rings.

The rings use a fixed visual identity across the game and editor: A is blue, B is yellow, and C is red. The three equal circles are positioned symmetrically so their pairwise and three-way intersection areas remain visually balanced.

Placement membership is calculated from the actual rendered circle geometry in the browser rather than from a separate normalized approximation. The center of the dropped word is therefore evaluated against the same circles the player sees on screen. Once placed, a word's border shows the membership immediately: one ring uses one border color, two-ring intersections split the border between both ring colors, and the three-ring intersection splits the border into thirds.

The first built-in puzzle uses these hidden rules:

- A: the word names an animal;
- B: the word contains the letter `а`;
- C: the word contains exactly five letters.

The player can reset the board, reveal or hide the rules, and validate the completed arrangement. Validation marks correct and incorrect placements without replacing the membership-colored border.

The public `/minigames` catalog exposes the game as the second minigame.

## Editor

Authenticated hosts see a **Word-rings editor** entry in the header menu. The route `/Admin/WordRingsEditor` currently contains a placeholder page only. Puzzle persistence, custom rule definitions, word-set editing, and host-specific game management are intentionally deferred to the next editor implementation step.
