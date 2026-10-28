# MiniWallet design

## Architecture and scope

Two independently runnable .NET 8 controller APIs share a repository/solution: MiniWallet.API is Wallet; MiniWallet.Users.API is Users. Each has Domain, Application, Infrastructure and API projects. Domain has no framework dependencies; Application defines explicit use cases, async FluentValidation and narrow ports; Infrastructure implements EF/PostgreSQL, HTTP and credential/email concerns; API composes them. Money and Currency enforce useful financial rules. No generic repository, mediator or shared entities/DbContexts. One PostgreSQL container has separate service databases and non-superuser credentials; neither service reads the other's tables.

Compose creates databases automatically, waits for PostgreSQL TCP readiness, then runs service-owned migrations before hosts start. Session advisory locks serialize migrations, including history initialization. API hosts never migrate. This keeps fresh startup simple and multiple Wallet hosts from racing schema changes.

## Identity, registration and provisioning

Users normalizes email and hashes passwords with salted PBKDF2-SHA256. User, random expiring verification-token hash and pending PlayerRegistered record commit together before 201. Verification consumes the token under a row lock. A key-protected Development mailbox exposes messages; authenticated resend recovers message loss and replaces old tokens. Duplicate email returns 409 without another player/event; after a lost response, retry then use original credentials for login/resend. No general registration replay framework or encrypted email pipeline.

The documented Development JWT issuer lives inside Users, not another API. Only verified Users identities receive player tokens; admin has separately seeded credentials. Provider and internal clients have distinct credentials/scopes/audiences. Wallet checks signature, issuer, audience, expiry, role and principal kind. Ownership comes from trusted sub; admin routes accept another player's ID. Public inputs cannot grant privileged roles.

One local Users worker polls durable pending records, makes bounded authenticated HTTP requests outside database transactions and retries after a persisted delay. No fixed attempt limit discards work and no distributed worker lease is needed. Wallet commits a unique receipt and USD/EUR/BTC zero wallets together. Event and player/currency uniqueness handle concurrent delivery; replay never resets balances. Registration while Wallet is down remains pending and completes automatically after recovery, with no player create-wallet call. Delivery is at least once, with deduplicated database effects.

## Financial transaction boundary

One provider-only route selects BET/WIN handlers. Unique provider/external-ID reservation precedes a short SELECT FOR UPDATE wallet transaction; mutable balance/reference/settlement checks happen after locking. BET cannot overdraw. WIN pays a positive amount against an accepted matching provider/player/currency BET. One successful WIN per BET is an explicit simplifying assumption, enforced by a unique reference and matching-BET database guard.

Money uses decimal, scales USD/EUR=2 and BTC=8, and cap 99999999999999999999.99999999 (USD/EUR effective .99). Invalid precision is rejected, never rounded. Exact numeric columns with range/quantization checks avoid PostgreSQL typmod coercion rounding away invalid BTC digits. Cross-currency arithmetic and out-of-range credits fail in Domain; persisted constraints remain authoritative against EF bypass.

Balance, immutable financial entry and serialized original outcome commit atomically. Exact typed replay returns original status/body/balanceAfter; changed payload conflicts. Terminal business rejections reserve the ID without moving funds. Validation/auth errors and missing provisioning do not reserve it; transient failures roll back and remain retryable. A deferred guard prevents unfinished reservations committing. No response claims success before commit. Ordinary retries after timeout/unknown commit use the same ID/payload, never a blind replacement ID.

## Failures, assumptions and limits

| Scenario | Recovery boundary |
| --- | --- |
| Wallet down / Users restart | Durable pending record survives; worker retries. |
| Receiver commits, acknowledgement lost | Stable event replay; one receipt/three wallets. |
| Partial provisioning or financial failure | Transaction rolls back every related change. |
| Duplicate or competing provider calls | Unique key plus wallet lock; no duplicate debit/overdraft. |
| Database/lock timeout or uncertain commit | Retryable 503 or caller uncertainty; same-ID replay discovers committed outcome. |

An explicit Development fixture has one opening ledger credit per currency (USD/EUR 100, BTC .01). It is never implicit registration funding or a public deposit route; restart does not replenish it. Simple bounded page/pageSize history sorts by createdAt/id; concurrent writes can shift pages. The Development issuer/mailbox are not production identity/email systems. No optional SignalR, broker, Redis, gateway, Kubernetes or event sourcing was added.

Real PostgreSQL tests cover atomicity, constraints, two-host contention, recovery and authorization; domain/application tests cover rules/validation. Production changes would include managed identity/secrets, real email delivery, monitoring/repair tooling, retention, backups and deployment hardening. Keep the assessment focused within its 6-8-hour budget. Detailed contracts and actual evidence are separate in docs/API_CONTRACT.md and docs/PROGRESS.md.