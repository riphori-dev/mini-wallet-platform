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
| Git | Repository initialized; source/status reviewed | HEAD is now 20ea7b0; no commit or push performed during the README edit |

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

No known runtime blocker remains. A Git commit now exists (20ea7b0); earlier empty-history notes describe the pre-commit cleanup snapshot. No history was rewritten. No optional implementation stage remains.
## README simplification

Shortened README.md to 511 words around startup, Swagger URLs, local credentials, verification, demo BET/WIN, tests and troubleshooting. Detailed implementation and retry rules stay in the linked design/API contract. Existing changes to DESIGN.md and MiniWallet.slnx were left untouched.

Checks: PASS Markdown code fences and referenced local files. Runtime tests: NOT RUN for this documentation-only change; application code/configuration are unchanged. No commit or push performed. Suggested commit: `docs: simplify reviewer quick start`.

## XML documentation and Swagger review

Documented all public members in both Domain projects and all exposed request/response fields and controller actions. Comments distinguish domain checks from caller/database responsibilities, provider references from internal ledger IDs, and caller-selected player IDs from authenticated identity. No business methods, fields, validation or persistence behavior changed. Added explicit HTTP response metadata where XML descriptions otherwise obscured inferred response schemas. Both existing Swashbuckle 10.3.1 pipelines consume API and referenced-project XML; Users errors now document the actual Problem Details media type. Missing public domain documentation remains a compiler warning; CS1591 is suppressed only outside Domain for unrelated infrastructure/composition surfaces.

Checks and actual commands:

- PASS `dotnet build MiniWallet.Tests/MiniWallet.Tests.csproj --no-restore -v minimal`: all nine projects built, zero warnings/errors.
- FAIL `dotnet build MiniWallet.slnx --no-restore`: SDK selected by unchanged global.json is 8.0.427 and cannot read .slnx. PASS full solution via installed MSBuild 10: `dotnet "C:/Program Files/dotnet/sdk/10.0.301/MSBuild.dll" MiniWallet.slnx /t:Build /verbosity:minimal /nologo`. Project targets remain net8.0. Existing solution edits preserved.
- PASS `dotnet test MiniWallet.Tests/MiniWallet.Tests.csproj --no-restore --filter "FullyQualifiedName~FoundationTests|FullyQualifiedName~MoneyTests|FullyQualifiedName~UserDomainTests|FullyQualifiedName~FinancialRulesAndValidationTests" -v minimal`: 35 passed, zero failed/skipped. Includes both generated OpenAPI documents, every exposed DTO property's description, endpoint summaries/remarks, Problem Details media types, Swagger HTML/JavaScript and representative replay/reference descriptions.
- PASS Release publish for each API: `dotnet publish <API.csproj> --no-restore -c Release -o .review/xml-doc-<wallet|users> /p:UseAppHost=false -v minimal`. All XML files consumed by Swagger are present in each published artifact. Outputs are ignored.
- PASS browser review of both Swagger UIs on temporary hosts at ports 5180/5181: endpoint summaries, expanded transaction amount precision/bounds and registration acceptance/recovery text rendered. Temporary hosts stopped after inspection; no database was used or migrated.
- PASS source audit: removing XML comments and response-type attributes leaves the same C# content in 28 reviewed domain/application/infrastructure/controller files. Swagger/configuration/test changes are documentation plumbing only.
- NOT RUN Docker rebuild and full PostgreSQL regression suite for this documentation change. Initial `docker compose ps` could not access the Docker Desktop pipe in the sandbox. Previously recorded full-suite results are historical, not a rerun. To rerun: `docker compose up --build` and the documented Docker test command in README.

Corrections during verification: initial endpoint comments were placed below attributes (CS1587), then moved above them. The first new OpenAPI assertion caught an absent inferred history schema after XML response comments; explicit success response metadata fixed it. Final focused checks pass without weakening those assertions.

Remaining limitation: the default SDK-8 solution command still cannot read the existing .slnx; direct project build or the installed MSBuild-10 command above works. No product features or optional infrastructure added. Existing DESIGN.md, MiniWallet.slnx and README.md edits were preserved. Estimated effort: about 30 minutes. No commit or push performed. Suggested commit: `docs: document domain rules and API contracts in Swagger`.
