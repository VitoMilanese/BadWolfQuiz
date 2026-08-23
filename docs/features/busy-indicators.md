# Fullscreen busy feedback

BadWolfQuiz.Web uses a shared fullscreen busy overlay for game-launch, quiz-management, import/export, and quiz-editor actions that can take noticeable time.

## Presentation

- The indicator is a modal `<dialog>` so it is rendered in the browser top layer, including above other open dialogs.
- The page is dimmed and softly blurred while the operation is busy.
- The centered animation uses two counter-rotating red orbit rings and a pulsing red core, using the existing site theme variables.
- `prefers-reduced-motion` disables the rotation and pulse while preserving a clear static busy state.
- The overlay blocks pointer, touch, keyboard, and scrolling interaction with the underlying page while active.
- Successful quiz import and export operations play a short three-note completion tone. Failed and cancelled transfers remain silent.

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
- swapping quiz questions with drag-and-drop in the quiz editor;
- Save actions in the quiz, question, final-question, round-description, and category-description editors;
- Back navigation from those editor pages, including Escape-key navigation;
- opening an already-resolved question from the host game board;
- **Show answer**, **Show question**, and **Return to board** navigation from the closed-question review screen;
- **Return to game** and its Escape-key equivalent from the answer-history editor.

## Lifecycle

For normal navigation and native form submissions, the overlay remains visible until the browser leaves the current page. Editor link and Escape navigation is delayed until the overlay has had a paint opportunity, avoiding a navigation that starts before the busy state can be seen.

Quiz import keeps the native multipart upload path. When JavaScript is available, the import form is submitted into a hidden same-origin frame so the current quiz-list page, busy overlay, and user-activated Web Audio context remain alive while a potentially large package is processed. A per-request `importToken` is sent with the form. The server writes a short-lived `badwolfquiz-transfer-complete` cookie containing the operation, token, and success/failure state, while the normal success/error TempData message remains pending. The current page releases the busy state when the matching completion arrives, plays the completion tone only for success, and reloads to show the updated quiz list and existing message. Without JavaScript, import keeps the original POST/redirect behavior.

Quiz export keeps the native browser download path rather than reading the generated package into a JavaScript `Blob`. The export link is locked immediately and a per-request `exportToken` is added to the download URL. The existing `badwolfquiz-export-complete` cookie still releases the busy overlay when package preparation finishes. The export handler additionally writes the tokenized `badwolfquiz-transfer-complete` success/failure cookie used by the completion-sound layer. Only a successful export plays the completion tone; server failures and cancelled requests do not. A fallback timeout and `pageshow` recovery prevent stale client tracking if browser download navigation behaves unexpectedly.

The completion tone is generated with the Web Audio API instead of a separate media asset. Its audio context is created/resumed from the user's import or export gesture before the long-running transfer begins, avoiding autoplay-policy dependence when completion occurs later.

Round and category rename dialogs use a dedicated AJAX rename endpoint instead of following the original POST/redirect path back through the full Quiz Editor GET. While the request is active, all controls in the rename dialog are disabled and the shared fullscreen overlay prevents conflicting interaction. A successful response updates the visible title and dependent client-side metadata in place, closes the dialog, shows the existing quiz save-status message, and releases the busy state. Errors release the busy state and restore the controls so the rename can be corrected or retried. The original Editor POST handlers remain available as the native/no-JavaScript fallback.

Quiz question drag-and-drop exchange shows the shared fullscreen overlay immediately after a valid drop and before the `ExchangeQuestions` request is awaited. An in-progress guard prevents a second exchange from starting while the first request is active. On success, the question cells are swapped in the DOM before the overlay is released; on failure, the existing error handling runs and the busy state is still released from the exchange cleanup path.

Quiz Editor and Question Editor Save actions already use AJAX. Their existing save handlers continue to own the request and error handling; the shared busy layer observes the existing submitter state and closes when the operation completes and the submitter is re-enabled.

Host gameplay navigation uses a page-scoped duplicate-action guard. Opening an already-resolved board question immediately locks the complete question board, while closed-question review actions lock the review-action group. The first gameplay click continues through the existing soft-navigation path. If the transition is not immediate, the shared fullscreen busy indicator appears after a short delay. The guard releases after the expected gameplay update, a visible gameplay error, browser page restoration, or a safety timeout.

Returning from Answer History to the live game is routed through the shared busy-navigation helper. The visible **Return to game** action and Escape use the same guarded navigation path, so repeated clicks or key presses cannot start duplicate requests. Failed navigation and page restoration release the lock.

Duplicate activation is blocked while the shared busy state is active. A `pageshow` handler clears stale state after browser back/forward restoration.

Escape still closes an open editor preview or dialog first. When no editor modal is open, Escape uses the same busy navigation path as the visible Back action.

## Compatibility

The busy feedback and completion tone do not change quiz validation, persistence, import/export package format, export filename/content type, game-launch rules, or gameplay behavior. Native multipart upload/download paths are preserved, and import keeps its original POST/redirect fallback when JavaScript is unavailable. Rename validation and persistence remain compatible with the original native POST handlers.

Regression coverage lives in `BusyIndicatorRegressionTests`, `QuizTransferCompletionRegressionTests`, `QuizRenameBusyRegressionTests`, and `HostNavigationActionGuardRegressionTests`. It checks global asset loading, route coverage, fullscreen modal behavior, quiz import locking, native multipart import completion tracking, export completion signaling and duplicate protection, success-only transfer audio, native large-export-safe download behavior, AJAX save lifetime, rename dialog locking and duplicate protection, in-place rename synchronization, native rename fallback preservation, closed-question board/review locks, answer-history return navigation, Escape handling, history restoration, and reduced-motion styling.
