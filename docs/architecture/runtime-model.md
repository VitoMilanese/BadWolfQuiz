# Runtime Model

## Purpose

The BadWolfQuiz runtime model represents a quiz game while it is being played. It records the current session, the players who joined it, the active board, question progress, timers, wagers, answers, scores, and the final-question workflow.

The runtime model is deliberately separate from the quiz definition. A quiz definition describes what can be played: rounds, categories, questions, content blocks, correct answers, point values, and configuration. The runtime model describes what has happened during one particular playthrough of that definition.

This separation is a core architectural boundary:

- the editor creates and persists quiz definitions;
- a game session reads a fixed quiz definition and creates runtime state from it;
- the game engine is the only component that changes runtime state;
- UI clients render projections of the current runtime state and submit commands;
- gameplay never mutates the original quiz definition.

The runtime model belongs to the `BadWolfQuiz.Game` project and must not depend on Razor Pages, SignalR hubs, database entities, browser-specific types, or presentation concerns.

## Design goals

The runtime model is designed to provide the following properties.

### A single source of truth

The server owns authoritative game state. Host, player, and presentation clients may cache data for rendering, but their local state is not authoritative. After a refresh or reconnection, every client must be able to reconstruct its current view from the server-side session.

### Explicit and valid transitions

A runtime object should not be placed into an impossible state through unrestricted property setters. Gameplay changes are performed by the game engine through commands such as selecting a question, submitting a wager, starting a timer, or judging an answer. Each command validates the current phase, actor, input, and relevant game rules before applying a transition.

### Independence from transport and UI

The runtime model describes gameplay, not screens or messages. For example, it stores that a final wager was submitted and locked; it does not store which modal was open or which SignalR event was sent. Different UIs can therefore use the same engine without duplicating business rules.

### Deterministic reconstruction

Given a quiz definition and persisted runtime state, the application should be able to reconstruct the same game session. Runtime state must contain durable gameplay facts, while transient delivery details such as active connections or animation progress belong elsewhere.

### Controlled exposure

Not every client may see every runtime value. A player's final answer, another player's wager, or the correct answer may need to remain hidden during part of the game. The runtime model stores the complete authoritative data, while role-specific projections decide which fields are visible to each client.

## Definition model and runtime model

The definition and runtime models have different responsibilities even when they refer to the same conceptual object.

| Definition model | Runtime model | Responsibility |
| --- | --- | --- |
| `Quiz` | `GameSession` | Reusable game content versus one playthrough |
| `QuizRound` | `GameBoard` or runtime round | Round structure versus current board progress |
| `QuizQuestion` | `RuntimeQuestion` | Immutable question content versus selection and resolution status |
| Point value | Score transaction or awarded points | Available value versus an applied result |
| Final-question definition | `FinalQuestion` | Final content versus participant submissions and judging |
| Timer configuration | `GameTimer` | Duration rules versus current running or paused timer |

Runtime objects may reference definition identifiers or immutable definition values. They must not reuse mutable editor entities as gameplay state. In particular, flags such as `IsOpened`, `SelectedByPlayerId`, or `AwardedPoints` must never be added to `QuizQuestion`.

A session should be created from a defined quiz version or immutable snapshot. Changes made later in the editor must not silently alter a game already in progress. The exact snapshot mechanism is an infrastructure decision, but the runtime contract requires stable question identity and stable content for the lifetime of the session.

## Aggregate boundary

`GameSession` is the aggregate root of a running game. All gameplay mutations occur through the game engine against a session. Child objects may protect their own invariants, but external application code must not independently modify a question, player, wager, or timer.

A conceptual shape is:

```text
GameSession
├── Players
├── CurrentBoard
│   └── RuntimeQuestions
├── Timer
└── FinalQuestion
    └── PlayerSubmissions
        ├── Wager
        └── Answer
```

This is a domain relationship, not a persistence schema. The storage representation may normalize or serialize the data differently, provided the aggregate can be loaded consistently and updated atomically.

## Core entities and value objects

### GameSession

`GameSession` identifies one playthrough and owns its complete authoritative runtime state.

Typical responsibilities include:

- identifying the source quiz and its fixed version or snapshot;
- maintaining session lifecycle and the current gameplay phase;
- owning the ordered collection of players;
- identifying the current round and board;
- tracking the active question, if any;
- tracking revealed-clue progress for clue-based questions;
- owning the shared game timer;
- owning final-question state when a final question exists;
- recording creation and significant transition timestamps;
- providing a concurrency token or revision for safe updates.

A session-level state may distinguish broad phases such as lobby, regular play, final wagering, final answering, final judging, and completed. Exact state-machine definitions belong in the game-state-machine document. The runtime model stores the current phase but does not replace the transition rules enforced by the engine.

The session must never reference an active question that is already resolved, a board outside the current round, or a final phase when the quiz has no final question.

### GameBoard

`GameBoard` is the runtime representation of the playable questions in a round. It preserves the layout and stable identities derived from the quiz definition while tracking which questions remain available.

It typically contains:

- the source round identifier;
- the round order;
- the collection of `RuntimeQuestion` instances;
- the currently selected question identifier, if any;
- whether the board is complete.

Board completion should be derived from question statuses rather than maintained as an unrelated mutable flag whenever practical. A board is complete when no selectable unresolved questions remain.

Only one regular question may be active at a time. Selecting a question removes it from future selection immediately, even if answering or judging has not yet finished.

### GamePlayer

`GamePlayer` represents a participant in the session. It is not an authentication user and must not depend on an account existing.

Typical data includes:

- a session-scoped player identifier;
- display name;
- join order;
- connection or participation status where it affects gameplay;
- current score;
- eligibility for the final question;
- optional reconnection identity handled through an opaque token outside client-visible domain data.

Score changes must be applied through validated engine operations. Arbitrary score assignment should be avoided. If auditability is required, the session may store score transactions containing the player, delta, reason, question, and timestamp; the displayed score can then be derived or maintained consistently from those transactions.

Connection identifiers are transport concerns and do not belong to `GamePlayer`. A player can disconnect without ceasing to be a participant, and reconnection must attach a new connection to the same runtime player.

### RuntimeQuestion

`RuntimeQuestion` pairs stable question identity with state specific to one playthrough.

It may contain:

- source question identifier;
- board position and immutable point value;
- runtime status;
- selecting player identifier, when selection is attributed to a player;
- selection and reveal timestamps;
- submitted regular-question wager, when applicable;
- answering player identifier;
- judgment result;
- awarded score delta;
- resolution timestamp.

A useful status progression is:

```text
Available -> Selected -> AwaitingWager -> Active -> AwaitingJudgment -> Resolved
```

Not every question passes through every status. A normal question can move directly from `Selected` to `Active`; a wager question enters `AwaitingWager` first. Exact commands and transitions are defined by the game engine.

Question content and the canonical correct answer remain part of the immutable quiz snapshot. The runtime question may expose them through an associated definition reference, but it does not own editable copies.

For a four-clue question, clue progress is runtime state. `RevealedClueCount` determines
which immutable clue blocks are currently public, and `CanRevealClue` indicates whether
another clue remains available. Revealing a clue is a domain transition rather than a
client-only visibility change.

A successful `RevealNextClue()` starts a new full question-timer interval. Timer restart
therefore belongs to the domain operation itself, so automatic timer-driven reveal and
manual host reveal have identical timing semantics. When the question timer expires and
another clue can be revealed, the session reveals exactly one clue and reports
`QuestionTimerOutcome.ClueRevealed` instead of resolving the question. Once no further
clue is available, a later expiration follows the normal no-correct-answer timeout flow.

Answer reward decay is derived runtime behavior rather than a mutation of the
question's immutable point value. During a regular-question individual answer
timer, the Engine derives the current correct-answer value from the effective
game settings, timer state, and the question's current base reward. Four-clue
questions first derive their base reward from `RevealedClueCount`. Incorrect
answer penalties do not use the decayed value.

When an incorrect judgment or answer timeout exhausts the set of players eligible
to buzz for that question, the Engine resolves the question without a correct
answer instead of reopening a buzzer phase that nobody can use.

Once resolved, a question cannot become available again during normal play. Administrative correction, if supported later, should be an explicit engine command with defined score and audit consequences rather than a direct property mutation.

### GameTimer

`GameTimer` models the shared gameplay timer. Pausing is timer state, not a separate session state: pausing the timer must not change the broader `GameSession` phase.

The timer should contain enough information to calculate the remaining time consistently, for example:

- configured duration;
- timer status such as stopped, running, paused, or expired;
- start or resume timestamp;
- remaining duration captured at pause;
- optional expiration timestamp or monotonic timing metadata.

`IsPaused` may be represented directly or derived from the timer status. The important invariant is that pause and resume affect only timer progression. The active question and game phase remain unchanged.

Wall-clock calculations should be centralized and testable through an injected time provider. Clients may animate a countdown locally, but the server's calculated remaining time is authoritative.

### Wager

`Wager` is a value submitted for a specific player and gameplay context. Regular special-question wagers and final wagers share validation concepts but have different workflows.

A wager typically records:

- amount;
- player identifier;
- question or final-question context;
- submission timestamp;
- confirmation or locked status.

For a special wager question, the player announces the wager verbally and the host enters it manually. The engine validates the amount before the question becomes active. The host operation can be represented by `SubmitQuestionWager()`.

For the final question, each eligible player submits and confirms a private wager from their own device. The engine operation can be represented by `SubmitFinalWager()`. Once confirmed or once the wagering phase is locked, the wager cannot be changed unless a future explicit administrative command permits it.

Validation rules such as minimum amount, maximum amount, and score-dependent limits must live in the game domain. UI controls may mirror those limits for convenience, but cannot be their only enforcement point.

### FinalQuestion

`FinalQuestion` coordinates the runtime state of the final phase. It references immutable final-question content and owns per-player participation state.

For every eligible player it tracks:

- final-wager status and value;
- whether the question may be revealed to that player;
- answer draft or submitted answer, as permitted by the domain design;
- answer submission timestamp;
- judgment status;
- judgment result and score delta.

The final workflow is intentionally staged:

1. eligible players submit and confirm wagers;
2. the engine locks final wagering;
3. the final question becomes visible;
4. players submit and confirm answers;
5. the engine locks final answering;
6. the host reviews and judges each answer;
7. score effects are applied;
8. the session is completed.

A player must not submit an answer before that player's wager is confirmed and the question has been released. `SubmitFinalAnswer()` stores a confirmed answer only during the answering phase. `JudgeFinalAnswer()` may be called only by the host workflow after the answer is locked for judging.

Submitted wagers and answers are authoritative private data. Presentation and player projections must hide them until the appropriate reveal or judging phase. Hiding is an application-projection responsibility, but the runtime model must expose sufficient phase and submission-status information to enforce it.

## Invariants

The following invariants apply across the runtime aggregate:

- the source quiz definition remains unchanged throughout gameplay;
- runtime entities use stable identifiers and belong to exactly one session;
- only the game engine changes gameplay state;
- at most one regular question is active;
- an unavailable or resolved question cannot be selected;
- a wager question cannot become active before a valid wager is confirmed;
- a resolved question has a complete and internally consistent outcome;
- scores change only as the result of a validated gameplay or administrative command;
- answer reward decay affects only correct-answer rewards during a regular-question individual answer timer and never reduces an incorrect-answer penalty;
- a regular question cannot return to an unusable buzzer phase after every player has exhausted an attempt;
- pausing a timer does not change the session phase;
- revealing another clue in a four-clue question restarts the full question timer;
- timer expiration reveals at most one next clue before the normal unresolved-question timeout can occur;
- only eligible players participate in the final question;
- a final answer cannot be submitted before final wagering is locked and the question is released;
- a final submission cannot be judged more than once;
- a completed session accepts no normal gameplay commands;
- secret values are not included in projections before their reveal conditions are met.

Where possible, invalid combinations should be unrepresentable through constructors, private setters, value objects, and domain methods. Cross-entity rules that require broader context belong in the game engine.

## Commands, events, and state changes

Application endpoints and SignalR hubs translate authenticated user actions into engine commands. They do not manipulate runtime objects directly.

Representative commands include:

```csharp
SelectQuestion(...)
SubmitQuestionWager(...)
RevealNextClue(...)
StartTimer(...)
PauseTimer(...)
ResumeTimer(...)
JudgeRegularAnswer(...)
SubmitFinalWager(...)
LockFinalWagers(...)
SubmitFinalAnswer(...)
LockFinalAnswers(...)
JudgeFinalAnswer(...)
```

Each command should:

1. load or receive the current `GameSession`;
2. verify the actor and expected session revision;
3. validate the current phase and input;
4. apply one atomic domain transition;
5. persist the updated session;
6. publish resulting notifications after persistence succeeds.

Domain events may describe completed facts such as `QuestionSelected`, `TimerPaused`, `FinalWagerSubmitted`, or `FinalAnswerJudged`. Events are useful for client updates, persistence integration, and audit logs, but they do not replace authoritative state. Reconnecting clients always rebuild their view from current state rather than relying on having received every past event.

## Persistence and concurrency

The runtime model is persistence-agnostic, but the infrastructure must preserve aggregate consistency. A command should update a session atomically. Concurrent commands must not both succeed against the same prior revision when their combination would violate an invariant.

Optimistic concurrency is appropriate for a host-controlled game:

- every loaded session has a revision or concurrency token;
- a successful mutation increments the revision;
- a stale update is rejected and retried only when the command remains meaningful;
- clients receive fresh state after a conflict.

Timestamps should be stored in UTC. Identifiers should remain stable across persistence and reconnection. Sensitive reconnection tokens and transport connection mappings should be stored outside the domain aggregate or represented only as opaque references.

The persistence layer may use snapshots initially. Event sourcing is not required by this model. If an event log is added later, current aggregate state remains the source used for command validation and client reconstruction.

## Client projections

The authoritative aggregate must not be serialized directly to every client. The application layer builds purpose-specific read models.

- The host projection includes controls, submission statuses, and judging information appropriate to the current phase.
- The player projection contains only that player's private inputs and the public state they are allowed to see.
- The presentation projection contains shared board, question, timer, and result data, without host controls or unrevealed private submissions.

Projection generation should be deterministic from the runtime aggregate and the requesting role. This allows reconnecting clients to receive a complete, correct view without UI-specific state being stored in the game domain.

## Testing expectations

Runtime-model tests should focus on invariants and observable transitions rather than property assignment.

At minimum, tests should cover:

- creating a session from an immutable quiz definition;
- selecting available questions and rejecting repeated selection;
- requiring a valid wager for a special question;
- starting, pausing, resuming, and expiring the timer without changing session phase;
- revealing exactly one next clue when a four-clue question timer expires;
- restarting the full question timer after both automatic and manual clue reveal;
- using the normal unresolved-question timeout after the final clue has already been revealed;
- applying score changes exactly once;
- deriving and clamping answer reward decay from the authoritative answer timer while preserving full incorrect-answer penalties;
- resolving a regular question automatically when an incorrect answer leaves no eligible buzzer players;
- final-wager eligibility, validation, confirmation, and locking;
- preventing early final answers;
- accepting and locking final answers;
- judging each final answer exactly once;
- rejecting gameplay commands after completion;
- reconstructing equivalent projections after reconnection;
- rejecting stale concurrent updates.

Tests should use a controllable time provider and deterministic identifiers where timestamps or identities affect assertions.

## Non-goals

This runtime model does not define:

- editor behavior or mutable quiz-authoring entities;
- database tables, Entity Framework mappings, or serialization formats;
- SignalR hub contracts and connection management;
- visual layout, animations, dialogs, or route structure;
- authentication and authorization implementation;
- team play, unless introduced by a later product decision;
- event sourcing as a required persistence strategy.

These concerns may integrate with the runtime model, but they must not leak into its core types or weaken the boundary between the game engine and the UI.

## Summary

The BadWolfQuiz runtime model represents one authoritative, server-owned playthrough of an immutable quiz definition. `GameSession` is the aggregate root; boards, players, questions, timers, wagers, and final submissions are runtime state owned by that session. The game engine validates every state transition, while UI and transport layers submit commands and render role-specific projections.

Keeping definition data immutable and runtime state explicit makes gameplay rules testable, reconnection reliable, private information controllable, and future clients independent from the current web UI.
