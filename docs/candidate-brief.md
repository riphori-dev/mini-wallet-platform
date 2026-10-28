# Your Take-Home Challenge: Mini Wallet Platform

We use a take-home rather than a whiteboard quiz for one reason: we want to see how you think and build when you have room to make real decisions. Nothing here has a single right answer. We're interested in the choices you make and how you explain them.

- **Time:** plan for about **6–8 hours** of focused work. Please don't go far beyond that; we'd rather see a smaller solution that's well reasoned.  
- **Deadline:** please send your submission a day before your scheduled interview.  
- **Questions:** if something is unclear, ask your recruiter. Asking good questions counts in your favour. If you'd rather not wait for a reply, make a sensible assumption and write it down.

---

## The scenario

You're building the backend for a small online gaming platform. It's made up of two services, and you'll build both:

- **Users service:** player sign-up and email verification.  
- **Wallet service:** player balances and the game transactions that change them.

---

## Tech stack

### Required

| Area | Technology |
| :---- | :---- |
| Language and runtime | **C\# on .NET 8** (or later) |
| Web framework | **ASP.NET Core** (Web API: controllers or minimal APIs, your choice) |
| API documentation | **OpenAPI / Swagger UI**, so the API can be explored interactively |
| Authentication | **JWT bearer tokens** issued by an OAuth 2.0 / OpenID Connect provider. Run one locally (for example Keycloak in a container), or use a dev token issuer you document. |
| Testing | **xUnit**, NUnit or MSTest |
| Running locally | **Docker and Docker Compose**: the whole system starts with one command |
| Source control | **Git**, with a readable commit history |

Everything else is up to you: the database, any infrastructure, the NuGet packages and how the code is structured.

---

## What you'll build

1. **Registration.** Players register through the Users service and then verify their email address.

2. **Wallets for new players.** Once a player has registered, they must have a wallet in each supported currency (**USD, EUR, BTC**), starting at a balance of zero. The player should not have to make any extra call to get them. How the Wallet service finds out about a new player is up to you.

3. **Game transactions.** The Wallet service exposes **one transaction endpoint**, which external game providers call to record activity against a player's wallet. It must accept at least these transaction types:

   - **BET** debits the stake from the player's wallet. A bet must never take a balance below zero.  
   - **WIN** credits a payout to the player's wallet for a bet they placed earlier.

   More transaction types are on the roadmap (refunds, bonuses, jackpots and so on), so design for adding them.

4. **Balances and history.** Players can view their balances and their transaction history.

5. **Access rules.** Players can see only their own wallet. Admins can see any player's wallet.

6. **Provider access.** Game providers are systems, not people. They authenticate separately from players and don't use a player login.

---

## Designing the Wallet API

The Wallet API contract is yours to design. That covers how resources are modelled, request and response shapes, status codes, the error format, versioning and pagination.

Design it as if you were about to hand it to external game providers who will integrate against it. It must be **documented and explorable interactively**, and the documentation should explain how to authenticate.

---

## Constraints to keep in mind

- **Several instances.** The Wallet service runs as several instances behind a load balancer. Balances must stay correct when requests for the same player arrive at the same time.  
- **Outages.** Either service can be temporarily unavailable. When that happens, no player registration or wallet operation may be lost or duplicated.  
- **One command to run it.** We should be able to start the whole system with a single command, for example `docker compose up`.

---

## If you have time (optional)

- **Live balance updates.** Push balance changes in real time to a connected player using SignalR.  
- **Anything else you'd want before going to production.** Tell us what you added and why.

None of this is required. A focused, solid core is worth more than a long list of extras.

---

## Use AI. We want you to.

We work with AI coding tools every day, and we want to see how you work with them. **Please build this with an AI coding assistant**, such as Claude Code, GitHub Copilot or Cursor.

Include an **`AI_USAGE.md`** that covers:

- **Tools:** which ones you used, and what you used each one for.  
- **Prompts:** 3–5 representative prompts or interactions.  
- **Overrides:** where the AI got something wrong or you overruled it, and why.  
- **Trade-offs:** the trade-offs you knowingly accepted.  
- **Verification:** how you checked that what the AI produced was correct.

You're welcome to include raw session logs or transcripts as well. We're not looking for code without AI help. We're looking for good judgement in how you used it.

---

## What to send us

A link to a git repository (or a zip) containing:

1. **The source code**, with commit history if possible.  
2. **A `README.md`** explaining how to run the system and how to try it out, including sample credentials or tokens.  
3. **Tests.** You decide what to test. Tell us why you chose to test those things.  
4. **A `DESIGN.md`**, at most 2 pages, covering:  
   - your architecture and the reasons behind it;  
   - the failure scenarios you considered, and how your solution handles each one;  
   - what you left out or simplified, and what you'd change for production.  
5. **`AI_USAGE.md`**, as described above.

---

## How we'll look at it

We'll review:

- **Correctness:** it does what's described here.  
- **Design:** clear design, and clear reasoning behind it.  
- **The Wallet API:** quality of the contract you designed.  
- **AI:** how you worked with AI tools.

After that, we'll set up a **follow-up conversation of about 45 minutes**. You'll walk us through your solution, and then we'll extend it together.

---

Put correctness and clarity ahead of the number of features. If you cut scope, tell us what you cut and why. We'd much rather read a good explanation than find a half-finished feature.

Good luck\! We're looking forward to seeing what you build\!