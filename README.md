# Mihira Stores – Branch Stock Transfer

A small system for moving stock between the warehouse and the Colombo, Kandy and Galle branches. It has an ASP.NET Core Web API (.NET 10, EF Core, PostgreSQL) and a single React + TypeScript screen.

> Work in progress. The sections below are filled in as each phase is finished.

## How to run

Prerequisites: .NET 10 SDK, Node.js 20+, and Docker (used for the database and for the tests).

_TODO: run steps once the API and UI are in place._

## Tests

`dotnet test` from the repository root (Docker must be running). The tests host the real API in memory with `WebApplicationFactory` and run against a throwaway PostgreSQL 17 container (Testcontainers). The concurrency rules rely on PostgreSQL row locks, which an in-memory database would not show. Each test starts from the seeded stock.

- **`TransferWorkflowTests`:** creating moves no stock. Dispatch takes stock from the sender only, and receive adds it to the receiver. An over-stock dispatch returns 409, lists each short line, and rolls back the lines it had already taken. Dispatching or receiving twice moves stock once. Every wrong-status action (for example, receive before dispatch, cancel after dispatch, dispatch after cancel) returns 409 and moves nothing.
- **`ConcurrencyTests`:** 10 simultaneous dispatches, or receives, of one transfer move stock once. Three transfers competing for the same 3 units: exactly one is dispatched and stock ends at 1, never below zero. 10 simultaneous creates with one idempotency key make one transfer.
- **`CreateTransferTests`:** each validation rule gives its own message and creates nothing. All problems are reported together. A repeated idempotency key returns the original transfer.

To check that the tests can fail, I broke the code on purpose twice:

- **Status guard removed** from the update: 9 tests failed.
- **Status check rewritten as a naive "read the status, then write"** with a 20 ms gap: only the concurrency tests caught it, which is their job. Run on their own, the dispatch and receive tests failed in 3 out of 3 runs (Warehouse rice 400 instead of 490, Kandy 105 instead of 15). In one full-suite run, only the dispatch test caught it. A race test can only catch a race when the timing lines up, so these tests are evidence for the locking, not proof.

## Assumptions

- **The warehouse is a location like any branch.** It can send and receive transfers. The brief lists it alongside the branches but gives it no special rules.
- **The product list is fixed.** The brief asks only for seeded products, so there is no endpoint to add products or branches.
- **Quantities are whole units** (integers).
- **A product appears at most once per transfer.** Two lines for the same product are rejected rather than merged, so the request matches what the user typed.
- **Every branch has a stock row for every product**, with zero where there is none. Receiving into a branch that has never held a product is then a plain increment.
- **Creating a request does not check stock.** Stock only moves on dispatch, and the sender's stock can change before then, so the stock check is made at dispatch. The UI shows the available stock next to each line while building the request, and warns (but still allows it) when a quantity is above it.
- **Repeating a step that already happened succeeds and changes nothing.** A second dispatch (or a dispatch of a transfer that has since been received), a second receive, or a second cancel returns 200 with the transfer as it is. Double-clicks and retries therefore look like success to the user, and stock moves only once. A step from the wrong status, such as receiving a Requested transfer or cancelling a Dispatched one, returns 409 with the reason.
- **A dispatch is all or nothing.** If any line is short, nothing is dispatched, and the error lists every short line with what is needed and what is available.
- **Creating a transfer is made idempotent by an optional `Idempotency-Key` header.** A repeat with the same key returns the original transfer (200) and does not create another. The body of the repeat is not compared with the original.
- **`GET /transfers?branchId=` matches transfers sent from or to that branch.**
- **The UI does not rely on disabling buttons for safety.** It disables an action while its request is in flight, which stops most double-clicks, but the API's guarded updates are what make a repeat move stock only once.
- **Errors are ProblemDetails** (`application/problem+json`). `detail` is a sentence the UI can show as-is, and `messages` lists each problem separately.

## Trade-offs

- **Conditional updates rather than an EF row version plus retries.** Each status change is a single `UPDATE "Transfers" SET "Status" = new WHERE "Id" = @id AND "Status" = expected`. Each stock change is `UPDATE "Stock" SET "Quantity" = "Quantity" - @q WHERE ... AND "Quantity" >= @q`. Both run in one transaction. The expected status acts as the concurrency token. PostgreSQL locks the row, so a concurrent second dispatch waits, re-checks the condition after the first commits, matches nothing and moves nothing. The guarded decrement cannot take stock below zero even when two transfers draw on the same branch at once, and the CHECK constraint backs it up. This avoids read-modify-write and retry loops. The cost is that these statements bypass EF change tracking (`ExecuteUpdate`). Stock rows are updated in product order, so two dispatches from the same branch cannot deadlock.
- **Business rules live in one service (`TransferService`)**, not in a richer domain model. At this size a single class that reads top to bottom is easier to review.

## What I would do with another day

_TODO_

## Time spent

| Session | Start | End | Notes |
|---|---|---|---|
| 1 | 2026-10-21 20:38 | | Read the brief, planned, Phase 0 setup |

Total: _TODO_
