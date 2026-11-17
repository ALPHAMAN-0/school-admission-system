# School Admission System

Online admission for a school where seats are **first come, first served**. Each class and shift
(for example "Class 6 - Morning Shift") has a fixed number of seats. Applying holds a seat for the
student for a limited time; paying the admission fee in time confirms the admission, otherwise the
seat is released automatically.

- **Backend:** C# / ASP.NET Core (.NET 10), EF Core, PostgreSQL 17
- **Frontend:** React 19 + TypeScript, built with Vite, served by nginx
- **Runs with:** Docker only

## Run it

```bash
docker compose up --build
```

| What | URL |
|---|---|
| Frontend (UI for the school admin and guardians) | **http://localhost:3000** |
| Backend API | http://localhost:8080 |
| Health check | http://localhost:8080/health |

The first build downloads the .NET, Node, nginx and PostgreSQL images and takes a few minutes.
Nothing else needs to be installed.

Data is stored in the Docker volume `pgdata`, so it survives `docker compose down` followed by
`docker compose up`. Only `docker compose down -v` deletes it.

### Using the app

1. **ক্লাস তৈরি (অ্যাডমিন)** tab: create a class with a name and a seat count. Each shift is a
   separate class.
2. Type the student's **17-digit birth registration number** in the box at the top and press
   **ব্যবহার করুন**. Bengali digits work too. This number is the student's identity; there is no login.
3. **ক্লাসের তালিকা** tab: total, held, confirmed and free seats per class, refreshed every 3 seconds.
   Press **আবেদন করুন** to apply.
4. The application page shows the status and a live countdown. Press **ভর্তি ফি দিন** to pay
   (simulated) or **বাতিল** to cancel. When the countdown ends the application turns `EXPIRED`
   and the pay button is disabled, without reloading the page.
5. **আমার আবেদন** tab: every application of the student and its status. The student number is
   remembered, so this survives a refresh or reopening the browser.

To see expiry quickly, start with a short hold time:

```bash
HOLD_DURATION_SECONDS=30 docker compose up --build
```

## Configuration

All settings are environment variables (or a `.env` file next to `docker-compose.yml`; see
[`.env.example`](.env.example)).

| Variable | Default | Used by | Meaning |
|---|---|---|---|
| `HOLD_DURATION_SECONDS` | `600` | backend | How long a seat stays held before the fee must be paid |
| `ConnectionStrings__Default` | built from `POSTGRES_*` | backend | PostgreSQL connection string |
| `API_BASE_URL` | `http://localhost:8080` | frontend | Backend URL the browser calls. Applied at container start, no rebuild needed |
| `CORS_ALLOWED_ORIGINS` | `*` | backend | Comma-separated origins allowed to call the API |
| `FRONTEND_PORT` / `BACKEND_PORT` | `3000` / `8080` | compose | Host ports |
| `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` | local defaults | db, backend | Database settings (local use only) |

For local frontend development (`npm run dev`) the backend URL can also be set at build time with
`VITE_API_BASE_URL`.

## Run the tests

### With Docker only

```bash
docker compose --profile test run --rm backend-tests    # 46 backend tests, real PostgreSQL
docker compose --profile test run --rm frontend-tests   # 24 frontend tests
```

`backend-tests` starts the `db` service if needed. Each test class creates its own throw-away
database and drops it afterwards, so the app's data is not touched.

### With the SDKs installed (.NET 10 SDK, Node 22)

```bash
# Backend: needs a PostgreSQL server. The tests default to localhost:55432 (postgres/postgres).
docker run -d --name admission-test-db -e POSTGRES_PASSWORD=postgres -p 127.0.0.1:55432:5432 postgres:17-alpine
dotnet test backend
# or point them at any server:
TEST_DB_CONNECTION="Host=...;Port=...;Username=...;Password=...;Database=postgres" dotnet test backend

# Frontend
cd frontend
npm ci --ignore-scripts
npm test
```

### What the tests prove

- **Concurrency** (`ConcurrencyTests`): 200 simultaneous applications for 10 seats create exactly
  10 holds. Also covered: one student applying 20 times at once gets one hold, 15 simultaneous
  confirms count once, and **two app instances with separate connection pools sharing one
  database** never over-book. These tests fail if the class row lock is removed (checked by
  deleting `FOR UPDATE` and re-running).
- **Expiry** (`ExpiryTests`): a manual clock moves past the hold time. The seat frees itself with
  no API call, confirming then fails with `HOLD_EXPIRED`, and the student can apply again.
- **Rules and contract** (`HoldApiTests`, `ClassApiTests`): field names and status codes,
  ownership (403), one active application per student per class, idempotent confirm and cancel,
  and 4xx (never 500) for malformed JSON, wrong types, bad student IDs and unknown IDs.
- **Frontend**: the countdown flips to `EXPIRED` and disables the pay button with no reload; seat
  counts refresh on their own; the student ID survives a reload; network failures, proxy errors
  and API error codes become plain Bengali messages.

## Two backend processes (bonus)

```bash
docker compose --profile multi up --build
```

This adds a second backend on **http://localhost:8081** using the same database. Seats stay
correct because the lock lives in PostgreSQL, not in the app (see
[ASSUMPTIONS.md](ASSUMPTIONS.md#concurrency-and-the-two-process-bonus)). Manual check: 120
simultaneous applications split across ports 8080 and 8081 for a 5-seat class gave exactly five
`201` responses and 115 `409` responses.

## API

Base URL `http://localhost:8080`, JSON in and out. The student is identified by the
`X-Student-Id` header (17 digits).

| # | Method and path | Student header | Success |
|---|---|---|---|
| 1 | `POST /classes` `{ "name", "totalSeats" }` | no | `201` class |
| 2 | `GET /classes` | no | `200` classes with `heldSeats`, `confirmedSeats`, `availableSeats` |
| 3 | `GET /classes/{classId}` | no | `200` one class with seat counts |
| 4 | `POST /classes/{classId}/holds` | yes | `201` hold (`409` when no seat is free) |
| 5 | `GET /holds/{holdId}` | no | `200` hold, `status` is `HELD`, `CONFIRMED`, `EXPIRED` or `CANCELLED` |
| 6 | `POST /holds/{holdId}/confirm` | yes | `200` `{ "holdId", "status": "CONFIRMED" }` |
| 7 | `DELETE /holds/{holdId}` | yes | `204` |
| 8 | `GET /me/holds` | yes | `200` the student's holds with `className` |

All contract fields are returned exactly as specified; a few extra fields (`className`,
`createdAt`, `confirmedAt`) help the UI.

Errors use [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) `application/problem+json` with a
stable `code`:

```json
{ "status": 409, "title": "No seats available", "detail": "All seats in this class are currently held or confirmed.", "code": "NO_SEATS_AVAILABLE" }
```

| Status | Codes |
|---|---|
| 400 | `VALIDATION_FAILED` (with per-field `errors`), `INVALID_REQUEST` (malformed JSON or wrong types), `MISSING_STUDENT_ID`, `INVALID_STUDENT_ID` |
| 403 | `NOT_HOLD_OWNER` |
| 404 | `CLASS_NOT_FOUND`, `HOLD_NOT_FOUND` |
| 409 | `NO_SEATS_AVAILABLE`, `ALREADY_APPLIED` (with the existing `holdId`), `CLASS_NAME_TAKEN`, `HOLD_EXPIRED`, `HOLD_CANCELLED`, `HOLD_ALREADY_CONFIRMED` |
| 503 | `SERVICE_UNAVAILABLE` (database busy or unreachable; has `Retry-After`) |

## Project layout

```
backend/
  src/SchoolAdmission.Api/
    Endpoints/        minimal API routes (the 8 endpoints)
    Services/         AdmissionService (all rules), HoldExpirySweeper (background job)
    Domain/           SchoolClass, AdmissionHold, HoldStatus
    Data/             EF Core DbContext and migrations
    Infrastructure/   errors, student ID, options, startup migration, JSON
  tests/SchoolAdmission.Api.Tests/   integration tests against real PostgreSQL
  Dockerfile
frontend/
  src/pages/          ClassList, CreateClass, Application, MyApplications
  src/hooks/          polling, clock, student ID, hash router
  src/api/            fetch client and Bengali error messages
  Dockerfile, nginx.conf, docker/40-runtime-config.sh
docker-compose.yml
ASSUMPTIONS.md        decisions where the brief was open
AI_USAGE.md           how AI tools were used
```
