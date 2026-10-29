param(
    [string]$UsersUrl = 'http://127.0.0.1:5081',
    [string]$WalletUrl = 'http://127.0.0.1:5080',
    [string]$StatePath,
    [switch]$CheckPersistence
)
# Local Development reviewer flow. No tokens/passwords are written to the optional state file.
$ErrorActionPreference = 'Stop'
function Post([string]$url, [object]$body, [hashtable]$headers = @{}) {
    Invoke-WebRequest $url -Method Post -UseBasicParsing -TimeoutSec 10 -ContentType application/json -Headers $headers -Body ($body | ConvertTo-Json)
}
function Get([string]$url, [hashtable]$headers = @{}) {
    Invoke-RestMethod $url -Headers $headers -TimeoutSec 5
}
function Login([string]$email, [string]$password) {
    $response = Post "$UsersUrl/dev/token" @{email=$email;password=$password}
    @{Authorization='Bearer ' + ($response.Content | ConvertFrom-Json).access_token}
}
function Ready([string]$base) {
    for ($i=0; $i -lt 40; $i++) {
        try { if ((Get "$base/health/ready").status -eq 'ready') { return } } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw "Service not ready: $base"
}
function ExpectDenied([string]$url, [hashtable]$headers) {
    try { $null = Get $url $headers; throw 'Expected authorization failure' }
    catch {
        if (-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 403) { throw }
    }
}
Ready $UsersUrl
Ready $WalletUrl
foreach ($base in @($UsersUrl,$WalletUrl)) {
    $html = Invoke-WebRequest "$base/swagger/index.html" -UseBasicParsing -TimeoutSec 5
    if ($html.StatusCode -ne 200 -or $html.Content -notmatch 'swagger-ui') { throw 'Swagger UI is unavailable' }
    $null = Get "$base/swagger/v1/swagger.json"
}
Write-Output 'PASS both Swagger UIs/OpenAPI documents and database readiness'

if ($CheckPersistence) {
    if (-not $StatePath -or -not (Test-Path -LiteralPath $StatePath)) { throw 'A previous walkthrough state file is required' }
    $state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
    $playerHeaders = Login $state.email 'Reviewer-password-123!'
    $demoHeaders = Login 'demo@miniwallet.test' 'Local-demo-password-123!'
    $own = Get "$WalletUrl/api/v1/me/wallets" $playerHeaders
    if ($own.playerId -ne $state.playerId -or $own.wallets.Count -ne 3 -or
        @($own.wallets | Where-Object { [decimal]$_.balance -ne 0 }).Count -ne 0) { throw 'Player state did not persist' }
    $demo = Get "$WalletUrl/api/v1/me/wallets" $demoHeaders
    if (($demo.wallets | Where-Object currency -eq USD).balance -ne $state.demoUsd) { throw 'Demo balance changed on restart' }
    $history = Get "$WalletUrl/api/v1/me/wallets/USD/transactions?pageSize=100" $demoHeaders
    if (-not ($history.items.externalTransactionId -contains $state.betId) -or
        -not ($history.items.externalTransactionId -contains $state.winId)) { throw 'History did not persist' }
    Write-Output 'PASS ordinary restart preserved verified login, three zero wallets, demo balance and BET/WIN history'
    return
}

$email = 'reviewer-' + [guid]::NewGuid().ToString('N') + '@example.test'
$registration = Post "$UsersUrl/api/v1/register" @{email=$email;password='Reviewer-password-123!'}
if ($registration.StatusCode -ne 201) { throw 'Registration failed' }
$player = $registration.Content | ConvertFrom-Json
$messages = Get "$UsersUrl/dev/mailbox" @{'X-Dev-Mailbox-Key'='local-mailbox-key'}
$message = $messages | Where-Object email -eq $email | Select-Object -Last 1
if (-not $message.token) { throw 'No verification message found' }
$verification = Post "$UsersUrl/api/v1/email-verifications" @{token=$message.token}
if ($verification.StatusCode -ne 204) { throw 'Verification failed' }
$playerHeaders = Login $email 'Reviewer-password-123!'
$own = $null
for ($i=0; $i -lt 40; $i++) {
    try { $own = Get "$WalletUrl/api/v1/me/wallets" $playerHeaders; break } catch {
        if (-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 404) { throw }
        Start-Sleep -Milliseconds 500
    }
}
if (-not $own -or $own.playerId -ne $player.playerId -or $own.wallets.Count -ne 3 -or
    @($own.wallets | Where-Object { [decimal]$_.balance -ne 0 }).Count -ne 0 -or
    (($own.wallets.currency | Sort-Object) -join ',') -ne 'BTC,EUR,USD') { throw 'New player must have three zero wallets' }
Write-Output 'PASS register -> verification message -> verify -> player token -> USD/EUR/BTC zero wallets'

$machine = Post "$UsersUrl/dev/connect/token" @{clientId='local-provider';clientSecret='local-provider-secret-123!';scope='transactions:write'}
$providerHeaders = @{Authorization='Bearer ' + ($machine.Content | ConvertFrom-Json).access_token}
$demoHeaders = Login 'demo@miniwallet.test' 'Local-demo-password-123!'
$demo = Get "$WalletUrl/api/v1/me/wallets" $demoHeaders
$demoId = $demo.playerId
$before = [decimal]($demo.wallets | Where-Object currency -eq USD).balance
$betId = 'review-bet-' + [guid]::NewGuid().ToString('N')
$winId = 'review-win-' + [guid]::NewGuid().ToString('N')
$betBody = @{externalTransactionId=$betId;playerId=$demoId;currency='USD';type='BET';amount='10.00'}
$bet = Post "$WalletUrl/api/v1/transactions" $betBody $providerHeaders
$win = Post "$WalletUrl/api/v1/transactions" @{externalTransactionId=$winId;playerId=$demoId;currency='USD';type='WIN';amount='25.00';betExternalTransactionId=$betId} $providerHeaders
$replay = Post "$WalletUrl/api/v1/transactions" $betBody $providerHeaders
if ($bet.StatusCode -ne 201 -or $win.StatusCode -ne 201 -or $replay.StatusCode -ne 201 -or
    $bet.Content -cne $replay.Content) { throw 'BET/WIN/replay contract failed' }
$after = Get "$WalletUrl/api/v1/me/wallets" $demoHeaders
$closing = ($after.wallets | Where-Object currency -eq USD).balance
if ([decimal]$closing -ne $before+15) { throw 'Balance arithmetic failed' }
$history = Get "$WalletUrl/api/v1/me/wallets/USD/transactions?pageSize=100" $demoHeaders
if (-not ($history.items.externalTransactionId -contains $betId) -or
    -not ($history.items.externalTransactionId -contains $winId) -or
    -not ($history.items.type -contains 'DEV_FUNDING')) { throw 'Financial history is incomplete' }
Write-Output "PASS separate funded demo BET/WIN, exact replay and opening/history inspection; USD $before -> $closing"

$adminHeaders = Login 'admin@miniwallet.test' 'Local-admin-password-123!'
$adminView = Get "$WalletUrl/api/v1/players/$demoId/wallets" $adminHeaders
if ($adminView.playerId -ne $demoId) { throw 'Admin read failed' }
ExpectDenied "$WalletUrl/api/v1/players/$demoId/wallets" $playerHeaders
ExpectDenied "$WalletUrl/api/v1/players/$demoId/wallets/USD/transactions" $playerHeaders
ExpectDenied "$WalletUrl/api/v1/me/wallets" $providerHeaders
$isolated = Get "$WalletUrl/api/v1/me/wallets?playerId=$demoId" $playerHeaders
if ($isolated.playerId -ne $player.playerId) { throw 'Player identity override leaked another account' }
Write-Output 'PASS admin access, player isolation and provider read rejection'
if ($StatePath) {
    @{email=$email;playerId=$player.playerId;demoUsd=$closing;betId=$betId;winId=$winId} |
        ConvertTo-Json | Set-Content -LiteralPath $StatePath -Encoding utf8
}

