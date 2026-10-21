# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Layout and commands

`ASSESSMENT_BRIEF.md` is the source of truth for requirements.

- `StockTransfer.slnx` (root) contains `backend/StockTransfer.Api` (.NET 10 Web API, controllers, EF Core + Npgsql) and `backend/StockTransfer.Tests` (xUnit, `WebApplicationFactory`, Testcontainers PostgreSQL).
- `frontend/` is a Vite React + TypeScript app. Vite proxies `/api/*` to the API at `http://localhost:5131`, with the `/api` prefix stripped, so the API routes have no prefix.
- `docker-compose.yml` runs PostgreSQL 17 on **host port 5433** (`ConnectionStrings:Default` in `appsettings.json`).

```
docker compose up -d --wait                       # database
dotnet run --project backend/StockTransfer.Api    # API on http://localhost:5131
cd frontend && npm run dev                        # UI
dotnet test                                       # all tests (Docker must be running)
dotnet test --filter "FullyQualifiedName~<TestName>"   # single test
cd frontend && npm run build && npm run lint      # type-check, build, oxlint
```

## Working conventions

- Commit after each phase in small, meaningful commits. Reviewers read the history.
- Keep the working log in `AI_USAGE.md` up to date as the work happens. Never invent entries. The "What I wrote or decided myself" section belongs to the candidate.
- Record each assumption in `README.md` when it is made.

## What this repo is for

A take-home assessment for Ravigno: a branch stock-transfer feature for the fictional retailer "Mihira Stores" (one Warehouse plus Colombo, Kandy and Galle branches).

- **Back end:** ASP.NET Core Web API on .NET 8+, EF Core, SQL Server or PostgreSQL.
- **Front end:** one React screen (TypeScript preferred).
- **Tests:** at least three meaningful automated tests of the business rules, e.g. over-stock dispatch, double dispatch, receive.
- **Seed data, created on app start:** the warehouse and three branches, five products, and starting stock.
- **Out of scope:** login, roles, styling, deployment. Docker is optional.

## Domain rules (these are what the work is graded on)

The transfer state machine is strict: **Requested → Dispatched → Received**, or **Requested → Cancelled**. No other transitions are allowed.

- Stock leaves the sender on **dispatch** and arrives at the receiver on **receive**. Creating a request moves no stock.
- Stock at a branch must never go below zero. A dispatch that would cause this is rejected with a clear message.
- **Idempotency:** dispatching or receiving the same transfer twice (double-click, network retry) must not move stock twice. Concurrent requests count too, so the status check and the stock change must happen atomically: one transaction plus a concurrency token or row version on the transfer and stock rows, not just a read-then-write status check.
- Actions are allowed only from the correct status. For example, you cannot receive a transfer that was never dispatched, and you can cancel only while it is Requested.
- From and to branches must differ. Every line quantity must be greater than zero. A transfer has one or more lines.
- Optional stretch goal: the receiver records a smaller received quantity (e.g. damaged goods) and the difference is kept on record.

Suggested API (the design may change): `GET /branches/{id}/stock`, `POST /transfers`, `POST /transfers/{id}/dispatch`, `POST /transfers/{id}/receive`, `POST /transfers/{id}/cancel`, `GET /transfers?branchId=&status=`.

The UI must let a user pick from and to branches, add products and quantities while seeing available stock, and submit. It must also list transfers with their status and offer dispatch, receive and cancel actions. Errors from the API must be shown clearly.

## Submission constraints

- Required deliverables: `README.md` (run steps, assumptions, trade-offs, what to do with another day, time spent) and `AI_USAGE.md` (tools used, one place the AI was wrong and how it was caught, which parts the candidate wrote or decided).
- Reviewers read the commit history, so commit in small, meaningful steps.
- About 3 hours of work in total. Favour a small, correct and clearly explained solution over breadth.
- Deadline: Thursday 22 October 2026, 2:00 pm Sri Lanka time.
- Record ambiguous decisions as assumptions in the README. The assessors will not answer questions.
