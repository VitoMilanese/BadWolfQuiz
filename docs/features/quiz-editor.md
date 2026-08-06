# Quiz and Question Editing

## Purpose

The quiz editor manages the reusable quiz definition without changing any game
snapshot that has already been created. It combines a visual board editor with
dedicated regular-question and final-question editors.

## Board settings

The regular **Save** action submits board settings asynchronously. A successful
save keeps the current page and scroll position and displays localized feedback
that disappears after four seconds. Validation and unexpected failures remain
visible so the host can correct them.

Changes to category or question counts may still reload the page because the
board structure must be rebuilt. The **Play** action also keeps its navigation
behavior and creates or opens a game normally.

## Regular questions

Regular questions contain independent ordered question and answer blocks. Saving
a question uses a multipart asynchronous request, including selected image and
audio files. The response returns identifiers for newly created blocks; the
browser writes those identifiers back into the form so another save updates the
same entities instead of creating duplicates.

After a successful save, the editor clears consumed file inputs and remove-file
flags. Localized success feedback disappears automatically, while validation
feedback remains beside the editor actions. A **Next question** action is shown
only when a question with a greater `RowIndex` exists in the same category.

The system file picker may be cancelled with Escape without triggering the
editor's own Escape navigation.

## Deleting regular questions

The board shows the delete control on hover or keyboard focus when either the
question or answer contains at least one non-empty block. Deletion requires an
HTML dialog confirmation and runs asynchronously without reloading the page.

Deleting clears every existing question and answer block, then creates exactly
one empty text block for each side. The board cell stays in place because its
question entity and row/category position are part of the board structure.

## Final question

Final question and answer blocks are stored directly on the quiz and edited in a
dedicated editor. The board toolbar shows **Delete final question** only when at
least one final question or answer block contains content.

Deletion requires confirmation and removes every final question and answer block.
Unlike regular-question deletion, it does not create empty replacement blocks.
