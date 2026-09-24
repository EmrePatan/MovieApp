param(
    [string]$TokensFile = $env:LOAD_TEST_TOKENS_FILE,

    [string]$OutFile = '',

    [int]$MaxShardChars = 4500,

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
Max shard size (characters): $MaxShardChars

This tooling writes local operator files only. It does NOT upload to Grafana Cloud.
JWT values are never printed to the terminal.

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
        schemaVersion = 1
        transport     = $transport
        identityCount = $identityCount
        shardCount    = 0
        maxShardChars = $MaxShardChars
        singleEnvVar  = 'LOAD_TEST_IDENTITIES_JSON'
        shardEnvNames = @()
        exportedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    }
    $manifestJson = $manifest | ConvertTo-Json -Compress -Depth 5
    if (-not $WhatIf) {
        [System.IO.File]::WriteAllText($manifestPath, $manifestJson, $utf8NoBom)
    }

    Write-Host "Transport: single env var (payload length $($singlePayload.Length) <= $MaxShardChars)."
    Write-Host "Manifest:  $manifestPath"
    Write-Host "Payload:   $OutFile"
    Write-Host ''
    Write-Host 'Grafana Cloud env vars to set:'
    Write-Host '  - LOAD_TEST_IDENTITIES_JSON  (paste payload file contents)'
    Write-Host '  - Remove LOAD_TEST_IDENTITIES_SHARD_COUNT and LOAD_TEST_IDENTITIES_JSON_* when using single-var mode.'
    exit 0
}

$split = Split-LoadTestIdentitiesForGrafanaShards -Identities $identities -MaxShardChars $MaxShardChars
$shardCount = $split.Shards.Count
foreach ($shard in $split.Shards) {
    if ($shard.Length -gt $MaxShardChars) {
        throw "Internal error: shard length $($shard.Length) exceeds limit $MaxShardChars"
    }
}

$manifest = @{
    schemaVersion = 1
    transport     = 'sharded'
    identityCount = $identityCount
    shardCount    = $shardCount
    maxShardChars = $MaxShardChars
    singleEnvVar  = $null
    shardEnvNames = $split.ShardEnvNames
    exportedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
}
$manifestJson = $manifest | ConvertTo-Json -Compress -Depth 5
if ($manifestJson -match 'bearerToken|eyJ[A-Za-z0-9_-]{10,}') {
    throw 'Manifest must not contain JWT material.'
}

if (-not $WhatIf) {
    [System.IO.File]::WriteAllText($manifestPath, $manifestJson, $utf8NoBom)
    for ($i = 0; $i -lt $shardCount; $i++) {
        $shardFile = Join-Path $dataDir ("grafana-cloud-identities.shard-{0}.txt" -f ($i + 1).ToString('000'))
        [System.IO.File]::WriteAllText($shardFile, $split.Shards[$i], $utf8NoBom)
    }
}

Write-Host "Transport: sharded ($shardCount Grafana env vars + LOAD_TEST_IDENTITIES_SHARD_COUNT)."
Write-Host "Manifest:  $manifestPath"
Write-Host "Shard files: $dataDir\grafana-cloud-identities.shard-*.txt"
Write-Host ''
Write-Host 'Grafana Cloud env vars to create/update (encrypted at rest in Grafana UI):'
Write-Host "  - LOAD_TEST_IDENTITIES_SHARD_COUNT = $shardCount"
for ($i = 0; $i -lt $shardCount; $i++) {
    $envName = $split.ShardEnvNames[$i]
    $len = $split.Shards[$i].Length
    Write-Host "  - $envName  (paste shard-$($i + 1) file; $len chars)"
}
Write-Host ''
Write-Host 'When switching from single-var to sharded mode:'
Write-Host '  - Clear or delete LOAD_TEST_IDENTITIES_JSON in Grafana Cloud to avoid single-var taking precedence.'
Write-Host 'Do NOT pass shard values via k6 cloud run -e (archive stores CLI env as plain text).'
