param(
    [string]$TokensFile = $env:LOAD_TEST_TOKENS_FILE,

    [string]$OutFile = '',

    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$loadRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if ([string]::IsNullOrWhiteSpace($TokensFile)) {
    $TokensFile = Join-Path $loadRoot 'data\tokens.json'
}

if (-not (Test-Path $TokensFile)) {
    throw "tokens.json not found at $TokensFile"
}

Import-Module (Join-Path $PSScriptRoot 'LoadTestHarness.psm1') -Force
$payload = Export-LoadTestIdentitiesPayload -TokensFilePath $TokensFile
$identityCount = (ConvertFrom-Json $payload).identities.Count

Write-Host @"

================================================================================
Grafana Cloud identity payload preparation (Movie Cave JWTs)
================================================================================

This step prepares ONLY bearer tokens (and identity ids) for Grafana Cloud.
It does NOT upload automatically.

What leaves your machine if you paste/upload this payload:
  - $identityCount production JWT access tokens (LOAD60 pool)
  - identity ids (non-secret labels)

What is NOT included:
  - campaign passwords
  - PostgreSQL credentials
  - JWT signing keys
  - email addresses (unless you stored them in tokens.json — avoid)

Recommended: Grafana Cloud UI → Testing & synthetics → Performance → Settings
→ Environment variables → create LOAD_TEST_IDENTITIES_JSON (encrypted at rest).

Do NOT pass this JSON via k6 cloud run -e (CLI env vars are stored in the archive in plain text).

"@

if (-not $WhatIf) {
    $confirm = Read-Host "Type YES to write the minimal JSON payload to a local file (still not uploaded)"
    if ($confirm -ne 'YES') {
        Write-Host 'Aborted. No file written.'
        exit 0
    }
}

if ([string]::IsNullOrWhiteSpace($OutFile)) {
    $OutFile = Join-Path $loadRoot "data\grafana-cloud-identities.payload.json"
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($OutFile, $payload, $utf8NoBom)
Write-Host "Wrote $identityCount identities to: $OutFile"
Write-Host 'Copy the file contents into Grafana Cloud env var LOAD_TEST_IDENTITIES_JSON, then delete the local payload file when done.'
