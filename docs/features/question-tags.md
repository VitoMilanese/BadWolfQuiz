# Question Tags

## Purpose

Question tags let hosts classify individual quiz questions with reusable free-form metadata. Tags are currently authoring metadata; they establish the data needed for future question filtering and search without changing gameplay behavior.

Regular questions and the Final Question both support question-level tags. Final-question tags are stored independently from quiz-level tags and from regular-question tag associations.

## Editing tags

The regular Question Editor and Final Question Editor use the same tag-combobox interaction. Hosts can add custom text or select an existing suggestion from the autocomplete dropdown below the tag input. Selected tags are rendered as removable chips, and the combobox supports the same keyboard navigation and add/remove behavior in both editors. The quiz metadata editor uses the same bulk tag-authoring controls for quiz-level tags.

When at least one tag is selected, the tag list starts with square icon-only Copy and Delete-all actions. Copy writes all current tags to the clipboard as a semicolon-separated string. Delete-all clears the current tag selection in one action.

The tag input accepts either a single tag or multiple tags separated by commas and/or semicolons. Bulk input is split, trimmed, and added in order as individual tags. Blank values and case-insensitive duplicates are ignored. Quiz-level bulk input still honors the existing 20-tag maximum and stops adding values once the limit is reached; regular and final questions keep their existing behavior without a separate tag-count limit.

Tag names are trimmed before saving. Blank values are ignored, tag names are limited to 100 characters, and duplicate tags on the same question are prevented case-insensitively. Removing a tag from either editor removes the corresponding question/tag association when that editor is saved.

When the autocomplete listbox is open, Escape closes only that dropdown. The surrounding editor stays open; a later Escape, after the dropdown is closed, keeps the normal editor Back behavior. The global capture-phase editor shortcut handler treats a visible listbox as blocking UI so it cannot navigate away before the combobox handles the key.

## Suggestions

Autocomplete suggestions are built from tags already used by the current host on both regular and final questions. The server filters suggestions using the text currently entered, ranks matching tags by usage frequency and then by name, and returns at most eight values. Tags already selected on the current question are omitted from the client-side suggestion list.

There is no separate global tag catalog: reusable suggestions are derived from the host's stored regular-question and final-question tag records.

## Persistence

Regular-question tags are stored as `QuizQuestionTag` rows linked to `QuizQuestion`. Final-question tags are stored as `FinalQuestionTag` rows linked directly to the owning `Quiz`. Each row stores both the display name and a normalized name used for case-insensitive duplicate protection.

Deleting a regular question cascades to its tags. Deleting the Final Question also clears its final-question tags.

Question tags are preserved when questions are copied, when quizzes are cloned, and when `.bwquiz` packages are exported and imported. Final-question tags participate in quiz clone and package round-tripping as well. Existing packages without the optional final-question tag field remain compatible.

Package import writes regular-question tags after imported questions receive their database IDs and verifies that the expected tag rows were persisted before the import transaction is committed. Final-question tags are restored with the imported quiz metadata.

## Performance

The Question Editor loads the tracked question/content/tag graph with split queries. This avoids a cartesian JOIN across question blocks, answer blocks, and tags, which would otherwise multiply stored media BLOB data and significantly slow saves on media-heavy questions.

The Final Question Editor keeps its existing content-block save behavior while loading only the additional final-question tag collection required for tag synchronization.

## Release

Question tags were introduced for regular questions in BadWolfQuiz Web `1.28.0`.

BadWolfQuiz Web `1.36.0` extends the same question-tag authoring experience to the Final Question Editor, including shared suggestions, persistence, deletion cleanup, quiz cloning, and `.bwquiz` import/export support.

BadWolfQuiz Web `1.37.0` improves all three tag-authoring surfaces with square Copy/Delete-all actions and comma/semicolon bulk input while preserving existing validation and quiz-level limits. It also fixes Escape handling so closing an open tag dropdown does not navigate away from the regular or Final Question Editor.
