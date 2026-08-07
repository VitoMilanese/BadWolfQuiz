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
- Asynchronous board-setting saves that preserve the current scroll position
- Question-card deletion with confirmation and immediate board updates
- Final-question deletion that removes all final question and answer blocks

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

Regular questions are saved asynchronously, including uploaded media. Newly
created content-block identifiers are synchronized after each save so repeated
saves do not create duplicates. The editor also provides a localized next-question
action within the current category and keeps file-picker cancellation from closing
the editor.

Quizzes can be exported and imported as `.bwquiz` packages. Each package is a ZIP archive containing a versioned `manifest.json` and separate media files, so imported quizzes receive new database identifiers and remain independent from the source host and game history.

---

## Live Game

Currently implemented:

- Create a game from an immutable quiz snapshot
- Continue one unfinished game per quiz after an application restart, preserving
  the board, players, scores, visuals, and answer history
- Lobby and join code
- Empty-lobby game start, with players still able to join during regular play
- Join and reconnect flows with host approval
- Host-managed blocked-player list with explicit unblocking and identity-preserving rejoin
- Real-time player presence and score updates
- Active player selection and automatic lowest-score selection for later rounds
- Random wager questions, including rounds configured with zero wager questions
- Wager keypad and validation
- Player buzzer with server-authoritative winner selection
- Compact player-page navigation and mobile screen-wake protection while the buzzer page is open
- Near-simultaneous buzzer results with millisecond differences
- Synchronized buzzer and answer countdown timers displayed as whole seconds
- Persistent global game defaults and per-game settings editable in the lobby or during regular play
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
- Host action to resolve all remaining questions and complete the current round
- Animated top-three inter-round leaderboard
- Adaptive game-board sizing with equal-height category headers
- Host-card dimensions preserved across reloads and responsive layout changes
- Deterministic final standings with score-growth, correct-answer, and attempt tie-breakers
- Private final wagers and answers submitted from player devices
- Host-controlled final question reveal, judging, and final results
- Separate live answer-key screen for the host's second display
- Host answer-history editing with immediate score and standings recalculation
- Persistent completed-game history with final scores and answer details
- Per-round answer statistics and lifetime player statistics per host
- Portable quiz import and export packages with embedded media
- Self-service host accounts with private quiz and game-history ownership
- QR player joining with direct game links

### Discord voice moderation

Each host can connect a Discord account, select a server where the shared bot is
installed, and select one voice channel. During a game, the host can mute or
unmute all participants in that channel except their own Discord user. Optional
automatic moderation mutes participants while question or answer audio, video,
or YouTube content is playing.

Manual and automatic mute requests are tracked independently. Participants are
unmuted only after both reasons have been released, which prevents a media-end
event from overriding a manual mute. See
[`docs/features/discord-voice-moderation.md`](docs/features/discord-voice-moderation.md)
for setup and operational details.

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

For Linux OOM investigation and cgroup commands, see
[Production OOM Diagnostics](docs/operations/oom-diagnostics.md).
