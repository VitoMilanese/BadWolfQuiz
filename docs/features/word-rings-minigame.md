# Word-rings minigame

BadWolfQuiz includes a standalone three-ring word-classification minigame at `/minigames/word-rings`.

The board contains three overlapping rings. Each ring follows a hidden condition. The player drags every word into the geometric area that represents all conditions satisfied by that word. A word may therefore belong to exactly one ring, one of the three two-ring intersections, the three-ring intersection, or the area outside all rings.

The rings use a fixed color identity across the game and editor: blue, yellow, and red. Visible A/B/C labels are intentionally omitted; the colors identify the rings and their matching revealed-rule cards. The three equal circles are positioned symmetrically so their pairwise and three-way intersection areas remain visually balanced.

Placement membership is calculated from the actual rendered circle geometry in the browser rather than from a separate normalized approximation. The center of the dropped word is therefore evaluated against the same circles the player sees on screen. Once placed, a word's border shows the membership immediately: one ring uses one border color, two-ring intersections split the border between both ring colors, and the three-ring intersection splits the border into thirds.

Word movement uses pointer events rather than the browser's native HTML drag-and-drop mechanism. This keeps the dragged preview identical to the pill-shaped word token and avoids browser-generated square or rectangular drag ghosts.

## Rule selection

Each color has its own persistent pool of rules. When the game page is opened, one rule is selected independently at random for the blue, yellow, and red rings. The board word bank is built from the words attached to those three selected rules, and the expected Venn membership is generated from the selected word sets. Up to two words that do not match any selected rule are added as outside-ring distractors when available; the legacy `дім` and `сир` words are fallback distractors.

The default installation seeds the original three conditions so the minigame remains immediately playable. At least one rule must remain for each ring.

The player can reset the board, reveal or hide the selected rules, and validate the completed arrangement. Revealed rules appear beside their matching rings and are identified by color only. Validation marks correct and incorrect placements without replacing the membership-colored border.

The public `/minigames` catalog exposes the game as the second minigame.

## Editor

The Master Host editor is available at `/Admin/WordRingsEditor`. The page uses three color-coded tabs, one per ring. Each tab lists the persistent rules for that ring and provides a **New rule** action.

A rule contains only:

- the condition text;
- a set of words.

Words may be separated only by commas, semicolons, or whitespace. Other separator punctuation is rejected on both the client and server. Words are de-duplicated case-insensitively before persistence.

Rules are persisted in `App_Data/word-rings-rules.json`. Deletion is performed through the styled BadWolfQuiz `<dialog>` confirmation rather than a browser alert/confirm prompt. The trash action has an explicit dark-red `#9f101b` background with a red border so destructive styling does not depend only on a semantic CSS class name.
