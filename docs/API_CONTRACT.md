# MiniWallet API contract (v1)

Users and Wallet have separate base URLs. All requests and responses use JSON unless stated otherwise. Swagger UI at `/swagger` documents authentication and examples for each service. Timestamps use UTC ISO 8601; identifiers are UUIDs unless described as external strings.

## Identity and authentication

Users hosts a separate Development-only issuer component and endpoints, not a third API. Tokens expire after 10 minutes; no refresh flow initially. Both APIs validate signature, issuer, audience, expiry and allowed algorithm. Configure signing material through development configuration, never production credentials in source.

| Principal | Required trusted claims | Allowed access |
| --- | --- | --- |
| Player | `sub` = persisted Users player UUID, `role=player`, `email_verified=true`, `aud=wallet-api`, `scope=wallet:read` | Own balances/history; identity always taken from `sub` |
| Admin | `sub`, `role=admin`, `aud=wallet-api`, `scope=wallet:read:any` | Any player's read routes |
| Provider | `sub` = machine client, `provider_id`, `role=provider`, `aud=wallet-api`, `scope=transactions:write` | Provider transaction route only |
| Outbox worker | `sub` = Users delivery client, `aud=wallet-internal`, `scope=players:provision` | Internal provisioning route only |

Policies check principal kind and scope together. Internal and provider clients have distinct configured credentials and tokens. Registration and player authentication cannot accept or assign roles, scopes or provider IDs. Development admin credentials are explicitly configured separately. Unverified players cannot obtain player tokens. No endpoint accepts a caller-supplied subject in place of credential verification.

## Users endpoints

| Method/path | Input / access | Success | Other outcomes |
| --- | --- | --- | --- |
| `POST /api/v1/players` | Anonymous, email/password | `201` durable registration | `400`, `409 email_already_registered`, `503` |
| `POST /api/v1/email-verifications/resend` | Email/password credentials; rotates unverified token | `204` (also already verified) | `400`, `401`, `503` |
| `POST /api/v1/email-verifications` | Anonymous, `{ "token": "opaque-secret" }` | `204` token consumed and account verified | `400 invalid_verification_token` (invalid/expired/consumed), `503` |
| `POST /dev/token` | Development only; player email/password, or configured admin credentials | `200` bearer token | `400`, `401`, `403 email_not_verified`, `503` |
| `POST /dev/connect/token` | Development only; JSON `clientId`, `clientSecret`, `scope` (documented dev protocol) | `200` scoped provider/internal token | `400`, `401`, `403` |
| `GET /dev/mailbox` | Development only; separate reviewer credential | `200` verification messages/links | `401`, `403`, `503` |

Development token/mailbox actions return 404 outside Development and the issuer is not registered there. Tokens use HS256, a checked-in local-only signing secret and 10-minute expiry. Player sub is the verified Users UUID; admin role comes only from the seeded database identity. Verification secrets are 32 random bytes, stored only as SHA-256 hashes with a 24-hour expiry, cleared on atomic consumption. Reuse/expiry returns 400 without undoing verification. Messages live in a bounded (100 messages), key-protected in-memory Development mailbox; restart/eviction loses messages, not user/token state. Authenticate with email/password to resend and invalidate the previous token. Failed email delivery after registration still returns durable 201 with verificationDeliveryStatus=unavailable. Resend delivery failure returns retryable 503. No external provider, durable email intent or encrypted email pipeline is implemented.

Example registration:

```http
POST /api/v1/players
Content-Type: application/json

{"email":"player@example.test","password":"Example-password-123!"}
```

```json
{"playerId":"5f514482-9a4c-4846-946c-ea87fbbd8138","email":"player@example.test","emailVerified":false,"provisioningStatus":"pending","verificationDeliveryStatus":"sent"}
```

`201` is sent only after player, verification hash/expiry and PlayerRegistered pending delivery commit together. It acknowledges durable acceptance, not completed Wallet provisioning. Provisioning starts after registration commits, independently of verification. No extra player call is required. The delivery worker sends these events automatically; pending can outlive a Wallet outage and completes after recovery.

Email is trimmed, restricted to simple ASCII address syntax and lowercased invariantly. Password is exact (12–128 characters at registration); salted PBKDF2-SHA256 uses 600,000 iterations. A unique normalized email constraint arbitrates concurrent requests. Duplicate registration returns 409 and never creates another player/event. No registration replay framework or Idempotency-Key is used. After timeout/lost response retry the same registration: 201 means newly committed; 409 means the email is already present, not proof of ownership. Use the original credentials for login, or authenticated resend then verification and login. If the original request never committed, caller retry is necessary. No success is claimed before commit; an uncertain commit is resolved through this same flow.

## Internal provisioning (Wallet)

`POST /internal/v1/player-registrations`, internal principal only:

```json
{"eventId":"ac9d25ee-0a04-48cd-a5f1-9fa47672be25","version":1,"playerId":"5f514482-9a4c-4846-946c-ea87fbbd8138","occurredAt":"2026-10-28T12:00:00Z"}
```

`204` means inbox and USD/EUR/BTC zero wallets committed. Exact event replay also returns `204`. Unique inbox event ID and wallet `(playerId,currency)` prevent duplicates. Persist typed event identity for conflict comparison (occurredAt normalized to UTC microseconds, matching PostgreSQL precision); event ID with changed payload returns `409 event_conflict`. A distinct event for an already provisioned player records its inbox receipt without resetting balances or adding wallets. Malformed/unsupported event version: `400`; wrong credentials: `401/403`; transient failure: `503`. One local Users worker polls batches of 20 with a persisted 2-second retry delay, 5-second per-HTTP-request timeout and separate internal credentials. It retains events until a successful acknowledgement; all failures, including non-transient errors, remain visible in Attempts/NextAttemptAt/LastError and retry without a fixed discard threshold. No database transaction spans HTTP and no distributed worker leases are used. Receiver success followed by a lost acknowledgement resends the same event.

## Provider transaction endpoint (Wallet)

Only `POST /api/v1/transactions` accepts provider financial writes. Add transaction handlers behind this route for future types. Provider identity comes exclusively from the token.

```json
{"externalTransactionId":"bet-1001","playerId":"5f514482-9a4c-4846-946c-ea87fbbd8138","currency":"USD","type":"BET","amount":"12.50"}
```

```json
{"externalTransactionId":"win-1001","playerId":"5f514482-9a4c-4846-946c-ea87fbbd8138","currency":"USD","type":"WIN","amount":"25.00","betExternalTransactionId":"bet-1001"}
```

External IDs are case-sensitive ASCII strings, 1–128 characters from `[A-Za-z0-9._:-]`. Currency/type are exact uppercase enums. BET must omit the bet reference; WIN requires it. Unknown properties are rejected so role/provider injection and ambiguous payloads fail validation.

`amount` must be a JSON string matching `^[0-9]+(\.[0-9]+)?$`, at most 29 characters, parsed with invariant decimal rules. No signs, exponent, grouping or whitespace. Positive amounts only. USD/EUR accept at most 2 fractional digits; BTC at most 8. Excess precision, including excess trailing zeros, is rejected before normalization; never round. Maximum amount and balance: **99999999999999999999.99999999** (20 integer digits, 8 fraction digits); the effective USD/EUR maximum is **99999999999999999999.99**. Storage is exact PostgreSQL `numeric` with explicit nonnegative/positive, maximum and currency-quantization constraints equivalent to the agreed numeric(28,8) monetary range. No typmod is applied: numeric(28,8) would silently round excess BTC digits before CHECK constraints could see them. Direct EF/SQL writes cannot bypass the value bounds or quantization checks. C# decimal holds these values; checked arithmetic rejects overflow or balance above the cap. Money includes currency and prohibits cross-currency operations. Responses use fixed currency scale decimal strings.

Successful first acceptance: `201` after commit:

```json
{"transactionId":"ceef5195-c616-470d-a80e-8eb7cf34f065","externalTransactionId":"bet-1001","playerId":"5f514482-9a4c-4846-946c-ea87fbbd8138","currency":"USD","type":"BET","amount":"12.50","balanceAfter":"87.50","createdAt":"2026-10-28T12:00:01+00:00","betExternalTransactionId":null}
```

The example assumes an explicit fixture previously funded USD to 100.00. Real registrations always start at zero. WIN references a committed accepted BET by the same provider, player and currency. WIN is a positive payout independent of stake. **Assumption:** one successful settlement WIN per BET, enforced by a unique referenced-BET constraint. This is a simplification, not a brief requirement. A rejected WIN does not settle a BET. No implicit funding or negative balances.

### Durable outcomes and retries

The idempotency key is `(trusted providerId, externalTransactionId)` across all currencies and players. No extra Idempotency-Key header is required here. Persist canonical typed fields (UUID, currency, type, decimal value, nullable bet ID); compare fields, not raw JSON or just a hash. Valid `"12.5"` and `"12.50"` compare equal. External IDs remain case-sensitive. Outcome records are retained without expiry for the assessment.

Reserve the key inside the database transaction using a unique insert; concurrent inserts wait for the committed result. A rollback releases the reservation. A deferred database guard prevents an unfinished reservation from committing, and committed outcomes/history cannot be updated or deleted. After reservation, acquire the wallet using `SELECT ... FOR UPDATE`; all mutable financial checks run after that lock. Under PostgreSQL READ COMMITTED, a waiter sees the previous committed balance and settlement. Same-key requests on different wallets still serialize through the provider key. Keep locks short; never call another service inside the financial transaction. Lock wait is bounded to 3 seconds and statements to 5 seconds. ON CONFLICT DO NOTHING avoids poisoning the transaction on normal key races; database exceptions unwind/roll back before any further request processing.

Commit accepted ledger row, updated balance and stored response atomically. Commit terminal business rejection as an outcome with no financial ledger/balance mutation. Stored rejections include `409 insufficient_funds`, `bet_not_found`, `bet_mismatch`, `bet_already_settled`, `balance_limit_exceeded`. Exact replay returns the original status/body, including original `balanceAfter` or rejection and original trace ID; it never re-evaluates funds or bet existence. A new intended attempt after a terminal rejection requires a new external ID. Changed canonical payload always returns `409 idempotency_conflict` without changing the original outcome.

Missing/unprovisioned wallet returns `404 wallet_unavailable` and does not reserve the key: provisioning is eventual, so retry the same payload/key later. Input/auth failures before acceptance do not reserve it. Database, lock timeout, deadlock or transient infrastructure failure rolls back, returning `503 transient_unavailability` where possible with `Retry-After: 1`; no terminal `503` outcome is stored. A connection loss during commit leaves the client uncertain, not necessarily rolled back: retry the same ID/input to discover the committed outcome. Never report success before commit confirmation.

Clients retry network timeouts and `503` with the same identity/payload, exponential backoff and jitter (start 1 second, cap 30 seconds). `404 wallet_unavailable` may be retried during provisioning; persistent absence needs investigation. Do not automatically retry `400/401/403/409`; renew expired credentials before repeating the same operation. Retries cannot change provider identity. Authentication is checked on every replay.

## Wallet reads

| Method/path | Policy | Success |
| --- | --- | --- |
| `GET /api/v1/me/wallets` | Player own subject | `200` three balances |
| `GET /api/v1/me/wallets/{currency}/transactions` | Player own subject | `200` bounded page |
| `GET /api/v1/players/{playerId}/wallets` | Admin | `200` three balances |
| `GET /api/v1/players/{playerId}/wallets/{currency}/transactions` | Admin | `200` bounded page |

```json
{"playerId":"5f514482-9a4c-4846-946c-ea87fbbd8138","wallets":[{"currency":"USD","balance":"87.50"},{"currency":"EUR","balance":"0.00"},{"currency":"BTC","balance":"0.00000000"}]}
```

History query: `?page=1&pageSize=20`. Page defaults 1, range 1–10000; pageSize defaults 20, range 1–100. Unsupported currency or invalid query returns `400`. Order is `createdAt DESC, transactionId DESC`, deterministic for unchanged data. This is simple bounded offset pagination: new commits between requests may move entries between pages, so it is not a snapshot/export protocol. This deliberately replaces the earlier opaque cursor proposal per the latest request.

```json
{"items":[{"transactionId":"ceef5195-c616-470d-a80e-8eb7cf34f065","externalTransactionId":"bet-1001","type":"BET","amount":"12.50","balanceAfter":"87.50","createdAt":"2026-10-28T12:00:01+00:00","originalBetId":null}],"page":1,"pageSize":20,"hasMore":false}
```

History includes only immutable accepted financial entries, including explicit DEV_FUNDING fixture credits; rejected outcomes are stored separately. Missing wallets return 404, including while provisioning. Authorization runs before resource lookup. Providers cannot access player/admin reads; players cannot use admin routes or provider writes. `/me` derives player identity exclusively from sub; a query parameter cannot override it. Admins use the explicit `/players/{playerId}` routes.

## Explicit Development fixture

The Development migrators seed verified demo@miniwallet.test / Local-demo-password-123!, playerId `de000000-0000-4000-8000-000000000001`. Wallet provisions its currencies and records one DEV_FUNDING opening credit each: USD=100.00, EUR=100.00, BTC=0.01000000. This is a reserved local fixture, not registration behavior. Re-running migrators does not replenish/reset balances or duplicate opening entries. Ordinary new registrations remain zero. No public deposit/funding endpoint exists. Demo and admin seeding are not run outside Development.

`./scripts/verify-transactions.ps1` uses fresh provider transaction IDs for BET 10.00 then WIN 25.00, checks exact original BET replay, player history and admin reads. Each run legitimately changes demo USD by +15.00; its opening balance is whatever the previous run left, not silently reset to 100.00.

## Error format

RFC 9457 `application/problem+json`, stable `code`, request `traceId`, optional field `errors`; no stack traces/secrets. Example (terminal insufficient funds):

```json
{"type":"urn:miniwallet:problem:insufficient_funds","title":"Transaction rejected","status":409,"detail":"insufficient funds","code":"insufficient_funds","traceId":"00-example"}
```

Common statuses: `400 invalid_request` (validation); `401 unauthenticated` (missing/invalid bearer, with challenge); `403 forbidden` (wrong role/scope); `404 wallet_unavailable`; `409` durable business conflict or idempotency conflict; `503 transient_unavailability`. Verification's already-consumed token returns `400` without undoing verification; after a lost verification response, attempt login to confirm success. GET retries are safe. Unexpected errors return sanitized `500`; a failed/uncertain write must be retried with its original ID, never assumed successful.

Database choice reference: [PostgreSQL 16 numeric storage and rounding](https://www.postgresql.org/docs/16/datatype-numeric.html). Exact numeric with bounded checks preserves values rather than silently coercing their scale.
