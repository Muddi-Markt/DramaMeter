# MUDDI's Drama Meter — Implementation Plan

> A Blazor Server .NET 10 app for a voluntary association.
> Branding: white text on red background (#e53231).

**Status:** Phase 1–3 complete — infrastructure, data layer, entities, DbContext, auto-migration on startup, unit tests (47 passing), and all three backend services (Session, Vote, Result).

## 1. Feature Summary

| Feature | Details                                                                                                                                           |
|---------|---------------------------------------------------------------------------------------------------------------------------------------------------|
| **Gauge** | 4 levels: "No Drama" → "It's Sparking" → "Bottomless" → "Extraordinary Session!"                                                                  |
| **Colors** | Green → Yellow → Orange → Dark Red (sorted by urgency)                                                                                            |
| **Voting** | User selects a level → result is displayed                                                                                                        |
| **User Cooldown** | 10 minutes per user (cookie-based, countdown timer shown)                                                                                         |
| **Overall Result** | EWMA (Exponentially Weighted Moving Average) of all votes (0–3 scale), animated on the gauge                                                      |
| **Last 10 Votes** | Tiny dots directly on the gauge scale marking the last 10 positions                                                                               |
| **Drama Drift** | The overall value drifts toward "No Drama" — if no one votes for 3 days, the gauge naturally settles at about zero. New votes "wake it up" again. |
| **Delete Vote** | User can delete their own vote (delete button)                                                                                     |
| **Data Storage** | PostgreSQL|
| **No Login** | Anonymous via server-generated UUID in cookie                                                                                                     |
| **No Consent Banner** | Small internal project                                                                                                                            |
| **Votes Never Expired** | All votes kept indefinitely (EWMA handles irrelevance mathematically)                                                                             |
| **Hosting** | Docker Compose, behind reverse proxy (HTTP, no HTTPS needed)                                                                                      |
| **UI Style** | White text, red background (#e53231), logo/branding optional                                                                                      |

---

## 2. Architecture

```
┌─────────────────────────────────────────────┐
│  Docker Compose                              │
│                                              │
│  ┌──────────────────┐    ┌────────────────┐  │
│  │  DramaMeter App   │───▶│  PostgreSQL    │  │
│  │  (Blazor Server)  │    │  (drama_meter) │  │
│  │  :8080            │    │                │  │
│  └──────────────────┘    └────────────────┘  │
│                                              │
│  (Reverse Proxy: nginx / Caddy / …)          │
└─────────────────────────────────────────────┘
```

- **Single Blazor Server app** (.NET 10) — serves both the Razor UI and the backend API
- **PostgreSQL** — stores votes, user sessions (anonymous UUID), voting cooldown state
- **Docker Compose** — orchestrates the app + DB

---

## 3. Data Model

### `users` table
| Column | Type | Description |
|--------|------|-------------|
| `id` | UUID (PK) | Server-generated anonymous UUID |
| `created_at` | timestamptz | When the user first visited |

### `votes` table
| Column | Type | Description |
|--------|------|-------------|
| `id` | BIGSERIAL (PK) | Auto-increment |
| `user_id` | UUID (FK → users) | Who voted |
| `level` | INT | 0=No Drama, 1=It's Sparking, 2=Bottomless, 3=Extraordinary Session |
| `created_at` | timestamptz | When voted |

### Indexes
- `votes(user_id, created_at DESC)` — for cooldown check & last 10
- `votes(created_at DESC)` — for overall calculation & ordering

---

## 4. API Endpoints

Not needed as we use blazor with server side rendering and state management.

---

## 5. UI Structure

### Layout
- **Full-screen**: red background (#e53231), white text
- **Header**: "MUDDI's Drama Meter" + logo (optional)
- **Footer**: "Delete Vote" button + note on anonymity

### Phase 1 — Before Voting (Start Screen)
- **Background**: Only the empty gauge (radial gauge with 4 segments: green, yellow, orange, dark red)
- **4 positions**: The 4 positions, arranged in a semicircle
- **No result yet visible (no needle yet) **

### Phase 2 — After Voting (Result Screen)
- **Needle fades in** (animation: needle animates to position)
- **Overall result**: Needle shows EWMA value
- **Last 10 votes**: Tiny dots on the gauge scale, they should be at the same positions as the user has clicked on for real in the phase 1
- **Cooldown timer**: "You can vote again in X min"
- **Delete vote** button

---

## 6. Gauge Design

- **SVG gauge** with 4 colored segments (arcs)
- **Animated needle** (CSS animation) shows current overall value (0.0–3.0)
- **10 small circles (dots)** directly on the gauge scale, black

---

## 7. Cooldown & Drama Drift Logic

Two **independent** mechanisms:

### Per-User Cooldown
- After voting, the user must **wait 10 minutes** before voting again
- Countdown timer visible in UI (e.g., "7 min 23 sec remaining")

### Global Drama Drift — EWMA (Exponentially Weighted Moving Average)

If **no one** votes for 3 days → the gauge drifts to "No Drama". If people keep voting, the result stays relevant.

**Formula:**

```
dramaLevel = Σ(level × e^(-λ × ageInDays)) / Σ(e^(-λ × ageInDays))
```

- `level` = 0, 1, 2, 3 (No Drama → Extraordinary Session)
- `ageInDays` = days since vote (relative to current time)
- `λ` = decay rate, so that after 3 days weight ≈ 5%
  - `e^(-λ × 3) = 0.05` → `λ = -ln(0.05) / 3 ≈ 0.998 ≈ 1.0`

**Example weights:**

| Age | Weight |
|-----|--------|
| 0 days | 1.00 |
| 0.5 days | 0.61 |
| 1 day | 0.37 |
| 2 days | 0.14 |
| 3 days | 0.05 |
| 4 days | 0.02 |
| 5 days | 0.007 |

**Behavior:**
- Fresh votes dominate the result
- Old votes contribute very little but are never deleted
- No votes in 3 days → total weight ≈ 0 → result → 0 (No Drama)
- New votes "wake up" the gauge and pull the result back up

**Implementation:**
- Load all votes from DB, calculate EWMA
- Votes older than ~7 days can be ignored in the UI (weight < 0.01%)
- All votes remain in the DB (infinite retention, but mathematically irrelevant after time)

---

## 8. Tech Stack

| Component | Technology |
|-----------|------------|
| **Framework** | .NET 10 Blazor Server |
| **DB Provider** | Npgsql (PostgreSQL) |
| **ORM** | EF Core 10 (code-first migrations) |
| **Container** | Docker + docker-compose |
| **Frontend** | Razor Components + CSS + SVG |
| **State Management** | Server-side Blazor (inherent session) |
| **Cookie** | HttpOnly, SameSite=Lax, Path=/ |

---

## 9. Project Structure

```
DramaMeter/
├── Dockerfile                   ← existing (in Blazor project)
├── docker-compose.yml           ← to create
├── PROJECT_PLAN.md              ← this file
├── Muddi.DramaMeter.slnx        ← existing
├── Muddi.DramaMeter.Blazor/     ← Blazor Server project
│   ├── Muddi.DramaMeter.Blazor.csproj
│   ├── Program.cs               ← existing (Blazor Server, InteractiveServer)
│   ├── Dockerfile               ← existing (multi-stage, .NET 10)
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── .dockerignore
│   ├── Components/
│   │   ├── App.razor            ← existing (HTML shell)
│   │   ├── Routes.razor
│   │   ├── _Imports.razor
│   │   ├── Layout/
│   │   │   ├── MainLayout.razor ← existing
│   │   │   └── ReconnectModal.razor
│   │   ├── Pages/
│   │   │   ├── Home.razor       ← existing (placeholder, to be replaced)
│   │   │   ├── Error.razor
│   │   │   └── NotFound.razor
│   │   ├── Gauge.razor          ← to create
│   │   ├── VoteButton.razor     ← to create
│   │   ├── CooldownTimer.razor  ← to create
│   │   └── LastVotes.razor      ← to create
│   ├── Data/                    ← to create
│   │   ├── DramaMeterDbContext.cs
│   │   └── SeedData.cs
│   ├── Models/                  ← to create
│   │   ├── User.cs
│   │   └── Vote.cs
│   ├── Services/                ← to create
│   │   ├── SessionService.cs
│   │   ├── VoteService.cs
│   │   └── ResultService.cs
│   ├── Pages/                   ← to create (replaces Components/Pages/Home.razor)
│   │   ├── Index.razor          ← main page
│   │   └── Privacy.razor        ← (optional)
│   ├── wwwroot/
│   │   ├── app.css              ← existing (placeholder)
│   │   └── css/
│   │       └── site.css         ← to create (main styling)
│   └── Properties/
│       └── launchSettings.json
└── .dockerignore                ← existing (root level)
```

---

## 10. Implementation Steps

### Phase 1: Foundation (Infrastructure) ✅ COMPLETE
1. [x] **Create .NET 10 Blazor Server project** — existing skeleton (`Muddi.DramaMeter.Blazor`)
2. [x] **Set up Dockerfile** — multi-stage, .NET 10, ports 8080/8081
3. [x] **Create docker-compose.yml** with app + PostgreSQL (healthcheck, named volume)
4. [x] **Add NuGet packages** — EF Core 10.0.9, Npgsql 10.0.3, EFCore.Design 10.0.9
5. [x] **Configure PostgreSQL connection string** — `appsettings.json`, `appsettings.Development.json`, registered in `Program.cs`

### Phase 2: Data Layer ✅ COMPLETE
6. [x] **Create EF Core entities** (`User`, `Vote`) — done as part of Phase 1
7. [x] **Set up `DramaMeterDbContext`** with Npgsql — done as part of Phase 1
8. [x] **Create first migration** and apply it — verified tables exist in PostgreSQL
9. [x] **Write unit tests** for entities & DbContext (optional) — `UserTests`, `VoteTests`, `DramaMeterDbContextTests`

### Phase 3: Backend Services ✅ COMPLETE
10. [x] **`SessionService`**: Cookie-based UUID management
    - Create session if no cookie exists
    - Validate existing cookie
11. [x] **`VoteService`**: Vote CRUD operations
    - Submit vote (with cooldown check)
    - Delete own vote
    - Check remaining cooldown
12. [x] **`ResultService`**: Result calculation
    - EWMA calculation
    - Last 10 votes retrieval
    - Drama drift logic (see Section 7)

### Phase 4: Frontend — Gauge Components
13. [x] **Create `Gauge.razor`** SVG component
    - 4 colored segments (green, yellow, orange, dark red)
    - Animatable needle (0.0 to 3.0) with CSS transition
    - Labels under each segment
14. [x] **Create `VoteButton.razor`** × 4
    - Hover effects, disabled state during cooldown
15. [x] **Create `LastVotes.razor`**
    - Small black dots on the gauge scale at real click positions
16. [x] **Create `CooldownTimer.razor`**
    - Countdown in minutes/seconds
    - Updates every second via Timer

### Phase 5: Frontend — Pages & Integration
17. [x] **`Index.razor`**: Main page
    - Gauge + 4 vote buttons
    - Animated needle + result + cooldown
    - Cooldown transition between phases
18. [x] **Integrate Blazor Server calls** (service methods called directly from Razor)
    - Vote, cooldown check, results, delete
19. [x] **Styling**: Red background, white text, responsive design

### Phase 6: Polish
20. [ ] **Animations**: Needle flies to position, fade-in effects
21. [ ] **Drama drift**: Implement exponential decay calculation
22. [ ] **Delete vote flow**: Confirmation dialog + UI update
23. [ ] **Error handling**: Network failures, DB errors
24. [ ] **Docker testing**: Build & run via docker-compose
25. [ ] **Final review & bug fixes**

---

## 11. Key Decisions & Rationale

| Decision | Choice | Why |
|----------|--------|-----|
| Blazor Server vs WebAssembly | **Server** | Simpler deployment, server-side state, no WASM overhead |
| Cookie for identity | **HttpOnly + Random UUID** | Anonymous, survives sessions, GDPR-compliant |
| File vs DB storage | **PostgreSQL** | Already available, scalable, concurrent writes |
| Separate API project | **No** | Blazor Server handles both UI and API routes |
| Vote retention | **Infinite** | EWMA handles irrelevance mathematically; no expiry needed |
| Drama drift | **EWMA** | Natural decay to "No Drama", no hard reset, smooth transition |
| Consent banner | **No** | Small internal project |

---

## 12. Open Questions (for later)

1. Should the gauge animation be smooth or snap? --> Smooth
2. Should we show the total number of votes somewhere? --> Yes
3. Should there be a "sharable link" feature (e.g., QR code on a table)? --> No
4. Should admin see a raw list of all votes (with timestamps, no user identity)? --> No
5. Should the 4 vote labels be in German or English? --> They should be changeable by a software devloper. Start with english
