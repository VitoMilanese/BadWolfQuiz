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
- Save actions in the quiz, question, final-question, round-description, and category-description editors;
- Back navigation from those editor pages, including Escape-key navigation.

## Lifecycle

For normal navigation and native form submissions, the overlay remains visible until the browser leaves the current page. Editor link and Escape navigation is delayed until the overlay has had a paint opportunity, avoiding a navigation that starts before the busy state can be seen.

Quiz import keeps the existing native multipart form submission. Once a `.bwquiz` file is selected, the shared busy state locks the import form and disables the Import quiz selector so duplicate import requests cannot be started while the current upload/import is in progress. Normal page navigation or the existing `pageshow` recovery restores the control state.

Quiz export keeps the native browser download path rather than reading the generated package into a JavaScript `Blob`. The export link is locked immediately and a per-request `exportToken` is added to the download URL. When package preparation finishes, including server failure paths, the export handler writes a short-lived `badwolfquiz-export-complete` cookie containing that token. The current page polls for the matching token, then releases the busy overlay and export lock. A fallback timeout and `pageshow` recovery prevent stale busy state if browser download navigation behaves unexpectedly.

Quiz Editor and Question Editor Save actions already use AJAX. Their existing save handlers continue to own the request and error handling; the shared busy layer observes the existing submitter state and closes when the operation completes and the submitter is re-enabled.

Duplicate activation is blocked while the shared busy state is active. A `pageshow` handler clears stale state after browser back/forward restoration.

Escape still closes an open editor preview or dialog first. When no editor modal is open, Escape uses the same busy navigation path as the visible Back action.

## Compatibility

The busy feedback does not change quiz validation, persistence, import/export package format, export filename/content type, game-launch rules, or gameplay behavior. It is presentation and duplicate-action protection around existing actions.

Regression coverage lives in `BusyIndicatorRegressionTests` and checks global asset loading, route coverage, fullscreen modal behavior, quiz import locking, export completion signaling and duplicate protection, native large-export-safe download behavior, AJAX save lifetime, Escape navigation, history restoration, and reduced-motion styling.
