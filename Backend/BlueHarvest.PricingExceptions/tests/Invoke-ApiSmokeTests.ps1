param([string]$BaseUrl = 'http://localhost:5267')
$ErrorActionPreference = 'Stop'
$runKey = [guid]::NewGuid().ToString()
$manager = @{'X-User-Id'='11111111-1111-1111-1111-111111111111'; 'Idempotency-Key'="$runKey-create"}
$reviewer = @{'X-User-Id'='22222222-2222-2222-2222-222222222222'; 'Idempotency-Key'="$runKey-review"}
$process = @{'X-User-Id'='33333333-3333-3333-3333-333333333333'}

function Assert-Status([string]$Path, [hashtable]$Headers, [int]$Expected, [string]$Method = 'Get', [string]$Body = '') {
    $actual = 0
    try {
        $arguments = @{Uri="$BaseUrl$Path"; Headers=$Headers; Method=$Method; UseBasicParsing=$true}
        if ($Body) { $arguments.Body=$Body; $arguments.ContentType='application/json' }
        $response = Invoke-WebRequest @arguments
        $actual = [int]$response.StatusCode
    } catch {
        if ($null -eq $_.Exception.Response) { throw }
        $actual = [int]$_.Exception.Response.StatusCode
    }
    if ($actual -ne $Expected) { throw "$Method $Path expected $Expected, received $actual" }
}

$application = [guid]::NewGuid().ToString()
$body = @{mortgageApplicationId=$application; discountBps=50; reason='HTTP smoke test'} | ConvertTo-Json
$created = Invoke-WebRequest "$BaseUrl/api/v1/pricing-exceptions" -Method Post -Headers $manager -Body $body -ContentType 'application/json' -UseBasicParsing
if ($created.StatusCode -ne 201 -or -not $created.Headers.Location) { throw 'Creation response is invalid' }
$request = $created.Content | ConvertFrom-Json
$replay = Invoke-RestMethod "$BaseUrl/api/v1/pricing-exceptions" -Method Post -Headers $manager -Body $body -ContentType 'application/json'
if ($replay.id -ne $request.id) { throw 'Retry created a duplicate' }
$changedBody = @{mortgageApplicationId=$application; discountBps=60; reason='Changed'} | ConvertTo-Json
Assert-Status '/api/v1/pricing-exceptions' $manager 409 'Post' $changedBody

$decision = @{outcome='Approved'; expectedVersion=1; reviewedRevisionNumber=1; reason='Accepted'} | ConvertTo-Json
$approved = Invoke-RestMethod "$BaseUrl/api/v1/pricing-exceptions/$($request.id)/decisions" -Method Post -Headers $reviewer -Body $decision -ContentType 'application/json'
if ($approved.status -ne 'Approved') { throw 'Approval failed' }
$discount = Invoke-RestMethod "$BaseUrl/api/v1/mortgage-applications/$application/approved-discount" -Headers $process
if ($discount.discountBps -ne 50) { throw 'Approved discount is incorrect' }

$manager['Idempotency-Key'] = "$runKey-revise"
$revision = @{discountBps=60; reason='Updated offer'; expectedVersion=2; expectedRevisionNumber=1} | ConvertTo-Json
$revised = Invoke-RestMethod "$BaseUrl/api/v1/pricing-exceptions/$($request.id)/revisions" -Method Post -Headers $manager -Body $revision -ContentType 'application/json'
if ($revised.status -ne 'Pending' -or $revised.revisionNumber -ne 2) { throw 'Revision failed' }
$history = Invoke-RestMethod "$BaseUrl/api/v1/pricing-exceptions/$($request.id)/history" -Headers $manager
if ($history.events.Count -ne 3 -or $history.revisions.Count -ne 2 -or $history.decisions.Count -ne 1) { throw 'History is incomplete' }

Assert-Status "/api/v1/mortgage-applications/$application/approved-discount" $process 404
Assert-Status "/api/v1/pricing-exceptions/$($request.id)" @{} 401
Assert-Status "/api/v1/mortgage-applications/$application/approved-discount" $manager 403
Assert-Status '/api/v1/pricing-exceptions' $manager 400 'Post' '{"discountBps":0,"reason":""}'
$reviewer['Idempotency-Key'] = "$runKey-invalid"
Assert-Status "/api/v1/pricing-exceptions/$($request.id)/decisions" $reviewer 400 'Post' '{"expectedVersion":3,"reviewedRevisionNumber":2}'
Assert-Status '/swagger/index.html' @{} 200
$schema = Invoke-RestMethod "$BaseUrl/openapi/v1.json"
if (-not $schema.paths.'/api/v1/pricing-exceptions'.post) { throw 'Create endpoint missing from OpenAPI' }
if (-not ($schema.paths.'/api/v1/pricing-exceptions'.post.parameters | Where-Object name -eq 'X-User-Id')) { throw 'Demo identity header missing from Swagger' }
Write-Output 'HTTP smoke tests passed: create, replay, conflict, approve, discount, revision, history, validation, authentication, authorization and Swagger.'
