param(
    [string]$UsersUrl = 'http://127.0.0.1:5081',
    [string]$WalletUrl = 'http://127.0.0.1:5080'
)
# Run against a fresh, isolated Compose project. This records real demo transactions.
$ErrorActionPreference = 'Stop'
function Token($body, $path) {
    (Invoke-RestMethod "$UsersUrl/$path" -Method Post -ContentType application/json -Body ($body | ConvertTo-Json)).access_token
}
$playerToken = Token @{email='demo@miniwallet.test';password='Local-demo-password-123!'} 'dev/token'
$providerToken = Token @{clientId='local-provider';clientSecret='local-provider-secret-123!';scope='transactions:write'} 'dev/connect/token'
$headers = @{Authorization="Bearer $playerToken"}
$before = Invoke-RestMethod "$WalletUrl/api/v1/me/wallets" -Headers $headers
if (($before.wallets | Where-Object currency -eq USD).balance -ne '100.00') {
    throw 'Use a fresh isolated Compose project: demo USD balance must be exactly 100.00. No data will be reset.'
}
$upstreams = @{}
1..8 | ForEach-Object {
    $probe = Invoke-WebRequest "$WalletUrl/health/ready" -TimeoutSec 10
    $upstreams[([string]$probe.Headers['X-Wallet-Upstream'])] = $true
}
if ($upstreams.Count -ne 2) { throw 'Expected routing to two different Wallet upstreams' }
Write-Output "PASS routing to two Wallet instances: $($upstreams.Keys -join ', ')"

$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(15)
$client.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $providerToken)
$requests = @()
$responses = @()
try {
    $payloads = 1..2 | ForEach-Object {
        @{externalTransactionId='lb-bet-'+[guid]::NewGuid().ToString('N');playerId=$before.playerId;currency='USD';type='BET';amount='80.00'} | ConvertTo-Json -Compress
    }
    # Send both before awaiting either; repeat a rejected payload to observe a second host if necessary.
    $requests = @($payloads | ForEach-Object { [System.Net.Http.StringContent]::new($_, [Text.Encoding]::UTF8, 'application/json') })
    $tasks = @($requests | ForEach-Object { $client.PostAsync("$WalletUrl/api/v1/transactions", $_) })
    $responses = @($tasks | ForEach-Object { $_.GetAwaiter().GetResult() })
    if (@($responses | Where-Object { [int]$_.StatusCode -eq 201 }).Count -ne 1 -or
        @($responses | Where-Object { [int]$_.StatusCode -eq 409 }).Count -ne 1) { throw 'Expected one 201 and one 409' }
    $raceHosts = @($responses | ForEach-Object { [string]($_.Headers.GetValues('X-Wallet-Upstream') -join ',') } | Sort-Object -Unique)
    if ($raceHosts.Count -ne 2) { throw 'Concurrent requests did not reach distinct hosts; rerun with a fresh isolated project' }
    $winner = if ([int]$responses[0].StatusCode -eq 201) { 0 } else { 1 }
    $original = $responses[$winner].Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $originalHost = [string]($responses[$winner].Headers.GetValues('X-Wallet-Upstream') -join ',')
    $otherHostReplay = $false
    for ($attempt=0; $attempt -lt 8; $attempt++) {
        $content = [System.Net.Http.StringContent]::new($payloads[$winner], [Text.Encoding]::UTF8, 'application/json')
        try {
            $replay = $client.PostAsync("$WalletUrl/api/v1/transactions", $content).GetAwaiter().GetResult()
            try {
                if ([int]$replay.StatusCode -ne 201 -or $replay.Content.ReadAsStringAsync().GetAwaiter().GetResult() -cne $original) { throw 'Replay differs from original' }
                if ([string]($replay.Headers.GetValues('X-Wallet-Upstream') -join ',') -ne $originalHost) { $otherHostReplay = $true; break }
            } finally { $replay.Dispose() }
        } finally { $content.Dispose() }
    }
    if (-not $otherHostReplay) { throw 'No replay observed on the other instance' }
    $after = Invoke-RestMethod "$WalletUrl/api/v1/me/wallets" -Headers $headers
    if (($after.wallets | Where-Object currency -eq USD).balance -ne '20.00') { throw 'Expected remaining USD balance 20.00' }
    Write-Output 'PASS concurrent 80+80 against 100 reached different hosts: one 201, one 409, final USD 20.00'
    Write-Output 'PASS exact replay through the other host returned the original response without a second debit'
} finally {
    foreach ($response in $responses) { $response.Dispose() }
    foreach ($request in $requests) { $request.Dispose() }
    $client.Dispose()
}
