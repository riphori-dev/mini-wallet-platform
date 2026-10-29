# Request audit trail and application logs

Both APIs use built-in ILogger JSON console output for diagnostics. Each service also writes a small, structured request audit record to its own PostgreSQL `AuditEvents` table. There is no new logging service, broker, UI or package dependency. Shared linked middleware source avoids coupling the service assemblies. The audit table is SQL-owned by an explicit migration, not an EF domain entity; model snapshots remain unchanged.

## What is recorded

One record per completed HTTP action under `/api`, `/internal` or `/dev`, including rejected/failed requests: UTC timestamp, service, instance hostname, trace ID, HTTP method, route template, HTTP status, outcome and duration. Health probes, Swagger/static assets and SignalR connection lifetimes are excluded.

Authenticated Wallet requests record the validated JWT subject/role. Successful Users login records the authenticated identity; successful machine token issuance records the authenticated client ID. Anonymous registration records the newly created player ID. Invalid logins and anonymous verification/resend requests do not assert an unverified actor identity. Admin routes record the target player ID. Financial requests record player/external transaction references, type/currency, and whether the response was a stored replay. Rejections handled by the exception handlers record the stable error code; financial business rejections also record their error code. Authorization failures are identified by status 401/403.

No request/response bodies, raw paths, query strings, Authorization headers, passwords, signing keys, access/verification tokens or emails are captured. The endpoint route template is used rather than arbitrary user input in the URL. Explicit reference metadata is bounded to 128 characters and JSON encoded. Financial amounts and balance history remain in the existing ledger, rather than duplicating them in the audit trail.

## Durability and limits

Records are append-only through application code and survive service/container restarts via the existing PostgreSQL named volume. No retention deletion job is introduced; choose retention/archival before production and monitor growth. The application database role owns these tables, so this is not tamper-proof or a compliance-grade security archive.

Request auditing is **best effort** and runs after the downstream handler using a separate database connection, with a two-second bound and independent of request cancellation. It is not part of the business transaction. If the audit insert fails, a safe `AuditFallback` record is emitted to console and the existing response is preserved. A crash between business commit and audit persistence can leave a missing request record; a timeout/disconnect does not prove a write failed. There is no automatic backfill. The financial ledger and stored transaction outcomes remain the authoritative atomic financial record.

Each accepted request/replay can produce its own request audit event, but only an original accepted operation creates a financial ledger entry. This is how repeated HTTP traffic is distinguished from repeated money movement. Unexpected failures and cancellations are recorded as failed attempts when middleware can run; abrupt process termination cannot be guaranteed to log. Background registration delivery continues using its existing durable retry records and operational logs; this middleware is not a background-event audit system.

Compose limits application console logs to three 10 MB files per container. These support troubleshooting/fallback inspection but are rotated and can disappear when containers are removed. Durable audit rows are in PostgreSQL; no secrets should be added to diagnostic logging configuration.

## Run and inspect

```powershell
docker compose up -d --build
docker compose logs --tail 50 wallet-a wallet-b users
```

The existing migrator jobs create each database's audit table before API hosts start. For local `dotnet run`, run the corresponding API once with `--migrate` first.

Inspect the latest safe audit records:

```powershell
docker compose exec postgres psql -U postgres -d wallet -c 'SELECT "CreatedAt", jsonb_pretty("Record") FROM "AuditEvents" ORDER BY "CreatedAt" DESC LIMIT 10;'
docker compose exec postgres psql -U postgres -d users -c 'SELECT "CreatedAt", jsonb_pretty("Record") FROM "AuditEvents" ORDER BY "CreatedAt" DESC LIMIT 10;'
```

For an error response, match its `traceId` to `Record.TraceId`. For financial operations, use `Details.ExternalTransactionId`. These are read-only reviewer queries; do not expose the database as an unauthenticated audit API.

## Manual check in Postman

1. Login through Users, then read Wallet balances using the player token. Look for login/read audit records with the correct service and identity.
2. Submit a fresh BET using the provider token. Find its external ID in Wallet audit rows: status 201, `IsReplay=False`.
3. Resend the identical BET. The next row has `IsReplay=True`, while money is debited only once.
4. Submit an insufficient-funds BET or change the first BET's payload under the same ID. Expect an audited 409 and the appropriate error code.
5. Read balances without a token. Expect an audited 401 with no claimed actor. Call an admin route with a player token: expect an audited 403.
6. Restart the APIs and re-query the tables. Audit rows persist. Confirm records contain no access tokens, passwords, emails or verification tokens.

Tests cover persisted financial events/replays/rejections, successful user login with sensitive data excluded, rejected authentication, trace correlation, probe exclusion, and a failed audit writer preserving the HTTP response. The existing financial/recovery suite remains necessary because request auditing does not replace transaction tests.
