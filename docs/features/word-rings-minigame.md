# Word-rings minigame

BadWolfQuiz includes a standalone three-ring word-classification minigame at `/minigames/word-rings`.

The board contains three overlapping rings. Each ring follows a hidden condition. The player drags every word into the geometric area that represents all conditions satisfied by that word. A word may therefore belong to exactly one ring, one of the three two-ring intersections, the three-ring intersection, or the area outside all rings.

The rings use a fixed color identity across the game and editor: blue, yellow, and red. Visible A/B/C labels are intentionally omitted; the colors identify the rings and their matching revealed-rule cards. The three equal circles are positioned symmetrically so their pairwise and three-way intersection areas remain visually balanced.

Placement membership is calculated from the actual rendered circle geometry in the browser rather than from a separate normalized approximation. The center of the dropped word is therefore evaluated against the same circles the player sees on screen. Once placed, a word's border shows the membership immediately: one ring uses one border color, two-ring intersections split the border between both ring colors, and the three-ring intersection splits the border into thirds.

Word movement uses pointer events rather than the browser's native HTML drag-and-drop mechanism. This keeps the dragged preview identical to the pill-shaped word token and avoids browser-generated square or rectangular drag ghosts.

## Rule selection

Each color has its own persistent pool of rules. Every new rule is enabled for games by default, and the editor can temporarily disable a rule without deleting it. Disabled rules and their word sets are excluded from game generation, including outside-ring distractor selection.

When the game page is opened, one enabled, non-empty rule is selected independently at random for the blue, yellow, and red rings. The board word bank is built from the words attached to those three selected rules, and the expected Venn membership is generated from the selected word sets. Up to two words from other enabled rules that do not match any selected rule are added as outside-ring distractors when available; the legacy `дім` and `сир` words are fallback distractors.

The default installation seeds the original three conditions so the minigame remains immediately playable. At least one rule must remain for each ring, and at least one non-empty rule per ring must remain enabled for games.

The player can reset the board, reveal or hide the selected rules, and validate the completed arrangement. Revealed rules appear beside their matching rings and are identified by color only. Validation marks correct and incorrect placements without replacing the membership-colored border.

The public `/minigames` catalog exposes the game as the second minigame.

## Editor

The Master Host editor is available at `/Admin/WordRingsEditor`. Opening the editor without an explicit tab selects **All words** by default. The tab order is **All words**, blue, yellow, and red. Tab changes, paging, rule mutations, and word mutations are loaded asynchronously and do not perform a full page navigation.

### All words

The **All words** tab derives one case-insensitive catalog from every word referenced by any rule. The catalog is sorted alphabetically using Ukrainian collation and displayed in pages of 25, matching the paging scale used by `Admin/MinigameEditor`. The same pager is rendered above and below the word list. Each row also shows how many rules in each ring currently contain the word.

Every word row provides edit and delete actions. Edit opens a styled dialog where the host selects a ring and browses that ring's rules in pages of 25. A checkbox is checked when the current word belongs to that rule. Changes can be made across multiple rule pages and rings, then saved as one batched mutation so the JSON rule file is written only once. The **Add word** action uses the same membership dialog with an editable word field; a word must contain at least three characters, and it becomes part of the catalog once it is assigned to at least one rule.

Words are shown in uppercase in the All words list, rule word lists, the word-membership editor, the add-word field, and the word-deletion dialog. This is presentation only: matching, de-duplication, membership checks, deletion, and CSV merge comparisons remain case-insensitive.

Deleting a word removes it from every rule in all three rings. Empty rules are automatically disabled when possible. An operation is rejected if it would leave a ring without any enabled non-empty rule, because such a ring could no longer generate a playable game.

### Ring rule tabs

Each color tab lists the persistent rules for that ring and provides a **New rule** action. A rule contains the condition text, a set of words, and an enabled/disabled game-participation flag.

Rules can be created, edited, enabled/disabled, and deleted without a full page refresh. The pencil action between the enabled checkbox and trash action opens the same styled editor dialog pattern and allows both the condition text and the attached word set to be changed. All rule mutations are submitted through AJAX and the visible rule list is refreshed in-place.

Words inside a rule may be separated only by commas, semicolons, or literal spaces. Tabs, line breaks, and other separator characters are rejected on both the client and server. Every word must be at least three characters long. Words are de-duplicated case-insensitively before persistence.

Rules are persisted in `App_Data/word-rings-rules.json`. Existing rule files created before the enabled flag was introduced are treated as enabled for backward compatibility. The editor uses the shared short-lived save/error popup used by the quiz editors instead of leaving permanent page-level status banners.

Deletion is performed through styled BadWolfQuiz `<dialog>` confirmations rather than browser alert/confirm prompts. The create, edit, delete, word-membership, and import-summary dialogs are constrained to the viewport. Destructive actions use the explicit dark-red `#9f101b` background and red border already used by the rule editor.

## CSV import and export

The **All words** toolbar can export the complete rule configuration as UTF-8 CSV. The stable columns are:

`Ring,Rule,Enabled,Words`

`Ring` uses the stable keys `blue`, `yellow`, and `red`. `Enabled` stores whether the rule is active for game generation. `Words` is a semicolon-separated list inside a normal quoted CSV field. The exported file can therefore be used as a backup or as an import template.

CSV import is merge-only for rules and words: it never deletes rules and never removes words from existing rules. Rows are matched case-insensitively by ring plus rule text. Missing words are appended to matching rules, missing rules are created from the CSV row, and the imported `Enabled` value is applied to both new and matching existing rules so active/inactive state round-trips through export/import. The entire merged configuration is validated before one persistence write; an import that would leave a ring without an enabled non-empty rule is rejected. Duplicate rows in the same import are merged before persistence. Import is bounded to a 5 MB file, 10,000 non-empty data rows, and the existing 250-word-per-rule limit.

After a successful import, a styled summary dialog reports for each color how many word-to-rule additions were made and how many new rules were created. If the import added no words or rules, the dialog explicitly says that nothing was added; rule-state changes may still have been applied.

Import and export use the same three-note Web Audio completion tone as quiz import/export. Transfer controls are disabled while an operation is running, and the shared `BadWolfBusy` overlay is shown after a short delay so quick operations do not flicker while long ones still provide clear feedback.

## Performance notes

The rule store keeps one immutable in-memory snapshot containing both rules and a precomputed unique-word index. Read-only word paging therefore does not repeatedly flatten and sort every rule. Mutations are serialized through the existing store gate, build a new snapshot only after a successful write, and publish it atomically. Word membership edits are batched, and a CSV merge — including imported enabled-state changes — performs a single persistence write regardless of how many rows changed.
