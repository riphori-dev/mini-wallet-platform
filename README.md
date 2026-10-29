# MiniWallet

Two .NET 8 APIs: Users handles registration/email verification; Wallet handles USD/EUR/BTC balances and provider BET/WIN transactions.

## Run

Install Docker with Compose, then from this repository:

```powershell
docker compose up --build
```

This starts PostgreSQL, Mailpit, service-owned migration jobs, Users, two Wallet instances and Nginx. Data persists in named volumes.

| Endpoint | URL |
| --- | --- |
| Load-balanced Wallet Swagger | http://localhost:5080/swagger |
| Users Swagger | http://localhost:5081/swagger |
| Single-instance Wallet / SignalR demo | http://localhost:5082/swagger |
| Captured verification emails | http://localhost:8025 |

## Try it

Use `POST http://localhost:5081/dev/token` with JSON email/password to obtain a player/admin token. Use it as Bearer authorization for wallet reads. The seeded demo player is `demo@miniwallet.test` / `Local-demo-password-123!`, ID `de000000-0000-4000-8000-000000000001`. The admin is `admin@miniwallet.test` / `Local-admin-password-123!`.

Providers use `POST /dev/connect/token` on Users with JSON `clientId: local-provider`, `clientSecret: local-provider-secret-123!`, `scope: transactions:write`. Use the provider token for `POST http://localhost:5080/api/v1/transactions`. Swagger and [API_CONTRACT.md](docs/API_CONTRACT.md) contain request shapes and examples. These credentials and the token issuer are for local Development only.

New registrations start with zero USD/EUR/BTC wallets automatically. Only the seeded demo account has opening funds (USD/EUR 100, BTC 0.01). Existing activity persists; restart does not replenish funds. Verification messages are captured in Mailpit.

See [LOAD_BALANCER.md](docs/LOAD_BALANCER.md) for routing headers, a concurrent-bet demonstration and separate application updates. See [REALTIME.md](docs/REALTIME.md) for the optional single-instance SignalR feature; use port **5082** for both the socket and transaction requests when testing it.

## Verification

```powershell
docker compose -f compose.yaml -f compose.tests.yaml run --build --rm tests
```

Tests focus on financial atomicity, concurrency across hosts, duplicate requests, ownership, database constraints, registration recovery, SMTP and SignalR isolation. The test fixtures create disposable databases. [PROGRESS.md](docs/PROGRESS.md) records actual runs and limitations; [DESIGN.md](DESIGN.md) explains the design and [AI_USAGE.md](AI_USAGE.md) records AI assistance.

## Earlier development narrative

I used Codex as a coding assistant while building this assessment, and ChatGPT to discuss the candidate brief and prepare the initial implementation prompts. I set the requirements and technical direction, divided the work into stages, and used AI to help with implementation, tests, troubleshooting, and documentation.

The AI suggestions were useful, but I had to make decisions about what belonged in the solution. My priority was to meet the assessment requirements within the timebox. The initial plan included more infrastructure than I thought was justified, so I reduced the scope before moving forward. Discussing the brief also helped me clarify the two-service requirement.

Some of the decisions I made during development were:
- Plan before coding. I asked for a review of the brief and an implementation plan first, so I could assess the API contract, failure scenarios, and assumptions before implementation.
- Use .NET 8. I changed the initial .NET 10 target to .NET 8 and kept the existing Wallet API project, adding the Users service alongside it.
- Keep registration recovery practical. I wanted duplicate registration and lost-response scenarios handled without building a general replay framework. The approach uses unique normalized emails, durable registration records, and recovery through login or verification resend.
- Limit worker infrastructure. I chose one Users background worker instead of distributed worker leases. Notifications are persisted, and the Wallet service handles repeated delivery safely.
- Test financial concurrency explicitly. I requested a scenario where two simultaneous bets of 80 compete against a balance of 100. The required result was one accepted bet, one rejection, and a remaining balance of 20. Verification included two Wallet hosts sharing PostgreSQL.
The generated code was not correct on the first attempt. Builds and tests exposed several issues that needed follow-up:
- Package compatibility and some .NET 8 API usage caused warnings or compilation failures.
- The first rollback test did not exercise the intended failure. It was replaced with a PostgreSQL trigger in the disposable test database.
- Transient database failures returned HTTP 500 instead of the intended retryable 503. Both services were corrected, and a registration regression test was added.
- External transaction ID validation needed stricter regex anchors to reject a trailing newline.
- The final startup checks exposed a PostgreSQL readiness check that needed to use TCP rather than a local socket.
I used these failures to direct further fixes and verification rather than treating generated code as finished. The commands, results, and limitations are recorded in docs/PROGRESS.md.

I also made deliberate tradeoffs to keep the implementation manageable: one PostgreSQL server with separate service databases, HTTP delivery with a durable outbox, development token and mailbox implementations, one successful WIN settlement per BET, bounded page-based pagination, and explicit demo funding while new wallets start at zero. I left out SignalR and additional production infrastructure to keep the work focused on the required behavior and correctness.

Codex helped run builds, tests, and local integration checks. The final automated run passed 74 tests covering domain rules, database constraints, concurrent transactions, duplicate requests, authorization, and outage recovery. An isolated Docker Compose startup and reviewer walkthrough also passed, and ordinary service restarts preserved account, wallet, and transaction data. DESIGN.md was rendered and checked against the two-page limit.

In a later documentation pass, I asked Codex to document the existing domain and API contracts and connect XML comments to Swagger without changing application behavior. That work exposed misplaced XML comments and missing response metadata, which were corrected. Verification included solution builds, publishing both APIs with their XML files, 35 focused tests, and browser checks of both Swagger UIs. The full PostgreSQL suite was not rerun for that follow-up.

AI helped me move faster, but I remained responsible for the scope, technical choices, acceptance criteria, and deciding what needed further verification. The checks above provide evidence for the implemented scenarios; they are not exhaustive production testing.
Live player balances are now available through the single-instance Wallet SignalR hub. See [docs/REALTIME.md](docs/REALTIME.md) for behavior, setup, and limitations; this was added after the assessment scope described above.

Audit records and JSON application logs are documented in [AUDITING.md](docs/AUDITING.md), including read-only inspection commands and failure/retention limits.
