param(
    [string]$BaseUrl = $env:LOAD_TEST_BASE_URL,
    [string]$ManifestPath,
    [string]$OutputPath,
    [int]$ThrottleSeconds = 13
)

$ErrorActionPreference = "Stop"
$loadRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

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
$entries = @($manifest.identities)
if ($entries.Count -lt 1) {
    throw "Manifest has no identities."
}

$expectedCount = $entries.Count
Write-Host "Minting tokens for $expectedCount identities via login (JWTs not displayed)."

$securePassword = Read-Host "Campaign password" -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    $securePassword.Dispose()
}

if ([string]::IsNullOrWhiteSpace($plainPassword)) {
    throw "Password cannot be empty."
}

$loginUri = "$($BaseUrl.TrimEnd('/'))/api/auth/login"
$identities = New-Object System.Collections.Generic.List[object]
$failures = 0
$index = 0

foreach ($entry in ($entries | Sort-Object harnessId)) {
    $index++
    $body = @{
        email    = $entry.email
        password = $plainPassword
    } | ConvertTo-Json -Compress

    try {
        $response = Invoke-RestMethod -Uri $loginUri -Method Post -Body $body -ContentType "application/json; charset=utf-8" -TimeoutSec 60
    } catch {
        $status = $_.Exception.Response.StatusCode.value__
        if ($status -eq 429) {
            Write-Error "Login rate limited (429) for $($entry.harnessId). Stop and retry later; do not hammer the endpoint."
            exit 2
        }
        Write-Host "  [FAIL] $($entry.harnessId) HTTP $status"
        $failures++
        $body = $null
        continue
    } finally {
        $body = $null
    }

    if ([string]::IsNullOrWhiteSpace($response.accessToken)) {
        Write-Host "  [FAIL] $($entry.harnessId) missing accessToken"
        $failures++
        continue
    }

    $expiresAtUtc = $null
    if ($response.expiresAt) {
        $expiresAtUtc = ([DateTime]$response.expiresAt).ToUniversalTime().ToString("o")
    }

    $identities.Add([ordered]@{
        id           = $entry.harnessId
        bearerToken  = $response.accessToken
        expiresAtUtc = $expiresAtUtc
    })
    Write-Host "  [OK]   $($entry.harnessId)"
    $response = $null

    if ($index -lt $expectedCount -and $ThrottleSeconds -gt 0) {
        Start-Sleep -Seconds $ThrottleSeconds
    }
}

$plainPassword = $null

if ($failures -gt 0 -or $identities.Count -ne $expectedCount) {
    throw "Mint incomplete: $($identities.Count)/$expectedCount succeeded, $failures failed. tokens.json was not updated."
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
