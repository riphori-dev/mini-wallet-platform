# MiniWallet

Two independent .NET 8 controller APIs: Users registers/verifies players and Wallet provisions balances, accepts provider BET/WIN and serves protected balances/history. PostgreSQL, EF Core, FluentValidation and xUnit; inward Domain/Application/Infrastructure/API projects per service. No broker, Redis, gateway or optional real-time features.

## Prerequisites and one-command startup

Docker Engine/Desktop with **Linux containers**, Docker Compose v2+, internet access for the first image/package restore, and free localhost ports 5080/5081. A host .NET SDK, manual SQL, external account or untracked configuration is **not required**.

```sh
docker compose up --build
```

- Wallet Swagger UI: http://127.0.0.1:5080/swagger
- Users Swagger UI: http://127.0.0.1:5081/swagger
- Both expose `/health/live` and `/health/ready`.

PostgreSQL 16 has one persistent named volume and separate `wallet`/`users` databases and non-superuser owners. Initialization SQL creates both automatically on a new volume. A TCP health check waits until initialization finishes, then per-service migration jobs complete before API startup. Normal hosts never migrate; PostgreSQL advisory locks serialize competing migrators. `docker compose restart postgres users wallet` preserves data; `docker compose down` also preserves the volume. Do not use `down -v` on data you want to keep.

All checked-in passwords/signing material are **local Development defaults only**. Compose binds APIs to loopback and does not publish PostgreSQL. Database credentials are in compose.yaml; token/mailbox defaults are in appsettings.Development.json. No real secrets are required. Optional `WALLET_PORT`/`USERS_PORT` environment variables change published ports for an isolated review.

## Local credentials and JWTs

| Identity | Credentials | Use |
| --- | --- | --- |
| Registered player | Own email/password after verification | `/dev/token`, role=player; own reads |
| Funded demo | `demo@miniwallet.test` / `Local-demo-password-123!` | `/dev/token`; separate fixture UUID below |
| Admin | `admin@miniwallet.test` / `Local-admin-password-123!` | `/dev/token`, role=admin; any-player reads |
| Provider client | `local-provider` / `local-provider-secret-123!` | `/dev/connect/token`, scope `transactions:write` |
| Internal client | `users-delivery` / `local-internal-secret-123!` | `/dev/connect/token`, scope `players:provision` |
| Reviewer mailbox | Header `X-Dev-Mailbox-Key: local-mailbox-key` | `GET /dev/mailbox` |

The Users-hosted JSON issuer is a documented **Development issuer, not production OAuth/OIDC**. Token/mailbox actions return 404 outside Development; admin/demo seeding only runs in Development migration jobs. Neither registration nor login accepts requested roles/subjects. Tokens expire in 10 minutes, with no refresh flow. Wallet checks HS256 signature, issuer, audience, lifetime and principal kind/role/scope. Player sub is the persisted verified Users UUID. Provider identities are machines; internal delivery has a separate audience, so ordinary provider/player/admin tokens cannot provision wallets.

## Reviewer walkthrough

Use Swagger **Try it out**; bodies below are JSON. Alternatively, Windows PowerShell 5+ or PowerShell 7 can run the complete asserted flow:

```powershell
./scripts/verify-reviewer.ps1
```

1. Users `POST /api/v1/players`:

   ```json
   {"email":"player@example.test","password":"Player-password-123!"}
   ```

   `201` means user + verification hash + pending registration delivery committed. `provisioningStatus=pending` acknowledges acceptance, not synchronous Wallet completion. An outage leaves durable pending work; the worker retries automatically. No player create-wallet call exists.
2. Users `GET /dev/mailbox` with the reviewer header; copy the latest token for the normalized email. `POST /api/v1/email-verifications` with `{"token":"<copied token>"}` returns 204. Secrets are random, hashed in storage and expire after 24 hours; consumed/expired tokens return 400.
3. Users `POST /dev/token` with the same email/password. Unverified login returns 403; wrong credentials return 401. Copy access_token. In Wallet Swagger **Authorize**, paste the JWT only.
4. Wallet `GET /api/v1/me/wallets`: automatically provisioned USD/EUR/BTC all start at zero. If provisioning is still pending, a temporary 404 may be retried. `/api/v1/me/wallets/USD/transactions?page=1&pageSize=20` shows own history.
5. Obtain a distinct provider token from Users `POST /dev/connect/token`:

   ```json
   {"clientId":"local-provider","clientSecret":"local-provider-secret-123!","scope":"transactions:write"}
   ```

6. Use the **separate funded demo**, UUID `de000000-0000-4000-8000-000000000001`. Development migration records opening `DEV_FUNDING` entries once: USD=100.00, EUR=100.00, BTC=0.01000000. Restart never replenishes/reset balances. New registrations remain zero; there is no public deposit endpoint.
7. With the provider JWT, Wallet `POST /api/v1/transactions`:

   ```json
   {"externalTransactionId":"demo-bet-001","playerId":"de000000-0000-4000-8000-000000000001","currency":"USD","type":"BET","amount":"10.00"}
   ```

   After 201, submit:

   ```json
   {"externalTransactionId":"demo-win-001","playerId":"de000000-0000-4000-8000-000000000001","currency":"USD","type":"WIN","amount":"25.00","betExternalTransactionId":"demo-bet-001"}
   ```

   On a fresh fixture, balance moves 100 -> 90 -> 115. Repeat the identical BET: its stored 201 body still reports original balanceAfter=90.00. Use fresh IDs for new intended actions. WIN requires an accepted matching provider/player/currency BET; **one successful WIN per BET is an explicit assumption**, not a brief requirement.
8. Log in as the demo to inspect its balances/history. Log in as admin and use `/api/v1/players/{playerId}/wallets` and `.../wallets/USD/transactions`. The newly registered player receives 403 on those admin routes; a playerId query cannot override `/me`. Providers cannot read player/admin wallets, and players cannot call provider writes.

A mailbox restart loses messages, not user state. Authenticate and resend via `POST /api/v1/email-verifications/resend` with email/password; old tokens are invalidated. After a lost registration response, retry registration: duplicate normalized email returns 409 with no extra user/event, then use original credentials for login/resend. No general registration replay framework. After a lost verification response, try login to confirm success.

## Financial and failure contract

Amounts are positive invariant **decimal strings**; JSON numbers, exponent, signs, grouping, whitespace and excess precision (including trailing zeros) are rejected. USD/EUR scale 2, BTC 8. Maximum amount/balance is 99999999999999999999.99999999 (USD/EUR effective .99). Exact PostgreSQL numeric plus explicit range/quantization checks avoids typmod rounding. C# decimal and Money enforce matching currencies, nonnegative balance and bounded credits.

One transaction route selects focused BET/WIN handlers. Unique `(providerId,externalTransactionId)` is global to that provider, across wallets/currencies. Reservation, locked wallet checks, balance, immutable ledger and stored response commit together. Exact typed replay returns original status/body; changed payload gives 409 idempotency_conflict. Terminal business 409 outcomes reserve the ID: a rejected BET cannot later succeed silently after funding. New intended attempts require new IDs. Validation/auth failures and missing provisioning do not reserve keys.

Transient database/3-second lock timeouts return 503 + Retry-After and roll back. Network timeout/unknown commit must be retried with the **same ID/payload** and backoff; never infer success or blindly replace the ID. Delivery is at least once with deduplicated effects, not exactly-once delivery. Worker HTTP requests have 5-second timeouts and persisted retry delays, no fixed discard limit and no database transaction across HTTP.

Errors use RFC 9457 application/problem+json with stable code and traceId. Statuses: 400 invalid input, 401 missing/invalid auth, 403 wrong permissions, 404 unavailable wallet, 409 conflict/business rejection, 503 transient failure; unexpected 500 responses are sanitized. History returns `{items,page,pageSize,hasMore}`, ordered createdAt/id descending; page defaults 1 (max 10000), size defaults 20 (max 100). New writes can shift offset pages. Full shapes and semantics: [API contract](docs/API_CONTRACT.md).

## Tests and development

```sh
docker compose -f compose.yaml -f compose.tests.yaml run --build --rm tests
```

This runs xUnit against real, automatically migrated, randomly named PostgreSQL test databases, then removes only those test databases. Domain/application cases test money, matching BET rules and async validation. Integration cases target atomic registration/provisioning/ledger changes, constraints, lock timeouts, 80+80 against 100 across two Wallet hosts, duplicate/replay/response-loss recovery, ownership, JWTs, pagination and error contracts. No EF InMemory/mocks prove database correctness. See [actual results](docs/PROGRESS.md); no coverage percentage is claimed.

Optional live recovery/demo checks (they mutate only their own review records):

```powershell
./scripts/verify-provisioning.ps1
./scripts/verify-transactions.ps1
```

The first temporarily stops Wallet/restarts Users and restores Wallet. The second adds a fresh demo BET/WIN pair (+15 USD). `verify-reviewer.ps1 -StatePath <temporary-file>` records only non-secret IDs/balance; after ordinary restart, run it with the same file and `-CheckPersistence` for read-only persistence assertions. URL parameters support alternative review ports.

Host development requires .NET SDK 8.0.4xx: `dotnet restore MiniWallet.sln`, `dotnet build MiniWallet.sln --no-restore`, `dotnet test MiniWallet.sln --no-build`, `dotnet tool restore`. Host tests skip PostgreSQL cases unless TestPostgres points to an appropriate local test server; use the Docker command for full evidence. Both .sln and the original .slnx list the same projects; SDK 8 uses .sln. Supply service ConnectionStrings__Database when running a host; HTTP launch profiles use 5080/5081. Generate migrations with `dotnet ef migrations add <Name> --project MiniWallet.Infrastructure --startup-project MiniWallet.API` (Users equivalents for Users). No speculative generic repository, mediator or shared entity/DbContext.

## Short troubleshooting

- Docker cannot connect: start the Linux engine; confirm `docker info` and `docker compose version`.
- Ports occupied: stop the other app or set WALLET_PORT/USERS_PORT; use matching reviewer script URL parameters.
- Startup fails: inspect `docker compose logs postgres wallet-migrate users-migrate`; failed migrations intentionally block API startup. Do not delete existing data as a generic repair.
- New player has no wallet yet: inspect Users delivery logs; keep both APIs running and retry the read. Events remain pending across outages/restarts.
- Verification message gone: authenticate and resend. Tokens cannot be recovered from stored hashes.
- 401: renew the expired token and use the correct audience. 403: use the intended player/admin/provider/internal credentials; do not request roles in a body.
- 409 replay conflict: preserve the original typed payload. Terminal business rejection needs a new ID only for a corrected, intentional attempt.

[Design](DESIGN.md) and [AI usage](AI_USAGE.md) explain decisions and actual tool interactions. Local credentials and demo balances are assessment conveniences, not production configuration.