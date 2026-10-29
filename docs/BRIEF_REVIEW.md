# Client brief comparison

Reviewed against the supplied Downloads/candidate-brief.md and the current working tree. Its SHA-256 matches docs/candidate-brief.md. This is an assessment of the implementation, not an instruction to add the brief's optional features.

## Overall assessment

The required backend behavior is implemented, and the current full test run passes 84 tests with no failures or skips, including real PostgreSQL and SMTP. No missing required product feature was identified in this review. This is evidence for the tested scenarios, not a production-readiness certification.

The main qualifications are: wallet provisioning is eventual; requests not durably accepted require caller retry; future transaction types need coordinated changes beyond a new handler; and substantial changes remain uncommitted/untracked. A reviewed working tree is not yet evidence that the final submitted commit contains everything.

## Requirement comparison

| Client requirement | Current implementation and evidence | Assessment |
| --- | --- | --- |
| .NET 8+, ASP.NET Core | Both controller APIs target net8.0; separate Domain/Application/Infrastructure layers | Implemented |
| Register and verify email | Users UserUseCases/UserStore, normalized unique email, password hashing, expiring hashed single-use token, credential-authenticated resend; UsersIntegrationTests/SmtpVerificationTests | Implemented |
| Automatic zero USD/EUR/BTC wallets | Atomic user/outbox commit; hosted delivery worker; PlayerProvisioner transaction with inbox and unique player/currency; ProvisioningTests | Implemented, eventual during outages; no extra player call |
| One provider transaction endpoint | POST /api/v1/transactions with BetHandler/WinHandler behind ITransactionHandler | Implemented |
| BET never overdraws | PlayerWallet.Bet/Money.Debit, locked wallet row, database checks; concurrent and exact-balance tests | Implemented |
| WIN for an earlier BET | Matching provider/player/currency checked in domain and database trigger; unique settlement reference | Implemented; one WIN per BET is our documented assumption |
| Balances and history | Own/admin controller routes, fixed-scale amount strings, deterministic bounded page/pageSize reads | Implemented; offset pages can shift during new writes |
| Players own-only; admin any player | JWT subject-derived owner and distinct policies in WalletAuthorization; endpoint isolation tests | Implemented |
| Separate machine provider authentication | Configured client credentials issue provider-kind JWTs with restricted scope; separate internal audience/client | Implemented; not player login |
| Several Wallet instances | PostgreSQL key reservation plus SELECT FOR UPDATE and atomic ledger/balance/outcome commit; two-host race test | Implemented and tested; default Compose runs one Wallet instance, not a load-balanced deployment |
| Service outages without lost/duplicate accepted work | Durable registration outbox/inbox and retained retries; durable provider outcomes with exact replay; rollback/restart/lost-ack tests | Implemented within acceptance/retry boundaries below |
| Documented/explorable API | Swagger UI, XML comments, examples, JWT instructions, Problem Details, contract/pagination/retry docs; FoundationTests | Implemented |
| JWT through local provider or documented dev issuer | Development-only issuer within Users; verified user identities and configured admin/provider credentials | Implemented using the explicitly permitted dev-issuer alternative |
| One-command local backend startup | Compose starts PostgreSQL, migrations, Mailpit and both APIs | Implemented; separate React demo is optional and currently starts with npm |
| Tests and rationale | xUnit domain/application, real PostgreSQL and SMTP, concurrency/authorization/recovery tests; README and progress | Implemented; fresh review run: 84/84 |
| README, DESIGN <=2 pages, AI_USAGE, Git | All documents and three commits exist; detailed verification recorded | Present; final DESIGN pagination and final committed snapshot still need submission review |

Code evidence is in MiniWallet.Users.Application/UserUseCases.cs, MiniWallet.Users.Infrastructure/UserStore.cs and RegistrationDeliveryWorker.cs, MiniWallet.Infrastructure/PlayerProvisioner.cs and TransactionStore.cs, MiniWallet.Domain/Entities, MiniWallet.API/WalletAuthorization.cs and Controllers, and MiniWallet.Tests. See PROGRESS.md for prior isolated Compose walkthrough evidence.

## Missing features versus optional scope

| Capability | Present? | What the brief actually asks |
| --- | --- | --- |
| SignalR live balance pushes | No hub, mapped hub route or SignalR client subscription found | Explicitly optional |
| REFUND transactions | Not accepted; currently rejected by validation | Roadmap example; design for adding it, not implement it now |
| BONUS transactions | Not accepted | Same roadmap expectation |
| JACKPOT transactions | Not accepted | Same roadmap expectation |
| Production OAuth/OIDC deployment | No; development issuer provided | Dev issuer explicitly allowed |
| Internet email delivery | Configurable SMTP sender exists; local Mailpit capture verified, external credentials/delivery untested | Email verification required; a particular external email provider is not required |
| Deposits, withdrawals or public top-up | No; separate development fixture has explicit funding ledger entries | Not requested |
| FX conversion, real blockchain transfers, odds engine, jackpot pool management | No backend implementation | Not requested; BTC is a wallet denomination here |
| Real-time game feed or production React site | No; React betting simulation is a local demonstration | No frontend required |
| Redis, broker, gateway, Kubernetes, monitoring stack | No | No specific production infrastructure required |

SMTP/Mailpit, XML documentation, health checks and the React demo are already additions beyond the minimum. A frontend refresh after an API response is not SignalR server push.

## Readiness for new transaction types

The reusable core is useful: one endpoint, typed amount parsing, provider identity from claims, handlers, wallet locking, durable identity, original-response replay and immutable ledger history. A new operation can reuse those transaction and authentication boundaries.

However, describing this as "add one handler and it works" would be inaccurate. Supported kinds are intentionally closed in several places:

1. TransactionValidator only permits BET/WIN and defines when the BET reference is allowed.
2. FinancialEntry.Record only permits BET/WIN/DEV_FUNDING, and requires a reference exactly for WIN.
3. TransactionStore only loads an earlier BET/settlement state for WIN; its handler signature is shaped around that use case.
4. WalletDbContext constrains ledger kinds, outcome kinds and reference presence; PostgreSQL also has matching-WIN and settlement uniqueness guards. A new type needs a migration, not just an EF model edit.
5. Dependency registration, OpenAPI examples, response/history semantics and tests need updating.

Those deliberate allowlists protect financial data. Extensibility should mean a small, explicit, testable change across these boundaries, not removing constraints or weakening validation.

| Type | Rules that must be decided first | Likely changes |
| --- | --- | --- |
| REFUND | Full or partial? Can a settled BET be refunded? May refunds accumulate up to the stake? Which provider may refund? | Explicit original-operation lookup, remaining-refundable calculation under lock, refund ledger/reference model and uniqueness/amount constraints. The current unique OriginalBetId cannot simply be reused if WIN and REFUND should coexist. |
| BONUS | Who may grant it? Is it ordinary wallet money or a restricted promotional balance? Is there a campaign/reference? | Authorized bonus handler and domain credit rule, allowed type constraints/migration and idempotency/overflow tests. A restricted bonus balance would be new product scope, not a simple transaction label. |
| JACKPOT | Is this a supplied payout against a BET, or does Wallet own a shared prize pool? Can it coexist with a WIN? | A supplied payout can reuse the financial core after reference/settlement rules are agreed. Shared-pool accounting is a separate, larger consistency problem. |

One extension hazard deserves a focused test before new kinds are enabled: TransactionStore catches FinancialRuleException and saves the rejection outcome in the same context. The current handlers check their business failures before mutating the balance. A future handler that changes the tracked wallet and then throws could have that mutation saved with a 409 rejection. For example, crediting first and then calling FinancialEntry.Record with an unsupported new kind is unsafe. Require prevalidation and a tested rollback/restore boundary for handler business failures. This is a future-extension concern identified by inspection, not a failing current BET/WIN scenario.

## SignalR readiness

SignalR is absent, and adding it is a separate optional task. A correct implementation would authenticate the connection, derive its player identity from the token, restrict subscriptions to that player's wallets, notify only after commit, and refresh authoritative balances on connect/reconnect. Exact transaction replay should not be treated as a new balance change. A failed notification must not invalidate an already committed financial operation.

For the brief's multiple-Wallet-instance model, process-local broadcasts alone are insufficient: the write may land on a different instance from the player's connection. A shared fan-out mechanism or managed SignalR service would need an explicit choice. Microsoft documents this cross-server delivery issue and the backplane/service options in its [SignalR scaling guidance](https://learn.microsoft.com/en-us/aspnet/core/signalr/scale). Push should remain a convenience; missed messages must be recoverable through wallet reads. No notification infrastructure was added in this review.

## Submission priorities

1. Finish and review the source snapshot before adding features. Git currently shows moved domain files as deletions with replacement folders untracked; UI, SMTP and other work also remain unstaged/uncommitted. Include intended source/configuration, exclude secrets/build output, and verify the final checkout rather than assuming this working-tree result proves the submitted commit.
2. Reconcile the final documents. AI_USAGE retains historical 74-test wording followed by the newer 84-test SMTP result; make the final result easy to identify. Recheck final DESIGN rendering against the two-page limit after all edits. Keep the registration route documented as /api/v1/register.
3. Prepare to explain the existing choices: eventual provisioning, caller retry for unaccepted requests, durable terminal rejections, one WIN per BET, decimal bounds, offset pagination and the development issuer. These are likely more valuable in the interview than more infrastructure.
4. If extending transactions, agree one operation's semantics and add a mutation-then-rejection atomicity test before broadening its allowlists. Do not implement refunds/bonuses/jackpots from names alone.
5. Add SignalR only if separately requested and time remains for authentication, reconnect and multi-instance testing. Its absence is not a reason to describe the required assessment as incomplete.

## Verification in this review

- PASS: supplied brief and repository brief have identical SHA-256 hashes.
- PASS: inspected current source, migrations, project dependency declarations, API/SMTP/Compose setup, tests and submission documents. Domain projects have no EF/ASP.NET packages; Application references its own Domain, not Infrastructure.
- PASS: `dotnet test MiniWallet.Tests/MiniWallet.Tests.csproj --no-restore -v minimal`, with local TestPostgres on port 55432 and TestMailpitHttpUrl=http://127.0.0.1:8025: **84 passed, 0 failed, 0 skipped**. Test fixtures use disposable PostgreSQL databases.
- NOT RUN again: fresh isolated Compose startup, external SMTP delivery, final DESIGN PDF pagination, React build or a clean clone of a final commit. Prior isolated Compose/SMTP checks are recorded in PROGRESS.md; they are not a new execution in this review.
- No application code, dependencies, database schema or optional features changed; this report and the progress record are the only review edits. No commit or push performed.
