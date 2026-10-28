# Implementation log

Historical stage snapshots and command results. Some paths and plans describe the repository at that time; see ../PROGRESS.md for the current state.



## Final assessment status

The required functional core and submission documents are implemented and verified. Final source-only isolated Compose startup, full reviewer walkthrough, ordinary-restart persistence and all 74 tests passed. Domain dependencies point inward; unused weather scaffolding was removed. No optional product infrastructure was added. README.md, DESIGN.md (two-page print proof) and AI_USAGE.md are finalized; the authoritative brief remains unchanged.

Submission note: this Git repository has no HEAD/commits; all assessment files are untracked. No historical commits were reconstructed, nothing was staged or committed, and nothing was pushed. Review and create a factual initial commit before submitting a Git repository link (or use the brief's zip alternative). There is no known runtime/core blocker. External submission/publication is not performed by this task.

## Authority and current state

The authoritative assessment is [candidate-brief.md](../candidate-brief.md). User instructions refine it; the latest prompt pack preserves MiniWallet.API as Wallet and places Development token endpoints inside Users (no third authentication API). Stage 0 is historical planning. Stage 1–6 results below supersede that baseline; original planning rows remain as history, while the final stage 6 matrix records implemented requirements and actual verification.

Initial stage 0 inspection baseline: MiniWallet.slnx included only MiniWallet.API, a net8.0 WeatherForecast controller template with Swagger and a .NET 8 Dockerfile. No service layers, test project, Compose file or Git repository was found. Preserve existing files until their implementation stage. No applicable AGENTS.md was found by the source-tree search. The brief now exists at docs/candidate-brief.md; a root copy also exists and is untouched.

## Stages and budget

| Stage | Checklist / intended result | Budget |
| --- | --- | --- |
| 0 Planning | [x] Inspect brief/repository; [x] stage/requirement map; [x] API contract; [x] concise initial design | 0.5 h |
| 1 Project structure and runnable foundation | [x] Preserve Wallet API; add Users and inward layers; [x] .NET 8 (latest override); [x] EF migrations/databases/credentials; [x] Compose and Swagger; [x] Initialize Git (no commit) | 0.75 h |
| 2 Users, verification and local JWT authentication | [x] Durable registration + pending event (duplicate-email recovery); [x] verification/mailbox/resend; [x] Users-hosted dev issuer; [x] player/admin/provider/internal policies and tests | 1.25 h |
| 3 Automatic wallets and recovery | [x] Registration outbox; [x] single local retry worker (no leases); [x] authenticated inbox; [x] atomic zero wallets; recovery checks below | 1 h |
| 4 Transactions, balances and history | [x] Money/precision; [x] provider strategy route; [x] durable success/rejection outcomes; [x] row locks; [x] scoped reads/page pagination; [x] explicit funding fixture and tests | 1.75 h |
| 5 Focused tests and correctness review | [x] Close important real PostgreSQL concurrency/failure gaps; [x] security and API checks; [x] inspect constraints/retry semantics; [x] fix Users transient classification | 1 h |
| 6 Fresh-start verification and submission documents | [x] Isolated clean Compose run; [x] reviewer flow/restart; [x] README; [x] two-page DESIGN proof; [x] truthful AI_USAGE; [x] final tests/Git status audit (no commits fabricated) | 0.75 h |

Total target: 7 hours; budget, not a guarantee. Test each feature while building it. Stop after each supplied stage for review; no automatic progression. No optional infrastructure to compensate for missing core work.

Planned projects: MiniWallet.API, MiniWallet.Domain, MiniWallet.Application, MiniWallet.Infrastructure; MiniWallet.Users.API, MiniWallet.Users.Domain, MiniWallet.Users.Application, MiniWallet.Users.Infrastructure; consolidated MiniWallet.Tests with service-organized unit/integration folders. Application depends on Domain; Infrastructure implements Application ports; API is composition root. Event DTOs alone may be shared if useful; never share entities or DbContexts.

## Requirement → implementation → verification

Test labels below are planned names, not execution claims. Stage 1 foundation evidence is recorded separately below; all later business checks remain NOT RUN.

| ID / brief requirement or user invariant | Planned code / stage | Planned evidence |
| --- | --- | --- |
| R1 C# .NET 8+ / ASP.NET | Both API csproj/controllers, stage 1 (.NET 8 per latest instruction) | Release build; dependency/project reference inspection |
| R2 Two independent services, one-command Docker run | Compose, Dockerfiles, separate DB migrations/roles, stage 1/6 | Fresh `docker compose up --build`; both Swagger UIs; independent startup/restart; DB role isolation |
| R3 Registration and verification | Users Application use cases, Domain User, Infrastructure hashing/email, stage 2 | RegistrationVerificationTests: duplicate normalized email, expiry, token hashing, atomic consume, unverified token denial |
| R4 Registration + event atomic; lost registration response | Users DbContext/pending registration delivery, stage 2/3 | RegistrationAtomicityTests: injected rollback, concurrent normalized email duplicates, lost-response duplicate recovery; authenticated email resend |
| R5 Automatic USD/EUR/BTC zero wallets, exactly once | Wallet Inbox/Wallet constraints, stage 3 | ProvisioningTests: exactly three zero balances, atomic rollback, duplicate events, different event for same player, no reset |
| R6 BET never negative; multi-instance correctness | Money, BetHandler, locked transaction port, stage 4 | ConcurrentBetTests on real PG using separate connections/hosts; insufficient funds, conservation and no negative balances |
| R7 WIN for earlier matching BET | WinHandler, referenced BET and unique settlement, stage 4 | WinTests: accepted/rejected/missing BET; provider/player/currency mismatch; concurrent duplicate settlement; payout differs from stake |
| R8 One extensible provider route | TransactionsController + typed handler dispatch, stage 4 | HTTP BET/WIN through one route; unknown type rejection; no mediator/generic repository |
| R9 Decimal precision/range | Currency/Money, exact numeric + explicit range/quantization constraints (stage 4 override) | MoneyTests: scales 2/8, excess trailing zero precision, cap, overflow, cross-currency; direct PG constraint violations |
| R10 Durable idempotency and atomic ledger/balance | Provider outcome table, unique provider/external ID, stage 4 | IdempotencyTests: simultaneous duplicates across instances/currencies, canonical equality/conflict, original balanceAfter; no partial ledger |
| R11 Terminal rejection versus transient retry | Stored rejected outcomes / rollback policy, stage 4/5 | Rejected bet remains rejected after funding; rejected missing BET remains rejected after BET; 503 then same-ID success; unknown commit replay |
| R12 Balances and transaction history | Wallet read use cases/controllers, stage 4 | Owned_reads_admin_access_page_bounds_and_machine_isolation: fixed-scale balances, accepted ledger, deterministic bounded page/pageSize traversal |
| R13 Own player access/admin any | JWT policies and subject-based queries, stage 2/4 | AuthorizationTests: cross-player reads denied, player on admin route denied, valid admin allowed, missing/expired/wrong audience/scope tokens |
| R14 Separate provider machine authentication | Users dev client issuer + Wallet policies, stage 2/4 | ProviderAuthTests: no player login privilege escalation, provider cannot read, client cannot provision with provider token, spoofed provider field rejected |
| R15 Outages / no lost or duplicate operations | Single-worker durable outbox, authenticated HTTP inbox, stage 3/5 | RecoveryTests: every failure-table scenario below, including separate worker/DB sessions and restarts |
| R16 Documented explorable external contract/auth | API_CONTRACT.md and per-API OpenAPI/Swagger, stage 0/1/4 | Swagger smoke test; request/response examples validated against implemented DTOs/statuses at stage 6 |
| R17 Tests and rationale | MiniWallet.Tests, stage 1–5 | xUnit unit + real PostgreSQL integration results; no in-memory substitute for locking/constraints |
| R18 Git/readable history and source | Stage-scoped Git work, stages 1–6 | Inspect git status/diff/log; no push/history rewrite/unrelated commit |
| R19 README/run instructions/sample credentials | README.md, stage 6 | Reviewer-style fresh-start walkthrough with documented development credentials |
| R20 DESIGN <=2 pages/failure scenarios/trade-offs | DESIGN.md, stage 0/6 | Concision review now; final rendered page-count check |
| R21 AI assistant usage disclosure | AI_USAGE.md, stage 6 | Actual tools, 3–5 real prompts, actual corrections, trade-offs, actual verification; no fabricated interactions |
| R22 Optional live updates/production extras | Intentionally omitted | Document omission; no SignalR/broker/Redis/gateway/Kubernetes/event sourcing |
| R23 Recoverable verification delivery and explicit funding | Users IEmailSender/mailbox/authenticated resend; Wallet dev fixture, stage 2/4 | Delivery retry/deduplication; mailbox accessibility; fixture ledger + repeat-run idempotency; zero registration wallets |
| R24 Manual async validation and error contract | Application validators/controllers, stage 2–4 | Async validation awaited; RFC 9457 code/traceId and statuses; DB constraints tested independently of validators |

## Failure acceptance matrix

| Scenario | Expected behavior / planned test (all NOT RUN) |
| --- | --- |
| Wallet down on registration | 201 only after Users durable commit; pending outbox survives and eventually creates three wallets |
| Users down after event delivery | Wallet state survives; restart Users and retry unacknowledged event |
| Lost delivery acknowledgement | Repeat same event, one inbox effect and three wallets |
| Two outbox workers | Single local worker is the supported topology; accidental duplicate delivery remains safe through Wallet uniqueness |
| Crash before Wallet commit | No partial wallet/inbox or ledger/outcome; retry succeeds once |
| Crash after Wallet commit | Retry discovers committed receipt/outcome without another mutation |
| Simultaneous duplicate provider calls | One committed financial effect; all exact callers receive original status/body |
| Simultaneous different bets | Serialize on wallet row; accepted total never exceeds funds |
| Mismatched idempotency payload | 409 across any player/currency for same provider key; original preserved |
| Database temporarily down | No pre-commit success; 503 retryable, or unknown commit recovered through same key |

## Assumptions and trade-offs

- Registration triggers eventual provisioning immediately; verification gates player tokens, not wallet creation. Pending reads return 404 and may be retried. Registration replay returns original pending response.
- One successful settlement WIN per BET is a user-selected assumption, not a requirement of the assessment. Rejected attempts never consume settlement uniqueness.
- Amount/balance cap is 99999999999999999999.99999999; USD/EUR effective cap is 99999999999999999999.99. Reject excess fractional digits before normalization, including trailing zeros. Stage 4 uses exact numeric with explicit bounds/quantization checks to reject excess BTC digits without typmod rounding.
- Terminal business failures reserve provider IDs durably; transient failures and missing provisioning do not. Missing BET is terminal, so a provider must use a new WIN ID after correcting event order. No claim of successful commit on uncertain connection failure.
- Polling authenticated HTTP and database leases replace a broker. Outbox records persist until acknowledgement; deterministic delivery errors remain visible for repair. No maximum-attempt discard. Capped exponential backoff/jitter applies to transient delivery failures.
- Development token issuer lives in Users, separated as a component/routes and disabled outside Development. This reconciles the earlier separate-issuer decision with the latest prohibition on a third authentication API.
- No production identity platform, SMTP provider, retention service or optional realtime features. Development mailbox and funding fixture are explicit reviewer tools. Outcomes/inbox retained for assessment lifetime; local signing/protection keys must survive restarts and be shared across replicas.

## Actual commands and results (planning stage)

| Check | Actual result |
| --- | --- |
| `Get-Content docs/candidate-brief.md -Encoding utf8` | PASS on current inspection; full authoritative brief read. Earlier inspection before upload found it missing. |
| `Get-Content candidate-brief_8155.md -Encoding utf8` | PASS; root copy read, left untouched. |
| `Get-ChildItem -Force`; `rg --files` excluding bin/obj/.vs | PASS; existing template inventory inspected. |
| Read solution, API csproj, Program, controller, Dockerfile, appsettings and launchSettings | PASS; .NET 8 template baseline confirmed. |
| `rg --files --hidden -g AGENTS.md -g '!**/.vs/**' C:/Users/rabunao/source` | No matching files returned; no source-tree AGENTS instructions found. |
| `git status --short` | FAIL: directory is not a Git repository; no status/diff/history available. No Git changes/commits performed. |
| `dotnet --version` | PASS: 10.0.301 available. |
| `docker --version` | PASS: Docker CLI 29.5.3; daemon/Compose runtime not verified by this command. |
| Build, xUnit, PostgreSQL, Compose startup, Swagger HTTP checks | NOT RUN: documentation-only stage; no implementation changes to validate. |
| Final DESIGN rendered page count | NOT RUN: Markdown has no fixed pagination; concise draft targets <=2 pages, final rendering required in stage 6. |
| PowerShell document checks: file reads, balanced fenced blocks, R1–R24 traceability presence | PASS: all three documents readable, fences balanced, all 24 map rows present. DESIGN is approximately 708 whitespace-delimited words; this is not a rendered page-count check. |
| Manual planning consistency review | PASS: all requested failure cases present; six implementation stages mapped; precision, identity, replay, settlement, delivery and retry choices documented. Only the three requested documentation files were created by this stage. |

## Remaining risks / next stage / time (stage 0 snapshot)

No application behavior described here is implemented yet. PostgreSQL lock/outcome race handling, database constraints, protected delivery secret storage, stable cursor keys and Development issuer isolation require integration evidence. Git initialization, Docker daemon availability and package restore remain to be checked in stage 1. Preserve template and unrelated files while adding the foundation. API names and semantics are now planned commitments; document any concrete implementation conflict before changing them.

Next: stage 1 only when supplied/authorized for implementation. Planning elapsed estimate: about 10–15 minutes including inspection and drafting, not stopwatch-measured. Stage 0 work remaining: none. No commit created; suggested message: `docs: plan MiniWallet stages, API contract and failure handling`.

## Stage 1 — implemented foundation

Latest instruction overrides .NET 10 with .NET 8. Existing MiniWallet.API, its controller template, configuration and paths are preserved. Added both sets of inward layers, Users host, consolidated tests, common net8.0 properties, SDK 8.0.4xx selection, explicit nuget.org source and local EF tool manifest. Added MiniWallet.sln for SDK 8; retained matching .slnx for newer tooling. Removed the VS-only container build package from the Wallet project; standard Dockerfiles build without Visual Studio. Added no speculative ports, entities or later-stage endpoints.

Files: both API Program.cs/csproj/Dockerfile and API-specific Problem Details/HealthController; six layer csproj files; two service-owned DbContexts and DatabaseSetup migration runners; generated empty InitialFoundation migrations/snapshots; MiniWallet.Tests; Compose/init.sql; README; SDK/build/NuGet/tool configuration; .gitignore; updated DESIGN and this tracker; initial AI_USAGE. Original WeatherForecast implementation and settings are untouched. All files are untracked because the directory had no Git history; Git was initialized, nothing staged or committed.

Architecture: Domain has no package/project/framework dependencies; Application references its Domain and FluentValidation; Infrastructure references Application and Npgsql/EF Relational; API references Application/Infrastructure for composition. Hosts expose health/live, health/ready and Development Swagger. Missing-route, framework validation and server errors use Problem Details with code/traceId. No auth behavior is claimed yet. Infrastructure owns migrations; models are empty until their use cases arrive.

Compose: PostgreSQL 16 persistent named volume, separate wallet/users databases and non-superuser owners; PUBLIC database CONNECT revoked. APIs bind localhost only. Migration jobs run after PostgreSQL health and before each API, protected by a database-scoped session advisory lock before EF8 history creation. Normal hosts never migrate; failure blocks startup. Connection credentials are documented local-only defaults, not untracked environment prerequisites. No host SDK/manual SQL/external account needed for Compose.

Resolved stable packages from NuGet index: EF Core Design/Relational and EF tool 8.0.33; Npgsql EF provider 8.0.11; Swashbuckle 10.3.1; FluentValidation 12.2.0; MVC Testing 8.0.33; Microsoft.NET.Test.Sdk 18.11.0; xUnit 2.9.3; VS runner 4.0.1. Successful restore/build verifies compatibility, not just assumed API signatures. JWT packages and validation use cases wait for stage 2. Provider release documentation consulted: https://www.npgsql.org/efcore/release-notes/10.0.html while considering the superseded .NET 10 target; final .NET 8 package selection was resolved directly from NuGet, not that release page.

| Actual command/check | Result |
| --- | --- |
| Read brief, API, solution, configuration; git status before edits | PASS inspection; git status initially FAIL: no Git repository. |
| SDK inventory | SDK 8.0.427 and 10.0.301 installed. Earlier dotnet --info partially failed on restricted Service Control Manager access; no claim that it passed. |
| NuGet index query | Initial sandbox DNS FAIL; fail-fast permitted query PASS, final 8.x versions listed above. |
| dotnet restore MiniWallet.sln; dotnet tool restore | PASS outside sandbox after initial SDK sentinel permission failure; all nine projects and EF tool restored. |
| dotnet ef migrations add InitialFoundation (both service projects/startup APIs) | PASS; generated empty migrations and snapshots, no live DB required. |
| dotnet build MiniWallet.sln --no-restore | Initial build had EF Relational assembly conflict warning; corrected both explicit Relational pins. Final PASS: zero warnings, zero errors. |
| dotnet test MiniWallet.sln --no-build | PASS: 2 tests, 0 failures/skips; each checks host liveness, Swagger HTML/JSON and missing-route Problem Details code/traceId. These are HTTP foundation tests, not financial integration coverage. |
| docker compose config --quiet | PASS. |
| docker info | Initial sandbox pipe permission FAIL; permitted retry PASS, engine 29.5.3. |
| docker compose up --build -d | PASS from newly created named volume; image restore/publish, PostgreSQL initialization and both migrator jobs (exit 0), then both API hosts. Repeated after final package alignment: PASS with existing volume. |
| HTTP checks on 5080/5081 | PASS: live, ready, Swagger HTML and JSON all 200 for both; missing routes 404. |
| PostgreSQL service ownership/isolation query | PASS: wallet owned by wallet_app; users owned by users_app; neither role has CONNECT on the other's database. |
| Migration history queries | Initial identifier quoting FAIL; corrected stdin SQL PASS: one InitialFoundation history row in each service database, EF 8.0.33. |
| Two concurrently dispatched docker compose run --rm --no-deps wallet-migrate | PASS: both exit 0, existing database up to date. This is rerun safety, not a fresh-schema contention stress test. |
| Git init/status | PASS; initialized empty repository, no staging/commit/push. Ignore rules cover .vs, bin/obj, user settings and test results. |
| Final readiness/dependency review | PASS: both rebuilt hosts ready (200), both migration jobs exit 0; all nine project references inspected and point inward. `git log --oneline -1` reports no commits (expected empty history, not an existing baseline commit). `git diff --check` returned 0 but cannot inspect untracked file contents. |
| Financial concurrency, registration/outbox/inbox, JWT policies, verification | NOT RUN: stages 2–5 not implemented. |
| Clean-volume simultaneous first migrations, forced migration failure, final two-page rendering | NOT RUN: lock design inspected; existing-volume parallel rerun checked, not these scenarios. |

Traceability now implemented in part: R1/R2/R16/R17/R18/R19/R20/R21/R24 have foundation code/docs and the evidence above. Functional financial/auth/outage rows remain planned. Acceptance of stage 1 does not imply acceptance of the full assessment.

Assumptions: provisioning after a Wallet outage will be eventual via durable Users delivery, not a distributed transaction. The brief provides no funding endpoint; no funding is added now. One-WIN-per-BET settlement is still an explicit stage 4 planning assumption, not a database rule implemented in stage 1. Local-only defaults are unsuitable outside this reviewer environment.

Remaining gaps: all later-stage business behavior, authentication and related validation; real PostgreSQL business integration tests; first-schema migration contention stress; finalized submission documents. No claim of exactly-once delivery. The Compose stack is left running for review. Next authorized stage to request: stage 2 Users/verification/local JWT. Stage 1 elapsed estimate: roughly 20–30 minutes, not stopwatch-measured; the initial 0.75h budget remains a planning allowance. Suggested commit: `feat: establish .NET 8 Wallet and Users service foundation`.

## Stage 2 — Users and Development identity (completed before stage 3)

Implemented Domain EmailAddress/User rules; Application registration/verification/login/resend use cases and manually awaited FluentValidation; Infrastructure EF store, database unique normalized email, PBKDF2-SHA256 (600,000 iterations with random 16-byte salt), 32-byte random verification secrets stored only as SHA-256 hashes, 24-hour expiry and locked atomic consumption. Registration and pending PlayerRegistrationDelivery commit together. Duplicate email returns 409; after a lost response retry registration then authenticate/login or resend. No registration replay framework or Idempotency-Key, per the latest stage 2 direction. This supersedes stage 0 replay planning.

Development-only in-memory mailbox (100 messages, reviewer header key); resend authenticates and rotates tokens, replacing lost/failed delivery without an encrypted durable email pipeline. Email exceptions do not invalidate already committed registration; failed resend is retryable 503. This supersedes the initial durable email intent proposal. Tokens/admin seed/mailbox credentials are in Development configuration only. Users migrator seeds a verified admin; normal registration always creates role=player. Provider and internal clients use distinct JSON credentials and scopes. JWT lifetime 10 minutes; Wallet validates HS256 signature/issuer/audience/lifetime and enforces separate principal kind/role/scope policies, including a different Internal authentication scheme/audience. Internal delivery route/worker still pending at the stage 2 boundary.

Files: Users Domain/Application source; Users Infrastructure store/security/mailbox/token/seed code, UsersRegistration migration/snapshot and JWT package; Users API controllers/composition/error handler/Swagger/Development settings; Wallet JWT authorization/Swagger/config/packages; test files/project plus Dockerfile.tests/compose.tests.yaml; Compose Development migration environment; README/API contract/AI record. No Git commit made; all prior files remain untracked in the initialized repository.

| Actual check | Result |
| --- | --- |
| Restore stable JWT packages from NuGet | PASS: JwtBearer 8.0.33 and IdentityModel JWT 8.23.0 restored; version query performed. |
| First stage 2 build | FAIL: wrong .NET 8 WriteAsJsonAsync overload. Corrected by supplying serializer options explicitly. |
| Subsequent build | PASS; initially four xUnit analyzer warnings, corrected Assert.Single usage. Docker Release build then PASS: zero warnings/errors. |
| Generate UsersRegistration EF migration | PASS; inspected user/event tables, unique email, foreign key and role/normalization constraints. |
| Host dotnet test MiniWallet.sln --no-build | PASS: 9 passed, 10 PostgreSQL tests SKIPPED because TestPostgres was absent. This was not counted as integration evidence. |
| docker compose -f compose.yaml -f compose.tests.yaml config --quiet | PASS. |
| docker compose -f compose.yaml -f compose.tests.yaml run --build --rm tests | PASS: 19 passed, 0 failed/skipped, real PostgreSQL disposable database. Includes concurrent duplicates, outbox rollback, hashed tokens, verification expiry/reuse/race, email failure/resend, invalid input/escalation, bad/unverified credentials, separate roles/machine flows, signature/issuer/audience/expiry validation and Production dev-route denial. |
| docker compose up --build -d | PASS: migrated existing Users database, seeded Development admin, both APIs started. |

Stage 2 traceability: R3/R4/R13/R14/R17/R21/R23/R24 implemented in relevant part; read endpoint ownership enforcement itself remains stage 4. No Wallet provisioning/financial behavior claimed at this boundary. Local issuer is documented Development JSON protocol, not production OAuth/OIDC. No refresh/revocation, real SMTP, production identity integration or distributed email infrastructure added. One user's next message authorized stage 3 while final stage 2 documentation was being completed; continue with that stage, preserving these results. Elapsed estimate roughly 25–35 minutes (not stopwatch measured). Suggested stage 2 commit: feat: add player registration verification and Development identities.

## Stage 3 — durable automatic wallet provisioning

Implemented Wallet Currency/PlayerWallet domain state (zero initialization), Application async event validation and narrow IPlayerProvisioner boundary, Infrastructure transactional receipt/provisioner and PlayerProvisioning migration, authenticated internal controller and sanitized exception handling. Users Infrastructure owns HTTP token acquisition/delivery, the single hosted polling worker and DeliveryRetryState migration. No shared entity/DbContext/event library and no cross-service table access. No player create-wallet endpoint exists.

Worker semantics: stable event ID/player ID from the committed Users record; 20 due records per batch; each token/delivery request timeout 5 seconds; persisted 2-second retry delay. Attempts/NextAttemptAt/LastError record failure; no fixed failure threshold discards work, including permanent HTTP errors that need configuration/data repair. DeliveredAt is written only after receiver 2xx. Database reads finish before HTTP; result updates use separate statements with no transaction spanning the network. Users restart retains pending work. One local worker is the supported topology; stage 0 distributed lease/SKIP LOCKED proposals are superseded, not implemented.

Wallet semantics: validate/authenticate first, unique receipt insert and USD/EUR/BTC inserts in one short PostgreSQL transaction. Concurrent receipt insertion waits on uniqueness; exact replay returns 204, changed event identity returns 409. Unique player/currency inserts do nothing on existing wallets, preserving balances even under a different event ID. OccurredAt is canonicalized to UTC microseconds to match PostgreSQL timestamp precision. Receipt and every wallet insert roll back if any insert fails. Balance uses decimal/numeric(28,8), with currency/range/scale constraints; there is no funding or transaction logic yet.

Files added/updated for stage 3: Wallet Currency/PlayerWallet; Application ProvisionPlayer; Infrastructure PlayerProvisioner/DbContext/migration; API internal controller/error handler/composition/Swagger security metadata; Users Infrastructure delivery client/worker/options/DI and retry fields/migration; Users API composition and Development delivery settings; Compose service URLs; ProvisioningTests and foundation test configuration/assertion; scripts/verify-provisioning.ps1; README/API_CONTRACT/DESIGN/AI_USAGE and this tracker. Stage 2 file changes are listed in its section above. Existing template and unrelated files remain preserved; all work is still untracked, nothing committed.

| Actual check | Result |
| --- | --- |
| Live stage 2 registration/verification/player+admin token smoke | Initial script FAIL due PowerShell mailbox array handling; corrected PASS: 201 registration, 204 verification and 600-second player/admin tokens. |
| NuGet worker dependency query + restore | PASS: Microsoft.Extensions.Http and Hosting.Abstractions 8.0.1; solution restore succeeded. |
| Generate Wallet PlayerProvisioning and Users DeliveryRetryState migrations | PASS; inspected changes and successfully applied through Compose. |
| dotnet build MiniWallet.sln --no-restore | PASS: zero warnings/errors. |
| First combined real PostgreSQL run | FAIL: 26 passed, 1 failed; test interceptor did not activate the intended partial-failure injection. No false rollback claim made. |
| Corrected partial-failure injection | Uses a PostgreSQL EUR-insert failure trigger solely in the disposable test database, removed in finally; test verifies no receipt/no wallets remain, then successful retry. |
| Full PostgreSQL suite after correction | PASS: 27 passed, 0 failed/skipped. A filtered PowerShell output wrapper reported nonzero status despite successful tests; direct docker compose test rerun confirmed process exit 0 and all 27 passing. |
| docker compose up --build -d | PASS: both service migration jobs exited 0, both hosts running; prior pending stage 2 registrations delivered automatically. |
| scripts/verify-provisioning.ps1 | Initial localhost runs stalled and were interrupted (not PASS). Explicit IPv4 plus bounded requests: PASS exit 0 for all five live checks: normal automatic provisioning; duplicate delivery; registration 201/pending while Wallet stopped; Users container restart preserves same event; Wallet recovery automatically provisions three zero wallets. Script restored Wallet in finally; both services running. |
| Receiver commit then lost acknowledgement | PASS real PostgreSQL test: receipt and three wallets committed, sender remains pending after simulated acknowledgement loss, same event retried, sender marks delivered, still exactly three wallets/one receipt. |
| Concurrent multiple-Wallet-host delivery | PASS: two independent test hosts/DB scopes, same event concurrently, both 204, one receipt and exactly USD/EUR/BTC zero wallets. |
| New event for existing player / mismatched event ID | PASS: existing balance preserved and still three wallets; same ID with different player rejected 409. |
| Authorization/validation before receipt | PASS: missing/provider token rejected; malformed IDs/version rejected; no receipt committed. Stage 2 policies also test player/admin token isolation from Internal. |
| Bounded timeout/persisted retry scheduling | PASS: HTTP timeout leaves pending/error, immediate dispatch respects delay, due retry succeeds. |
| Final Swagger authorization metadata | PASS host build/foundation test: protected internal operation declares Bearer security, so UI Authorize attaches the token. |
| Final deployment and documentation checks | PASS: Wallet rebuilt/restarted after Swagger fix, live internal-operation Bearer metadata checked, both API readiness endpoints 200; Markdown code fences balanced and both Domain csproj files contain no framework/package references. Compose configuration validates. |
| Final host dotnet test (no TestPostgres) | PASS: 9 passed, 18 explicitly skipped; PostgreSQL evidence comes from the separate 27-test Docker run, not these skips. |
| Financial BET/WIN, ledger/rejection idempotency, scoped reads/history | NOT RUN: stage 4 not implemented. |
| Production identity/email, distributed Users topology, final rendered design pagination | NOT RUN/out of current scope. Local issuer/mailbox are explicitly Development only; one local worker requested. |

Traceability update: R4 atomic registration/delivery, R5 automatic exactly-three zero wallets, R13/R14 internal identity isolation, R15 outage recovery, R17 integration tests and R24 async validation/database correctness have executable evidence above. R6–R12 financial/read behavior is still planned. Stage 0 failure table remains a plan except the scenarios now tested here. No exactly-once delivery claim: HTTP is at least once and database effects are deduplicated.

Latest effective decisions supersede early planning paragraphs: no registration Idempotency-Key/replay framework; duplicate normalized email plus credential-based recovery; no encrypted durable email pipeline; no distributed worker leases. See current DESIGN and API_CONTRACT for the reconciled contracts. Existing registration acceptance always says provisioningStatus=pending even if background delivery finishes quickly; it does not pretend to synchronously observe Wallet completion.

Elapsed estimate for stage 3: roughly 25–35 minutes (not stopwatch measured); stage 2 estimate recorded separately. Remaining work: stage 4 financial operations/read authorization and pagination, then focused gap review/submission verification. No stage 4 features added. Suggested commit: feat: deliver player registrations and provision wallets durably. No staging, commit or push performed.

## Stage 4 — financial transactions and protected reads

Implemented only the requested stage and its demo/test dependencies. Preserved MiniWallet.API as Wallet and existing paths. Domain now owns Money, accepted FinancialEntry state and BET/WIN rules; Currency remains service-owned. Application owns manual async FluentValidation, typed request parsing, SubmitTransaction, BET/WIN handlers and ReadWallets with narrow transaction/read ports. Infrastructure owns PostgreSQL reservation/locking, ledger/outcome mapping, serialization of stored responses, reads, migration guards and Development funding. API controllers derive provider/subject from verified claims and compose these parts. No generic repository, mediator, shared domain library, in-process lock, Redis or public deposit endpoint.

Financial boundary: reserve unique (providerId,externalTransactionId) by ON CONFLICT DO NOTHING in a short transaction, then read current wallet FOR UPDATE. All current financial checks happen after that lock. Same-key requests, including changed player/currency, serialize before wallet access. On exact typed replay return stored status/body; changed payload returns 409. Save balance, immutable entry and response together, then commit before returning. A deferred constraint trigger prohibits unfinished outcomes at commit. ON CONFLICT avoids a failed transaction on normal key races; actual DB failures unwind/roll back before error handling, with no queries attempted inside an aborted transaction.

Terminal business rejections persist 409 outcome/body/traceId without financial entries or balance changes. Replay remains rejected after funding or later BET creation; a corrected intentional attempt needs a new external ID. Invalid/auth requests and 404 missing provisioning do not reserve keys. Transient errors/3-second lock wait/5-second statement timeout roll back and return 503 + Retry-After; unknown commit or lost response requires retry with the original ID/payload. No write success is reported before commit. Exact response text is serialized once with web JSON options and emitted verbatim for both first acceptance and replay, including original balanceAfter.

Precision decision (supersedes the original typmod plan): C# decimal, maximum 99999999999999999999.99999999 (USD/EUR effective .99), USD/EUR scale 2, BTC 8. API strings reject excess fractional digits including trailing zeros, numeric JSON, signs, exponent, commas and whitespace. Domain Money rejects nonpositive amounts/cross-currency arithmetic/overdraft and bounds credits. Storage uses exact numeric with explicit range/quantization checks instead of numeric(28,8), because PostgreSQL typmod coercion would silently round a ninth BTC digit before CHECK constraints. Verified direct database bypass attempts fail. Reference: https://www.postgresql.org/docs/16/datatype-numeric.html. Numeric response amounts/balances use fixed-scale strings.

One-WIN-per-BET is an explicit simplification, not brief-mandated. Accepted WIN references an immutable BET matching provider/player/currency, with a self-FK, matching-BET database trigger and unique settlement reference. Rejected attempts never consume the settlement. History and committed outcomes have update/delete guards. Player reads derive identity solely from sub; admins use explicit any-player routes; provider/internal identities cannot use player/admin reads. Simple page/pageSize replaces the earlier cursor plan: page 1–10000, default 1; size 1–100, default 20; createdAt DESC/id DESC, with hasMore. New writes between pages may shift offset boundaries; no snapshot/export promise.

Development-only fixture: verified demo@miniwallet.test / Local-demo-password-123!, fixed player UUID de000000-0000-4000-8000-000000000001. Explicit DEV_FUNDING entries open USD/EUR 100.00 and BTC 0.01000000 once. Repeated migration does not reset/replenish or duplicate entries. Normal registrations still start at zero. No funding HTTP route or unreferenced WIN shortcut. Users seeds its identity/pending delivery; Wallet seeds its own fixture without reading Users tables. Wallet's migrator is now explicitly Development in Compose, consistent with Users' migrator.

Files: Domain Money/FinancialEntry/PlayerWallet; Application Transactions/WalletReads; Infrastructure TransactionStore/WalletReader/DevelopmentWalletFixture/WalletDbContext plus FinancialTransactions and FinancialGuards migrations/designers/snapshot; Wallet Transactions/Wallets controllers, Program, exception handler, SwaggerSetup/WalletContractFilter; Users Development player factory and demo seed/composition; Compose Development migration setting; MoneyTests/TransactionTests; scripts/verify-transactions.ps1; README/API_CONTRACT/DESIGN/AI_USAGE and this tracker. Existing unrelated template files remain untouched. Git is still an uncommitted initial workspace; nothing staged, committed or pushed.

| Actual command/check | Result |
| --- | --- |
| Inspect Git/brief/current Wallet code/solution and identity policies | PASS inspection; all existing work remains untracked in the previously initialized repository. |
| dotnet build MiniWallet.sln --no-restore | PASS initial implementation. Swagger filter edit later FAIL: read-only interface property + nullable warning; corrected. Final build PASS: zero warnings/errors. |
| dotnet ef migrations add FinancialTransactions / FinancialGuards | PASS; reviewed widening numeric migration and added immutable/matching/final-outcome SQL guards. EF emitted its generic possible-data-loss scaffold warning for the type alteration; the Up change widens numeric storage and retains explicit bounds. No destructive reset performed. |
| First complete PostgreSQL financial suite | FAIL: 48 passed, 2 failed. Lock timeout and injected serialization failure incorrectly surfaced as 500 through EF exception wrapping. Fixed error-chain classification. |
| Corrected full real PostgreSQL suite | PASS: 52 passed, 0 failed/skipped, process exit 0. Includes all prior stage tests, Money rules and new financial integration tests. |
| Strict external-ID parser review | Found .NET dollar-anchor final-newline allowance; changed to strict absolute anchors and added regression test. |
| dotnet test MiniWallet.sln --no-build --filter FullyQualifiedName~MoneyTests | PASS after final parser edit: 13 passed, 0 failures/skips. |
| Final complete Docker suite after strict parser regression | PASS: 53 passed, 0 failed/skipped, real PostgreSQL enabled, process exit 0; Docker Release build zero warnings/errors. |
| Final documentation/dependency checks | PASS: balanced Markdown fences; both Domain projects have no package, framework or project dependencies. Git status inspected; all initial workspace files remain untracked, nothing staged/committed. |
| docker compose config --quiet | PASS. |
| docker compose up --build -d | PASS: both image restore/publish and migration jobs exit 0; demo identity/opening entries seeded; both hosts started. |
| scripts/verify-transactions.ps1 | PASS exit 0: real provider BET 201, WIN 201, exact BET replay 201/original body after WIN, own history and admin read. Demo USD 100.00 -> 115.00. |
| Live Swagger JSON | PASS: provider operation bearer security and string-amount BET example present; complete precision/reference/retry/status/pagination descriptions published. |
| docker compose run --rm --no-deps wallet-migrate | PASS repeat after live operations; USD remains 115.00, EUR 100, BTC .01; exactly 3 DEV_FUNDING entries. |
| Physical process kill/TCP drop during commit, full database outage under financial load | NOT RUN: committed replay across independent hosts, transaction fault rollback and real lock timeout tested; destructive process/network fault injection remains a stage 5 review consideration, not fabricated evidence. |
| Clean-volume final submission walkthrough / rendered two-page design | NOT RUN: reserved for stage 6. |

### Current requirement-to-code/test evidence

| Requirements | Implemented code | Executed test evidence |
| --- | --- | --- |
| R6 nonnegative BET / multi-instance | Money, PlayerWallet.Bet, TransactionStore row lock | Different_concurrent_bets_never_overdraw_and_rejections_are_terminal; Identical_concurrent_requests_have_one_effect_and_one_original_response |
| R7 matching accepted BET / one settlement | PlayerWallet.Win, WinHandler, settlement unique index/matching trigger | Win_requires_accepted_matching_bet_and_one_settlement; Database_rejects_bypass_precision_negative_balance_unmatched_win_and_history_mutation |
| R8 single extensible transaction route | TransactionsController, SubmitTransaction, ITransactionHandler/BetHandler/WinHandler | Actual BET/WIN POST tests and deployed demo; add a handler/domain rule and schema type support for a new type, not another route/framework |
| R9 precision/bounds | Money/AmountText, numeric/check mapping | MoneyTests; Currency_precision_and_upper_bound_are_enforced_without_silent_rounding; direct database bypass test |
| R10 durable idempotency / atomic state | TransactionStore, unique outcome key, deferred finalization/immutability guards | Exact replay across hosts; simultaneous duplicates; changed wallet/currency conflict; distinct provider namespaces; unfinished outcome cannot commit |
| R11 rejected/transient semantics | Stored terminal 409, transaction disposal, exception-chain handling | Rejection replay after funding; Ledger_failure_rolls_back_balance_ledger_and_key_then_same_id_retries; Lock_timeout_is_retryable_without_a_reserved_key |
| R12 balances/history and page bounds | ReadWallets, WalletReader, WalletsController | Owned_reads_admin_access_page_bounds_and_machine_isolation; deterministic repeat page/nonoverlap; opening entries visible |
| R13/R14 principal access | Existing claim policies plus protected controllers | Player denied provider/admin routes, provider denied reads, admin any-player allowed; /me override ignored and subject enforced |
| R16/R19 Swagger and runnable docs | WalletContractFilter, API_CONTRACT, README, verification script | Deployed Swagger security/example inspection and live script |
| R23 explicit funding / zero registrations | Users/Wallet Development seeds, opening ledger | Development_fixture_is_explicit_idempotent_and_new_players_are_zero; live migrator rerun preserves post-transaction balance |
| R24 manual validation / persisted rules | Validators explicitly awaited; domain math and DB constraints/triggers | Invalid input/no key reservation and direct persisted precision/reference/immutability tests |

No coverage percentage claimed. One local Users worker remains the topology; no optional infrastructure added. Remaining stages: 5 focused correctness review/gap closure; 6 fresh-start verification/submission polish. Stage 4 elapsed estimate about 45–60 minutes, not stopwatch measured. Suggested commit: feat: add atomic idempotent wallet transactions and protected history. Stop for review; no stage 5/6 work started.

## Stage 5 — focused correctness review and regression coverage

Scope completed without product features or new infrastructure. Read the brief and inspected existing tests, transaction/verification storage, authorization, error handling and contract before edits. Added 21 focused cases (13 domain/application cases and 8 real-PostgreSQL cases) on top of the 53-test baseline. Existing assertions were retained. TransactionTests and UsersIntegrationTests are partial solely to keep their review additions in small separate files, reusing existing fixtures/helpers. No production domain rules, access policies, endpoints or schema were relaxed.

Confirmed correctness fix: Users exception handling recognized direct PostgreSQL/DbUpdateException errors but missed EF's outer InvalidOperationException for transient database failures. A new PostgreSQL-trigger registration test first reproduced 500 instead of required retryable 503. The production fix walks the error chain and recognizes the transient SQLSTATE cases, consistent with Wallet's handler. The unchanged regression assertions now verify 503 application/problem+json + Retry-After: 1, zero user/outbox/mailbox effects after rollback, and successful same-email retry with one pending delivery. This is an HTTP retry-semantics fix, not a registration replay framework or new feature.

Files changed: MiniWallet.Tests/FinancialRulesAndValidationTests.cs, TransactionReviewTests.cs, UsersRecoveryTests.cs (new); TransactionTests.cs and UsersIntegrationTests.cs (partial declarations only); MiniWallet.Users.API/UseCaseExceptionHandler.cs (transient classification fix); README.md, AI_USAGE.md and this tracker. Existing application and infrastructure behavior otherwise remains intact. No package or migration changes.

### Stage 5 checklist → actual evidence (all PASS in final suite)

| Requested check | Test/scenario evidence |
| --- | --- |
| 1 Registration/verification, invalid/duplicate email, expired/reused token | UsersIntegrationTests existing PostgreSQL cases run again; UserDomainTests. Added Registration_transient_failure_is_retryable_and_never_partially_accepted verifies atomic failure and caller retry. |
| 2 Three automatic zero wallets, duplicate/restart/outage recovery | Existing ProvisioningTests run again against PostgreSQL. Also reran scripts/verify-provisioning.ps1 against real containers: normal registration, replay, Wallet stopped, Users restarted with same pending event, Wallet restored and automatic completion. |
| 3 BET/WIN, exact-balance BET, insufficient funds without state/history mutation | Exact_balance_bet_and_positive_win_are_domain_rules; Exact_balance_acceptance_then_insufficient_funds_preserves_entire_history compares full financial-entry ID sequence before/after rejection and asserts zero balance/no rejected entry; existing success tests. |
| 4 WIN matching and one settlement | Win_handler_rejections_do_not_change_balance covers missing/wrong provider/player/currency/settled in domain/application; existing PostgreSQL matching/concurrent settlement tests. Database_unique_settlement_survives_application_guard_bypass asserts actual PostgreSQL unique constraint name/SQLSTATE through EF and no second credit. |
| 5 Amount/precision bounds and unsupported currency | Existing MoneyTests/financial boundary PostgreSQL checks; added application contract cases for unsupported/lowercase currency, unsupported type and invalid references/identity. Unsupported_currency_and_page_error_contracts_do_not_reserve_keys asserts HTTP 400 and no transaction key. |
| 6 Exact replay, changed payload, original result after later activity | Existing simultaneous duplicates/canonical replay/cross-wallet conflict tests run again. Response_loss_after_commit_then_host_restart_replays_one_financial_effect checks stored response text against retry after the first host is disposed. |
| 7 Two BETs of 80 against 100 and duplicate calls | Two_wallet_hosts_racing_bets_of_80_against_100_leave_20 uses distinct Wallet WebApplicationFactory service providers/HTTP scopes against the same PostgreSQL database, with a test start gate. Asserts one 201, one 409 insufficient_funds, balance 20, one BET entry and two terminal outcomes. Existing identical-request test also spans two hosts. |
| 8 Player A/B isolation, admin/machine privileges, invalid/missing/expired JWT | Player_A_cannot_read_B_and_actual_endpoints_enforce_token_failures uses two persisted verified Users identities, distinct balances/history, real Users-issued tokens, actual read/write routes and validated expired JWT. A cannot read B; /me override cannot leak B; admin sees B; player writes/provider reads fail. Existing policy/credential tests retained. |
| 9 Failure rollback and response loss/restart | Existing real PostgreSQL outcome-trigger failure + lock timeout tests prove balance/history/key rollback and same-ID retry. New response-loss handler discards the 201 after the receiver committed and throws to the caller; first host is disposed before new-host retry. This is focused host-lifetime/transport simulation, not a physical OS kill or claim of a real TCP cut. |
| 10 Response/pagination/error contracts | Existing fixed-scale balances/history/deterministic repeat pages tests plus exact zero response; new page lower/upper bound, empty-page/hasMore checks and Problem Details media type/status/type/code/title/traceId assertions across 400/401/403/404/409. Existing rollback tests exercise 503; new registration regression checks its error code/header/body. |
| PostgreSQL migrations/independent connections | Every PostgreSQL fixture migrates a fresh randomly named database. Migrations_match_models_and_repeat_safely_on_independent_connections uses separate DbContexts/connections concurrently, checks no pending migrations/model drift and applied FinancialGuards; also checks Users model/migrations. |
| Explicit asynchronous validation | Asynchronous_validation_rejects_before_reserving_persistence uses a genuinely async rule and a unit-only persistence sentinel. The sentinel is not used to claim lock, transaction or PostgreSQL correctness. |

All concurrency/database evidence uses Npgsql/PostgreSQL with independent request-scoped or explicit DbContexts, never EF InMemory or a mocked database. Database trigger faults are confined to disposable test databases and removed in finally blocks. Existing application databases remain preserved. Test-only policy probes/response-loss handlers are not included in API publish output.

### Actual commands/results

| Command/check | Actual outcome |
| --- | --- |
| git status --short; read brief/current code/tests/contracts | PASS inspection. Entire initial repository remains untracked; no baseline commit exists, so git diff is not claimed as a complete content review. |
| dotnet build MiniWallet.sln --no-restore | PASS: zero warnings/errors with added review tests. |
| dotnet test MiniWallet.sln --no-build --filter FullyQualifiedName~FinancialRulesAndValidationTests | PASS: 13 passed, 0 failed/skipped. |
| docker compose -f compose.yaml -f compose.tests.yaml config --quiet | PASS. |
| Initial expanded full Docker/PostgreSQL suite | PASS: 73 passed, 0 failed/skipped. Review afterward identified missing Users transient-failure coverage; this passing run was not treated as proof of that untested behavior. |
| Docker test runner with --filter FullyQualifiedName~Registration_transient_failure_is_retryable | FAIL before fix: 1 failed, expected 503 but received 500. Existing assertions were not removed or weakened. |
| docker compose -f compose.yaml -f compose.tests.yaml run --build --rm -e Logging__LogLevel__Default=None -e Logging__LogLevel__Microsoft.AspNetCore=None tests | PASS after fix: restore and Release build succeeded with zero warnings/errors; 74 passed, 0 failed/skipped, process exit 0. |
| ./scripts/verify-provisioning.ps1 | PASS exit 0: all five live registration/duplicate/outage/restart/recovery checks; Wallet restored in finally. |
| docker compose up --build -d users | PASS: Users image restored/published, migration job exited 0 without data reset, corrected Users host started. |
| Final readiness/documentation checks | PASS: Wallet 5080 and Users 5081 readiness each 200; PostgreSQL healthy; migration jobs exit 0; Markdown fences balanced. |

All ten requested verification areas have available passing evidence. No current required check is blocked by unavailable tooling. Do not count an ordinary host dotnet test without TestPostgres as PostgreSQL evidence: it intentionally skips those cases. Reproduce the full available suite using the Docker command above, or configure TestPostgres explicitly for a host run against service-owned test database roles.

Remaining gaps: stage 6's fresh-volume one-command reviewer walkthrough and final rendered-design/submission polish are NOT RUN in this stage. A separate Compose project can exercise fresh volumes without deleting the current data: stop the current Users/Wallet hosts to free their fixed loopback ports, then set COMPOSE_PROJECT_NAME=miniwallet-fresh for both docker compose up --build -d AND the documented verification scripts (their internal Compose commands must target the same project). Stop that fresh project, unset COMPOSE_PROJECT_NAME and restore the original hosts afterward. Execute this as the stage 6 review, not as a claimed completed check here. Physical process kills at exact commit boundaries and a load-testing platform were intentionally not pursued; focused transaction-fault, response-loss and restart scenarios satisfy this stage's requested scope. No coverage percentage or exhaustive-fault claim.

Elapsed estimate: roughly 20–30 minutes (not stopwatch measured). Next stage: 6 fresh-start verification and final submission documents, only when supplied. Suggested commit: test: strengthen correctness checks and fix registration retries. No staging, commit, push, history rewrite or unrelated changes.

## Stage 6 — final audit, isolated fresh run and submission documents

Scope: brief-required core only. Inspected brief/source/configuration/packages/dependency graph and actual prior verification before polishing. Removed unused WeatherForecast controller/model, replaced its .http request with real health/registration requests, removed unused UserSecretsId, and simplified HTTP launch profiles to the documented ports. Retained packages all serve runtime, migration tooling or tests; explicit EF/JWT pins are intentional compatibility choices. No new runtime package, service, product endpoint or optional infrastructure added.

Startup correction from audit: the PostgreSQL image's docker-entrypoint.sh confirms its temporary initialization server has listen_addresses='' and no TCP listener. A socket-only pg_isready could therefore report readiness during initialization. Compose now checks TCP 127.0.0.1 before launching migration jobs. This risk was found by inspection, not claimed as an observed failed startup. Optional published-port variables preserve default one-command behavior and allow isolated review. Hardened .gitignore/.dockerignore to exclude Git metadata, user settings, actual local credential files and ignored QA artifacts; checked-in defaults remain explicitly local-only.

### Isolation and actual reviewer run

Copied 109 Git-visible source/documentation files into ignored .review/stage6-2528b87d/source. No .git, .vs, bin, obj, user files or untracked .env secrets were used. Unique project miniwallet-review-f7c29c33, loopback ports 53336/53337 and new volume miniwallet-review-f7c29c33_postgres-data isolated it from the existing stack. A temporary Compose override changed image tags only; ports/project name were process environment settings. Application commands, initialization SQL, migrations and configuration defaults were the submitted source. No host dotnet command, manual SQL or external account was needed to start it.

From that source-only directory, docker compose up --build -d succeeded: new network/volume, both databases/owners initialized automatically, both migration jobs exit 0, both API hosts ready. scripts/verify-reviewer.ps1 checked both Swagger HTML/UIs/OpenAPI documents, registered a new player, retrieved the verification message with the reviewer header, consumed it, obtained a player token and observed exactly USD/EUR/BTC zero wallets. It obtained the separate provider token, used only the funded demo for BET 10/WIN 25, replayed the original BET body exactly, inspected opening/transaction history and balance 100 -> 115, demonstrated admin access and denied cross-player/provider reads.

Then docker compose restart postgres users wallet was executed in that disposable project. The script's -CheckPersistence mode re-authenticated the verified normal/demo accounts and checked the same player ID, three zero balances, demo USD 115 and both committed transaction IDs. All passed. The state file contains only review email/IDs/balance, no JWT/password. The in-memory mailbox was not assumed durable; verified account state and hashed token lifecycle are in PostgreSQL.

The final full test command in the isolated source copy was:

docker compose -f compose.yaml -f compose.override.yaml -f compose.tests.yaml run --build --rm -e Logging__LogLevel__Default=None -e Logging__LogLevel__Microsoft.AspNetCore=None tests

PASS: anonymous package restore, Release build with zero warnings/errors, 74 xUnit tests passed, 0 failed/skipped, exit 0. Tests created/dropped only their own randomly named PostgreSQL databases inside the isolated server. The source copy had no prebuilt host artifacts. A hash comparison confirmed 96 source/config/script/build files matched the reviewed working source exactly.

### Required artifacts and architecture audit

| Brief requirement | Final implementation / evidence |
| --- | --- |
| Two C#/ASP.NET services, independent models | Nine-project inward graph audited: Domain has no references/packages; Application -> own Domain; Infrastructure -> own Application; API -> own Application/Infrastructure; consolidated tests intentionally reference both. |
| Registration/email verification | Users use cases, normalized unique email, password/token hashes, locked consumption/resend; real PostgreSQL tests and fresh HTTP walkthrough. |
| Automatic zero wallets | Atomic pending User event, single retrying HTTP worker, authenticated Wallet inbox plus three wallet inserts; full fresh flow and restart evidence; prior live outage scenario retained. |
| One extensible provider BET/WIN route | Focused handlers, Money/Currency rules, matching accepted BET and durable one-settlement assumption; fresh demo and concurrent PostgreSQL tests. |
| Concurrent/outage-safe balance/history | Unique outcome reservation, FOR UPDATE, atomic balance/immutable ledger/replay commit, terminal rejection semantics, transient rollback/retry; two-host 80+80 test, duplicate/race, rollback/response-loss and lock-timeout tests. |
| Player/admin/provider boundaries | Verified sub ownership, explicit admin routes, distinct provider/internal audiences/scopes; fresh cross-player/admin/provider checks and auth regression suite. |
| Explorable/documented API | Both Swagger UIs, JWT Authorize metadata/examples, full API_CONTRACT; actual string money/status/error/page shapes exercised. |
| One-command Docker/Compose | PASS from clean source and a newly created volume; automatic initialization/migrations; ordinary restart persistence verified without deleting existing user volumes. |
| README | Final prerequisites/defaults/URLs/credentials, token/email flow, API examples, fixture funding, retry semantics, test commands/rationale and short troubleshooting. |
| DESIGN <=2 pages | Final 727-word Markdown rendered to a temporary Letter PDF, 11pt body/1in side margins: exactly 2 pages. Both PNG pages visually inspected with legible unclipped text/table. PDF/renderer are QA only, not a runtime/submission dependency. |
| AI_USAGE | Final document has five actual representative user interactions, real tools/mistakes/overrides/trade-offs and actual verification. Scope explicitly excludes unobserved human/tool activity; no invented details or missing required human facts. |
| Git/source/history | Git status/diff/staged status inspected. No HEAD: ordinary diff cannot represent untracked source. Candidate inventory, no-index addition inspection and source/hash/secret-pattern audits used instead; no fabricated commit history or push. Factual initial commit remains the user's review/submission step. |
| Optional work | SignalR, broker, Redis, gateway, Kubernetes and other production enhancements intentionally not added. |

Finalized only the required README/DESIGN/AI record plus this tracker; added scripts/verify-reviewer.ps1 as a runnable version of the required walkthrough. Existing verification scripts remain available. The root uploaded brief copy was preserved as unrelated input; docs/candidate-brief.md remains authoritative.

### Final check log and cleanup

| Check | Actual result |
| --- | --- |
| Dependency/project audit | PASS: inward service-owned references; no EF/ASP.NET dependency in Domain; no shared entity/context project. |
| Source credential-pattern audit | PASS: no private-key, AWS-access-key or GitHub-token pattern matches in candidate source; known Development defaults intentionally included/documented. Not a claim of a formal security scan. |
| compose config + up --build from clean copy | PASS; isolated startup/automatic migrations complete. |
| Reviewer script then ordinary restart/persistence mode | PASS exit 0 for complete required flow and retained data. |
| Isolated full Docker/xUnit/PostgreSQL suite | PASS: 74 passed, 0 failed/skipped, zero build warnings/errors. |
| DESIGN print QA | PASS: 2 pages, both images inspected. Poppler emitted font fallback warnings; rendered body/table text remained legible and unclipped. |
| Git no-index check tooling | Initial checks were interrupted by global CRLF warnings/check exit handling. No product/test failure inferred; read-only normalization override/no-index addition inspection completed. Ordinary git diff/staged diff are empty because files are untracked, not because there are no changes. |
| Final source snapshot hash comparison | PASS: 96 runtime/source/config/script files equal to the isolated tested copy. |
| Disposable cleanup safety check | Initial Go-template label quoting failed and halted before any deletion. Corrected JSON volume inspection verified the unique project label before cleanup. |
| Disposable cleanup | PASS: removed only miniwallet-review-f7c29c33 containers/network/new volume. Existing miniwallet_postgres-data retained the same name/creation identity (2026-10-28T13:02:36Z); original Wallet 5080 and Users 5081 readiness both 200 afterward. |
| Commit/history publication | NOT RUN: no commits exist and the working agreement requests a suggested commit/review, not reconstructed history or a push. No real staged history is claimed. |
| Exhaustive physical commit-time kill/TCP/load experiments | NOT RUN: intentionally outside the brief's focused core; existing fault/response-loss/restart tests provide bounded evidence. |

No failed functional check remains unresolved. Known limits are explicit: Development-only issuer/mailbox and public local sample secrets; mailbox loss recovered by authenticated resend; one local Users worker; one successful WIN per BET; bounded offset pagination may shift under new writes; assessment-lifetime retention and no production monitoring/backups/identity platform. No exact-once delivery, production readiness or coverage percentage is claimed.

Remaining submission issue: reviewed source is uncommitted, so create one truthful current initial commit before a Git-link submission, or use the brief's zip option. Do not fabricate retrospective stage commits. No external repository was created/pushed and no submission was sent. All functional core requirements and required documents are complete; optional work requires a separate request.

Stage 6 elapsed estimate: roughly 30–45 minutes, not stopwatch measured. No next implementation stage is required. Suggested final commit: feat: complete MiniWallet assessment core and verified local setup.
