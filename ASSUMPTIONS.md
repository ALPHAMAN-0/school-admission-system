# Assumptions

Where the brief left something open, this is what I chose and why.

## Classes

- **Each shift is its own class** with its own seats, as the brief says. Class names must be
  unique, ignoring case and extra spaces, so "class 6 -  morning shift" duplicates
  "Class 6 - Morning Shift" and returns `409`. Two classes with the same name would confuse
  guardians.
- **Limits:** a name has 1-100 characters and `totalSeats` is 1-10000. Anything else is a `400`.
- **No admin login.** The brief says no login; anyone using the UI can create classes. A real
  deployment would protect the admin screen.

## Student identity

- The `X-Student-Id` header must be **exactly 17 ASCII digits** (birth registration number);
  otherwise `400`. The UI also accepts Bengali digits and removes spaces and dashes before sending.
- Without login, anyone who knows a number can act for that student. This is accepted per the brief.
- The student number is saved in the browser's `localStorage`, so after a refresh or reopening
  the browser the student's applications are still shown.

## Holds, expiry and status

- A hold **occupies a seat only while `now < expiresAt`**. The moment the time passes, the seat
  counts as free and the hold reads as `EXPIRED`. This is computed on every read, so nothing has
  to run for the seat to come back. A background sweeper (every 5 s) also rewrites such rows to
  `EXPIRED` to keep the stored data tidy.
- `expiresAt` = creation time rounded down to the second + `HOLD_DURATION_SECONDS`, matching the
  `2026-11-01T04:10:00Z` format in the brief. Times come from the API server clock in UTC. With
  several backend processes the clocks are assumed to be in sync (same host in compose).
- **One active application per student per class:** `HELD` or `CONFIRMED` blocks a new
  application (`409 ALREADY_APPLIED`, which includes the existing `holdId`). After the hold
  expires or is cancelled, the student may apply again. Different classes are independent.
- **Confirm:** only the owner (`403` otherwise), only while `HELD` and not expired (`409
  HOLD_EXPIRED` / `HOLD_CANCELLED`). Confirming an already-confirmed hold returns `200` with the
  same body and changes nothing, so a double click or retry is safe.
- **Cancel (`DELETE`):** only the owner; `HELD` becomes `CANCELLED` and the seat is free again.
  Repeating it returns `204` (idempotent). A `CONFIRMED` admission cannot be cancelled (`409`),
  because the fee is paid and refunds are out of scope. An expired hold returns `409`.
- `GET /holds/{holdId}` needs no student header because the brief lists none. Hold IDs carry 96
  random bits, so they cannot be guessed, and this endpoint does not return the student number.
- **Payment** is simulated: pressing the pay button is a successful payment.

## Concurrency and the two-process bonus

- Creating a hold runs in one transaction that first locks the class row with
  `SELECT ... FOR UPDATE`. Under the lock it expires stale holds, checks the student's own
  applications, counts `HELD + CONFIRMED`, and inserts only if a seat is free. Every other
  application for the same class waits for that lock.
- **The lock is held by PostgreSQL, not by the app.** Two (or more) backend processes on
  different ports therefore queue on the same row lock, and `held + confirmed <= totalSeats`
  holds across processes. This was tested with two app instances that have separate connection
  pools (automated test), and with two real containers (`--profile multi`).
- A partial unique index (`class_id, student_id WHERE status IN ('HELD','CONFIRMED')`) is a second
  guard for the one-application rule. Confirm and cancel lock the hold row, so concurrent
  confirms count once.
- The sweeper uses `FOR UPDATE SKIP LOCKED`, so it never waits for or deadlocks with live
  requests and can run in every process. Migrations run at startup under a PostgreSQL advisory
  lock, so processes that start together do not migrate twice.
- **Trade-off:** applications to the *same* class are serialised (each takes a few
  milliseconds); different classes do not block each other. That is enough for school admission
  traffic and much simpler than optimistic retries.

## Errors

- Every error is `application/problem+json` with a stable `code` that the UI maps to a Bengali
  message. Client mistakes are always 4xx: malformed JSON, wrong types and missing fields are
  `400`, unknown IDs `404`, someone else's hold `403`, state conflicts `409`. A busy or
  unreachable database gives `503` with `Retry-After`. Unexpected bugs give a generic `500`
  without internal details.

## Frontend

- The UI is in **Bengali**, the brief's language. Statuses show both, e.g. "মেয়াদ শেষ · EXPIRED".
- Live data uses **polling**: every 3 s for the class list and the application page, every 5 s
  for "my applications". The countdown ticks every second and switches to `EXPIRED` locally at
  zero, without waiting for the server. Polling is simpler than WebSockets and enough here.
- The backend URL is read at **runtime** from `config.js`, which the frontend container writes
  from `API_BASE_URL` on start, so the URL can change without rebuilding. `VITE_API_BASE_URL`
  also works at build time.
- CORS allows any origin by default because the API uses no cookies or credentials. It can be
  restricted with `CORS_ALLOWED_ORIGINS`.

## Known limitation

- `npm audit` reports a ReDoS advisory in `picomatch` (GHSA-fc5q-39xc-vjpf), pulled in by Vite and
  Vitest. It only affects the build and test tools matching our own file globs, never the shipped
  app, and no fixed version exists yet. The only "fix" npm offers is downgrading Vite and Vitest
  by several major versions.
