param(
    [string]$TokensFile = $env:LOAD_TEST_TOKENS_FILE,

    [string]$OutFile = '',

    [int]$MaxShardChars = 4500,

    [int]$MaxSecretPartUtf8Bytes = 22528,

    [switch]$IncludeEnvShardArtifacts,

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
$identities = Read-LoadTestMinimalIdentitiesFromTokensFile -TokensFilePath $TokensFile
$identityCount = $identities.Count
$singlePayload = (@{ identities = $identities } | ConvertTo-Json -Compress -Depth 5)
$manifestPath = Get-LoadTestGrafanaCloudManifestPath -LoadRoot $loadRoot
$dataDir = Join-Path $loadRoot 'data'

Write-Host @"

================================================================================
Grafana Cloud identity export (Movie Cave JWTs)
================================================================================

Source identities (valid bearer tokens): $identityCount
Env shard max (characters): $MaxShardChars
Grafana secret part max (UTF-8 bytes): $MaxSecretPartUtf8Bytes

Writes gitignored local artifacts only. JWT values are never printed.
Normal Cloud workflow uses transport 'grafana-secrets' + Sync-LoadTestIdentitiesToGrafanaSecrets.ps1.

"@

if (-not $WhatIf) {
    $confirm = Read-Host "Type YES to write manifest and export artifact(s) under tests/load/data"
    if ($confirm -ne 'YES') {
        Write-Host 'Aborted. No files written.'
        exit 0
    }
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

if ($singlePayload.Length -le $MaxShardChars) {
    $transport = 'single'
    if ([string]::IsNullOrWhiteSpace($OutFile)) {
        $OutFile = Join-Path $dataDir 'grafana-cloud-identities.payload.json'
    }

    if (-not $WhatIf) {
        [System.IO.File]::WriteAllText($OutFile, $singlePayload, $utf8NoBom)
    }

    $manifest = @{
        schemaVersion       = 1
        transport           = $transport
        identityCount       = $identityCount
        shardCount          = 0
        secretPartCount     = 0
        maxShardChars       = $MaxShardChars
        maxPartUtf8Bytes    = $MaxSecretPartUtf8Bytes
        singleEnvVar        = 'LOAD_TEST_IDENTITIES_JSON'
        shardEnvNames       = @()
        secretNames         = @()
        secretPartFiles     = @()
        secretPartByteSizes = @()
        exportedAtUtc       = (Get-Date).ToUniversalTime().ToString('o')
    }
    $manifestJson = $manifest | ConvertTo-Json -Compress -Depth 5
    if (-not $WhatIf) {
        [System.IO.File]::WriteAllText($manifestPath, $manifestJson, $utf8NoBom)
    }

    Write-Host "Transport: single env var (payload length $($singlePayload.Length) <= $MaxShardChars)."
    Write-Host "Manifest:  $manifestPath"
    Write-Host "Payload:   $OutFile"
    exit 0
}

$secretSplit = Split-LoadTestIdentitiesForGrafanaSecrets -Identities $identities -MaxPartUtf8Bytes $MaxSecretPartUtf8Bytes
foreach ($byteSize in $secretSplit.PartByteSizes) {
    if ($byteSize -gt $MaxSecretPartUtf8Bytes) {
        throw "Internal error: secret part byte size $byteSize exceeds limit $MaxSecretPartUtf8Bytes"
    }
}

$manifest = @{
    schemaVersion       = 1
    transport           = 'grafana-secrets'
    identityCount       = $identityCount
    shardCount          = 0
    secretPartCount     = $secretSplit.Parts.Count
    maxShardChars       = $MaxShardChars
    maxPartUtf8Bytes    = $MaxSecretPartUtf8Bytes
    singleEnvVar        = $null
    shardEnvNames       = @()
    secretNames         = $secretSplit.SecretNames
    secretPartFiles     = $secretSplit.PartFiles
    secretPartByteSizes = $secretSplit.PartByteSizes
    exportedAtUtc       = (Get-Date).ToUniversalTime().ToString('o')
}
$manifestJson = $manifest | ConvertTo-Json -Compress -Depth 6
if ($manifestJson -match 'bearerToken|eyJ[A-Za-z0-9_-]{10,}') {
    throw 'Manifest must not contain JWT material.'
}

if (-not $WhatIf) {
    [System.IO.File]::WriteAllText($manifestPath, $manifestJson, $utf8NoBom)
    for ($i = 0; $i -lt $secretSplit.Parts.Count; $i++) {
        $secretFile = Join-Path $dataDir $secretSplit.PartFiles[$i]
        [System.IO.File]::WriteAllText($secretFile, $secretSplit.Parts[$i], $utf8NoBom)
    }
}

Write-Host "Transport: grafana-secrets ($($secretSplit.Parts.Count) secret part file(s), max $MaxSecretPartUtf8Bytes UTF-8 bytes each)."
Write-Host "Manifest:  $manifestPath"
Write-Host "Secret files: $dataDir\grafana-cloud-identities.secret-*.txt"
Write-Host ''
Write-Host 'Next step (no JWT paste in Grafana UI):'
Write-Host '  .\scripts\Sync-LoadTestIdentitiesToGrafanaSecrets.ps1 -DryRun'
Write-Host '  .\scripts\Sync-LoadTestIdentitiesToGrafanaSecrets.ps1'

if ($IncludeEnvShardArtifacts) {
    $envSplit = Split-LoadTestIdentitiesForGrafanaShards -Identities $identities -MaxShardChars $MaxShardChars
    if (-not $WhatIf) {
        for ($i = 0; $i -lt $envSplit.Shards.Count; $i++) {
            $shardFile = Join-Path $dataDir ("grafana-cloud-identities.shard-{0}.txt" -f ($i + 1).ToString('000'))
            [System.IO.File]::WriteAllText($shardFile, $envSplit.Shards[$i], $utf8NoBom)
        }
    }

    Write-Host ''
    Write-Host "Optional env-shard fallback artifacts written: $($envSplit.Shards.Count) shard file(s)."
    Write-Host 'Fallback only — do NOT pass shard values via k6 cloud run -e.'
}
