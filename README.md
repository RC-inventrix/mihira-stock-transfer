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
- _More assumptions about the transfer rules will be added in Phase 2._

## Trade-offs

_TODO_

## What I would do with another day

_TODO_

## Time spent

| Session | Start | End | Notes |
|---|---|---|---|
| 1 | 2026-10-21 20:38 | | Read the brief, planned, Phase 0 setup |

Total: _TODO_
