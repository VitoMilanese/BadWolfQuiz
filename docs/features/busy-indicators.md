# Fullscreen busy feedback

BadWolfQuiz.Web uses a shared fullscreen busy overlay for game-launch, quiz-management, import/export, and quiz-editor actions that can take noticeable time.

## Presentation

- The indicator is a modal `<dialog>` so it is rendered in the browser top layer, including above other open dialogs.
- The page is dimmed and softly blurred while the operation is busy.
- The centered animation uses two counter-rotating red orbit rings and a pulsing red core, using the existing site theme variables.
- `prefers-reduced-motion` disables the rotation and pulse while preserving a clear static busy state.
- The overlay blocks pointer, touch, keyboard, and scrolling interaction with the underlying page while active.

## Covered flows

The shared busy state is used for:

- creating or continuing a game from the quiz list;
- creating a game from Public Quizzes;
- importing a `.bwquiz` package from the quiz list;
- exporting a quiz to a `.bwquiz` package;
- creating a game from the quiz editor;
- opening the quiz, question, final-question, round-description, and category-description editors;
- switching rounds in the quiz editor;
- renaming rounds and categories from the quiz editor;
- Save actions in the quiz, question, final-question, round-description, and category-description editors;
- Back navigation from those editor pages, including Escape-key navigation;
- opening an already-resolved question from the host game board;
- **Show answer**, **Show question**, and **Return to board** navigation from the closed-question review screen;
- **Return to game** and its Escape-key equivalent from the answer-history editor.

## Lifecycle

For normal navigation and native form submissions, the overlay remains visible until the browser leaves the current page. Editor link and Escape navigation is delayed until the overlay has had a paint opportunity, avoiding a navigation that starts before the busy state can be seen.

Quiz import keeps the existing native multipart form submission. Once a `.bwquiz` file is selected, the shared busy state locks the import form and disables the Import quiz selector so duplicate import requests cannot be started while the current upload/import is in progress. Normal page navigation or the existing `pageshow` recovery restores the control state.

Quiz export keeps the native browser download path rather than reading the generated package into a JavaScript `Blob`. The export link is locked immediately and a per-request `exportToken` is added to the download URL. When package preparation finishes, including server failure paths, the export handler writes a short-lived `badwolfquiz-export-complete` cookie containing that token. The current page polls for the matching token, then releases the busy overlay and export lock. A fallback timeout and `pageshow` recovery prevent stale busy state if browser download navigation behaves unexpectedly.

Round and category rename dialogs use a dedicated AJAX rename endpoint instead of following the original POST/redirect path back through the full Quiz Editor GET. While the request is active, all controls in the rename dialog are disabled and the shared fullscreen overlay prevents conflicting interaction. A successful response updates the visible title and dependent client-side metadata in place, closes the dialog, shows the existing quiz save-status message, and releases the busy state. Errors release the busy state and restore the controls so the rename can be corrected or retried. The original Editor POST handlers remain available as the native/no-JavaScript fallback.

Quiz Editor and Question Editor Save actions already use AJAX. Their existing save handlers continue to own the request and error handling; the shared busy layer observes the existing submitter state and closes when the operation completes and the submitter is re-enabled.

Host gameplay navigation uses a page-scoped duplicate-action guard. Opening an already-resolved board question immediately locks the complete question board, while closed-question review actions lock the review-action group. The first gameplay click continues through the existing soft-navigation path. If the transition is not immediate, the shared fullscreen busy indicator appears after a short delay. The guard releases after the expected gameplay update, a visible gameplay error, browser page restoration, or a safety timeout.

Returning from Answer History to the live game is routed through the shared busy-navigation helper. The visible **Return to game** action and Escape use the same guarded navigation path, so repeated clicks or key presses cannot start duplicate requests. Failed navigation and page restoration release the lock.

Duplicate activation is blocked while the shared busy state is active. A `pageshow` handler clears stale state after browser back/forward restoration.

Escape still closes an open editor preview or dialog first. When no editor modal is open, Escape uses the same busy navigation path as the visible Back action.

## Compatibility

The busy feedback does not change quiz validation, persistence, import/export package format, export filename/content type, game-launch rules, or gameplay behavior. Rename validation and persistence remain compatible with the original native POST handlers. The busy layer provides presentation, duplicate-action protection, and no-reload AJAX handling around existing editor operations.

Regression coverage lives in `BusyIndicatorRegressionTests`, `QuizRenameBusyRegressionTests`, and `HostNavigationActionGuardRegressionTests`. It checks global asset loading, route coverage, fullscreen modal behavior, quiz import locking, export completion signaling and duplicate protection, native large-export-safe download behavior, AJAX save lifetime, rename dialog locking and duplicate protection, in-place rename synchronization, native rename fallback preservation, closed-question board/review locks, answer-history return navigation, Escape handling, history restoration, and reduced-motion styling.
