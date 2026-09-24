param(
    [string]$BaseUrl = $env:LOAD_TEST_BASE_URL,
    [string]$IdentityId = "load-test-smoke-001",
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    $BaseUrl = "https://movieapp-fpkg.onrender.com"
}

$repoLoad = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoLoad "data\tokens.json"
}

$email = Read-Host "Load-test account email"
$securePassword = Read-Host "Password" -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    $securePassword.Dispose()
}

$loginUri = "$($BaseUrl.TrimEnd('/'))/api/auth/login"
$body = @{
    email    = $email.Trim()
    password = $plainPassword
} | ConvertTo-Json -Compress

# Clear password from managed string ASAP
$plainPassword = $null

try {
    $response = Invoke-RestMethod -Uri $loginUri -Method Post -Body $body -ContentType "application/json; charset=utf-8" -TimeoutSec 60
} catch {
    $status = $_.Exception.Response.StatusCode.value__
    if ($status -eq 401) {
        throw "Login failed (401). Check email/password and that the account email is verified."
    }
    if ($status -eq 429) {
        throw "Login rate limited (429). Wait and retry from this machine."
    }
    throw "Login failed: $($_.Exception.Message)"
} finally {
    $body = $null
}

if ([string]::IsNullOrWhiteSpace($response.accessToken)) {
    throw "Login response did not include accessToken."
}

$expiresAtUtc = $null
if ($response.expiresAt) {
    $expiresAtUtc = ([DateTime]$response.expiresAt).ToUniversalTime().ToString("o")
}

$payload = @{
    identities = @(
        @{
            id            = $IdentityId
            bearerToken   = $response.accessToken
            expiresAtUtc  = $expiresAtUtc
        }
    )
}

$json = $payload | ConvertTo-Json -Depth 5
$dir = Split-Path $OutputPath -Parent
if (-not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($OutputPath, $json, $utf8NoBom)

# Never log token or password
Write-Host "Wrote token identity '$IdentityId' to $OutputPath (JWT not displayed)."
if ($expiresAtUtc) {
    Write-Host "ExpiresAtUtc: $expiresAtUtc"
}

# Clear response reference
$response = $null
