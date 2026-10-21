# Mihira Stores – Branch Stock Transfer

A small system for moving stock between the warehouse and the Colombo, Kandy and Galle branches. It has an ASP.NET Core Web API (.NET 10, EF Core, PostgreSQL) and a single React + TypeScript screen.

> Work in progress. The sections below are filled in as each phase is finished.

## How to run

Prerequisites: .NET 10 SDK, Node.js 20+, and Docker (used for the database and for the tests).

_TODO: run steps once the API and UI are in place._

## Assumptions

- **The warehouse is a location like any branch.** It can send and receive transfers. The brief lists it alongside the branches but gives it no special rules.
- **The product list is fixed.** The brief asks only for seeded products, so there is no endpoint to add products or branches.
- **Quantities are whole units** (integers).
- **A product appears at most once per transfer.** Two lines for the same product are rejected rather than merged, so the request matches what the user typed.
- **Every branch has a stock row for every product**, with zero where there is none. Receiving into a branch that has never held a product is then a plain increment.
- **Creating a request does not check stock.** Stock only moves on dispatch, and the sender's stock can change before then, so the stock check is made at dispatch. The UI shows available stock while building the request.
- **Repeating a step that already happened succeeds and changes nothing.** A second dispatch (or a dispatch of a transfer that has since been received), a second receive, or a second cancel returns 200 with the transfer as it is. Double-clicks and retries therefore look like success to the user, and stock moves only once. A step from the wrong status, such as receiving a Requested transfer or cancelling a Dispatched one, returns 409 with the reason.
- **A dispatch is all or nothing.** If any line is short, nothing is dispatched, and the error lists every short line with what is needed and what is available.
- **Creating a transfer is made idempotent by an optional `Idempotency-Key` header.** A repeat with the same key returns the original transfer (200) and does not create another. The body of the repeat is not compared with the original.
- **`GET /transfers?branchId=` matches transfers sent from or to that branch.**
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
