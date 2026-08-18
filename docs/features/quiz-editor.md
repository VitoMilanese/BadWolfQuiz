# Quiz and Question Editing

## Purpose

The quiz editor manages the reusable quiz definition without changing any game
snapshot that has already been created. It combines a visual board editor with
dedicated regular-question and final-question editors.

## Cloning quizzes

On `/Admin/Quizzes`, an active quiz provides **Clone** immediately after **Edit**
in its Actions menu. Selecting Clone opens a localized dialog that requires the
name of the new quiz before anything is created. After a successful clone, the
new quiz opens directly in the quiz editor.

Cloning creates a new independent editable quiz graph. It copies the source quiz
description, rounds, rows, categories, questions, round/category descriptions,
final-question and final-answer content, question settings, content-block order
and metadata, captions, URLs, and stored media bytes. Every cloned quiz, round,
category, question, and content block receives its own database identity, so later
editing of the clone cannot modify the source quiz.

Publication and runtime history are intentionally not cloned. A cloned quiz starts
unpublished with no publication timestamp even when the source is public. Game
sessions/play history and rating/star data are not copied, and play/archive runtime
state is reset. The source quiz is not modified by the cloning operation.

Clone is offered only while the source quiz media state is `Active`, matching the
existing Edit availability. This ensures the reusable stored media needed for an
independent clone is available when the clone is created.

## Board settings

The regular **Save** action submits board settings asynchronously. Save-result
feedback is shown as a temporary localized overlay in the upper part of the
screen, below the top bar, so it stays clear of the footer and does not disturb
the current page position.

Changes to category or question counts may still reload the page because the
board structure must be rebuilt. The **Play** action also keeps its navigation
behavior and creates or opens a game normally.

Adding a round uses the previous round as the template for its category names and
category descriptions. Description blocks are copied as separate entities while
preserving their order, content, captions, media references, and stored file data,
so editing the description in the new round does not modify the source round.
Categories without description blocks remain without descriptions in the new
round.

## Save feedback

The quiz editor, regular-question editor, final-question editor,
round-description editor, and category-description editor share the same
save-result presentation. Success and failure results appear in a prominent,
non-interactive overlay below the top bar for approximately 1.5 seconds and then
fade automatically. The overlay uses `pointer-events: none`, so it never blocks
editor interaction.

Existing save requests, redirects, validation, and error handling are preserved.
Round and category description saves continue to redirect back to the description
editor after a successful save; the `saved` query value is interpreted as a
boolean so the shared success overlay is rendered reliably after that redirect.

## Reset and unsaved changes

The regular-question, final-question, round-description, and category-description
editors provide a compact **↻ Reset** action immediately to the right of **Back**.
Reset intentionally discards the current client-side state without saving and
reloads the editor from the persisted server state. This restores edited fields,
content-block order, captions, media selections, removed blocks, and other
unsaved changes, and removes newly added unsaved blocks.

Reset uses a fresh GET navigation with a one-time `_editorReset` query token so
browser form-state restoration cannot revive stale values from the page being
replaced. The token is removed from the visible URL after the fresh page loads.
Description-editor reset also removes the transient `saved` query value.

These editors also track whether the current form differs from its clean baseline.
Dirty-state detection covers named text/select/checkbox/radio values, selected
file metadata, added or removed blocks, block reordering, and structural changes
including Container children. **Back** is intercepted when changes are unsaved and
shows a localized confirmation dialog before leaving. The regular-question
**Next question** action uses the same warning. Browser Back, refresh, tab close,
and other unloads use the browser's native `beforeunload` warning as a fallback.

After a successful asynchronous regular-question save, the returned block IDs and
file-input cleanup are applied before the clean baseline is refreshed, so leaving
immediately after Save does not produce a false warning. Failed saves and
validation-error states remain dirty. Successful final-question and description
saves keep their existing POST/redirect behavior and load back in a clean state.
The explicit Reset action bypasses the unsaved-change warning because discarding
the current changes is its intended purpose.

## Regular questions

Regular questions contain independent ordered question and answer blocks. Saving
a question uses a multipart asynchronous request, including selected image and
audio files. The response returns identifiers for newly created blocks; the
browser writes those identifiers back into the form so another save updates the
same entities instead of creating duplicates.

Question and answer blocks are edited in separate tabs. The question tab is
selected by default, and switching tabs changes only which block collection is
visible. Both collections stay in the same form, so unsaved edits are preserved
when moving between tabs. The question-type controls belong to the question side
and are hidden while the answer tab is active.

After a successful save, the editor clears consumed file inputs and remove-file
flags. Localized save-result feedback uses the shared top-screen overlay, while
existing validation state remains available in the editor. A **Next question**
action is shown only when a question with a greater `RowIndex` exists in the same
category.

The system file picker may be cancelled with Escape without triggering the
editor's own Escape navigation.

## Content blocks and ordering

Question and final-question editors support the existing content-block types plus
a **Container** block for horizontal media groups. New Container children can be
Image, YouTube, or Audio blocks; YouTube is the video source exposed for new
Container content. Existing legacy Video blocks remain readable and renderable
for compatibility but are not offered as a new Container add action.

Container children keep the normal media editing controls, captions, stored files,
YouTube URLs, and playback behavior. In gameplay, regular editor previews, and
closed-question previews, Container children share the available horizontal space
and are vertically centered in the row. The layout uses responsive equal-width
columns and avoids unnecessary horizontal overflow. Media outside a Container
keeps the normal top-level layout.

Container editor labels and empty-state text follow the active supported UI
language. Round and category description editors keep their existing Text/Image
scope, and Container is not offered for Four Clues question content.

Every content-block toolbar also provides compact **↑** and **↓** buttons before
the existing drag handle and remove button. These controls swap the block with the
previous or next sibling on the same level, immediately reindex form fields and
`SortOrder`, and disable the unavailable direction at the first or last sibling.
Drag-and-drop remains available. Blocks nested in a Container can only move among
siblings inside that same Container.

## Content previews

Regular-question, answer, final-question, round-description, and
category-description previews use the available preview-dialog width instead of
a fixed desktop text/media cap. Normal responsive padding is preserved so content
does not touch the dialog or viewport edges.

Text and media blocks stay centered, while image/video sizing keeps the existing
height and `object-fit` constraints. Four-clue previews retain their dedicated
clue-grid sizing rather than being stretched by the shared wide-preview rules.

## Deleting regular questions

The board shows the delete control on hover or keyboard focus when either the
question or answer contains at least one non-empty block. Deletion requires an
HTML dialog confirmation and runs asynchronously without reloading the page.

Deleting clears every existing question and answer block, then creates exactly
one empty text block for each side. The board cell stays in place because its
question entity and row/category position are part of the board structure.

After an asynchronous deletion, board-size validation uses the current board
state in the DOM rather than the state captured when the page was rendered. A
cell is treated as filled when it contains a `.question-completion-item.complete`
marker. When reducing the number of categories or question rows, the destructive
confirmation is therefore shown only if content still exists in the part of the
board that would actually be removed. No page refresh is required after deleting
a question for these checks to become accurate.

## Final question

Final question and answer blocks are stored directly on the quiz and edited in a
dedicated editor. The final-question editor uses the same question/answer tab
behavior as the regular question editor: the question tab opens by default and
only the active block collection is shown, while both collections remain in the
same form so switching tabs does not discard unsaved edits.

The board toolbar shows **Delete final question** only when at least one final
question or answer block contains content.

Deletion requires confirmation and removes every final question and answer block.
Unlike regular-question deletion, it does not create empty replacement blocks.
