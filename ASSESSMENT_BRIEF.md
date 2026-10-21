# Take-home Assessment – Software Engineer

*Ravigno (Pvt) Ltd · Candidate brief*

## The task in brief

Build a small branch stock-transfer feature: an ASP.NET Core Web API with a React screen. You have a 48-hour window to complete it (our email gives you the exact start and end time), and you should spend no more than 3 hours on it. A smaller piece of work done well and explained honestly beats a big one rushed.

- **AI is allowed.** Use any tools you like (ChatGPT, Claude, Copilot, Cursor and so on). Tell us how you used them in `AI_USAGE.md`. Using AI never counts against you; hiding it does.
- **Assumptions are part of the task.** If something is unclear, make a sensible choice and write it down in your README. We will not answer questions about the task during the window.
- **Your work stays yours.** Ravigno uses it only to assess you and will not use it in any product.
- **What happens next.** We review every submission. If yours moves forward, we will contact you about the next step.

## The scenario

Mihira Stores (a fictional retail chain) has a warehouse and three branches: Colombo, Kandy and Galle. Branches often run short of an item while another branch has too much, so they move stock between each other.

Today this happens by phone and a notebook, and the stock figures never match. They want a simple system where:

1. A branch requests a transfer: from branch, to branch, and one or more products with quantities.
2. The sending branch dispatches it. Stock leaves the sender at this point.
3. The receiving branch receives it. Stock arrives at the receiver at this point.
4. A transfer that has not been dispatched can be cancelled.

Their biggest worry: stock going negative, and the same transfer being counted twice when someone double-clicks or the network retries.

## What to build

A transfer moves through these statuses: **Requested → Dispatched → Received**, or **Requested → Cancelled**. Nothing else is allowed.

### Back end (required)

ASP.NET Core Web API on .NET 8 or later, with EF Core and either SQL Server or PostgreSQL. Suggested endpoints (you may design your own):

| Endpoint | What it does |
|---|---|
| `GET /branches/{id}/stock` | Stock level of every product at one branch |
| `POST /transfers` | Create a transfer request with one or more lines |
| `POST /transfers/{id}/dispatch` | Take the stock out of the sending branch |
| `POST /transfers/{id}/receive` | Add the stock to the receiving branch |
| `POST /transfers/{id}/cancel` | Cancel a transfer that is still Requested |
| `GET /transfers?branchId=&status=` | List transfers, filtered |

### Business rules (required)

- Stock at a branch can never go below zero. A dispatch that would do this is rejected with a clear message.
- Dispatching or receiving the same transfer twice must not move stock twice.
- Actions are only allowed from the right status (for example, it should not be possible to receive a transfer that was never dispatched).
- From and to branches must be different, and quantities must be more than zero.

### Front end (required)

One React screen (TypeScript preferred; Angular is also fine) where a user can:

- pick the from and to branches, add products and quantities, see the stock available, and submit a request;
- see a list of transfers with their status, and dispatch, receive or cancel them.

It does not need to look polished. It needs to work and show errors clearly.

### Tests (required)

At least three meaningful automated tests of the business rules, for example an over-stock dispatch, a double dispatch, and a receive.

### Seed data (required)

The warehouse and three branches, five products, and some starting stock, created when the app starts.

### Not needed

Login, user roles, styling, deployment. Docker is optional.

### If you have time left (optional)

Let the receiving branch record a smaller quantity than was sent (for example, damaged goods) and keep a record of the difference.

## What to submit

Reply to our email before the window closes with a link to a GitHub repository (public, or private with access for the account we name in the email) or a zip file. Commit as you go; we look at the history. Please include:

- **README.md:** how to run it in a few steps, the assumptions you made, the trade-offs, and what you would do with another day.
- **AI_USAGE.md:** which tools you used and for what; one place where the AI was wrong or unhelpful and how you noticed; and which parts you wrote or decided yourself.
- **Time spent:** roughly how long you worked on it.

## How we review it

- It runs by following your README.
- The stock rules hold, including the double-dispatch case.
- The code is clear and sensibly structured for its size.
- The tests check things that matter.
- Your README and AI notes are clear and honest.

---

## Submission details (from the invitation email)

- **Deadline:** Thursday, 22 October 2026, 2.00 pm (Sri Lanka time)
- **How:** reply to the email with a link to a public GitHub repository or a zip file
- **Time limit:** no more than 3 hours of work
