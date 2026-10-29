# Live balances (single Wallet instance)

With the load-balanced Compose setup, use port 5082 for both Wallet HTTP requests and WebSocket connections to reach wallet-a directly. Port 5080 balances across two instances and does not provide cross-instance live delivery. See LOAD_BALANCER.md. Users remains on port 5081. For the UI set WALLET_API_URL=http://127.0.0.1:5082 before starting Vite.

Wallet hosts an authenticated SignalR endpoint at `/hubs/balance`. Users remains independently deployed and requires no changes. Run one Wallet API instance for live delivery; notifications are process-local with no backplane.

Only verified players with the existing `PlayerRead` authorization policy can connect. The server derives a private player group from the validated JWT `sub`; there is no client method to select a player or join another group. All connected tabs for the owner receive `BalanceChanged` with `{ "currency": "USD" }` (or EUR/BTC).

Accepted BET/WIN operations send the notification after the database commits. Rejected operations, rollbacks and idempotent replays do not send it. Dispatch is best effort with a two-second bound: a failed or lost notification never reverses a transaction or changes its successful response. This is not a durable event stream. Initial wallet provisioning and development fixture funding do not emit notifications.

The demo game and player API explorer subscribe using `@microsoft/signalr`, refresh balances on initial connection and reconnection, and fetch the authoritative REST balances on each notification. Reads are serialized and repeated if another notification arrives during a read. History retains its existing refresh behavior. Admin views use manual refresh. Manual refresh also remains available if a notification or refresh fails. An expired token closes the connection; sign in again (or reconnect the demo wallet) to obtain a fresh token.

The Vite `/wallet-api` proxy forwards WebSocket upgrades. Run the existing API setup, then `npm install` and `npm run dev` in `MiniWallet.UI`. Connect the same demo player in two tabs, place a bet in one, and the other tab's balance should refresh automatically. Restart Wallet and confirm balances reload on reconnection. The running Wallet must contain this implementation; rebuild/restart an older image first.

Browser clients supply the JWT through SignalR's `accessTokenFactory`; WebSocket/SSE transports use the `access_token` query parameter, accepted only under the hub route. Use HTTPS outside local development and exclude query tokens from proxy/access logs. A separately hosted UI needs a proxy to Wallet or explicit origin configuration; the provided UI uses the same-origin Vite proxy.

If Wallet later runs multiple replicas, add cross-instance SignalR delivery before claiming live updates across replicas. Ordinary database financial concurrency remains independent of this notification limitation.
