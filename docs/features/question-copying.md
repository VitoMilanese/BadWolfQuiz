# Question copying and cloning

## Purpose

The Quiz Editor board lets a host copy an existing regular question into another active quiz or clone it inside the same quiz without modifying the source question.

## User experience

Each non-empty question card exposes a compact copy/clone action in the board action column. The action is positioned between the move and delete controls. When all three controls are present, the action column spans the cell height, distributes the buttons evenly, and uses 30×30 controls so the actions remain usable in compact question cells.

Selecting the copy action opens a localized dialog for that question. The dialog lists active quizzes owned by the current host, including the source quiz, and their destination categories. Categories without room for another question are disabled. The copy action is intentionally not exposed inside QuestionEditor.

After a successful copy, the dialog closes immediately and the Quiz Editor shows the localized success message through the same temporary save-status overlay used by normal quiz saving. Validation and capacity errors remain inside the dialog.

## Copy semantics

The new question is an independent deep copy. Question-type settings, question and answer blocks, block order, captions, URLs, media metadata, stored file data, and relevant playback settings are copied to new entities so later edits cannot affect the source question.

The source quiz may be selected as the destination, which makes the operation a same-quiz clone.

Only active, non-archived quizzes owned by the current host are eligible as source or destination quizzes.

## Board capacity

Copying first fills an existing missing question slot in the selected category when one is available. Otherwise the destination round gains one new row, provided the round has not reached `QuizEditorOptions.MaximumQuestionCount`.

When a new row is created, blank sibling questions are added to the other categories in that round so the Quiz Editor board remains rectangular. If the round is already at the maximum row count and no reusable slot exists, the destination is reported as full and the server rejects the operation with `NoCapacity`.

## Release

Question copying and same-quiz cloning are introduced in BadWolfQuiz Web `1.19.0` (`web-v1.19.0`).
