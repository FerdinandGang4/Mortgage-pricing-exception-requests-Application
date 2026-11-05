param([string]$BaseUrl = 'http://localhost:5173')
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '../../..')
try {
    $manager = @{'X-User-Id'='11111111-1111-1111-1111-111111111111'; 'Idempotency-Key'=[guid]::NewGuid().ToString()}
    $reviewer = @{'X-User-Id'='22222222-2222-2222-2222-222222222222'; 'Idempotency-Key'=[guid]::NewGuid().ToString()}
    $body = @{mortgageApplicationId=[guid]::NewGuid().ToString(); reviewerId=$reviewer['X-User-Id']; discountBps=50; reason='PostgreSQL persistence verification'} | ConvertTo-Json
    $created = Invoke-RestMethod "$BaseUrl/api/v1/pricing-exceptions" -Method Post -Headers $manager -ContentType 'application/json' -Body $body
    $decision = @{outcome='Approved'; expectedVersion=1; reviewedRevisionNumber=1; reason='Persistence check'} | ConvertTo-Json
    $null = Invoke-RestMethod "$BaseUrl/api/v1/pricing-exceptions/$($created.id)/decisions" -Method Post -Headers $reviewer -ContentType 'application/json' -Body $decision

    # Recreate every container while retaining the named database volume.
    docker compose down
    if ($LASTEXITCODE -ne 0) { throw 'Compose down failed' }
    docker compose up -d
    if ($LASTEXITCODE -ne 0) { throw 'Compose up failed' }
    $loaded = $null
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try {
            $loaded = Invoke-RestMethod "$BaseUrl/api/v1/pricing-exceptions/$($created.id)" -Headers $manager -TimeoutSec 2
            break
        } catch { Start-Sleep -Seconds 1 }
    }
    if ($null -eq $loaded -or $loaded.status -ne 'Approved' -or $loaded.version -ne 2) { throw 'Approved request did not survive container recreation' }
    $history = Invoke-RestMethod "$BaseUrl/api/v1/pricing-exceptions/$($created.id)/history" -Headers $manager
    if ($history.events.Count -ne 2 -or $history.revisions.Count -ne 1 -or $history.decisions.Count -ne 1) { throw 'Audit data did not persist' }
    $replay = Invoke-RestMethod "$BaseUrl/api/v1/pricing-exceptions" -Method Post -Headers $manager -ContentType 'application/json' -Body $body
    if ($replay.id -ne $created.id -or $replay.version -ne 1) { throw 'Original retry result did not persist' }
    Write-Output 'PostgreSQL persistence passed: approved request, revision, decision, history, and original retry result survived Compose down/up.'
} finally {
    Pop-Location
}
