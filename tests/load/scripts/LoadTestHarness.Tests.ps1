$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'LoadTestHarness.psm1') -Force

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

Assert-Equal 'reuse 150/100' 1.5 (Get-LoadTestIdentityReuseRatio -StageVus 150 -IdentityCount 100)
Assert-Equal 'reuse 250/100' 2.5 (Get-LoadTestIdentityReuseRatio -StageVus 250 -IdentityCount 100)

$vh = Get-LoadTestVuHourEstimate -StageVus 150 -PresetName capacity -StageTarget 150
Assert-True 'vu-hours > 0 for capacity 150' ($vh -gt 0)

$prod = Test-LoadTestIsProductionTarget -BaseUrl 'https://movieapp-fpkg.onrender.com' -EnvironmentLabel 'staging'
Assert-True 'render host treated as production' $prod

$args = Build-LoadTestK6EnvArgs -EnvVars @{ LOAD_TEST_BASE_URL = 'https://example.com'; LOAD_TEST_SCENARIO = 'user-concurrency' }
Assert-True 'env args include -e' ($args -contains '-e')
Assert-True 'env args omit secrets' (-not ($args -join ' ' -match 'bearerToken'))

$tempTokens = Join-Path $env:TEMP ("load-test-harness-tokens-{0}.json" -f [Guid]::NewGuid())
$sample = @{ identities = @(
        @{ id = 't1'; bearerToken = 'eyJ.test' },
        @{ id = 't2'; bearerToken = 'eyJ.two' }
    ) }
$sample | ConvertTo-Json -Compress -Depth 5 | Set-Content -Path $tempTokens -Encoding UTF8
$payload = Export-LoadTestIdentitiesPayload -TokensFilePath $tempTokens
Remove-Item $tempTokens -Force
Assert-True 'payload has identities' ($payload -match 'identities')
Assert-True 'payload omits description field' (-not ($payload -match 'description'))
Assert-True 'payload strips to bearer only' ($payload -match 'eyJ\.test')

$preflight = Format-LoadTestCloudPreflight -ExecutionMode Cloud -StageVus 150 -IdentityCount 100 -ReuseRatio 1.5 -VuHours 36 -Duration '00:17:00' -LoadZone 'amazon:de:frankfurt' -SearchProfileHint 'autocomplete-only' -IsProduction $true -LocalTokenCount 100 -CloudManifestIdentityCount 100 -CloudManifestShardCount 3 -CloudTransport 'grafana-secrets'
Assert-True 'preflight mentions reuse' ($preflight -match '1.5:1')
Assert-True 'preflight uses manifest cloud pool line' ($preflight -match 'Cloud identity pool \(manifest\): 100')
Assert-True 'preflight lists grafana secrets transport' ($preflight -match 'Grafana Secrets')
Assert-True 'preflight lists secret part count' ($preflight -match 'Cloud identity secret parts:\s+3')

$fakeIdentities = @()
for ($n = 1; $n -le 100; $n++) {
    $fakeIdentities += @{
        id          = "load60-$n"
        bearerToken = ('eyJ.fake.{0}.sig' -f ($n.ToString('000')))
    }
}
$split = Split-LoadTestIdentitiesForGrafanaShards -Identities $fakeIdentities -MaxShardChars 4500
Assert-True '100 identities shard into at least 2 parts' ($split.Shards.Count -ge 2)
Assert-True 'reassembled count is 100' (($split.AssembledJson | ConvertFrom-Json).identities.Count -eq 100)
Assert-True 'every shard under 4500 chars' (-not ($split.Shards | Where-Object { $_.Length -gt 4500 }))
Assert-Equal 'first shard env name' 'LOAD_TEST_IDENTITIES_JSON_001' $split.ShardEnvNames[0]
Assert-Equal 'shard env name count matches shards' $split.Shards.Count $split.ShardEnvNames.Count
$joined = -join $split.Shards
$roundTrip = $joined | ConvertFrom-Json
Assert-Equal 'order preserved first id' 'load60-1' $roundTrip.identities[0].id
Assert-Equal 'order preserved last id' 'load60-100' $roundTrip.identities[99].id
foreach ($shard in $split.Shards) {
    Assert-True 'shard does not split bearerToken key mid-token' ($shard -notmatch 'bearerTo$')
}

$manifestTemp = Join-Path $env:TEMP ("grafana-manifest-{0}.json" -f [Guid]::NewGuid())
@{
    schemaVersion = 1
    transport     = 'sharded'
    identityCount = 100
    shardCount    = $split.Shards.Count
} | ConvertTo-Json -Compress | Set-Content -Path $manifestTemp -Encoding UTF8
Assert-True 'manifest file has no jwt material' (Test-LoadTestManifestContainsNoSecrets -ManifestPath $manifestTemp)
$readManifest = Read-LoadTestGrafanaCloudManifest -ManifestPath $manifestTemp
Assert-Equal 'manifest identityCount' 100 $readManifest.identityCount
Remove-Item $manifestTemp -Force

$secretSplit = Split-LoadTestIdentitiesForGrafanaSecrets -Identities $fakeIdentities -MaxPartUtf8Bytes 22528
Assert-True '100 fake identities produce grafana secret parts' ($secretSplit.Parts.Count -ge 1)
$secretSplitSmall = Split-LoadTestIdentitiesForGrafanaSecrets -Identities $fakeIdentities -MaxPartUtf8Bytes 3000
Assert-True '100 identities split into multiple parts when limit is small' ($secretSplitSmall.Parts.Count -ge 2)
Assert-True 'secret parts within utf8 byte limit' (-not ($secretSplit.PartByteSizes | Where-Object { $_ -gt 22528 }))
Assert-Equal 'first secret name' 'movie-cave-load-identities-001' $secretSplit.SecretNames[0]
Assert-Equal 'secret names match parts' $secretSplit.Parts.Count $secretSplit.SecretNames.Count
$secretJoined = -join $secretSplit.Parts
Assert-Equal 'secret round-trip count' 100 (($secretJoined | ConvertFrom-Json).identities.Count)

$secureBody = New-LoadTestGrafanaSecureValueRequestBody -SecretName 'movie-cave-load-identities-001' -SecretValue 'not-a-jwt' -Description 'MC load id 001'
Assert-True 'secure value request includes k6-cloud decrypter' ($secureBody.spec.decrypters -contains 'k6-cloud')
Assert-True 'secure value description within 25 chars' ($secureBody.spec.description.Length -le 25)

$stale = Get-LoadTestStaleManagedGrafanaSecretNames -PreviousSecretNames @('movie-cave-load-identities-001', 'movie-cave-load-identities-002', 'other-secret') -CurrentSecretNames @('movie-cave-load-identities-001')
Assert-Equal 'stale managed secret count' 1 $stale.Count
Assert-Equal 'stale managed secret name' 'movie-cave-load-identities-002' $stale[0]

$syncRoot = Join-Path $env:TEMP ("load-grafana-sync-{0}" -f [Guid]::NewGuid())
$syncData = Join-Path $syncRoot 'data'
New-Item -ItemType Directory -Path $syncData -Force | Out-Null
for ($i = 0; $i -lt $secretSplit.Parts.Count; $i++) {
    $partPath = Join-Path $syncData $secretSplit.PartFiles[$i]
    [System.IO.File]::WriteAllText($partPath, $secretSplit.Parts[$i], (New-Object System.Text.UTF8Encoding($false)))
}
$syncManifest = @{
    schemaVersion       = 1
    transport           = 'grafana-secrets'
    identityCount       = 100
    secretPartCount     = $secretSplit.Parts.Count
    maxPartUtf8Bytes    = 22528
    secretNames         = $secretSplit.SecretNames
    secretPartFiles     = $secretSplit.PartFiles
    secretPartByteSizes = $secretSplit.PartByteSizes
} | ConvertTo-Json -Compress -Depth 6
$syncManifestPath = Join-Path $syncData 'grafana-cloud-identities.manifest.json'
[System.IO.File]::WriteAllText($syncManifestPath, $syncManifest, (New-Object System.Text.UTF8Encoding($false)))

$httpCalls = [System.Collections.Generic.List[object]]::new()
$mockRest = {
    param($Method, $Uri, $Headers, $Body)
    $httpCalls.Add([pscustomobject]@{ Method = $Method; Uri = $Uri; Body = $Body }) | Out-Null
    if ($Method -eq 'Get') {
        throw '404 not found'
    }
    return @{ status = 'ok' }
}

$env:GRAFANA_URL = 'https://grafana.example.net'
$env:GRAFANA_SA_TOKEN = 'unit-test-token'
$dry = Invoke-LoadTestGrafanaSecretsSync -LoadRoot $syncRoot -DryRun -RestMethodInvoker $mockRest
Assert-True 'dry run makes no http calls' ($httpCalls.Count -eq 0)
Assert-True 'dry run returns secret names' ($dry.SecretNames.Count -eq $secretSplit.Parts.Count)

$null = Invoke-LoadTestGrafanaSecretsSync -LoadRoot $syncRoot -RestMethodInvoker $mockRest
$postCalls = @($httpCalls | Where-Object { $_.Method -eq 'Post' })
Assert-Equal 'sync post calls per secret part' $secretSplit.Parts.Count $postCalls.Count
foreach ($call in $postCalls) {
    Assert-True 'sync body includes k6-cloud decrypter' ($call.Body.spec.decrypters -contains 'k6-cloud')
}

Remove-Item $syncRoot -Recurse -Force
Remove-Item Env:GRAFANA_URL -ErrorAction SilentlyContinue
Remove-Item Env:GRAFANA_SA_TOKEN -ErrorAction SilentlyContinue

$oversizeRoot = Join-Path $env:TEMP ("load-grafana-oversize-{0}" -f [Guid]::NewGuid())
$oversizeData = Join-Path $oversizeRoot 'data'
New-Item -ItemType Directory -Path $oversizeData -Force | Out-Null
$oversizeManifest = @{
    schemaVersion   = 1
    transport       = 'grafana-secrets'
    identityCount   = 1
    secretPartCount = 1
    maxPartUtf8Bytes = 10
    secretNames     = @('movie-cave-load-identities-001')
    secretPartFiles = @('grafana-cloud-identities.secret-001.txt')
} | ConvertTo-Json -Compress
[System.IO.File]::WriteAllText((Join-Path $oversizeData 'grafana-cloud-identities.manifest.json'), $oversizeManifest, (New-Object System.Text.UTF8Encoding($false)))
[System.IO.File]::WriteAllText((Join-Path $oversizeData 'grafana-cloud-identities.secret-001.txt'), '{"identities":[{"id":"x","bearerToken":"eyJ.oversize"}]}', (New-Object System.Text.UTF8Encoding($false)))
$oversizeFailed = $false
try {
    Invoke-LoadTestGrafanaSecretsSync -LoadRoot $oversizeRoot -DryRun
} catch {
    $oversizeFailed = $true
}
Assert-True 'oversized secret part fails before network' $oversizeFailed
Remove-Item $oversizeRoot -Recurse -Force

$loadRootSample = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$cloudPaths = Get-LoadTestK6PathEnvValues -ExecutionMode Cloud -LoadRoot $loadRootSample
Assert-Equal 'cloud data dir archive-relative' '../data' $cloudPaths.LOAD_TEST_DATA_DIR
Assert-Equal 'cloud thresholds archive-relative' '../config/thresholds.json' $cloudPaths.LOAD_TEST_THRESHOLDS_FILE
Assert-True 'cloud data dir not windows absolute' (-not ($cloudPaths.LOAD_TEST_DATA_DIR -match '^[A-Za-z]:'))
Assert-Equal 'cloud hot content path' '../data/hot-content.json' ($cloudPaths.LOAD_TEST_DATA_DIR + '/hot-content.json')

$localPaths = Get-LoadTestK6PathEnvValues -ExecutionMode Local -LoadRoot $loadRootSample
Assert-True 'local data dir is host absolute' ($localPaths.LOAD_TEST_DATA_DIR -match '^[A-Za-z]:/')
Assert-True 'local data dir ends with data' ($localPaths.LOAD_TEST_DATA_DIR -match '/data$')
Assert-True 'local thresholds is host absolute' ($localPaths.LOAD_TEST_THRESHOLDS_FILE -match '^[A-Za-z]:/')

$cloudEnvArgs = Build-LoadTestK6EnvArgs -EnvVars @{
    LOAD_TEST_DATA_DIR        = $cloudPaths.LOAD_TEST_DATA_DIR
    LOAD_TEST_THRESHOLDS_FILE = $cloudPaths.LOAD_TEST_THRESHOLDS_FILE
    LOAD_TEST_EXECUTION_MODE  = 'grafana-cloud'
    LOAD_TEST_BASE_URL        = 'https://example.com'
}
$cloudArgsJoined = $cloudEnvArgs -join ' '
Assert-True 'cloud k6 env omits windows absolute data path' (-not ($cloudArgsJoined -match 'LOAD_TEST_DATA_DIR=[A-Za-z]:'))
Assert-True 'cloud k6 env uses archive data dir' ($cloudArgsJoined -match 'LOAD_TEST_DATA_DIR=\.\./data')

if ($failures -gt 0) {
    Write-Host ($failures.ToString() + ' assertion(s) failed.')
    exit 1
}

Write-Host 'All LoadTestHarness assertions passed.'
exit 0
