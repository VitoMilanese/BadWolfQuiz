# 🐺 BAD WOLF QUIZ

**Bad Wolf Quiz** — це сучасна real-time платформа для проведення квізів, написана на **ASP.NET Core 8**.

Проєкт складається з двох основних частин:

- редактора квізів;
- real-time ігрової платформи для ведучого та гравців.

Детальні специфікації та архітектурні рішення зберігаються в [docs](docs/README.md).

---

# Technologies

- ASP.NET Core 8 Razor Pages
- Entity Framework Core 8
- SQLite
- SignalR
- QRCoder

---

# Current Features

## Quiz Management

- Create quizzes
- Quiz list
- SQLite database created automatically on first launch
- Automatic demo quiz generation

---

## Round Management

- Create rounds
- Rename rounds
- Delete rounds
- Drag & Drop round reordering
- Individual point values for every row
- Automatic creation of questions for newly created rounds
- Automatic category cloning when creating new rounds

---

## Category Management

- Rename categories
- Exchange categories between rounds
- Drag & Drop category reordering inside a round

---

## Quiz Board Editor

- Visual Jeopardy-style board
- Sticky row header
- Sticky category header
- Responsive board layout
- Automatic row point editor
- Category toolbar
- Visual feedback during Drag & Drop

---

## Question Editor

Supports multiple content blocks.

Currently implemented:

- Text
- Image
- Audio
- Video
- YouTube

Each question contains independent:

- Question blocks
- Answer blocks

Questions may use the standard presentation or the four-clue presentation. A four-clue question contains exactly four text, image, or audio clues: two are visible initially and the host may reveal the remaining clues one at a time. Correct answers are worth 100%, 50%, or 25% of the question value depending on how many clues were revealed; an incorrect answer always deducts the full value.

The quiz also has a dedicated final-question editor with independent question and answer blocks.

Content blocks can be:

- reordered using Drag & Drop;
- removed;
- added dynamically.

---

## Live Game

Currently implemented:

- Create a game from an immutable quiz snapshot
- Continue one unfinished game per quiz after an application restart, preserving
  the board, players, scores, visuals, and answer history
- Lobby and join code
- Join and reconnect flows with host approval
- Real-time player presence and score updates
- Active player selection and automatic lowest-score selection for later rounds
- Random wager questions, including rounds configured with zero wager questions
- Wager keypad and validation
- Player buzzer with server-authoritative winner selection
- Near-simultaneous buzzer results with millisecond differences
- Synchronized buzzer and answer countdown timers
- Persistent global game defaults and editable per-game settings snapshots
- Compact icon-only player-card controls
- Built-in player avatars and uploaded player images, restored by player name
- Optional player webcam feeds with static-image fallback
- Optional host card with an uploaded image, built-in avatar, or webcam feed
- Global host-card defaults with per-game visibility and visual-source overrides
- Timer pause and resume controls for the host
- Automatic timer-based judging for regular and wager questions
- Correct and incorrect answer judging
- Positive and negative scoring
- Focused question and answer presentation
- Read-only question and answer previews for resolved board cells
- Player scoreboard with answering and ineligible states
- Multi-round progression
- Animated top-three inter-round leaderboard
- Deterministic final standings with score-growth, correct-answer, and attempt tie-breakers
- Private final wagers and answers submitted from player devices
- Host-controlled final question reveal, judging, and final results
- Separate live answer-key screen for the host's second display
- Host answer-history editing with immediate score and standings recalculation
- Persistent completed-game history with final scores and answer details
- Self-service host accounts with private quiz and game-history ownership
- QR player joining with direct game links

---

## Media

Implemented:

- Image upload
- Audio upload
- Video upload
- Local file storage
- Preview inside editor
- Caption support
- Stored file preview

---

## QR join address

The server must listen on a network interface, for example:

```powershell
dotnet run --project src/BadWolfQuiz.Web --urls http://0.0.0.0:5080
```

The QR code uses the explicitly configured public address:

```json
{
  "Game": {
    "PublicBaseUrl": "http://203.0.113.10:5080"
  }
}
```

The configured port must be forwarded by the router and allowed by the firewall.

---

## Architecture

- Entity Framework Core data model
- Separate Quiz / Round / Category / Question hierarchy
- Content blocks stored independently
- Database indexes
- Server-side validation
- Localized UI
- Razor Pages architecture

---

# Project Structure

```
Quiz
 ├── Final Question Blocks
 ├── Final Answer Blocks
 ├── Round
 │     ├── Rows
 │     ├── Categories
 │     │      ├── Questions
 │     │      │      ├── Question Blocks
 │     │      │      └── Answer Blocks
 │     │      └── ...
 │     └── ...
 └── ...
```

---

# Run

1. Open `BadWolfQuiz.sln`
2. Restore NuGet packages
3. Set **BadWolfQuiz.Web** as Startup Project
4. Run using **https** or **http**

SQLite database is automatically created in:

```
src/BadWolfQuiz.Web/App_Data/badwolfquiz.db
```

Unfinished game snapshots are stored atomically in:

```
src/BadWolfQuiz.Web/App_Data/active-games.json
```

See [Active Game Recovery](docs/features/active-game-recovery.md) for recovery
behavior and snapshot scope.

---

# Planned Features

## Game

- Player lifetime statistics and per-round statistics UI
- Team mode

## Media

- Dedicated presentation window for a secondary monitor

## Administration

- Import / Export quizzes
- Backup
