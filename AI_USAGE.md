# AI usage

## Tools

| Tool | Used for |
|---|---|
| **Claude Code** (Anthropic, Claude Opus model) | Almost all of the work: design, backend, frontend, tests, Dockerfiles, compose file and these documents |
| ECC skills inside Claude Code | Guidance per area: `dotnet-patterns`, `postgres-patterns`, `api-design`, `csharp-testing`, `react-patterns`, `react-testing`, `docker-patterns`, `security-review` |
| ECC reviewer agents (`csharp-reviewer`, `react-reviewer`) | Independent code review of the finished backend and frontend |

The assignment brief was given to the AI locally. It is not in this repository.

## How the AI's work was checked

- A curl smoke test of every endpoint and error case after the backend was written.
- **Mutation tests:** the class row lock (`FOR UPDATE`) and the client-side expiry logic were
  deliberately removed to prove the tests then fail.
- An end-to-end run in a headless browser against `docker compose up`: create a class, apply, pay,
  watch a hold expire live, reopen the browser, stop the backend. All 14 checks passed with no
  console or CSP errors.
- Two real backend containers on ports 8080 and 8081 were sent 120 simultaneous applications for
  5 seats. The result was exactly five `201` responses.
- Two independent reviewer agents read the code. Every finding was checked against the code
  before it was fixed or rejected.

## Where the AI was wrong, and how it was caught

| # | Mistake | Caught by | Fix |
|---|---|---|---|
| 1 | The first concurrency test used 25 seats with a 25-connection pool. The first wave of requests filled the class exactly, so **the test passed even with the lock removed** | Mutation test (deleted `FOR UPDATE`, re-ran) | 10 seats vs 200 requests, and a separate connection pool for the 2nd instance. It now fails without the lock |
| 2 | Made class names unique (`409` on duplicate). The brief does not ask for this, and an automated test that creates the same class twice would have failed | Reviewer agent | Duplicates allowed (new migration) |
| 3 | A class name containing a NUL byte reached PostgreSQL and produced a **500** | Reviewer agent | Validated as `400`; database encoding errors mapped to `400` |
| 4 | `expiresAt` was rounded *down*, so a hold could last up to 1 s less than `HOLD_DURATION_SECONDS` | Reviewer agent | Rounded up; a test checks the full duration |
| 5 | EF Core query sorted *after* projecting into a record, which made `GET /classes` return **500** | curl smoke test | Sort before projecting |
| 6 | An error extension named `status` clashed with the problem-details `status` field (duplicate JSON key) | Reading the raw response | Renamed to `holdStatus` |
| 7 | Frontend: a malformed link (`%E0%A4`) threw during render and left a **blank page** | Reviewer agent | Safe decoding plus an outer error boundary |
| 8 | Frontend: polling aborted the previous request on every tick, so a server slower than 3 s never showed data | Reviewer agent | Skip a tick while a request is in flight; test added |
| 9 | Frontend: a `200` with HTML (e.g. a wrong `API_BASE_URL`) rendered an empty page with no message | Reviewer agent | Response shape checked; friendly error |
| 10 | Picked NuGet packages built for .NET 11 in a .NET 10 project | Restore error `NU1202` | Pinned 10.0.x |
| 11 | A `sed` command used `#` as delimiter while its pattern also contained `#` | Reading the script, then testing it with tricky URLs | Changed delimiter |
| 12 | A test regex used `\d`, which does not match Bengali digits | Failing test | Used `[০-৯]` |

Some reviewer findings were wrong and were not applied. One claimed a fallback connection string
in `appsettings.json`, but it is only in `appsettings.Development.json`. Another said that clicking
pay twice shows a "cannot be cancelled" message, but confirm is idempotent and returns `200`.
