# Implementation status

The assessment core is complete. Users and Wallet run independently from one Docker Compose command. The canonical brief is [candidate-brief.md](candidate-brief.md); [README](../README.md) covers startup and review, [DESIGN](../DESIGN.md) explains decisions, and [API_CONTRACT](API_CONTRACT.md) documents the API.

## Completed stages

- [x] 0: Requirements, API contract and implementation plan.
- [x] 1: Service projects, PostgreSQL databases, migrations, Compose, Swagger and health checks.
- [x] 2: Registration, verification, local mailbox and Development JWT identities.
- [x] 3: Durable registration delivery and atomic zero-wallet provisioning.
- [x] 4: BET/WIN, concurrency control, durable replay, protected reads and funded demo.
- [x] 5: Focused correctness review and PostgreSQL regression tests.
- [x] 6: Isolated fresh startup, complete reviewer walkthrough, restart persistence and submission documents.

## Requirements and evidence

| Requirement | Implementation | Verification |
| --- | --- | --- |
| Register and verify players | Users Application use cases; normalized email/unique constraint; salted password and verification-token hashes | UsersIntegrationTests (including UsersRecoveryTests.cs), UserDomainTests; live reviewer flow |
| Automatic USD/EUR/BTC zero wallets | Users registration/outbox transaction; single HTTP retry worker; Wallet receipt/provisioning transaction | ProvisioningTests; live outage/restart/recovery scenario |
| BET/WIN through one provider route | SubmitTransaction, focused handlers, Money/Currency and WalletExceptionHandler | TransactionTests (including TransactionCorrectnessTests.cs), MoneyTests, FinancialRulesAndValidationTests |
| Correct money and concurrent writes | Decimal bounds/precision; exact numeric checks; unique provider reference; wallet row lock; atomic balance/ledger/outcome | Real PostgreSQL tests, including two hosts racing BETs of 80 against 100 |
| Matching BET and one WIN | Domain checks, immutable accepted history, matching-BET trigger and unique settlement reference | Provider/player/currency mismatch and direct EF bypass tests |
| Durable retries and outage recovery | Terminal outcome storage; retryable transient errors; persistent pending delivery; receipt deduplication | Response-loss/host-restart, rollback, duplicate delivery, lock timeout and Users transient-error regression |
| Player/admin/provider permissions | Trusted JWT subject and role/kind/scope policies; separate internal audience | Concrete A/B isolation, admin access, provider restrictions and invalid/expired JWT tests |
| Balances/history and documented API | Scoped routes, fixed-scale strings, bounded page/pageSize and Problem Details | HTTP contract/pagination/error assertions; both Swagger UIs and reviewer script |
| One-command startup and persistence | Separate service-owned databases; TCP readiness; locked migration jobs; persistent volume | Fresh source-only isolated Compose startup and ordinary restart |
| Required documentation/AI disclosure | README.md, DESIGN.md, AI_USAGE.md | Walkthrough executed; design print proof is two pages; actual interactions recorded |
| Git | Repository initialized; source/status reviewed | No commits or push performed; initial commit remains a review/submission step |

## Current verification

The last complete release verification, before this formatting cleanup:

| Command/check | Result |
| --- | --- |
| Source-only isolated `docker compose up --build -d` | PASS: fresh volume, automatic databases/migrations, both APIs ready |
| `scripts/verify-reviewer.ps1` | PASS: registration, message retrieval, verification, player token, three zero wallets, separate demo BET/WIN, replay, balances/history, admin access and player isolation |
| Ordinary PostgreSQL/API restart, then reviewer persistence check | PASS: verified login, balances and transaction history retained |
| Complete Docker xUnit/PostgreSQL suite | PASS: 74 passed, 0 failed/skipped; Release build had zero warnings/errors |
| DESIGN print proof | PASS: two Letter pages, 11pt body; both pages visually inspected |
| Isolated environment cleanup | PASS: removed only the newly created review resources; original volume identity unchanged and original APIs ready |

Reproduce the complete test suite:

```sh
docker compose -f compose.yaml -f compose.tests.yaml run --build --rm tests
```

Host tests without TestPostgres intentionally skip database cases and are not PostgreSQL correctness evidence. No test uses EF InMemory/mocks to prove locks or transaction semantics. Physical commit-time process kills and a load-testing platform were NOT RUN; focused fault/restart tests cover the requested assessment scope.

## Final readability cleanup

Standardized C#, XML and JSON formatting; added repository formatting rules; removed stale stage wording and unused imports. Renamed the Wallet exception handler and Development identity seeder to reflect their existing responsibilities. The duplicate uploaded brief was removed only after its SHA-256 matched the canonical copy. No financial rules, routes, authorization policies or migrations were changed.

The detailed stage history, superseded plans, failures and command results are preserved in [implementation-log.md](archive/implementation-log.md). It is historical evidence, not the current contract.

Cleanup verification:

| Check | Result |
| --- | --- |
| `dotnet format whitespace MiniWallet.sln --no-restore --exclude MiniWallet.Infrastructure/Migrations MiniWallet.Users.Infrastructure/Migrations --verify-no-changes` | PASS |
| `dotnet build MiniWallet.sln --no-restore` | PASS: zero warnings/errors |
| `docker compose config --quiet` | PASS |
| Full Docker xUnit/PostgreSQL suite | PASS: 74 passed, 0 failed/skipped; Release build zero warnings/errors |
| Active docs/source stale-phase scan and Markdown fences | PASS; historical wording is retained only in the archived log/AI record |

The tests reused the existing PostgreSQL service and disposable test databases. Compose refreshed the PostgreSQL container for the current health check; its persistent volume was retained. No application behavior, migration contents or access rules changed. No commit/push was performed. Cleanup elapsed estimate: about 15 minutes, not stopwatch-measured.

Suggested commit: `chore: tidy formatting and submission documentation`.

## Decisions and limits

- .NET 8 follows the final runtime instruction. Each service owns its model, database and migrations; dependencies point inward.
- Delivery is at least once. A single local Users worker retries indefinitely with bounded HTTP timeouts; no distributed leases or cross-service database reads.
- Registration commits the user and event before 201. Duplicate normalized email returns 409; lost-response recovery uses the original credentials and login/resend.
- Development-only JWT issuer/mailbox and checked-in local credentials are assessment conveniences. Mailbox loss is recovered with authenticated resend.
- New registrations start at zero. The separate demo has explicit opening ledger credits, never a public deposit route or implicit registration funding.
- One successful WIN per BET is an assumption. Terminal business rejections reserve the provider transaction ID; transient failures do not.
- Money bounds/quantization are enforced in Domain and PostgreSQL. No silent rounding or cross-currency arithmetic.
- Offset pages may shift under concurrent writes. Outcomes are retained for the assessment lifetime.
- No optional SignalR, broker, Redis, gateway, Kubernetes or production identity/email system.

No known runtime blocker remains. Git has no HEAD; all source remains uncommitted. Review and create a truthful initial commit before submitting a repository link, or use the brief's zip option. Historical stage commits were not fabricated. No optional implementation stage remains.