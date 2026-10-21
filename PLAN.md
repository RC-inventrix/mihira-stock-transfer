# Plan

The working plan for the Mihira Stores stock-transfer assessment. `ASSESSMENT_BRIEF.md` is the source of truth for the requirements. This file records how the work is split, what has been decided and what is left. **Update the status table at the end of every phase.**

> The original plan from session 1 was only in the conversation and was lost when the session ended. This version was rebuilt on 2026-10-21 at 22:18 from the brief, the commit history and the session notes. Phases 0 to 3 describe what was actually built.

## Status

| Phase | What | Status | Commits |
|---|---|---|---|
| 0 | Scaffold: solution, API, tests, React app, Postgres compose, doc skeletons | Done | `8edb757` |
| 1 | Domain model, DbContext with constraints, seed data, migration | Done | `5c191be` |
| 2 | Business rules and API endpoints | Done | `35024f6`, `4c5e358` |
| 3 | Automated tests against real PostgreSQL | Done | `962a665` |
| 4 | React screen | Done | `688539f`, `1c96978`, `6677852` |
| 5 | Optional stretch: receive a smaller quantity | Done | `8b966d8`, `de3e74a`, `ddec680` |
| 6 | Finish README and AI_USAGE, final run-through | **Next** | |

## Time budget (3 hours in total)

| | Planned | Actual |
|---|---|---|
| Phases 0 to 3, including reading the brief and planning | ~1h 45m | ~1h 40m (20:38 to 22:18) |
| Phase 4: UI | ~45m | ~15m (22:18 to 22:31) |
| Phase 6: docs and final check | ~20m | |
| Phase 5: stretch, only if time is left | ~30m | ~10m (22:31 to 22:41) |

About 1h 20m of the budget is left. Phases 4 and 6 fit inside it, and Phase 5 probably does not. If it does not, it goes in the README under "What I would do with another day".

## Grading criteria and where each is met

From "How we review it" in the brief:

| Criterion | Where |
|---|---|
| Runs by following the README | Phase 6: fresh run-through on a clean database |
| Stock rules hold, including double dispatch | Phase 2 (`TransferService`), proven in Phase 3 (`ConcurrencyTests`, `TransferWorkflowTests`) |
| Code is clear and sensibly structured for its size | One service holds the rules, with thin controllers and a single error handler |
| Tests check things that matter | 30 integration tests (26 + 4 for receiving a smaller quantity). The deliberate-break checks are written up in the README |
| README and AI notes are clear and honest | Kept up to date every phase. The candidate writes their own section of AI_USAGE |

## Decisions already made (do not reopen)

- **Stack:** .NET 10, EF Core, PostgreSQL 17 in Docker (host port 5433), Vite + React + TypeScript.
- **Concurrency:**
  - Each status change is a single conditional UPDATE (`WHERE "Status" = expected`), in one transaction with the stock changes.
  - The stock decrement is guarded (`WHERE "Quantity" >= needed`), and a CHECK constraint backs it up.
  - We do not use an EF row version with retries.
  - Stock rows are updated in product order, so concurrent updates cannot deadlock.
- **Repeats:** a repeated action that already happened returns 200 and changes nothing. An action from the wrong status returns 409. A missing transfer returns 404. A validation failure returns 400.
- **Errors:** errors are ProblemDetails. `detail` is a sentence to show the user, and `messages` lists each problem. (ASP.NET's own model-binding 400s use an `errors` object instead.)
- **Creating a transfer:**
  - All validation problems are reported at once.
  - Creating does not check stock; the check happens at dispatch.
  - An optional `Idempotency-Key` header makes create safe to retry.
- **Stock rows:** every branch/product pair is seeded with a stock row, so receiving stock is always an update and never an insert.
- **Tests:** `WebApplicationFactory` with a Testcontainers PostgreSQL. There is one container per run, and the data is reset to the seed before each test.
- **Assumptions:** the assumptions are listed in `README.md`. Add new ones there as they are made.

## Phase 4: React screen (done)

The brief asks for one screen. It does not need to look polished, but it must work and show errors clearly.

**API client (`src/api.ts`)**
- TypeScript types that mirror the DTOs: `Branch`, `BranchStock`, `Transfer`, `TransferStatus`.
- A `request()` wrapper that throws an `ApiError` carrying the message to show. Take the message from `detail` and `messages` in our ProblemDetails, from the `errors` object in ASP.NET validation 400s, or from the status text when there is no body (for example, when the API is down).
- Calls: `getBranches`, `getStock(branchId)`, `getTransfers({branchId, status})`, `createTransfer(body, idempotencyKey)`, `dispatch/receive/cancel(id)`. All go through the `/api` prefix, which the Vite proxy forwards.

**New transfer form**
- From and to branch dropdowns. Picking the same branch for both is blocked in the UI, and the API also rejects it.
- Lines: a product dropdown, a quantity input, and the **available stock at the from branch** next to each line, with a remove button. "Add line" adds another.
- Show a warning (but still allow submitting) when a quantity is more than the available stock. Stock is checked at dispatch, which matches the assumption in the README.
- Submit sends an `Idempotency-Key` from `crypto.randomUUID()`. The key is kept until the request succeeds, so a retry after a network error reuses it, and a new key is made only for the next new transfer. The button is disabled while the request is in flight.
- Show errors from the API above the form. Clear the form on success.

**Transfer list**
- Filter by branch and by status, using the `GET /transfers?branchId=&status=` query.
- Each row shows the id, from → to, the lines, the status and the dates.
- Buttons appear only for the actions the current status allows: Dispatch and Cancel when Requested, Receive when Dispatched. Each button is disabled while its request is in flight.
- After any action or create, reload the list and the stock.
- Show action errors (for example "Not enough stock … Coconut Oil 1L: 4 needed, 3 available.") in a clear error area that the user dismisses.

**Housekeeping**
- Remove the Vite template boilerplate (`App.css`, `assets/`, hero and logo markup). Keep the styling minimal and readable.

**Commits (small)**
1. API client and types.
2. New transfer form with available stock.
3. Transfer list with filters and actions.

**As built (differences from the plan above)**
- There is no products endpoint, so the product list comes from the from branch's stock (every branch has a row for every product).
- A new line starts with no product chosen; Create is disabled until every line has one. A blank quantity is sent as 0, so the API's "greater than zero" message reports it.
- Added a **stock levels table** (every product at every branch) under the list. It was not in the plan, but it makes "stock moved once" visible on screen.
- With several problems, the error box shows the API's `title` with the `messages` as a list; with one, it shows `detail`.

**Done when**
- `npm run build` and `npm run lint` pass.
- In a browser, with the API and database running: create a transfer, see the stock, dispatch, receive, cancel; trigger an over-stock dispatch and see its message; double-click Dispatch and see stock move once.
- Add a row to the AI_USAGE working log and update this table.

## Phase 5: optional stretch, receive a smaller quantity (done)

Do this only if at least 40 minutes are left after Phase 4. If not, list it under "What I would do with another day".

- Add `TransferLine.ReceivedQuantity` (nullable) and a migration. The difference, sent minus received, is the record of the loss.
- `POST /transfers/{id}/receive` takes an optional body, `{ lines: [{ productId, receivedQuantity }] }`. Each received quantity must be between 0 and the sent quantity. Lines left out are received in full. With no body, everything is received in full, as it is now.
- The receiver's stock increases by the received quantity. The sender has already lost the full quantity at dispatch.
- Tests: a smaller receive adds only what was received and records the difference; receiving more than was sent is rejected.
- UI: quantity inputs in the Receive action. The list shows any shortfall.

**As built (additions to the plan above)**
- A CHECK constraint backs up the range rule: `ReceivedQuantity` is null, or between 0 and `Quantity`.
- The migration backfills `ReceivedQuantity = Quantity` on transfers that were already Received, since those always took in the full amount.
- Invalid received quantities are rejected inside the receive transaction, so the status change rolls back with them and the transfer stays Dispatched.
- A repeated receive with different figures returns 200 and changes nothing, like any other repeat.
- Done: 30 tests pass; breaking the stock update (adding the sent quantity instead of the received one) failed 2 of the 4 new tests; the receive form was checked in the browser (7 checks), and the Phase 4 browser checks still pass.

## Phase 6: finish the docs and do a final check

- `README.md`:
  - "How to run" in a few steps: `docker compose up -d --wait`, run the API, `npm install && npm run dev`, and the URL to open.
  - The trade-offs, "What I would do with another day", and the time-spent table with a total.
- `AI_USAGE.md`:
  - Summarise the tools section from the working log.
  - Pick one real case where the AI was wrong. The log has candidates: the `ToDictionaryAsync` client-side selector bug, and the EF version clash (CS1705).
  - Leave "What I wrote or decided myself" for the candidate.
- Final run-through, following the README word for word:
  - `docker compose down -v`, then a fresh start;
  - `dotnet test`;
  - `npm run build && npm run lint`;
  - in the browser: create, dispatch, receive, cancel and an over-stock dispatch.
- Check that `git status` is clean, then make the final commit. The deadline is Thursday 22 October 2026, 2:00 pm Sri Lanka time.

## Ideas for "another day" (collect as they come up)

- A reason per shortfall (damaged, missing, wrong item), and a way to follow up on it.
- Who did what: recording the user on each step once login exists.
- Paging for the transfer list.
- Reserving stock at request time, so a request cannot be made against stock that is already promised.
- Frontend component tests, and an end-to-end test (Playwright) of the screen.
- Docker Compose services for the API and the UI, so the whole app starts with one command.
