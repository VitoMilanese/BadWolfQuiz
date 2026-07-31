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

The quiz also has a dedicated final-question editor with independent question and answer blocks.

Content blocks can be:

- reordered using Drag & Drop;
- removed;
- added dynamically.

---

## Live Game

Currently implemented:

- Create a game from an immutable quiz snapshot
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

---

# Planned Features

## Game Session

- Reserved player nicknames

## Game

- Persistent global settings and per-game settings UI
- Per-round and lifetime correct-answer and attempt statistics
- Team mode

## Player and Host Cards

- Compact icon-only card controls
- Built-in and uploaded player avatars
- Webcam or OBS feed in place of an avatar
- Optional host card with an image, webcam, or OBS feed
- Global defaults with per-game visibility overrides

## Media

- Full-screen presentation mode
- Advanced transitions
- Multiple monitor support

## Administration

- Import / Export quizzes
- Backup
- Version history
- Search
- Category templates
