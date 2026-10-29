# MiniWallet

A .NET 8 backend for a small gaming platform, built as two independently runnable ASP.NET Core APIs:

- **Users:** registration, email verification and a documented development JWT issuer.
- **Wallet:** USD/EUR/BTC balances, provider BET/WIN transactions and transaction history.

This submission is backend-only. Use Swagger UI or Postman to explore the APIs; no frontend or Node.js setup is required.

## Start the system

Prerequisite: Docker with Docker Compose and Linux container support. From the repository root:

```powershell
docker compose up --build
```

For detached mode, use `docker compose up -d --build`.

Compose starts PostgreSQL, Mailpit, service-owned migration jobs, Users, two Wallet instances and an Nginx load balancer. Users and Wallet own separate databases on the same PostgreSQL server. Migration jobs finish before their API hosts start. Initial image downloads and builds require internet access.

| Service | Address |
| --- | --- |
| Load-balanced Wallet API | http://localhost:5080 |
| Wallet Swagger UI | http://localhost:5080/swagger |
| Users API | http://localhost:5081 |
| Users Swagger UI | http://localhost:5081/swagger |
| Direct Wallet A, for single-instance SignalR testing | http://localhost:5082 |
| Mailpit verification-email inbox | http://localhost:8025 |

Readiness endpoints are `/health/ready`; liveness endpoints are `/health/live`. Stop the stack with `docker compose down`. Account, wallet, transaction and audit data persist in named volumes; restarting does not reset balances.

## Import the APIs into Postman

After startup, import each of these OpenAPI JSON URLs into Postman to generate HTTP collections:

- Wallet: http://localhost:5080/swagger/v1/swagger.json
- Users: http://localhost:5081/swagger/v1/swagger.json

Set the imported collection's base URL to the corresponding API address above if needed. The JSON can also be downloaded and imported as a file. These are API definitions, not a prepared Postman test collection: tokens must be obtained and supplied as described below. The SignalR WebSocket handshake is separate from the HTTP imports.

## Local credentials

These credentials and the issuer are for Development only. Tokens expire after 10 minutes.

| Identity | Email / client ID | Password / client secret |
| --- | --- | --- |
| Demo player | `demo@miniwallet.test` | `Local-demo-password-123!` |
| Admin | `admin@miniwallet.test` | `Local-admin-password-123!` |
| Game provider | `local-provider` | `local-provider-secret-123!` |

The demo player ID is `de000000-0000-4000-8000-000000000001`. Its initial balances are USD 100, EUR 100 and BTC 0.01, recorded as explicit opening ledger credits. Previous activity affects these balances. All newly registered players start at zero, with no public top-up endpoint.

## Try a balance read, BET and WIN

For HTTP bodies, use JSON and `Content-Type: application/json`.

1. Obtain a player token with `POST http://localhost:5081/dev/token`:

   ```json
   {
     "email": "demo@miniwallet.test",
     "password": "Local-demo-password-123!"
   }
   ```

   Copy `access_token`. Use it as Bearer authorization for `GET http://localhost:5080/api/v1/me/wallets` and note the current USD balance. Admin login uses the same token endpoint with the admin credentials above.

2. Obtain a separate provider token with `POST http://localhost:5081/dev/connect/token`:

   ```json
   {
     "clientId": "local-provider",
     "clientSecret": "local-provider-secret-123!",
     "scope": "transactions:write"
   }
   ```

3. Use the provider token as Bearer authorization for `POST http://localhost:5080/api/v1/transactions`:

   ```json
   {
     "externalTransactionId": "review-bet-001",
     "playerId": "de000000-0000-4000-8000-000000000001",
     "currency": "USD",
     "type": "BET",
     "amount": "1.00"
   }
   ```

   With sufficient funds, expect `201` and a balance reduced by 1.00. Resend the identical request: the original response is returned and no additional debit occurs. Changing the payload under the same external ID returns `409`.

4. Credit a payout through the same endpoint using the provider token:

   ```json
   {
     "externalTransactionId": "review-win-001",
     "playerId": "de000000-0000-4000-8000-000000000001",
     "currency": "USD",
     "type": "WIN",
     "amount": "2.00",
     "betExternalTransactionId": "review-bet-001"
   }
   ```

   Expect `201` and a 2.00 credit. One successful WIN is allowed per BET. Use fresh IDs for a new BET/WIN pair; keep the original IDs and payloads when retrying an uncertain operation.

5. Using the player token, inspect `GET http://localhost:5080/api/v1/me/wallets/USD/transactions?page=1&pageSize=10`. Using the admin token, read any player's balances through `/api/v1/players/{playerId}/wallets`.

Amounts are JSON strings. USD/EUR allow two decimal places; BTC allows eight. Excess precision is rejected rather than rounded. Currency balances are independent; no conversion or blockchain transfer is performed. Players cannot submit financial writes, and provider tokens cannot read player wallets.

## Register and verify a new player

1. Send `POST http://localhost:5081/api/v1/register` with a fresh email and password:

   ```json
   {
     "email": "reviewer@example.test",
     "password": "Reviewer-password-123!"
   }
   ```

   Expect `201`. Save `playerId`. Wallet provisioning is queued automatically; no extra create-wallet call is needed.

2. Open Mailpit at http://localhost:8025 and copy the verification token from the message. Alternatively, `GET http://localhost:5081/dev/mailbox` with header `X-Dev-Mailbox-Key: local-mailbox-key` provides an in-memory preview of SMTP-accepted messages.

3. Send `POST http://localhost:5081/api/v1/email-verifications` with `{"token":"COPIED_TOKEN"}`. Expect `204`, then obtain a player token through `/dev/token` using the new credentials.

4. Read `/api/v1/me/wallets` with the new token. Expect zero USD/EUR/BTC wallets. Provisioning is asynchronous; a temporary `404` can occur before delivery completes. If Wallet is unavailable, the durable registration notification remains pending and retries after recovery.

Login before verification returns `403`. Duplicate normalized email registration returns `409`. Verification tokens are single-use. For a replacement message, call `/api/v1/email-verifications/resend` on Users with the registered email/password. Mailpit captures local SMTP mail; it does not deliver to internet recipients.

## Load balancing, SignalR and audit records

Repeated Wallet responses on port 5080 include `X-Wallet-Upstream`, identifying which Wallet instance handled the request. [LOAD_BALANCER.md](docs/LOAD_BALANCER.md) includes a reproducible competing-bet check and separate service update commands. Balances, ledger entries and idempotency outcomes commit atomically in PostgreSQL across instances.

Optional SignalR notifications are process-local. For Postman WebSocket testing, use `ws://localhost:5082/hubs/balance?access_token=PLAYER_TOKEN` and send provider transactions to **http://localhost:5082** so both reach Wallet A. [REALTIME.md](docs/REALTIME.md) explains the behavior; cross-instance notifications through port 5080 are not supported.

Each service records a best-effort HTTP audit trail in its own PostgreSQL database, excluding passwords, tokens, emails and request bodies. Audit failures do not change committed financial results. [AUDITING.md](docs/AUDITING.md) provides read-only inspection commands and explains retention and durability limits. Diagnostic logs are available with:

```powershell
docker compose logs --tail 50 wallet-a wallet-b users
```

## Run the tests

```powershell
docker compose -f compose.yaml -f compose.tests.yaml run --build --rm tests
```

The latest recorded full run passed **92 tests, with no failures or skips**. Tests focus on financial atomicity, concurrent requests across hosts, idempotency, authorization, database constraints, registration/outage recovery, SMTP, SignalR isolation and auditing. Test fixtures create disposable PostgreSQL databases rather than modifying application databases.

A PowerShell reviewer walkthrough is also available:

```powershell
./scripts/verify-reviewer.ps1
```

It exercises real API flows and adds a fresh registered player and BET/WIN entries to the local demo data. Its demo BET requires at least USD 10. Detailed commands, results and limitations are in [PROGRESS.md](docs/PROGRESS.md).

## Further documentation

- [API contract](docs/API_CONTRACT.md): authentication, payloads, status codes, pagination and retry semantics.
- [Design](DESIGN.md): architecture, failure handling and accepted trade-offs.
- [AI usage](AI_USAGE.md): how I directed AI assistance, corrections and verification.
- [SMTP setup](docs/SMTP.md): local email capture and external-provider configuration.

This is an assessment implementation with documented local credentials and simplifications. Production identity, cross-instance SignalR, audit archival and additional deployment hardening are outside the implemented scope.
