# MiniWallet

A .NET 8 wallet platform with two independent APIs: **Users** handles registration and email verification; **Wallet** manages USD, EUR and BTC balances and provider BET/WIN transactions. Built with ASP.NET Core, EF Core, PostgreSQL, FluentValidation and xUnit.

## Run

Requires Docker with Linux containers and Docker Compose. Keep ports **5080** and **5081** free; the first build needs internet access.

```sh
docker compose up --build
```

Databases, migrations and demo accounts are created automatically. No host .NET SDK, manual SQL or external account is needed.

- [Users Swagger](http://127.0.0.1:5081/swagger)
- [Wallet Swagger](http://127.0.0.1:5080/swagger)
- [Verification email inbox (Mailpit)](http://127.0.0.1:8025)

Mailpit captures verification emails locally; no external email account or credentials are needed. For local debugging, run `docker compose up -d mailpit` before starting Users. See [SMTP setup and recovery](docs/SMTP.md).

Data survives restart and `docker compose down`. Adding `-v` deletes the database volume.

## Try it

### React betting demo

A basic React sports betting page in `MiniWallet.UI` shows a fictional Premier League fixture, Arsenal vs Chelsea. Pick Arsenal (2.10), draw (3.40) or Chelsea (3.60), choose USD, EUR or BTC and enter a stake. Placing the bet submits a BET debit. A separate **Simulate full-time** control finishes the match; a winning selection submits a matching WIN for stake × decimal odds, including the stake and rounded down to the currency's smallest unit. All odds and results are sample data; no real money is involved.

Start the APIs with `docker compose up --build`, then in another terminal (Node.js 20.19+ or 22.12+ required):

```sh
cd MiniWallet.UI
npm install
npm run dev
```

Open [the betting demo](http://127.0.0.1:5173), click **Connect demo wallet**, select a full-time result on the match card, choose a stake and **Place bet**. Then select a demo result and click **Simulate full-time**. For example, USD 10 on Arsenal returns USD 21 if Arsenal wins; a draw or Chelsea win has no payout. Random mode gives each result an equal chance, independently of the illustrative odds. Your balance and transaction activity update after placing and settling the bet. Expand **Under the hood** to see the API calls. The page uses the seeded demo account and provider credentials automatically; both APIs must run in Development. Its shared starting funds are USD 100, EUR 100 and BTC 0.01; existing activity changes these balances and there is no top-up endpoint.

An unfinished bet is saved locally with its exact transaction IDs and payload. After simulating full-time, its result is also saved before settlement. **Resume saved bet** retries the same operation after an uncertain response instead of placing another bet or generating a new result. Tokens remain in memory. Do not clear browser storage while a bet is pending. A completed demo can be played again as a fresh simulated fixture.

The **API explorer** link opens the original endpoint playground for registration, verification, player/admin login, wallet reads, manual provider requests and health checks. Credentials and tokens are redacted from its inspector.

For a new account, enter a new email and a password of at least 12 characters under **Player onboarding**, register, read the development mailbox preview or open Mailpit, paste the token, and verify. Then sign in under Wallets. New wallets start at zero and may take a moment to provision.

The Vite server proxies `/users-api` and `/wallet-api` to ports 5081 and 5080, so no backend CORS changes are needed. To use different ports, copy `MiniWallet.UI/.env.example` to `MiniWallet.UI/.env.local`, adjust the targets, and restart Vite. Tokens and credentials stay in browser memory and are cleared by a page refresh. Development issuers and the mailbox require the APIs to run in Development. `npm run build` creates a production bundle; `npm run preview` serves it locally with the same proxy. Deploying the bundle elsewhere requires equivalent reverse proxy routes.

### PowerShell and Swagger

The quickest walkthrough uses PowerShell:

```powershell
./scripts/verify-reviewer.ps1
```

It checks registration, verification, zero-balance wallets, demo BET/WIN, replay, history and access rules. To explore manually, use Swagger's **Try it out**:

1. In Users, register with `POST /api/v1/register`:
   `{"email":"player@example.test","password":"Player-password-123!"}`.
2. Open [Mailpit](http://127.0.0.1:8025), find the verification email and submit its token to `POST /api/v1/email-verifications` as `{"token":"<token>"}`. The existing key-protected `GET /dev/mailbox` preview also works after SMTP acceptance.
3. Send the same email/password to `POST /dev/token`. Copy `access_token`, then paste it into Wallet Swagger's **Authorize** dialog.
4. Read `/api/v1/me/wallets` and `/api/v1/me/wallets/USD/transactions`. New players automatically receive three **zero-balance** wallets; provisioning may take a moment.

### Local demo credentials

These credentials and the token issuer are **Development-only**. Tokens expire after 10 minutes.

| Identity | Credentials | Token endpoint |
| --- | --- | --- |
| Funded player | `demo@miniwallet.test` / `Local-demo-password-123!` | `POST /dev/token` |
| Admin | `admin@miniwallet.test` / `Local-admin-password-123!` | `POST /dev/token` |
| Provider client | `local-provider` / `local-provider-secret-123!` | `POST /dev/connect/token` |

Providers use a separate machine login:

```json
{"clientId":"local-provider","clientSecret":"local-provider-secret-123!","scope":"transactions:write"}
```

Use that provider token for `POST /api/v1/transactions`. The funded demo player ID is `de000000-0000-4000-8000-000000000001`:

```json
{"externalTransactionId":"demo-bet-001","playerId":"de000000-0000-4000-8000-000000000001","currency":"USD","type":"BET","amount":"10.00"}
```

Then settle the bet:

```json
{"externalTransactionId":"demo-win-001","playerId":"de000000-0000-4000-8000-000000000001","currency":"USD","type":"WIN","amount":"25.00","betExternalTransactionId":"demo-bet-001"}
```

The demo starts with explicit opening credits of **USD 100, EUR 100 and BTC 0.01**; restart does not replenish them. Ordinary registrations stay at zero. One successful WIN per BET is the documented settlement assumption.

Repeat the same request to replay its original result. Use a new transaction ID for a new action; after a timeout, retry the **same ID and payload**. Amounts are decimal strings: USD/EUR allow 2 fractional digits, BTC 8.

Log in as the demo player to inspect its balances/history. Admins can read `/api/v1/players/{playerId}/wallets` and its currency history routes. Players can read only their own wallets; providers cannot use those read routes.

## Test

```sh
docker compose -f compose.yaml -f compose.tests.yaml run --build --rm tests
```

Runs xUnit against real, disposable PostgreSQL test databases. Tests focus on financial rules, concurrent writes, atomicity, replay/recovery and authorization. Application data is preserved. See [verification results](docs/PROGRESS.md).

## Troubleshooting

- **Startup fails:** confirm Docker is running, check port availability, then inspect `docker compose logs postgres wallet-migrate users-migrate`.
- **Verification message missing:** call `POST /api/v1/email-verifications/resend` with the player's email/password.
- **401 or 403:** renew the token or use the correct player, admin or provider credentials.

See the [API contract](docs/API_CONTRACT.md) for precision, pagination, errors and retry rules; [design](DESIGN.md) for architecture and trade-offs; and [AI usage](AI_USAGE.md) for the development record.
