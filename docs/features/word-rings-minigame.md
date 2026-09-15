# Word-rings minigame

BadWolfQuiz includes a standalone three-ring word-classification minigame at `/minigames/word-rings`.

The board contains three overlapping rings. Each ring follows a hidden condition. The player drags every word into the geometric area that represents all conditions satisfied by that word. A word may therefore belong to exactly one ring, one of the three two-ring intersections, the three-ring intersection, or the area outside all rings.

The rings use a fixed color identity across the game and editor: blue, yellow, and red. Visible A/B/C labels are intentionally omitted; the colors identify the rings and their matching revealed-rule cards. The three equal circles are positioned symmetrically so their pairwise and three-way intersection areas remain visually balanced.

Placement membership is calculated from the actual rendered circle geometry in the browser rather than from a separate normalized approximation. The center of the dropped word is therefore evaluated against the same circles the player sees on screen. Once placed, a word's border shows the membership immediately: one ring uses one border color, two-ring intersections split the border between both ring colors, and the three-ring intersection splits the border into thirds.

Word movement uses pointer events rather than the browser's native HTML drag-and-drop mechanism. This keeps the dragged preview identical to the pill-shaped word token and avoids browser-generated square or rectangular drag ghosts.

## Solo play

Solo play uses a maximum 20-word session pool with at most 10 words visible in the right-side bank at once. As soon as a bank word is dragged onto the table, the next queued word is streamed into the bank. Only one unchecked new word can be placed at a time.

The solo selector strongly prefers words that satisfy one or more selected rules, prioritizes three-ring and two-ring overlaps, and balances coverage across all three rings. Incorrect checked words are moved automatically into their correct region while retaining red error feedback. A solo game ends after 10 correct placements or when all 20 available words have been attempted.

When a solo game ends, a custom animated victory/defeat dialog is shown instead of a browser alert. Reset requests a fresh puzzle in-place, clears the completed board, and keeps the browser on the same clean URL instead of performing a full page navigation or exposing internal previous-rule/word query parameters.

## Multiplayer rooms

The Word Rings toolbar can create or join a multiplayer room. The creator chooses a target score from **5 to 15** and can optionally enable **0.5-point partial placement scoring**. Partial scoring is disabled by default. Rooms use six-character shareable codes and can be joined through `/minigames/word-rings?room=CODE`.

A creator who plays can start after one other player joins. A dedicated non-playing host can start with one connected playing participant after selecting all three rules. During play those rules are visible only to the host; player room-state responses redact all three rule texts. Each participant receives a personal, separately randomized word queue of up to 20 words with at most 10 visible at once. The left side of the game shows every player's independent score, the host marker immediately before the host name, and the current turn; the raw remaining server-queue count is not exposed. The toolbar score is also personal to the viewer; there is no shared team score.

The multiplayer board scales against both viewport width and height. On large displays the board can grow to 1640 px wide with a 1.4:1 play area, while the player and word-bank columns stay bounded and compress at smaller breakpoints. The three equal circles use an equilateral-center layout so the AB, AC, and BC intersections are symmetric instead of squeezing the upper AB region; automatic-placement anchors are aligned with that geometry. Host-only Start, room-lock, and rule controls remain hidden for ordinary players even before the first polling response arrives.

Multiplayer actions also produce short localized spoken announcements in every connected browser. The room event stream distinguishes a submitted check, host **Correct** versus **Move**, player join, voluntary leave, kick, and manual host turn transfer. Victory is announced only to the winning player; defeat is announced to the other playing participants, while a dedicated non-playing host receives neither result announcement. Every announcement always schedules a short Web Audio signal after the page audio context has been unlocked by pointer, keyboard, or touch input; browser speech synthesis is supplemental so a silent or blocked speech engine cannot suppress the audible signal.

Multiplayer scoring and turn progression are server-authoritative. Rooms where the creator also plays keep automatic rule/dictionary validation. A dedicated non-playing host instead acts as the referee: a player submits one placement, everyone sees the enlarged pulsing pending word, and no score or turn change occurs until the host resolves it. The host can leave it in the submitted region and choose **Correct**, or drag it to any other Venn region and choose **Move**. Dedicated-host scoring is based only on that host decision and never on the catalog membership: unchanged region = 1 point and the player keeps the turn; moving to a region sharing at least one ring with the submitted region = 0.5 point and passes the turn; moving to a non-intersecting region = 0 points and passes the turn. Correct outside-ring placements can award at most one point during the same uninterrupted turn. After adjudication the token returns to normal size and stops pulsing.

For automatically judged rooms:

- a fully correct placement awards **1 point** to the player who placed it;
- when partial scoring is enabled, a non-exact placement that overlaps at least one required ring awards **0.5 points** to that player and is automatically moved into its fully correct Venn region while retaining partial/yellow feedback;
- a fully correct placement keeps the turn with the same player;
- an incorrect or partially correct placement passes the turn to the next player;
- a correctly placed word outside all rings can award only **1 point per continuing turn**; additional correct outside-ring words in that same turn remain correct but award 0 points until the turn changes;
- the first player whose own score reaches the configured target is the single winner; every other player receives a defeat result;
- if every player's personal queue is exhausted before anyone reaches the target, every player loses.

The browser submits the word, detected Venn membership, and board coordinates, while the server calculates correctness, personal points, turn ownership, the winner, and the final per-player outcome. Checked placements remain draggable inside their locked Venn membership even after the round finishes. The host can start another round in the same room after a result; scores, placements, queues, winner state, and puzzle data are reset, while finished clients keep polling so they observe the restart. Room state is kept in memory and expires after inactivity. Multiplayer victory and defeat use the same animated result-dialog system as solo play.

## Rule selection

Each color has its own persistent pool of rules. Every new rule is enabled for games by default, and the editor can temporarily disable a rule without deleting it. Disabled rules and their word sets are excluded entirely from game generation, including outside-ring distractor selection.

When the game page is opened, one enabled, non-empty rule is selected independently at random for the blue, yellow, and red rings. The board word bank is built from the words attached to those three selected rules, and the expected Venn membership is generated from the selected word sets. Up to two words from other enabled rules that do not match any selected rule are added as outside-ring distractors when available; the legacy `дім` and `сир` words are fallback distractors.

The default installation seeds the original three conditions so the minigame remains immediately playable. At least one rule must remain for each ring, and at least one non-empty rule per ring must remain enabled for games.

The player can reset the board in-place and reveal or hide the selected rules. Revealed rules appear beside their matching rings and are identified by color only. Validation happens one newly placed word at a time and marks correct and incorrect placements without replacing the membership-colored border.

The public `/minigames` catalog exposes the game as the second minigame.

## Editor

The Master Host editor is available at `/Admin/WordRingsEditor`. Its first tab is **All words**, followed by the blue, yellow, and red rule tabs. Opening the editor without an explicit `ring` query parameter selects **All words**. Tab changes, paging, rule mutations, and word mutations are loaded asynchronously and do not perform a full page navigation.

### All words

The **All words** tab derives one case-insensitive catalog from every word referenced by any rule. The catalog is sorted alphabetically using Ukrainian collation and displayed in pages of 25, with the same pager shown above and below the list. Each row also shows the total number of rules using the word plus how many rules in each ring currently contain it.

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

CSV import is merge-only for rules and words. It never deletes rules and never removes words from existing rules. Rows are matched case-insensitively by ring plus rule text. Missing words are appended to matching rules; missing rules are created from the CSV row. Duplicate rows in the same import are merged before persistence. Import is bounded to a 5 MB file and 10,000 non-empty data rows. There is no per-rule word-count limit. Imported words shorter than 3 characters are ignored instead of failing the row.

If CSV validation fails, the editor opens a styled error dialog instead of a generic toast. Row-level validation reports the physical CSV line number (including the header and blank lines) together with the specific reason, such as an invalid ring key, malformed quoting, wrong column count, invalid Enabled value, or an invalid word list. File-level errors such as an empty or oversized upload are reported without inventing a row number.

After a successful import, the summary still reports per ring how many word-to-rule additions and new rules were created. It additionally reports the number of truly new unique words that did not exist anywhere in the catalog before the import. Three detail actions open lists of: every new word, every new rule, and every pre-existing rule that received one or more imported words. The existing-rule detail also shows the exact words added to that rule. Detail matching uses the same case-insensitive ring/rule/word semantics as the import merge, and displayed words remain uppercase.

The detail comparison is captured against the current exported configuration immediately before the import POST, so the dialog distinguishes a globally new word from an existing word that was merely attached to another rule. If the import produced no additions, the dialog keeps the explicit “nothing added” result and the detail actions remain disabled at zero.

Import and export use the same three-note Web Audio completion tone as quiz import/export. Transfer controls are disabled while an operation is running, and the shared `BadWolfBusy` overlay is shown after a short delay so quick operations do not flicker while long ones still provide clear feedback.

## Performance notes

The rule store keeps one immutable in-memory snapshot containing both rules and a precomputed unique-word index. Read-only word paging therefore does not repeatedly flatten and sort every rule. Mutations are serialized through the existing store gate, build a new snapshot only after a successful write, and publish it atomically. Word membership edits are batched, and a CSV merge performs a single persistence write regardless of how many rows changed.

New-word import details use responsive pages containing as many chips as fit into nine visual rows at the current dialog width; imported-rule detail dialogs keep their five-item pages. The nine-row layout is measured once per width and recalculated after a resize.


### Starter examples and room presence

Solo rounds and multiplayer rooms where the creator also plays now start with non-scoring example tokens already positioned on the board. The automatic selector prefers one A-only, B-only, C-only, and outside-all-rings word, plus an ABC word; when ABC is unavailable it uses a two-ring overlap when possible. These examples are excluded from the playable word pool and do not count as attempts, answers, or points.

Dedicated non-playing hosts instead receive four setup words after their selected rules are applied. No player owns the turn during this setup phase. The host places the four examples anywhere on the Venn board and confirms them; only then does the first playing participant receive the turn. The confirmed examples remain visible as starter associations and never score points.

The multiplayer turn/status line below the board is collapsed completely because the current player is already indicated in the player list. Non-host clients send a departure hint on page shutdown/navigation. The server keeps a five-second grace period so an ordinary refresh can reconnect with the same token instead of being treated as a leave; if the page does not come back, another client removes that player and emits the normal leave event. A longer heartbeat timeout is retained as a fallback for abrupt connection loss without falsely ejecting briefly backgrounded tabs.
