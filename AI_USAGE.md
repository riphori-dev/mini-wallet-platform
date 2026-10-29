# AI Usage

I used Codex as a coding assistant while building this assessment, and ChatGPT to discuss the candidate brief and prepare the initial implementation prompts. I set the requirements and technical direction, divided the work into stages, and used AI to help with implementation, tests, troubleshooting, and documentation.

I did not accept every suggestion. My priority was to meet the brief with a solution I could understand and explain. The initial plan included more infrastructure than I thought the assessment needed, so I reduced the scope before continuing. I also asked questions about the generated design and used Postman to follow the registration, transaction, and real-time flows myself.

## How I directed the work

These are five representative interactions, summarized rather than quoted verbatim:

1. **Review the brief before writing application code.** I asked for an implementation plan first so I could understand the two-service requirement, API contract, failure scenarios, and assumptions. I changed the initial runtime target from .NET 10 to .NET 8 and kept the existing Wallet API, adding Users alongside it.

2. **Keep registration recovery simple.** I asked for duplicate registration and lost-response recovery without a general replay framework. I chose unique normalized emails, durable registration notifications, and recovery through login or verification resend. I also kept one Users background worker rather than introducing distributed worker leases.

3. **Prove that competing bets cannot overspend.** I requested two simultaneous bets of 80 against a balance of 100, with one acceptance, one rejection, and 20 remaining. Testing covered two Wallet hosts sharing PostgreSQL. Later, I asked for a small Nginx demonstration so the same behavior could be checked through a load balancer while keeping one-command Compose startup.

4. **Add live balances without expanding the infrastructure.** I asked whether SignalR would fit, then chose a single-instance implementation. While testing in Postman, I worked through the handshake, token expiration, and the difference between a new bet and an idempotent replay. I received a balance-change event after a new bet. I accepted that cross-instance SignalR delivery would remain unsupported and documented a direct Wallet endpoint for that demo.

5. **Add a practical audit trail.** I asked for logging without another service or a large framework. The result records HTTP actions in each service's existing database, with trace IDs and separate records for new transactions and replays. I accepted best-effort request auditing so an audit failure would not change an already committed financial result. The financial ledger remains the authoritative record of money movements.

## Where corrections were needed

Generated code and explanations needed follow-up. Builds and tests exposed package compatibility issues, incorrect .NET 8 API usage, and missing imports during a folder refactor. Other corrections included:

- A rollback test did not trigger the intended failure. It was replaced with a PostgreSQL trigger in the disposable test database so the rollback assertion actually ran.
- Temporary database failures initially returned 500 instead of retryable 503. Both services were corrected, with regression coverage.
- External transaction ID validation needed stricter regex anchors to reject a trailing newline.
- PostgreSQL readiness needed a TCP check rather than only a local socket check.
- XML comments and Swagger response metadata needed fixes after compiler warnings and an OpenAPI assertion exposed problems.
- Some test and script callers still used the old registration route after it moved to `/api/v1/register`.

I also questioned whether adding a load balancer was overengineering. Codex initially overstated the absence of a local load-balanced deployment as a missing implementation requirement, then corrected that distinction: the financial concurrency behavior was already implemented and tested. I chose to add a minimal deployment demonstration anyway because it made the constraint easier to show and explain.

These were resolved through targeted changes and reruns. I treated failed checks as reasons to investigate, rather than assuming generated code was finished.

## Trade-offs I accepted

I kept one PostgreSQL server with separate service databases, HTTP delivery with a durable outbox instead of a broker, and a documented development token issuer. Verification emails use SMTP with local Mailpit capture; external internet delivery was not tested. Failed verification email delivery is recovered through authenticated resend rather than an automatic email queue.

The financial API allows one successful WIN per BET, uses bounded page-based pagination, and has explicit demo funding. Newly registered wallets still start at zero. Refunds, bonuses, and jackpots remain future transaction types; adding them would require validation, domain rules, database changes, and tests, not just another handler.

The final local setup includes two Wallet instances behind Nginx. SignalR remains process-local, so its demo uses port 5082 for both the WebSocket and transaction requests. Request audit records persist in PostgreSQL, but they are not atomic with business operations or tamper-proof. Production identity, cross-instance SignalR, audit archival, and additional operational infrastructure remain outside this implementation.

## How I checked the result

Codex helped run builds, tests, and isolated Docker checks. The latest full automated run passed **92 tests with no failures or skips**, covering domain rules, PostgreSQL constraints, concurrent transactions, duplicate requests, authorization, outage recovery, SMTP, SignalR, and auditing.

The isolated Compose walkthrough also passed registration, email verification, automatic zero-wallet provisioning, BET/WIN processing, replay, and access checks. Load-balancer verification showed competing requests reaching different Wallet instances without overspending, and exact replay through the other instance without a second debit. Account, balance, history, and audit persistence were checked across service restarts.

I used Postman myself to observe requests reaching both Wallet instances. I checked that a balance of 111 became 110 after a bet and stayed at 110 when I repeated the identical request. I also received the SignalR `BalanceChanged` notification. Those manual checks helped me understand the behavior; they do not replace the automated concurrency tests.

An earlier version of `DESIGN.md` was rendered and checked against the two-page limit. That pagination check has not yet been repeated after the latest edits. Detailed commands, results, corrections, and remaining verification limits are recorded in `docs/PROGRESS.md`.

AI helped me move faster, but I remained responsible for choosing the scope, setting acceptance criteria, questioning the design, and deciding what needed further verification. The checks above support the implemented scenarios; they are not exhaustive production testing.
