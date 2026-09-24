param(
    [int]$SampleCount = 10,
    [switch]$FailOnAuthError,
    [int]$MinMinutesUntilExpiry = 75,
    [string]$BaseUrl = $env:LOAD_TEST_BASE_URL,
    [string]$TokensFile = $env:LOAD_TEST_TOKENS_FILE
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    throw "Set LOAD_TEST_BASE_URL or pass -BaseUrl"
}
if ([string]::IsNullOrWhiteSpace($TokensFile) -or -not (Test-Path $TokensFile)) {
    throw "Set LOAD_TEST_TOKENS_FILE to a readable tokens.json path"
}

$base = $BaseUrl.TrimEnd("/")
$payload = Get-Content $TokensFile -Raw | ConvertFrom-Json
$identities = @($payload.identities)
if ($identities.Count -eq 0) {
    throw "No identities in tokens file"
}

function Get-JwtExpiryUtc([string]$jwt) {
    $parts = $jwt.Split(".")
    if ($parts.Count -lt 2) { return $null }
    $body = $parts[1]
    $pad = "=" * ((4 - ($body.Length % 4)) % 4)
    $bytes = [Convert]::FromBase64String(($body + $pad).Replace("-", "+").Replace("_", "/"))
    $json = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
    if ($null -eq $json.exp) { return $null }
    return [DateTimeOffset]::FromUnixTimeSeconds([int64]$json.exp).UtcDateTime
}

$sample = if ($SampleCount -ge $identities.Count) { $identities } else { $identities | Get-Random -Count $SampleCount }
$authFailures = 0
$expiryWarnings = 0
$stageDeadline = (Get-Date).ToUniversalTime().AddMinutes($MinMinutesUntilExpiry)

Write-Host "Token preflight against $base ($($sample.Count) sampled / $($identities.Count) total)"

foreach ($identity in $sample) {
    $id = $identity.id
    $token = $identity.bearerToken
    if ([string]::IsNullOrWhiteSpace($token)) {
        Write-Host "  [SKIP] $id — empty token"
        continue
    }

    $exp = Get-JwtExpiryUtc $token
    if ($exp) {
        if ($exp -lt $stageDeadline) {
            Write-Host "  [WARN] $id — JWT exp $exp UTC is before required window ($stageDeadline UTC)"
            $expiryWarnings++
        } else {
            Write-Host "  [OK]   $id — JWT exp $exp UTC"
        }
    } elseif ($identity.expiresAtUtc) {
        $parsed = [DateTime]::Parse($identity.expiresAtUtc).ToUniversalTime()
        if ($parsed -lt $stageDeadline) {
            Write-Host "  [WARN] $id — expiresAtUtc $parsed before window"
            $expiryWarnings++
        }
    }

    $headers = @{ Authorization = "Bearer $token"; Accept = "application/json" }
    try {
        $response = Invoke-WebRequest -Uri "$base/api/home?type=all&sectionSize=5" -Headers $headers -Method Get -UseBasicParsing -TimeoutSec 30
        Write-Host "  [OK]   $id — home HTTP $($response.StatusCode)"
    } catch {
        $status = $_.Exception.Response.StatusCode.value__
        Write-Host "  [FAIL] $id — home HTTP $status"
        if ($status -eq 401 -or $status -eq 403) { $authFailures++ }
    }
}

if ($expiryWarnings -gt 0) {
    Write-Host ""
    Write-Host "$expiryWarnings token(s) may expire during the next stage — re-login out of band and update tokens.json."
}

if ($FailOnAuthError -and $authFailures -gt 0) {
    Write-Host ""
    throw "$authFailures auth failure(s). Do not start k6; refresh tokens first."
}

if ($authFailures -eq 0) {
    Write-Host ""
    Write-Host "Token preflight complete (no 401/403 in sample)."
}
