# Word-rings minigame

BadWolfQuiz includes a standalone three-ring word-classification minigame at `/minigames/word-rings`.

The board contains three overlapping rings. Each ring follows a hidden condition. The player drags every word into the geometric area that represents all conditions satisfied by that word. A word may therefore belong to exactly one ring, one of the three two-ring intersections, the three-ring intersection, or the area outside all rings.

The rings use a fixed color identity across the game and editor: blue, yellow, and red. Visible A/B/C labels are intentionally omitted; the colors identify the rings and their matching revealed-rule cards. The three equal circles are positioned symmetrically so their pairwise and three-way intersection areas remain visually balanced.

Placement membership is calculated from the actual rendered circle geometry in the browser rather than from a separate normalized approximation. The center of the dropped word is therefore evaluated against the same circles the player sees on screen. Once placed, a word's border shows the membership immediately: one ring uses one border color, two-ring intersections split the border between both ring colors, and the three-ring intersection splits the border into thirds.

Word movement uses pointer events rather than the browser's native HTML drag-and-drop mechanism. This keeps the dragged preview identical to the pill-shaped word token and avoids browser-generated square or rectangular drag ghosts.

## Rule selection

Each color has its own persistent pool of rules. Every new rule is enabled for games by default, and the editor can temporarily disable a rule without deleting it. Disabled rules and their word sets are excluded entirely from game generation, including outside-ring distractor selection.

When the game page is opened, one enabled, non-empty rule is selected independently at random for the blue, yellow, and red rings. The board word bank is built from the words attached to those three selected rules, and the expected Venn membership is generated from the selected word sets. Up to two words from other enabled rules that do not match any selected rule are added as outside-ring distractors when available; the legacy `дім` and `сир` words are fallback distractors.

The default installation seeds the original three conditions so the minigame remains immediately playable. At least one rule must remain for each ring, and at least one non-empty rule per ring must remain enabled for games.

The player can reset the board, reveal or hide the selected rules, and validate the completed arrangement. Revealed rules appear beside their matching rings and are identified by color only. Validation marks correct and incorrect placements without replacing the membership-colored border.

The public `/minigames` catalog exposes the game as the second minigame.

## Editor

The Master Host editor is available at `/Admin/WordRingsEditor`. Its first tab is **All words**, followed by the blue, yellow, and red rule tabs. Opening the editor without an explicit `ring` query parameter selects **All words**. Tab changes, paging, rule mutations, and word mutations are loaded asynchronously and do not perform a full page navigation.

### All words

The **All words** tab derives one case-insensitive catalog from every word referenced by any rule. The catalog is sorted alphabetically using Ukrainian collation and displayed in pages of 25, with the same pager shown above and below the list. Each row also shows how many rules in each ring currently contain the word.

Every word row provides edit and delete actions. Edit opens a styled dialog where the host selects a ring and browses that ring's rules in pages of 10. A checkbox is checked when the current word belongs to that rule, and an optional filter shows only rules that already contain the word. Changes can be made across multiple rule pages and rings, then saved as one batched mutation so the JSON rule file is written only once. The **Add word** action uses the same membership dialog with an editable word field; a word must contain at least three characters and becomes part of the catalog once it is assigned to at least one rule.

Words are presented in uppercase throughout the All Words list, rule word lists, word add/edit field, and delete confirmation, while comparisons and de-duplication remain case-insensitive.

Deleting a single word removes it from every rule in all three rings. The All Words toolbar also has a destructive delete-all action, guarded by a styled confirmation dialog, that clears every word from every rule while keeping the rules themselves. If the configuration no longer has a playable enabled rule for each color, the game opens with an empty puzzle instead of failing.

### Ring rule tabs

Each color tab lists the persistent rules for that ring and provides a **New rule** action plus a destructive delete-all action for that ring, guarded by a styled confirmation dialog. A rule contains the condition text, a set of words, and an enabled/disabled game-participation flag.

Rules can be created, edited, enabled/disabled, and deleted without a full page refresh. The pencil action between the enabled checkbox and trash action opens the same styled editor dialog pattern and allows both the condition text and attached words to be changed. All rule mutations are submitted through AJAX and the visible rule list is refreshed in-place.

Words inside a rule may be separated only by commas, semicolons, or literal spaces. Tabs, line breaks, and other separator characters are rejected on both the client and server. Words are de-duplicated case-insensitively before persistence.

Rules are persisted in `App_Data/word-rings-rules.json`. Existing rule files created before the enabled flag was introduced are treated as enabled for backward compatibility. The editor uses the shared short-lived save/error popup used by the quiz editors instead of leaving permanent page-level status banners.

Deletion is performed through styled BadWolfQuiz `<dialog>` confirmations rather than browser alert/confirm prompts. The create, edit, delete, word-membership, and import-summary dialogs are constrained to the viewport. Destructive actions use the explicit dark-red `#9f101b` background and red border already used by the rule editor.

## CSV import and export

The **All words** toolbar can export the complete rule configuration as UTF-8 CSV. The stable columns are:

`Ring,Rule,Enabled,Words`

`Ring` uses the stable keys `blue`, `yellow`, and `red`. `Words` is a semicolon-separated list inside a normal quoted CSV field. The exported file can therefore be used as a backup or as an import template. `Enabled` is round-tripped for both new and existing rules, so the active/inactive game-participation state is restored by import.

CSV import is merge-only for rules and words. It never deletes rules and never removes words from existing rules. Rows are matched case-insensitively by ring plus rule text. Missing words are appended to matching rules; missing rules are created from the CSV row. Duplicate rows in the same import are merged before persistence. Import is bounded to a 5 MB file, 10,000 non-empty data rows, and the existing 250-word-per-rule limit.

After a successful import, the summary still reports per ring how many word-to-rule additions and new rules were created. It additionally reports the number of truly new unique words that did not exist anywhere in the catalog before the import. Three detail actions open lists of: every new word, every new rule, and every pre-existing rule that received one or more imported words. The existing-rule detail also shows the exact words added to that rule. Detail matching uses the same case-insensitive ring/rule/word semantics as the import merge, and displayed words remain uppercase.

The detail comparison is captured against the current exported configuration immediately before the import POST, so the dialog distinguishes a globally new word from an existing word that was merely attached to another rule. If the import produced no additions, the dialog keeps the explicit “nothing added” result and the detail actions remain disabled at zero.

Import and export use the same three-note Web Audio completion tone as quiz import/export. Transfer controls are disabled while an operation is running, and the shared `BadWolfBusy` overlay is shown after a short delay so quick operations do not flicker while long ones still provide clear feedback.

## Performance notes

The rule store keeps one immutable in-memory snapshot containing both rules and a precomputed unique-word index. Read-only word paging therefore does not repeatedly flatten and sort every rule. Mutations are serialized through the existing store gate, build a new snapshot only after a successful write, and publish it atomically. Word membership edits are batched, and a CSV merge performs a single persistence write regardless of how many rows changed.

New-word import details use pages of 50 entries, while imported-rule detail dialogs keep their five-item pages.
