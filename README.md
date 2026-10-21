# Mihira Stores – Branch Stock Transfer

A small system for moving stock between the warehouse and the Colombo, Kandy and Galle branches. It has an ASP.NET Core Web API (.NET 10, EF Core, PostgreSQL) and a single React + TypeScript screen.

## How to run

Prerequisites: .NET 10 SDK, Node.js 20.19+ or 22.12+ (required by Vite 8), and Docker (used for the database and for the tests).

From the repository root:

1. Start PostgreSQL 17 (host port **5433**, so it does not clash with a local PostgreSQL):
   ```
   docker compose up -d --wait
   ```
2. Start the API on http://localhost:5131. On startup it applies the migrations and the seed data (the warehouse, three branches, five products and starting stock):
   ```
   dotnet run --project backend/StockTransfer.Api
   ```
3. In a second terminal, start the UI:
   ```
   cd frontend
   npm install
   npm run dev
   ```
4. Open **http://localhost:5173**. The Vite dev server forwards `/api/*` to the API.

To start again from the seed data, stop the API and run `docker compose down -v`, then repeat from step 1.

On a fresh database EF logs a `fail` line about the missing `__EFMigrationsHistory` table before it creates it. That is expected.

**Using the screen:** pick the from and to branches, add product lines (the stock available at the from branch is shown on each line) and create the transfer. The list below offers Dispatch and Cancel for a Requested transfer, and Receive for a Dispatched one. Receive asks how much of each line arrived, prefilled with the quantity sent. The stock-levels table at the bottom shows every product at every branch, so each movement can be seen. To see the over-stock error, send 4 Coconut Oil 1L from Galle (which holds 3) and dispatch it.

## API

The routes have no prefix (the UI calls them through `/api`). OpenAPI is at `/openapi/v1.json` in Development.

| Method and route | What it does |
|---|---|
| `GET /branches` | The warehouse and branches. |
| `GET /branches/{id}/stock` | Stock of every product at one branch. |
| `GET /transfers?branchId=&status=` | Transfers, newest first. Both filters are optional. |
| `GET /transfers/{id}` | One transfer. |
| `POST /transfers` | Create a Requested transfer: `{ "fromBranchId", "toBranchId", "lines": [{ "productId", "quantity" }] }`. An optional `Idempotency-Key` header makes it safe to retry. |
| `POST /transfers/{id}/dispatch` | Requested → Dispatched. Takes the stock from the sender. |
| `POST /transfers/{id}/receive` | Dispatched → Received. Adds the stock at the receiver. Optional body: `{ "lines": [{ "productId", "receivedQuantity" }] }`. |
| `POST /transfers/{id}/cancel` | Requested → Cancelled. |

Errors are ProblemDetails: 400 for invalid input, 404 for a missing transfer, 409 for a step from the wrong status or not enough stock.

## Tests

`dotnet test` from the repository root (Docker must be running) runs 30 integration tests. The tests host the real API in memory with `WebApplicationFactory` and run against a throwaway PostgreSQL 17 container (Testcontainers). The concurrency rules rely on PostgreSQL row locks, which an in-memory database would not show. Each test starts from the seeded stock.

- **`TransferWorkflowTests`:** creating moves no stock. Dispatch takes stock from the sender only, and receive adds it to the receiver. An over-stock dispatch returns 409, lists each short line, and rolls back the lines it had already taken. Dispatching or receiving twice moves stock once. Every wrong-status action (for example, receive before dispatch, cancel after dispatch, dispatch after cancel) returns 409 and moves nothing.
- **`ConcurrencyTests`:** 10 simultaneous dispatches, or receives, of one transfer move stock once. Three transfers competing for the same 3 units: exactly one is dispatched and stock ends at 1, never below zero. 10 simultaneous creates with one idempotency key make one transfer.
- **`ReceiveTests`:** a short receive adds only what arrived and records it, and lines left out arrive in full. Receive with no body takes in everything. Invalid received quantities (more than sent, negative, a product not on the transfer) are all reported together, and the transfer stays Dispatched. A repeated receive with different figures changes nothing.
- **`CreateTransferTests`:** each validation rule gives its own message and creates nothing. All problems are reported together. A repeated idempotency key returns the original transfer.

To check that the tests can fail, I broke the code on purpose:

- **Status guard removed** from the update: 9 tests failed.
- **Status check rewritten as a naive "read the status, then write"** with a 20 ms gap: only the concurrency tests caught it, which is their job. Run on their own, the dispatch and receive tests failed in 3 out of 3 runs (Warehouse rice 400 instead of 490, Kandy 105 instead of 15). In one full-suite run, only the dispatch test caught it. A race test can only catch a race when the timing lines up, so these tests are evidence for the locking, not proof.
- **Receive adds the sent quantity instead of the received one:** 2 of the 4 `ReceiveTests` failed, the two that receive less than was sent.

**Frontend:** there are no automated UI tests. In `frontend`, `npm run build` type-checks and builds, and `npm run lint` runs oxlint. The screen was checked in headless Chrome with throwaway Playwright scripts that are not in the repo (see `AI_USAGE.md`).

## Assumptions

- **The warehouse is a location like any branch.** It can send and receive transfers. The brief lists it alongside the branches but gives it no special rules.
- **The product list is fixed.** The brief asks only for seeded products, so there is no endpoint to add products or branches.
- **Quantities are whole units** (integers).
- **A product appears at most once per transfer.** Two lines for the same product are rejected rather than merged, so the request matches what the user typed.
- **Every branch has a stock row for every product**, with zero where there is none. Receiving into a branch that has never held a product is then a plain increment.
- **Creating a request does not check stock.** Stock only moves on dispatch, and the sender's stock can change before then, so the stock check is made at dispatch. The UI shows the available stock next to each line while building the request, and warns (but still allows it) when a quantity is above it.
- **Repeating a step that already happened succeeds and changes nothing.** A second dispatch (or a dispatch of a transfer that has since been received), a second receive, or a second cancel returns 200 with the transfer as it is. Double-clicks and retries therefore look like success to the user, and stock moves only once. A step from the wrong status, such as receiving a Requested transfer or cancelling a Dispatched one, returns 409 with the reason.
- **The receiver can record that less arrived than was sent** (the optional stretch goal). `POST /transfers/{id}/receive` takes an optional body, `{ "lines": [{ "productId": 1, "receivedQuantity": 8 }] }`. Each received quantity must be from 0 to the quantity sent. Lines left out, or no body at all, are received in full. Only what arrived is added to the receiver. The sender already lost the full quantity at dispatch, so the shortfall stays on record as `quantity - receivedQuantity` on each line, and the screen shows it (for example "received 8, 2 short"). The brief does not say what happens to the missing stock afterwards, so it is only recorded.
- **A dispatch is all or nothing.** If any line is short, nothing is dispatched, and the error lists every short line with what is needed and what is available.
- **Creating a transfer is made idempotent by an optional `Idempotency-Key` header.** A repeat with the same key returns the original transfer (200) and does not create another. The body of the repeat is not compared with the original.
- **`GET /transfers?branchId=` matches transfers sent from or to that branch.**
- **The UI does not rely on disabling buttons for safety.** It disables an action while its request is in flight, which stops most double-clicks, but the API's guarded updates are what make a repeat move stock only once.
- **Errors are ProblemDetails** (`application/problem+json`). `detail` is a sentence the UI can show as-is, and `messages` lists each problem separately.

## Trade-offs

- **Conditional updates rather than an EF row version plus retries.** Each status change is a single `UPDATE "Transfers" SET "Status" = new WHERE "Id" = @id AND "Status" = expected`. Each stock change is `UPDATE "Stock" SET "Quantity" = "Quantity" - @q WHERE ... AND "Quantity" >= @q`. Both run in one transaction. The expected status acts as the concurrency token. PostgreSQL locks the row, so a concurrent second dispatch waits, re-checks the condition after the first commits, matches nothing and moves nothing. The guarded decrement cannot take stock below zero even when two transfers draw on the same branch at once, and the CHECK constraint backs it up. This avoids read-modify-write and retry loops. The cost is that these statements bypass EF change tracking (`ExecuteUpdate`). Stock rows are updated in product order, so two dispatches from the same branch cannot deadlock.
- **Business rules live in one service (`TransferService`)**, not in a richer domain model. At this size a single class that reads top to bottom is easier to review.
- **Stock is checked at dispatch, not reserved at request time.** Creating a request is cheap and never blocks, but two requests can be made against the same stock. Only one of them can then be dispatched, and the other is refused with a clear message.
- **Stock is a current quantity per branch and product, with no movement ledger.** The transfers themselves are the history of what moved, which is enough for this feature. A ledger table would be the next step if stock could change in other ways (sales, stock counts).
- **A shortfall on receive is only recorded.** It is not written off or sent back anywhere, because the brief does not say what should happen to it.
- **Integration tests over unit tests.** The rules that matter most (atomic status changes, no negative stock, concurrency) live in SQL and transactions, so the tests run the real API against real PostgreSQL rather than mocking the database. They take about 4 seconds once the container image is pulled.
- **A plain React screen.** It uses component state and a reload after every action, with no state or data-fetching library and no optimistic updates. The list always shows what the server holds, at the cost of an extra request per action.

## What I would do with another day

- **Automated UI tests:** component tests for the form and the list, and a committed Playwright end-to-end test of the main flow (the checks I ran by hand-written script, kept in CI).
- **Who did what:** record the user on each step once there is a login, and show it in the list.
- **Reserving stock at request time** (or at least warning about stock already promised to other Requested transfers).
- **A stock movement ledger,** so every change to a branch's stock can be traced, and a reason per shortfall on receive (damaged, missing, wrong item) with a way to follow up.
- **Paging and sorting** for the transfer list.
- **Docker Compose services for the API and the UI,** so the whole app starts with one command, and a CI pipeline that runs the tests, build and lint.

## Time spent

| Session | Start | End | Time | Notes |
|---|---|---|---|---|
| 1 | 2026-10-21 20:38 | 22:18 | 1h 40m | Read the brief, planned, scaffold, domain model, API and business rules, integration tests |
| 2 | 2026-10-21 22:18 | 22:49 | 31m | Rebuilt the plan as `PLAN.md`, React screen, receiving a smaller quantity, docs and final run-through |

Total: about **2h 11m**, inside the 3-hour budget.
