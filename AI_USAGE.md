# AI Usage

## How I used AI

I used Codex as a coding assistant while building this assessment. I defined the requirements and technical preferences, broke the work into stages, and used Codex to help with implementation, tests, troubleshooting, and documentation.

I also used ChatGPT to discuss the candidate brief and prepare the initial implementation prompts. That discussion helped me clarify the two-service requirement and reduce an initially overcomplicated plan.

My main direction was to keep the solution focused on the assessment. Some initial suggestions introduced more infrastructure than the timebox justified, so I adjusted the scope before continuing.

## Representative prompts and decisions

1. **Plan before implementing.**  
   I asked for a review of the brief and an implementation plan before application code was written. This helped identify the API contract, failure scenarios, and assumptions early.

2. **Follow the updated runtime choice.**  
   I changed the target from .NET 10 to .NET 8. The existing Wallet API project was preserved, with the Users service added alongside it.

3. **Keep registration recovery simple.**  
   I asked for duplicate registration handling and recovery after a lost response without introducing a general registration replay framework. The resulting approach uses unique normalized emails, durable registration records, and login or verification-resend recovery.

4. **Avoid unnecessary worker infrastructure.**  
   I chose one Users background worker rather than distributed worker leases. Pending notifications are persisted, while the Wallet service handles repeated delivery safely.

5. **Verify concurrent financial operations.**  
   I requested a test where two simultaneous bets of 80 target a balance of 100. The expected outcome is one accepted bet, one rejection, and a remaining balance of 20. Verification included two Wallet hosts sharing PostgreSQL.

## Corrections during development

Codex-generated code and tooling needed several corrections:

- Package compatibility and some .NET 8 API usage initially caused warnings or compilation failures.
- An attempted rollback test did not trigger the intended failure. It was replaced with a PostgreSQL trigger in the disposable test database so the rollback assertion could be exercised.
- Transient database failures initially returned HTTP 500 instead of the intended retryable 503. This was corrected in both services, with a regression test for registration.
- External transaction ID validation needed stricter regex anchors to reject a trailing newline.
- Final startup checks identified a PostgreSQL readiness check that needed to use TCP rather than only a local socket.

These issues were addressed through failed checks, targeted fixes, and reruns. More detailed results are recorded in `docs/PROGRESS.md`.

## Trade-offs

I chose a smaller implementation that addresses the required behavior:

- One PostgreSQL server with separate databases for each service.
- HTTP delivery with a durable outbox instead of a message broker.
- A documented development token issuer and mailbox instead of external providers.
- One successful WIN settlement per BET.
- Bounded page-based pagination.
- Explicit demo funding, while newly registered wallets start at zero.

SignalR and additional production infrastructure were left out so the work could stay focused on correctness and the assessment requirements.

## Verification

Codex assisted with running builds, tests, and local integration checks. The final automated run passed **74 tests**, including domain rules, PostgreSQL constraints, concurrent transactions, duplicate requests, authorization, and outage recovery.

The final isolated Docker Compose startup and reviewer walkthrough also passed. Ordinary service restart preserved account, wallet, and transaction data. `DESIGN.md` was rendered and checked against the two-page limit.

These checks provide evidence for the implemented scenarios; they do not represent exhaustive production testing. Detailed commands, results, and limitations are recorded in `docs/PROGRESS.md`.


## XML documentation follow-up

The user asked Codex to document the existing domain and API contracts accurately and connect XML comments to Swagger without changing behavior. Codex inspected domain rules, validators, persistence, authorization and installed Swashbuckle 10.3.1, then added comments and documentation metadata. Initial comments below method attributes caused CS1587 warnings; moving them above attributes fixed this. A new OpenAPI assertion caught a missing inferred history schema, fixed with explicit response metadata. Final verification built all projects/the solution, published both APIs with their XML files, passed 35 focused tests and inspected both Swagger UIs in a browser. This follow-up did not rerun the previously recorded full PostgreSQL suite; detailed commands and limitations are in docs/PROGRESS.md.
