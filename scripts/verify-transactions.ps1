# Uses only documented Development credentials; runs against the existing Compose stack.
$ErrorActionPreference = 'Stop'
$users = 'http://127.0.0.1:5081'
$wallet = 'http://127.0.0.1:5080'
function Token([object]$body, [string]$path) {
    (Invoke-RestMethod "$users/$path" -Method Post -TimeoutSec 5 -ContentType application/json -Body ($body | ConvertTo-Json)).access_token
}
$providerToken = Token @{clientId='local-provider';clientSecret='local-provider-secret-123!';scope='transactions:write'} 'dev/connect/token'
$playerToken = Token @{email='demo@miniwallet.test';password='Local-demo-password-123!'} 'dev/token'
$playerHeaders = @{Authorization="Bearer $playerToken"}
$providerHeaders = @{Authorization="Bearer $providerToken"}
$player = 'de000000-0000-4000-8000-000000000001'
$before = Invoke-RestMethod "$wallet/api/v1/me/wallets" -Headers $playerHeaders -TimeoutSec 5
$opening = [decimal]($before.wallets | Where-Object currency -eq USD).balance
$betId = 'review-bet-' + [guid]::NewGuid().ToString('N')
$bet = @{externalTransactionId=$betId;playerId=$player;currency='USD';type='BET';amount='10.00'} | ConvertTo-Json
$first = Invoke-WebRequest "$wallet/api/v1/transactions" -Method Post -Headers $providerHeaders -Body $bet -ContentType application/json -UseBasicParsing -TimeoutSec 8
if ($first.StatusCode -ne 201) { throw 'BET not accepted' }
$win = @{externalTransactionId='review-win-'+[guid]::NewGuid().ToString('N');playerId=$player;currency='USD';type='WIN';amount='25.00';betExternalTransactionId=$betId} | ConvertTo-Json
$settlement = Invoke-WebRequest "$wallet/api/v1/transactions" -Method Post -Headers $providerHeaders -Body $win -ContentType application/json -UseBasicParsing -TimeoutSec 8
if ($settlement.StatusCode -ne 201) { throw 'WIN not accepted' }
$replay = Invoke-WebRequest "$wallet/api/v1/transactions" -Method Post -Headers $providerHeaders -Body $bet -ContentType application/json -UseBasicParsing -TimeoutSec 8
if ($replay.StatusCode -ne 201 -or $replay.Content -cne $first.Content) { throw 'Replay differs from original acceptance' }
$after = Invoke-RestMethod "$wallet/api/v1/me/wallets" -Headers $playerHeaders -TimeoutSec 5
$closing = [decimal]($after.wallets | Where-Object currency -eq USD).balance
if ($closing -ne ($opening + 15)) { throw 'Unexpected closing balance' }
$history = Invoke-RestMethod "$wallet/api/v1/me/wallets/USD/transactions?page=1&pageSize=2" -Headers $playerHeaders -TimeoutSec 5
if ($history.items.Count -ne 2 -or $history.pageSize -ne 2) { throw 'History page is invalid' }
$adminToken = Token @{email='admin@miniwallet.test';password='Local-admin-password-123!'} 'dev/token'
$admin = Invoke-WebRequest "$wallet/api/v1/players/$player/wallets" -Headers @{Authorization="Bearer $adminToken"} -UseBasicParsing -TimeoutSec 5
if ($admin.StatusCode -ne 200) { throw 'Admin read failed' }
Write-Output "PASS BET 201, WIN 201, exact replay 201/original body, own history and admin read; USD $opening -> $closing"
Write-Output 'Fixture actions are recorded in immutable history; rerunning adds another BET/WIN with fresh IDs.'

