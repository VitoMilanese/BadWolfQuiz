# 🐺 BAD WOLF QUIZ

**Bad Wolf Quiz** — це сучасна real-time платформа для проведення квізів, написана на **ASP.NET Core 8**.

Проєкт складається з двох основних частин:

- потужного редактора квізів;
- майбутньої ігрової платформи для ведучого та гравців.

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

Content blocks can be:

- reordered using Drag & Drop;
- removed;
- added dynamically.

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

- Create live game
- Lobby
- QR code
- Join by code
- Player reconnect
- Team mode

## Game

- Real-time board
- Question presentation
- Timers
- Buzz system
- Automatic scoring
- Host controls

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