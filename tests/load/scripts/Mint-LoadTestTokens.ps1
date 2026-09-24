param(
    [string]$BaseUrl = $env:LOAD_TEST_BASE_URL,
    [string]$ManifestPath,
    [string]$OutputPath,
    [int]$ThrottleSeconds = 15
)

$ErrorActionPreference = "Stop"
$loadRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
. (Join-Path $PSScriptRoot "Read-LoadTestCampaignPassword.ps1")

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    throw "Set LOAD_TEST_BASE_URL or pass -BaseUrl"
}

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $loadRoot "data\load60-identities.manifest.json"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $loadRoot "data\tokens.json"
}

if (-not (Test-Path $ManifestPath)) {
    throw "Manifest not found: $ManifestPath"
}

$manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
$entries = @($manifest.identities | Sort-Object harnessId)
if ($entries.Count -lt 1) {
    throw "Manifest has no identities."
}

$expectedCount = $entries.Count
Write-Host "Minting tokens for $expectedCount identities via login (JWTs not displayed)."
Write-Host "Login rate limit: 5 attempts per minute per IP (failed logins count). Using ${ThrottleSeconds}s spacing."

$plainPassword = Get-LoadTestCampaignPassword -Prompt "Campaign password"

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
        $response = Invoke-RestMethod -Uri $loginUri -Method Post -Body $body -ContentType "application/json; charset=utf-8" -TimeoutSec 60
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

$first = $entries[0]
Write-Host "Preflight login: $($first.harnessId)"
$preflight = Invoke-LoadTestLogin -Email $first.email -Password $plainPassword

if ($preflight.Status -eq 429) {
    Write-Error "Login rate limited (429) on preflight. Wait at least 60s before retrying; run verify-password via DB first."
    exit 2
}

if ($preflight.Status -eq 401) {
    if ($preflight.ErrorCode -eq "email_not_verified") {
        Write-Error "Preflight authentication failed (401 email_not_verified). User is not verified for $($first.harnessId)."
    } else {
        Write-Error "Preflight authentication failed (401). Run verify-password against the DB before retrying HTTP login."
    }
    exit 3
}

if ($preflight.Status -eq 403) {
    Write-Error "Preflight authentication failed (403)."
    exit 3
}

if (-not $preflight.Ok -or [string]::IsNullOrWhiteSpace($preflight.Response.accessToken)) {
    Write-Error "Preflight login failed with HTTP $($preflight.Status). Stop before batch mint."
    exit 3
}

Write-Host "  [OK]   $($first.harnessId) (preflight)"

$identities = New-Object System.Collections.Generic.List[object]
$expiresAtUtc = $null
if ($preflight.Response.expiresAt) {
    $expiresAtUtc = ([DateTime]$preflight.Response.expiresAt).ToUniversalTime().ToString("o")
}

$identities.Add([ordered]@{
    id           = $first.harnessId
    bearerToken  = $preflight.Response.accessToken
    expiresAtUtc = $expiresAtUtc
})
$preflight.Response = $null

if ($expectedCount -gt 1 -and $ThrottleSeconds -gt 0) {
    Start-Sleep -Seconds $ThrottleSeconds
}

for ($i = 1; $i -lt $expectedCount; $i++) {
    $entry = $entries[$i]
    $attempt = Invoke-LoadTestLogin -Email $entry.email -Password $plainPassword

    if ($attempt.Status -eq 429) {
        Write-Error "Login rate limited (429) at $($entry.harnessId). tokens.json was not updated. Wait 60s+ before retry."
        exit 2
    }

    if ($attempt.Status -eq 401 -or $attempt.Status -eq 403) {
        Write-Error "Authentication failed ($($attempt.Status)) at $($entry.harnessId). Stopping batch; tokens.json was not updated."
        exit 3
    }

    if (-not $attempt.Ok -or [string]::IsNullOrWhiteSpace($attempt.Response.accessToken)) {
        Write-Error "Unexpected login failure at $($entry.harnessId) HTTP $($attempt.Status). Stopping batch."
        exit 3
    }

    $expiresAtUtc = $null
    if ($attempt.Response.expiresAt) {
        $expiresAtUtc = ([DateTime]$attempt.Response.expiresAt).ToUniversalTime().ToString("o")
    }

    $identities.Add([ordered]@{
        id           = $entry.harnessId
        bearerToken  = $attempt.Response.accessToken
        expiresAtUtc = $expiresAtUtc
    })
    Write-Host "  [OK]   $($entry.harnessId)"
    $attempt.Response = $null

    if ($i -lt ($expectedCount - 1) -and $ThrottleSeconds -gt 0) {
        Start-Sleep -Seconds $ThrottleSeconds
    }
}

$plainPassword = $null

if ($identities.Count -ne $expectedCount) {
    throw "Mint incomplete: $($identities.Count)/$expectedCount. tokens.json was not updated."
}

$payload = @{ identities = $identities }
$json = $payload | ConvertTo-Json -Depth 5
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

$dir = Split-Path $OutputPath -Parent
if (-not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

$tempPath = Join-Path $dir ("tokens.json.tmp." + [Guid]::NewGuid().ToString("N"))
[System.IO.File]::WriteAllText($tempPath, $json, $utf8NoBom)
Move-Item -Path $tempPath -Destination $OutputPath -Force

Write-Host "Wrote $expectedCount identities to $OutputPath (UTF-8 no BOM, JWTs not displayed)."
