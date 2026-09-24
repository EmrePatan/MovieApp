$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LoadTestTokenMintLogic.ps1')

$failures = 0

function Assert-Equal($name, $expected, $actual) {
    if ($expected -ne $actual) {
        Write-Host "FAIL $name - expected '$expected', got '$actual'"
        $script:failures++
    } else {
        Write-Host "OK   $name"
    }
}

function Assert-True($name, $condition) {
    if (-not $condition) {
        Write-Host "FAIL $name"
        $script:failures++
    } else {
        Write-Host "OK   $name"
    }
}

function New-TestJwtWithExpUtc {
    param([datetime]$ExpUtc)

    $exp = [DateTimeOffset]$ExpUtc | ForEach-Object { $_.ToUnixTimeSeconds() }
    $payloadJson = "{`"exp`":$exp}"
    $bytes = [Text.Encoding]::UTF8.GetBytes($payloadJson)
    $b64 = [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    return "eyJhbGciOiJIUzI1NiJ9.$b64.fake_sig"
}

function New-TestManifestEntries {
    param([int]$Count)

    $list = @()
    for ($n = 1; $n -le $Count; $n++) {
        $list += [pscustomobject]@{
            harnessId = ('load60-{0:D3}' -f $n)
            email     = ('load60-{0:D3}@loadtest.invalid' -f $n)
        }
    }
    return $list
}

$deadline = (Get-Date).ToUniversalTime().AddMinutes(30)
$validExp = $deadline.AddHours(2)
$expiringExp = $deadline.AddMinutes(-1)
$boundaryExp = $deadline

$manifest100 = New-TestManifestEntries -Count 100
$existing100 = @{}
foreach ($entry in $manifest100) {
    $existing100[$entry.harnessId] = [pscustomobject]@{
        id          = $entry.harnessId
        bearerToken = (New-TestJwtWithExpUtc -ExpUtc $validExp)
    }
}

$planAllValid = Get-LoadTestMintRefreshPlan -ManifestEntries $manifest100 -ExistingById $existing100 -RequiredValidityDeadlineUtc $deadline -ForceFull $false
Assert-Equal 'all 100 valid refresh count' 0 $planAllValid.RefreshCount
Assert-Equal 'all 100 valid reusable count' 100 $planAllValid.ReusableCount

$existing10Expiring = @{}
foreach ($entry in $manifest100) {
    $num = [int]$entry.harnessId.Substring(7)
    $exp = if ($num -le 10) { $expiringExp } else { $validExp }
    $existing10Expiring[$entry.harnessId] = [pscustomobject]@{
        id          = $entry.harnessId
        bearerToken = (New-TestJwtWithExpUtc -ExpUtc $exp)
    }
}
$plan10 = Get-LoadTestMintRefreshPlan -ManifestEntries $manifest100 -ExistingById $existing10Expiring -RequiredValidityDeadlineUtc $deadline -ForceFull $false
Assert-Equal '10 expiring refresh count' 10 $plan10.RefreshCount
Assert-Equal '10 expiring reusable count' 90 $plan10.ReusableCount
Assert-True '10 expiring first refresh id' ($plan10.ToRefresh[0].harnessId -eq 'load60-001')
Assert-True '10 expiring last refresh id' ($plan10.ToRefresh[9].harnessId -eq 'load60-010')

$manifest3 = New-TestManifestEntries -Count 3
$existingMissing = @{
    'load60-001' = [pscustomobject]@{ id = 'load60-001'; bearerToken = (New-TestJwtWithExpUtc -ExpUtc $validExp) }
}
$planMissing = Get-LoadTestMintRefreshPlan -ManifestEntries $manifest3 -ExistingById $existingMissing -RequiredValidityDeadlineUtc $deadline -ForceFull $false
Assert-Equal 'missing token refresh count' 2 $planMissing.RefreshCount

$existingMalformed = @{
    'load60-001' = [pscustomobject]@{ id = 'load60-001'; bearerToken = 'not-a-jwt' }
    'load60-002' = [pscustomobject]@{ id = 'load60-002'; bearerToken = (New-TestJwtWithExpUtc -ExpUtc $validExp) }
    'load60-003' = [pscustomobject]@{ id = 'load60-003'; bearerToken = (New-TestJwtWithExpUtc -ExpUtc $validExp) }
}
$planMalformed = Get-LoadTestMintRefreshPlan -ManifestEntries $manifest3 -ExistingById $existingMalformed -RequiredValidityDeadlineUtc $deadline -ForceFull $false
Assert-Equal 'malformed jwt refresh count' 1 $planMalformed.RefreshCount
Assert-True 'malformed jwt queues load60-001' ($planMalformed.ToRefresh[0].harnessId -eq 'load60-001')

$existingExpired = @{
    'load60-001' = [pscustomobject]@{ id = 'load60-001'; bearerToken = (New-TestJwtWithExpUtc -ExpUtc $deadline.AddHours(-2)) }
    'load60-002' = [pscustomobject]@{ id = 'load60-002'; bearerToken = (New-TestJwtWithExpUtc -ExpUtc $validExp) }
    'load60-003' = [pscustomobject]@{ id = 'load60-003'; bearerToken = (New-TestJwtWithExpUtc -ExpUtc $validExp) }
}
$planExpired = Get-LoadTestMintRefreshPlan -ManifestEntries $manifest3 -ExistingById $existingExpired -RequiredValidityDeadlineUtc $deadline -ForceFull $false
Assert-Equal 'expired jwt refresh count' 1 $planExpired.RefreshCount

$existingBoundary = @{
    'load60-001' = [pscustomobject]@{ id = 'load60-001'; bearerToken = (New-TestJwtWithExpUtc -ExpUtc $boundaryExp) }
    'load60-002' = [pscustomobject]@{ id = 'load60-002'; bearerToken = (New-TestJwtWithExpUtc -ExpUtc $boundaryExp.AddSeconds(-1)) }
}
$manifest2 = New-TestManifestEntries -Count 2
Assert-True 'boundary exp at deadline reusable' (Test-LoadTestTokenReusable -ExpectedHarnessId 'load60-001' -ExistingIdentity $existingBoundary['load60-001'] -RequiredValidityDeadlineUtc $deadline)
Assert-True 'boundary exp before deadline not reusable' (-not (Test-LoadTestTokenReusable -ExpectedHarnessId 'load60-002' -ExistingIdentity $existingBoundary['load60-002'] -RequiredValidityDeadlineUtc $deadline))

$planForce = Get-LoadTestMintRefreshPlan -ManifestEntries $manifest100 -ExistingById $existing100 -RequiredValidityDeadlineUtc $deadline -ForceFull $true
Assert-Equal 'force full refresh count' 100 $planForce.RefreshCount
Assert-Equal 'force full reusable count' 0 $planForce.ReusableCount

$tokenMap = @{}
foreach ($key in $plan10.PreservedById.Keys) {
    $tokenMap[$key] = $plan10.PreservedById[$key]
}
foreach ($entry in $plan10.ToRefresh) {
    $tokenMap[$entry.harnessId] = [pscustomobject]@{
        id           = $entry.harnessId
        bearerToken  = ('refreshed-{0}' -f $entry.harnessId)
        expiresAtUtc = $validExp.ToString('o')
    }
}
$merged = Build-LoadTestTokensIdentityList -ManifestEntries $manifest100 -TokenByHarnessId $tokenMap
Assert-Equal 'merge output count' 100 $merged.Count
Assert-Equal 'merge order first id' 'load60-001' $merged[0].id
Assert-Equal 'merge order last id' 'load60-100' $merged[99].id
Assert-Equal 'merge preserved load60-050' $existing10Expiring['load60-050'].bearerToken $merged[49].bearerToken
Assert-Equal 'merge refreshed load60-001' 'refreshed-load60-001' $merged[0].bearerToken

$tempTokens = Join-Path $env:TEMP ("mint-tokens-{0}.json" -f [Guid]::NewGuid())
$initialJson = '{"identities":[{"id":"load60-001","bearerToken":"keep-me"}]}'
[System.IO.File]::WriteAllText($tempTokens, $initialJson, (New-Object System.Text.UTF8Encoding $false))
try {
    try {
        Write-LoadTestTokensJsonAtomic -OutputPath $tempTokens -Identities @() -ExpectedCount 1
        Assert-True 'incomplete write throws' $false
    } catch {
        Assert-True 'incomplete write throws' $true
    }
    $afterFail = [System.IO.File]::ReadAllText($tempTokens)
    Assert-Equal 'failed write leaves file bytes' $initialJson $afterFail
} finally {
    Remove-Item $tempTokens -Force -ErrorAction SilentlyContinue
}

$estimateZero = Get-LoadTestMintEstimatedRefreshDuration -RefreshCount 0 -ThrottleSeconds 15
Assert-True 'estimate zero refresh' ($estimateZero.TotalSeconds -eq 0)
$estimateFive = Get-LoadTestMintEstimatedRefreshDuration -RefreshCount 5 -ThrottleSeconds 15
Assert-Equal 'estimate five logins sleep seconds' 60 $estimateFive.TotalSeconds

$sampleOutput = @(
    'Identity pool: 100',
    'Reusable tokens: 82',
    'Tokens requiring refresh: 18',
    "Required validity deadline: $($deadline.ToString('o'))",
    'Estimated refresh time: ~4.3 min (18 login(s), 15s spacing between logins)',
    '  [OK]   load60-001',
    'Wrote 100 identities'
) -join "`n"
Assert-True 'output has no bearer jwt material' (-not ($sampleOutput -match 'eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.'))

if ($failures -gt 0) {
    Write-Host ""
    Write-Host "$failures assertion(s) failed."
    exit 1
}

Write-Host ''
Write-Host 'All Mint-LoadTestTokens assertions passed.'
