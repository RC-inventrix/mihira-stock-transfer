# AI Usage

> Draft. The working log at the bottom is kept up to date as the work happens. The final sections are written at the end from that log.

## Tools used and what for

- **Claude Code (Claude Opus 5.5)**: _TODO: summarise from the working log._

## Where the AI was wrong or unhelpful, and how I noticed

_TODO: take a real case from the working log. Do not invent one._

## What I wrote or decided myself

_TODO: the candidate writes this section personally._

---

## Working log

Notes recorded as the work happens, so the summary above is accurate.

| When | What the AI did | What I checked, changed or decided |
|---|---|---|
| 2026-10-21, session 1 | Read the brief and drafted `CLAUDE.md` and a phased plan: the conditional-update design for dispatch, receive and cancel, the data model, the test list and the time budget. | Reviewed the plan and accepted its defaults: PostgreSQL rather than SQL Server, and a repeated action returns 200 with no effect rather than 409. |
| 2026-10-21, session 1 | Phase 0: ran `git init`, scaffolded the .NET 10 API and xUnit projects and the Vite React-TS app, added Docker Compose for PostgreSQL, the Vite `/api` proxy and these doc skeletons. | |
| 2026-10-21, session 1 | Phase 1: wrote the entities, `AppDbContext` (check constraints for non-negative stock, positive line quantity and different branches; unique idempotency key; one product per transfer), seed data through `HasData`, the `InitialCreate` migration and migrate-on-startup. | Verified against the real database: 20 stock rows seeded and the check constraints present. A second startup applied nothing and seeded nothing twice. The EF "fail" log on first start was checked: it is EF querying the migration history table before it exists, which is expected. |
| 2026-10-21, session 1 | Phase 2: wrote the ProblemDetails error handling, the branch and stock endpoints, and `TransferService` with create (validation that reports every problem at once, plus an `Idempotency-Key` header), dispatch, receive and cancel as conditional updates in one transaction, with the transfers controller. | AI bug caught at review before the first run: the shortage message read product names with `ToDictionaryAsync(key, element)`. That selector runs on the client, where `Product` is not loaded, so it would have thrown. Fixed by projecting first. Then tested against the running API: every validation message; receive before dispatch (409); 10 concurrent dispatches all returned 200 and the warehouse lost the stock exactly once (rice 500 → 490); 5 concurrent receives added it once; an over-stock dispatch was rejected with each short line listed, and the line it had already taken was rolled back; cancel after dispatch (409); 8 concurrent creates with one idempotency key made one row. |
