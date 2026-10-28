# Run from the repository root after docker compose up --build.
# Uses local reviewer credentials to inspect each database; services never cross-read tables.
$ErrorActionPreference = 'Stop'

function Sql([string]$database, [string]$user, [string]$query) {
    $output = $query | docker compose exec -T postgres psql -U $user -d $database -Atq
    if ($LASTEXITCODE -ne 0) { throw "SQL check failed for $database" }
    return (($output -join [Environment]::NewLine) | ConvertFrom-Json)
}
function Delivery([guid]$player) {
    Sql users users_app @"
SELECT json_build_object('eventId', "EventId", 'playerId', "PlayerId", 'occurredAt', "OccurredAt",
 'deliveredAt', "DeliveredAt", 'attempts', "Attempts")
FROM "PlayerRegistrationDeliveries" WHERE "PlayerId" = '$player';
"@
}
function Wallets([guid]$player) {
    Sql wallet wallet_app @"
SELECT COALESCE(json_agg(json_build_object('currency', "Currency", 'balance', "Balance")), '[]'::json)
FROM "Wallets" WHERE "PlayerId" = '$player';
"@
}
function Register {
    $email = 'scenario-' + [guid]::NewGuid().ToString('N') + '@example.test'
    $response = Invoke-WebRequest http://127.0.0.1:5081/api/v1/players -Method Post -UseBasicParsing -TimeoutSec 5 -ContentType application/json -Body (@{
        email = $email; password = 'Scenario-password-123!'
    } | ConvertTo-Json)
    if ($response.StatusCode -ne 201) { throw 'Registration was not accepted' }
    return ($response.Content | ConvertFrom-Json)
}
function Wait-Ready {
    for ($i = 0; $i -lt 40; $i++) {
        try {
            if ((Invoke-WebRequest http://127.0.0.1:5081/health/ready -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { return }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw 'Users did not become ready'
}
function Wait-Provisioned([guid]$player) {
    for ($i = 0; $i -lt 40; $i++) {
        $delivery = Delivery $player
        if ($delivery.deliveredAt) {
            $wallets = @(Wallets $player)
            if ($wallets.Count -ne 3 -or (@($wallets | Where-Object { [decimal]$_.balance -ne 0 }).Count -ne 0)) {
                throw 'Expected exactly three zero wallets'
            }
            if (($wallets.currency | Sort-Object) -join ',' -ne 'BTC,EUR,USD') { throw 'Unexpected currencies' }
            return $delivery
        }
        Start-Sleep -Milliseconds 500
    }
    throw 'Provisioning did not finish within the bounded observation window'
}

Wait-Ready
$normal = Register
$normalDelivery = Wait-Provisioned ([guid]$normal.playerId)
Write-Output 'PASS normal registration -> automatic three zero wallets'

$token = Invoke-RestMethod http://127.0.0.1:5081/dev/connect/token -Method Post -TimeoutSec 5 -ContentType application/json -Body (@{
    clientId='users-delivery'; clientSecret='local-internal-secret-123!'; scope='players:provision'
} | ConvertTo-Json)
$replay = @{ eventId=$normalDelivery.eventId; playerId=$normal.playerId; occurredAt=$normalDelivery.occurredAt; version=1 } | ConvertTo-Json
$response = Invoke-WebRequest http://127.0.0.1:5080/internal/v1/player-registrations -Method Post -UseBasicParsing -TimeoutSec 5 -ContentType application/json -Headers @{
    Authorization='Bearer ' + $token.access_token
} -Body $replay
if ($response.StatusCode -ne 204 -or @(Wallets ([guid]$normal.playerId)).Count -ne 3) { throw 'Duplicate delivery failed' }
Write-Output 'PASS duplicate event -> 204 and still three wallets'

docker compose stop wallet
if ($LASTEXITCODE -ne 0) { throw 'Could not stop Wallet' }
try {
    $pending = Register
    $before = Delivery ([guid]$pending.playerId)
    if ($before.deliveredAt) { throw 'Unexpected delivery while Wallet is down' }
    if (@(Wallets ([guid]$pending.playerId)).Count -ne 0) { throw 'Unexpected wallets during outage' }
    Write-Output 'PASS Wallet outage -> registration 201, durable pending delivery'
    docker compose restart users
    if ($LASTEXITCODE -ne 0) { throw 'Could not restart Users' }
    Wait-Ready
    $after = Delivery ([guid]$pending.playerId)
    if ($after.eventId -ne $before.eventId -or $after.deliveredAt) { throw 'Pending event did not survive Users restart' }
    Write-Output 'PASS Users restart -> same pending event survives'
} finally {
    docker compose start wallet
    if ($LASTEXITCODE -ne 0) { throw 'Could not restore Wallet' }
}
$null = Wait-Provisioned ([guid]$pending.playerId)
Write-Output 'PASS Wallet recovery -> automatic provisioning completes without a player create-wallet call'

