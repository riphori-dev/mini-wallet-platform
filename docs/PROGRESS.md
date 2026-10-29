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


## SMTP verification email (requested enhancement)

Implemented MailKit 4.18.2 SMTP behind the existing Users Application IEmailSender port. Domain, token hashing/expiry/atomic consumption and registration/outbox transactions are unchanged. Infrastructure owns SMTP configuration, message composition, bounded async sending and a Development preview decorator. The plain-text message includes a token, API request body, UTC expiry and resend guidance; it deliberately has no nonfunctional verification link. SMTP success means server acceptance only. A failed QUIT after acceptance does not reverse that result. Logs omit tokens, credentials and server response bodies.

Compose now starts healthy Mailpit v1.31 with a persistent inbox volume and host-loopback SMTP/UI ports 1025/8025. Users connects to mailpit inside Compose and localhost in local Development. Existing Swagger/API-explorer mailbox previews and reviewer scripts still work after SMTP acceptance. Outside Development, SMTP must be explicitly configured and requires StartTls or SslOnConnect; plaintext mode is Development-only. SMTP credentials remain optional, validated as a pair, and untracked when supplied. No external accounts, automatic email retry worker or durable plaintext mail queue added. Registration still returns committed 201 plus unavailable on send failure; authenticated resend rotates the token and recovers after SMTP returns.

Requirement-to-code/test map:

| Requested behavior | Implementation | Evidence |
| --- | --- | --- |
| Actual SMTP verification delivery | SmtpEmailSender.cs, Users Program.cs, appsettings.Development.json | SmtpVerificationTests: registration -> actual Mailpit message -> verify -> login; hash-only PostgreSQL storage |
| Useful verification message | SmtpEmailSender plain-text MIME message | SmtpEmailTests: sender, recipient, subject, token, expiry, API instructions |
| Failure/recovery without losing registration | Existing UserUseCases acceptance/resend boundary plus bounded SMTP sender | Real PostgreSQL/Mailpit outage -> 201 unavailable -> failed resend 503 -> recovered resend -> verify; one player/outbox |
| Safe transport settings and preview | VerificationEmailSetup and DevelopmentSmtpEmailSender | TLS/config validation, rejection leaves no preview, cancellation/timeout, SMTP acceptance followed by lost QUIT |
| One-command startup and review | compose.yaml, compose.tests.yaml, docs/SMTP.md, README/API contract/Swagger text | Isolated Compose build/start, reviewer walkthrough and browser inbox inspection |

Actual commands/results:

- PASS `dotnet add MiniWallet.Users.Infrastructure/MiniWallet.Users.Infrastructure.csproj package MailKit`: resolved stable 4.18.2 and restored compatible net8.0 dependencies.
- Initial full-suite build FAIL: existing folder refactor omitted interface/validator imports in two test files. Added only the missing imports. Initial executable suite FAIL: 16 tests still used removed registration route /api/v1/players; existing controller uses /api/v1/register. Updated registration calls in tests, walkthrough scripts and docs, without changing the controller or admin /players/... routes. An SMTP nullable warning and new xUnit assertion warning were fixed before final runs.
- PASS host `dotnet test MiniWallet.Tests/MiniWallet.Tests.csproj --no-restore -v minimal`, with TestPostgres pointing to the existing local PostgreSQL server on port 55432 and TestMailpitHttpUrl=http://127.0.0.1:8025: **84 passed, 0 failed, 0 skipped**. PostgreSQL fixtures create/drop their own test databases; application databases are preserved.
- PASS `docker compose config --quiet` and final `git diff --check`.
- PASS isolated `docker compose -p miniwallet-smtp-review up -d --build`, with WALLET_PORT=5280, USERS_PORT=5281, MAILPIT_UI_PORT=8125, MAILPIT_SMTP_PORT=1125. Fresh project volumes initialized automatically; migrations exited successfully and both hosts started.
- PASS `./scripts/verify-reviewer.ps1 -UsersUrl http://127.0.0.1:5281 -WalletUrl http://127.0.0.1:5280 -StatePath .review/smtp-review-state.json`: Swagger/readiness, registration/verification/three zero wallets, demo BET/WIN/replay/history, admin access and player/provider isolation. Confirmed the actual SMTP message in Mailpit's API and browser inbox.
- PASS isolated `docker compose -p miniwallet-smtp-review -f compose.yaml -f compose.tests.yaml run --build --rm tests` with the same port overrides: Release solution build had zero warnings/errors; **84 passed, 0 failed, 0 skipped**, including real SMTP/PostgreSQL tests.
- PASS ordinary Mailpit restart: 3 inbox messages before and after restart. Browser confirmed rendered sender, recipient, token, UTC expiry and POST verification instructions.
- Cleanup: removed only miniwallet-smtp-review containers/network and its two newly created volumes. Original miniwallet PostgreSQL container/volume retained; the requested local Mailpit remains healthy on 1025/8025.
- NOT RUN external authenticated SMTP/TLS handshake or Gmail/Outlook delivery: no external account selected or credentials supplied. Local Mailpit captures mail; it does not deliver to internet recipients. TLS configuration is validated, but real provider delivery needs that provider's credentials and authorized sender.

Files: new SMTP sender/setup, two SMTP test files and docs/SMTP.md; updated Users composition/settings/package/Swagger, Compose/test Compose, .dockerignore, README/API contract/design/AI usage/progress and stale registration callers. Existing UI, domain folder refactor, launch settings and Wallet changes preserved; two test files received imports needed by that refactor. No changes to financial behavior or public funding. Estimated effort: 15–20 minutes. No commit or push performed. Suggested commit: `feat: send verification emails through SMTP with local Mailpit`.

## Client brief comparison review

Analysis only; implementation unchanged. The supplied Downloads/candidate-brief.md and canonical brief have identical SHA-256 hashes. Added docs/BRIEF_REVIEW.md with requirement-to-implementation/test mapping, absent optional features, concrete REFUND/BONUS/JACKPOT extension points, SignalR multi-instance considerations, and submission priorities. No missing required product feature identified in the inspected/tested working tree. Future transaction types require validator/domain/database changes; a new-handler mutation followed by business rejection needs explicit rollback/restore testing before extensions are enabled. No SignalR or new transaction kinds implemented.

PASS fresh `dotnet test MiniWallet.Tests/MiniWallet.Tests.csproj --no-restore -v minimal` with TestPostgres at localhost:55432 and TestMailpitHttpUrl=http://127.0.0.1:8025: **84 passed, 0 failed, 0 skipped**. Inspected migrations, authorization, delivery, money, dependency direction, Swagger, Compose and submission documents. Existing working-tree changes preserved. NOT RUN again: clean-clone/fresh Compose startup, final DESIGN PDF pagination, React build or external SMTP delivery; prior results remain historical. Current Git state includes untracked moved Domain/Application/UI/SMTP work, so final submitted-commit verification remains necessary. Estimated review effort: 10–15 minutes. No commit or push performed. Suggested documentation commit: `docs: compare implementation with client brief and roadmap`.

## 2026-10-29: Single-instance live player balances

Added Wallet `/hubs/balance`, protected by the verified-player read policy. Server-owned groups use the validated player GUID. Successful BET/WIN commits send a best-effort `BalanceChanged` currency invalidation; rollback, rejection and idempotent replay do not send it. Notification dispatch has a two-second bound and cannot change the committed financial response. Users needs no changes. No backplane, broker or durable notification queue was added.

Connected the existing demo game and player explorer to the SignalR browser client, enabled WebSocket forwarding in Vite, and added authoritative REST balance refreshes on connect, reconnect and notification. Serialized reads coalesce notifications arriving during a refresh. Manual refresh remains available. History and admin views retain manual refresh behavior. See REALTIME.md for token handling, startup, a two-tab walkthrough and single-instance limitations.

Verification:
- `docker compose -f compose.yaml -f compose.tests.yaml run --build --rm --no-deps tests --logger 'console;verbosity=quiet'`: Release solution build succeeded with zero warnings/errors; all 87 tests passed, zero skipped. Test fixtures use disposable databases on the existing local PostgreSQL container. Includes real WebSocket authorization and owner isolation, multiple owner connections, committed balance observed from a separate context, no notifications on rollback/replay/rejection, WIN notifications and successful writes despite dispatch failure.
- `npm run build` in MiniWallet.UI passed. Rollup emitted harmless annotation-removal notices from the SignalR dependency.
- `git diff --check` passed.

Local Windows test execution could not connect to its testhost within the runner timeout; verification therefore used the successful Docker suite. Initial sandbox dependency downloads failed due to network restrictions; the SignalR dependency was installed successfully with authorized network access. An initial Docker invocation mistakenly repeated the image entrypoint and was corrected before running the successful checks.

No running API container was replaced and no browser walkthrough was performed for this change. Rebuild/restart Wallet to activate the hub. The earlier two-page DESIGN.md rendering check was not repeated after updating the architecture wording. Live delivery is best effort for one Wallet instance; reconnect/manual refresh recovers missed notifications. Initial provisioning and development opening credits do not emit notifications.

## 2026-10-29: Minimal load-balanced Compose deployment

User authorized adding the load balancer after discussing the brief's multi-instance constraint. The default single command remains `docker compose up --build`. The wallet service is now Nginx 1.28-alpine at 5080; wallet-a/wallet-b use the same Wallet image/database and wait for the existing one-shot migrator. Users calls the proxy without application changes. Nginx uses round-robin routing, Docker DNS refresh, WebSocket upgrade forwarding, passive failure handling, bounded connect/read timeouts and an X-Wallet-Upstream diagnostic header. POST retries after sending are not enabled; application errors are not automatically retried. Access logging is disabled to avoid ordinary access-log exposure of query tokens.

SignalR remains intentionally process-local. Port 5082 directly targets wallet-a: both the WebSocket and provider writes must use that port for a reliable single-instance live demonstration. No Redis or other backplane was introduced. Added docs/LOAD_BALANCER.md, a reproducible financial verification script and actual run/credential instructions at the top of README while retaining the user's earlier narrative. Updated design/realtime/AI usage documentation.

Verification used isolated Compose project miniwallet-lb-check with fresh named volumes and ports 5180/5181/5182/8125/1125; the existing stack/data were not replaced:
- PASS `docker compose config --quiet`.
- PASS isolated `docker compose -p miniwallet-lb-check up -d --build`: both APIs published, migrations completed, two Wallet hosts, Users, Nginx, PostgreSQL and healthy Mailpit started.
- PASS `scripts/verify-load-balancer.ps1 -UsersUrl http://127.0.0.1:5181 -WalletUrl http://127.0.0.1:5180`: observed upstreams 172.19.0.5:8080 and 172.19.0.4:8080; concurrent 80+80 reached different hosts against USD 100, exactly one 201 and one 409, final USD 20; exact replay through the other host returned the original body without further debit.
- PASS existing verify-reviewer.ps1 against proxy: both Swagger UIs/OpenAPI/readiness, registration -> captured verification -> login -> three zero wallets, demo BET/WIN/replay/history, admin access/player isolation/provider read rejection.
- PASS stopping only wallet-b: four readiness requests returned 200 via surviving wallet-a (one transparently retried failed connection). wallet-b restarted successfully.
- PASS restarting wallet-a, wallet-b and proxy then reviewer CheckPersistence: verified account, zero wallets, demo balance and accepted transaction history survived.
- PASS git diff --check.

No new application financial logic/schema was changed for the load balancer; the earlier 87-test suite was not rerun in this configuration-only follow-up. No browser walkthrough, DESIGN PDF pagination check, production deployment, or cross-instance SignalR delivery claim. Nginx/PostgreSQL are single points of failure in this local example. The isolated verification containers are stopped/removed after verification; their volumes are retained. No commit or push performed.

## 2026-10-29: Minimal request auditing and structured logging

User requested a practical audit/log mechanism without overengineering. Added Shared/Auditing/AuditTrail.cs as linked source in each API (no service assembly coupling or new packages). Both APIs register an independent-connection PostgreSQL audit writer and outer request middleware that observes handled errors/auth failures. Each database has an explicit SQL-owned AuditEvents migration with timestamp index; the table is not a domain entity. Existing ledger/outcome persistence is unchanged except an internal IsReplay response flag for safe request metadata. Controller metadata records transaction/player references, authenticated login/client identity, registration delivery status and internal event identity. Exception handlers provide error codes. Built-in JSON console output includes service/instance/trace scopes; Compose rotates application logs at 10 MB x 3 files per container.

Audit coverage is completed /api, /internal and /dev HTTP actions. Excludes probes, Swagger, static files, SignalR lifetimes, request/response bodies, raw URLs/query strings, headers, emails, passwords and tokens. Anonymous verification/resend/failed login records cannot identify an unverified actor. Audit inserts are best effort after the request handler, bounded to two seconds, independent of request cancellation, with safe console AuditFallback on failure. They are not atomic with business commits; crashes/outages can leave missing audit rows. No automatic backfill, retention worker, tamper-proof archive or audit UI added. Financial ledger and durable transaction outcomes remain authoritative. Added docs/AUDITING.md with inspection/manual checks, limits and activation; README and AI_USAGE link/explain it.

The current working tree also contained incomplete namespace imports from the existing folder refactor (moved DbContext, EmailAddress, application contracts/services, mailbox and UserStore references). Initial builds failed on those references before audit verification. Corrected imports/global usings and explicit test type references without moving files back or changing domain rules. One transient test import ordering error was corrected. Existing refactor and unrelated work preserved.

Verification:
- PASS local `dotnet build MiniWallet.sln --no-restore -m:1 -v:q`: zero warnings/errors after import repair.
- PASS full final `docker compose -f compose.yaml -f compose.tests.yaml run --build --rm --no-deps tests --logger 'console;verbosity=quiet'`: Release solution build zero warnings/errors, **92 passed, 0 failed, 0 skipped**, ~8 seconds. Five added cases cover rejected authentication with safe metadata/trace correlation/probe exclusion, failed audit writer preserving responses, persisted user login without email/password/token, durable committed/replay/business-rejection records, and audit outage preserving committed BET/replay/balance.
- PASS `docker compose config --quiet` and `git diff --check`.
- PASS upgraded prior isolated miniwallet-lb-check databases on ports 5180/5181/5182/8125/1125 with `up -d --build`: both audit migrations completed and both APIs started. Existing demo funds/history retained (reviewer flow USD 35 -> 50).
- PASS existing reviewer walkthrough via Nginx: Swagger/readiness, registration/mail/verification/login, automatic zero wallets, BET/WIN/replay/history, admin/player/provider access boundaries.
- PASS read-only PostgreSQL inspection: safe Wallet status/replay summaries and Users audit records persisted.
- PASS idle API restart retained **13 Wallet audit rows and 8 Users audit rows**. Removed only isolated containers/network afterward; retained volumes. Normal running stack was not replaced.

Not performed: browser walkthrough, final DESIGN page render, production log collector/retention configuration, clean committed checkout or commit/push. Activate in the normal stack with `docker compose up -d --build`; the existing one-shot migrators apply audit tables first. This is a lightweight best-effort operational request trail, not a claim of complete, tamper-proof financial/security auditing.
