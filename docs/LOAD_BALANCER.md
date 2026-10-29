# Local load-balancer demonstration

From the repository root, run `docker compose up --build` (or add `-d` for detached mode).

The `wallet` Compose service is now Nginx, listening at http://localhost:5080. It routes HTTP requests round-robin to `wallet-a` and `wallet-b`, two independent processes built from the same Wallet image. They share only the Wallet PostgreSQL database. Both wait for the one `wallet-migrate` job. Users still owns its separate database and runs at http://localhost:5081; its delivery worker calls the Wallet proxy over the Compose network.

## Observe routing in Postman

Send `GET http://localhost:5080/health/ready` several times. In the response Headers tab, `X-Wallet-Upstream` identifies the container IP/port that served each request. You should observe two addresses. This diagnostic header is for the local demonstration, not part of the financial API contract. Map addresses to names with `docker compose exec wallet getent hosts wallet-a wallet-b`.

Financial safety is provided by PostgreSQL locking, constraints and durable idempotency outcomes, not by Nginx routing. The proxy does not enable replaying POSTs after they have been sent to an upstream, and does not retry application 409/503 responses. An ambiguous timeout still requires the caller to retry the same external transaction ID and payload. Passive failure handling and Docker DNS refresh provide a small local demonstration; this is not a production availability deployment, and Nginx/PostgreSQL remain single points of failure.

## Reproducible competing-bet check

Use a fresh isolated project to avoid changing your normal demo player's funds. In PowerShell:

```powershell
$env:WALLET_PORT = '5180'
$env:USERS_PORT = '5181'
$env:WALLET_DIRECT_PORT = '5182'
$env:MAILPIT_UI_PORT = '8125'
$env:MAILPIT_SMTP_PORT = '1125'
docker compose -p miniwallet-lb-check up -d --build
./scripts/verify-load-balancer.ps1 -UsersUrl http://localhost:5181 -WalletUrl http://localhost:5180
```

Wait for both `/health/ready` endpoints before running the script. It checks two upstream addresses, sends simultaneous BETs of 80 against the demo's initial USD 100, verifies they reach different hosts, expects one 201 and one 409, then replays the accepted BET through the other host and verifies the original response and final USD 20. It deliberately refuses to run if the starting balance is not 100. A repeat needs a fresh project name/unused ports; never reset an existing user's balance to force the test to pass.

Stop the isolated project with `docker compose -p miniwallet-lb-check down`. Its database volumes remain available. These port variables apply to subsequent Compose commands in the same shell; use a new terminal for the default ports.

The existing `scripts/verify-reviewer.ps1` accepts UsersUrl/WalletUrl parameters and exercises registration, verification, provisioning, transactions and authorization through the proxy. Unit/integration tests remain available with `docker compose -f compose.yaml -f compose.tests.yaml run --build --rm tests`.

## SignalR limitation

SignalR groups are process-local. A socket on A cannot receive a write notification emitted on B. WebSocket upgrade forwarding alone does not solve this; neither does sticky routing for separate player/provider clients.

For the optional single-instance live demo, use **http://localhost:5082 for every Wallet HTTP request** and **ws://localhost:5082/hubs/balance** for the socket. Both reach `wallet-a` directly. Users token requests still use 5081. For the React demo set `WALLET_API_URL=http://127.0.0.1:5082` before starting Vite. The normal load-balanced financial API stays at 5080. SignalR across the two instances is intentionally not supported; see REALTIME.md.

## Separate application updates

Wallet and Users retain separate Dockerfiles/images and migration jobs. To update Wallet only:

```powershell
docker compose build wallet-a
docker compose run --rm --no-deps wallet-migrate
docker compose up -d --no-deps wallet-a wallet-b
```

Users need not be redeployed. To update Users only:

```powershell
docker compose build users
docker compose run --rm --no-deps users-migrate
docker compose up -d --no-deps users
```

These commands assume the supporting database is already running. Schema compatibility during rolling updates and production rollback policies are beyond this demonstration. The proxy re-resolves Docker names after replacement.
