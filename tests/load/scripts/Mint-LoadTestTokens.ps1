param(
    [string]$BaseUrl = $env:LOAD_TEST_BASE_URL,
    [string]$ManifestPath,
    [string]$OutputPath,
    [int]$ThrottleSeconds = 15,
    [int]$MinMinutesUntilExpiry = 30,
    [switch]$ForceFull
)

$ErrorActionPreference = 'Stop'
$loadRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Read-LoadTestCampaignPassword.ps1')
. (Join-Path $PSScriptRoot 'LoadTestTokenMintLogic.ps1')

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    throw 'Set LOAD_TEST_BASE_URL or pass -BaseUrl'
}

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $loadRoot 'data\load60-identities.manifest.json'
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $loadRoot 'data\tokens.json'
}

if (-not (Test-Path $ManifestPath)) {
    throw "Manifest not found: $ManifestPath"
}

$manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
$entries = @($manifest.identities | Sort-Object harnessId)
if ($entries.Count -lt 1) {
    throw 'Manifest has no identities.'
}

$expectedCount = $entries.Count
$requiredDeadline = (Get-Date).ToUniversalTime().AddMinutes($MinMinutesUntilExpiry)

$existingById = @{}
if ((Test-Path $OutputPath) -and -not $ForceFull) {
    try {
        $existingPayload = Get-Content $OutputPath -Raw | ConvertFrom-Json
        $existingById = Get-LoadTestTokensById -Identities @($existingPayload.identities)
    } catch {
        Write-Host 'Existing tokens.json could not be parsed; all identities will be refreshed.'
        $existingById = @{}
    }
}

$plan = Get-LoadTestMintRefreshPlan -ManifestEntries $entries -ExistingById $existingById -RequiredValidityDeadlineUtc $requiredDeadline -ForceFull:([bool]$ForceFull)

Write-Host "Identity pool: $expectedCount"
if ($ForceFull) {
    Write-Host 'Mode: ForceFull (re-authenticate all identities)'
} else {
    Write-Host "Reusable tokens: $($plan.ReusableCount)"
    Write-Host "Tokens requiring refresh: $($plan.RefreshCount)"
}
Write-Host "Required validity deadline: $($requiredDeadline.ToString('o')) (UtcNow + $MinMinutesUntilExpiry minutes)"

$estimate = Get-LoadTestMintEstimatedRefreshDuration -RefreshCount $plan.RefreshCount -ThrottleSeconds $ThrottleSeconds
if ($plan.RefreshCount -eq 0) {
    Write-Host 'Estimated refresh time: 0 (no logins required)'
} else {
    $estimateMinutes = [math]::Round($estimate.TotalMinutes, 1)
    Write-Host "Estimated refresh time: ~$estimateMinutes min ($($plan.RefreshCount) login(s), ${ThrottleSeconds}s spacing between logins)"
    Write-Host 'Login rate limit: 5 attempts per minute per IP (failed logins count).'
}

$tokenById = @{}
foreach ($key in $plan.PreservedById.Keys) {
    $tokenById[$key] = $plan.PreservedById[$key]
}

if ($plan.RefreshCount -eq 0) {
    $merged = Build-LoadTestTokensIdentityList -ManifestEntries $entries -TokenByHarnessId $tokenById
    Write-LoadTestTokensJsonAtomic -OutputPath $OutputPath -Identities @($merged) -ExpectedCount $expectedCount
    Write-Host "Wrote $expectedCount identities to $OutputPath (UTF-8 no BOM, JWTs not displayed)."
    exit 0
}

$plainPassword = Get-LoadTestCampaignPassword -Prompt 'Campaign password'
$loginUri = "$($BaseUrl.TrimEnd('/'))/api/auth/login"

function Invoke-LoadTestLogin {
    param(
        [string]$Email,
        [string]$Password
    )

    $body = @{
        email    = $Email.Trim()
        password = $Password
    } | ConvertTo-Json -Compress

    try {
        $response = Invoke-RestMethod -Uri $loginUri -Method Post -Body $body -ContentType 'application/json; charset=utf-8' -TimeoutSec 60
        return @{ Ok = $true; Status = 200; Response = $response; ErrorCode = $null }
    } catch {
        $status = $_.Exception.Response.StatusCode.value__
        $errorCode = $null
        if ($_.ErrorDetails.Message) {
            try {
                $problem = $_.ErrorDetails.Message | ConvertFrom-Json
                if ($problem.errorCode) { $errorCode = $problem.errorCode }
                elseif ($problem.extensions -and $problem.extensions.errorCode) { $errorCode = $problem.extensions.errorCode }
            } catch {
                # ignore parse errors
            }
        }

        return @{ Ok = $false; Status = $status; Response = $null; ErrorCode = $errorCode }
    } finally {
        $body = $null
    }
}

function Invoke-LoadTestLoginWithChecks {
    param(
        [object]$Entry,
        [string]$Password,
        [bool]$IsPreflight
    )

    $label = if ($IsPreflight) { ' (preflight)' } else { '' }
    Write-Host "Login: $($Entry.harnessId)$label"

    $attempt = Invoke-LoadTestLogin -Email $entry.email -Password $Password

    if ($attempt.Status -eq 429) {
        Write-Error "Login rate limited (429) at $($Entry.harnessId). tokens.json was not updated. Wait 60s+ before retry."
        exit 2
    }

    if ($attempt.Status -eq 401) {
        if ($attempt.ErrorCode -eq 'email_not_verified') {
            Write-Error "Authentication failed (401 email_not_verified) at $($Entry.harnessId). tokens.json was not updated."
        } else {
            Write-Error "Authentication failed (401) at $($Entry.harnessId). Run verify-password against the DB before retrying HTTP login. tokens.json was not updated."
        }
        exit 3
    }

    if ($attempt.Status -eq 403) {
        Write-Error "Authentication failed (403) at $($Entry.harnessId). tokens.json was not updated."
        exit 3
    }

    if (-not $attempt.Ok -or [string]::IsNullOrWhiteSpace($attempt.Response.accessToken)) {
        Write-Error "Unexpected login failure at $($Entry.harnessId) HTTP $($attempt.Status). tokens.json was not updated."
        exit 3
    }

    Write-Host "  [OK]   $($Entry.harnessId)"
    return $attempt.Response
}

$refreshQueue = @($plan.ToRefresh)
$firstResponse = Invoke-LoadTestLoginWithChecks -Entry $refreshQueue[0] -Password $plainPassword -IsPreflight $true
$tokenById[$refreshQueue[0].harnessId] = New-LoadTestTokenRowFromLogin -HarnessId $refreshQueue[0].harnessId -LoginResponse $firstResponse
$firstResponse = $null

for ($i = 1; $i -lt $refreshQueue.Count; $i++) {
    if ($ThrottleSeconds -gt 0) {
        Start-Sleep -Seconds $ThrottleSeconds
    }

    $entry = $refreshQueue[$i]
    $response = Invoke-LoadTestLoginWithChecks -Entry $entry -Password $plainPassword -IsPreflight $false
    $tokenById[$entry.harnessId] = New-LoadTestTokenRowFromLogin -HarnessId $entry.harnessId -LoginResponse $response
    $response = $null
}

$plainPassword = $null

$merged = Build-LoadTestTokensIdentityList -ManifestEntries $entries -TokenByHarnessId $tokenById
Write-LoadTestTokensJsonAtomic -OutputPath $OutputPath -Identities @($merged) -ExpectedCount $expectedCount
Write-Host "Wrote $expectedCount identities to $OutputPath (UTF-8 no BOM, JWTs not displayed)."
