param(
    [Parameter(Mandatory = $true)]
    [string]$Scenario,

    [ValidateSet('Local', 'Cloud')]
    [string]$ExecutionMode = 'Local',

    [string]$Preset = "smoke",

    [int]$StageTarget = 0,

    [string]$ContentDataset = "hot",

    [string]$BaseUrl = $env:LOAD_TEST_BASE_URL,

    [string]$Environment = $env:LOAD_TEST_ENVIRONMENT,

    [string]$CloudLoadZone = $env:LOAD_TEST_CLOUD_LOAD_ZONE,

    [string]$DeployedBackendSha = $env:LOAD_TEST_DEPLOYED_BACKEND_SHA,

    [switch]$UseDocker,

    [switch]$CloudValidateOnly,

    [switch]$ConfirmProductionCloudRun,

    [switch]$ConfirmHighScaleCloudRun,

    [switch]$ConfirmVeryHighScaleCloudRun
)

$ErrorActionPreference = "Stop"
Import-Module (Join-Path $PSScriptRoot 'LoadTestHarness.psm1') -Force

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$loadRoot = Join-Path $repoRoot "tests\load"
$scenarioPath = Join-Path $loadRoot "scenarios\$Scenario.js"

if (-not (Test-Path $scenarioPath)) {
    throw "Scenario not found: $scenarioPath"
}

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    throw "Set LOAD_TEST_BASE_URL or pass -BaseUrl"
}

if ([string]::IsNullOrWhiteSpace($Environment)) {
    $Environment = "local"
}

if ($ExecutionMode -eq 'Cloud' -and $UseDocker) {
    throw "Cloud execution requires native k6 (Grafana Cloud). Do not combine -UseDocker with -ExecutionMode Cloud."
}

if ([string]::IsNullOrWhiteSpace($CloudLoadZone)) {
    $CloudLoadZone = 'amazon:de:frankfurt'
}

$backendSha = ""
try {
    Push-Location $repoRoot
    $backendSha = (git rev-parse HEAD).Trim()
} finally {
    Pop-Location
}

if (-not [string]::IsNullOrWhiteSpace($DeployedBackendSha)) {
    $backendSha = $DeployedBackendSha.Trim()
}

$tokensFile = $env:LOAD_TEST_TOKENS_FILE
if ([string]::IsNullOrWhiteSpace($tokensFile)) {
    $defaultTokens = Join-Path $loadRoot "data\tokens.json"
    if (Test-Path $defaultTokens) {
        $tokensFile = $defaultTokens
    }
}

$localTokenCount = if ($tokensFile) { Read-LoadTestIdentityCount -TokensFilePath $tokensFile } else { 0 }
$cloudManifestPath = Get-LoadTestGrafanaCloudManifestPath -LoadRoot $loadRoot
$cloudManifest = $null
$cloudManifestIdentityCount = 0
$cloudManifestShardCount = 0
$cloudTransport = ''
$manifestNote = ''
$identityCount = $localTokenCount

if ($ExecutionMode -eq 'Cloud') {
    $cloudManifest = Read-LoadTestGrafanaCloudManifest -ManifestPath $cloudManifestPath
    if ($null -ne $cloudManifest) {
        $cloudManifestIdentityCount = [int]$cloudManifest.identityCount
        $cloudManifestShardCount = [int]$cloudManifest.shardCount
        $cloudTransport = [string]$cloudManifest.transport
        $identityCount = $cloudManifestIdentityCount
        $manifestNote = 'Manifest records operator export; runtime proof is identityPool.length in Grafana summary.'
        if ($localTokenCount -gt 0 -and $localTokenCount -ne $cloudManifestIdentityCount) {
            Write-Warning "Local tokens.json count ($localTokenCount) does not match Cloud manifest identityCount ($cloudManifestIdentityCount)."
        }
    }
}

$stageVus = if ($StageTarget -gt 0) { $StageTarget } else { 5 }
$reuseRatio = Get-LoadTestIdentityReuseRatio -StageVus $stageVus -IdentityCount $identityCount
$vuHours = Get-LoadTestVuHourEstimate -StageVus $stageVus -PresetName $Preset -StageTarget $StageTarget
$duration = Get-LoadTestStageDuration -PresetName $Preset -StageTarget $StageTarget
$isProduction = Test-LoadTestIsProductionTarget -BaseUrl $BaseUrl -EnvironmentLabel $Environment

$hotContent = Join-Path $loadRoot "data\hot-content.json"
if ($ContentDataset -eq 'hot' -and -not (Test-Path $hotContent)) {
    throw "HOT content required: copy data/hot-content.example.json to data/hot-content.json"
}

$searchProfileHint = if ($stageVus -ge 250) { 'off (default)' } else { 'autocomplete-only (default)' }
if ($env:LOAD_TEST_SEARCH_PROFILE) {
    $searchProfileHint = $env:LOAD_TEST_SEARCH_PROFILE
}

$executionModeEnv = if ($ExecutionMode -eq 'Cloud') { 'grafana-cloud' } else { 'local' }
$pathEnv = Get-LoadTestK6PathEnvValues -ExecutionMode $ExecutionMode -LoadRoot $loadRoot

$envMap = @{
    LOAD_TEST_BASE_URL                 = $BaseUrl.TrimEnd("/")
    LOAD_TEST_SCENARIO                 = $Scenario
    LOAD_TEST_PRESET                   = $Preset
    LOAD_TEST_CONTENT_DATASET          = $ContentDataset
    LOAD_TEST_ENVIRONMENT              = $Environment
    LOAD_TEST_BACKEND_SHA              = $backendSha
    LOAD_TEST_TOOL_SHA                 = $backendSha
    LOAD_TEST_DATA_DIR                 = $pathEnv.LOAD_TEST_DATA_DIR
    LOAD_TEST_THRESHOLDS_FILE          = $pathEnv.LOAD_TEST_THRESHOLDS_FILE
    LOAD_TEST_EXECUTION_MODE           = $executionModeEnv
    LOAD_TEST_CLOUD_LOAD_ZONE          = $CloudLoadZone
    LOAD_TEST_INCLUDE_EXTERNAL_RATINGS = $env:LOAD_TEST_INCLUDE_EXTERNAL_RATINGS
    LOAD_TEST_ALLOW_BULK_WATCH         = $env:LOAD_TEST_ALLOW_BULK_WATCH
    LOAD_TEST_BULK_TV_SHOW_ID          = $env:LOAD_TEST_BULK_TV_SHOW_ID
    LOAD_TEST_SEARCH_PROFILE           = $env:LOAD_TEST_SEARCH_PROFILE
}

if ($StageTarget -gt 0) {
    $envMap.LOAD_TEST_STAGE_TARGET = "$StageTarget"
}

$stamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ")
$reportPath = Join-Path $loadRoot "reports\$Scenario-$Preset-$(if ($StageTarget -gt 0) { $StageTarget } else { 'custom' })-$stamp.json"
$envMap.LOAD_TEST_REPORT_PATH = $reportPath

if ($ExecutionMode -eq 'Local') {
    if (-not [string]::IsNullOrWhiteSpace($tokensFile)) {
        $envMap.LOAD_TEST_TOKENS_FILE = $tokensFile.Replace('\', '/')
    }
} else {
    # Never bundle production JWTs into a cloud archive via -e or open(tokens.json).
    if ($localTokenCount -lt 1) {
        throw "Cloud mode requires a local tokens.json for operator preflight (Test-LoadTokens.ps1)."
    }
    if ($null -eq $cloudManifest) {
        throw @"
Cloud mode requires a Grafana Cloud identity manifest at:
  $cloudManifestPath
Run: .\scripts\Export-LoadTestIdentitiesForGrafanaCloud.ps1
Then configure Grafana Cloud env vars per tests/load/docs/grafana-cloud-k6.md (never via k6 cloud -e).
"@
    }
    if ($cloudManifestIdentityCount -lt 1) {
        throw 'Cloud manifest identityCount must be >= 1.'
    }
    if ($cloudTransport -eq 'sharded' -and $cloudManifestShardCount -lt 1) {
        throw 'Sharded Cloud manifest requires shardCount >= 1.'
    }
}

foreach ($key in $envMap.Keys) {
    if ($null -ne $envMap[$key] -and $envMap[$key] -ne '') {
        Set-Item -Path "Env:$key" -Value $envMap[$key]
    }
}

Write-Host (Format-LoadTestCloudPreflight `
    -ExecutionMode $ExecutionMode `
    -StageVus $stageVus `
    -IdentityCount $identityCount `
    -ReuseRatio $reuseRatio `
    -VuHours $vuHours `
    -Duration $duration `
    -LoadZone $(if ($ExecutionMode -eq 'Cloud') { $CloudLoadZone } else { 'n/a' }) `
    -SearchProfileHint $searchProfileHint `
    -IsProduction $isProduction `
    -LocalTokenCount $(if ($ExecutionMode -eq 'Cloud') { $localTokenCount } else { 0 }) `
    -CloudManifestIdentityCount $cloudManifestIdentityCount `
    -CloudManifestShardCount $cloudManifestShardCount `
    -CloudTransport $cloudTransport `
    -ManifestNote $(if ($ExecutionMode -eq 'Cloud') { $manifestNote } else { '' }))

if ($ExecutionMode -eq 'Cloud') {
    if ($vuHours -ge 50) {
        Write-Warning "Approximate VU-hours ($vuHours) may consume significant Grafana Cloud quota. Verify your plan before running."
    }

    if ($isProduction -and -not $ConfirmProductionCloudRun) {
        throw "Production Grafana Cloud load requires -ConfirmProductionCloudRun. Cloud mode never defaults to production load."
    }

    if ($stageVus -ge 500 -and -not $ConfirmVeryHighScaleCloudRun) {
        throw "Stage >= 500 VU requires -ConfirmVeryHighScaleCloudRun (explicit operator approval)."
    }
    elseif ($stageVus -ge 250 -and -not $ConfirmHighScaleCloudRun) {
        throw "Stage >= 250 VU requires -ConfirmHighScaleCloudRun."
    }

    if ($CloudValidateOnly) {
        Write-Host "Cloud validate-only: no VUs will run against production."
    }
    elseif (-not $ConfirmProductionCloudRun -and $isProduction) {
        throw "Missing production confirmation."
    }
    elseif ($isProduction -and -not $CloudValidateOnly) {
        $typed = Read-Host "Type RUN to start Grafana Cloud execution against $BaseUrl"
        if ($typed -ne 'RUN') {
            throw 'Aborted by operator.'
        }
    }

    $k6Exe = Get-LoadTestK6Executable
    Assert-LoadTestCloudAuth -K6Exe $k6Exe

    $envArgs = Build-LoadTestK6EnvArgs -EnvVars $envMap
    $runName = "movie-cave-$Scenario-$(if ($StageTarget -gt 0) { $StageTarget } else { 'custom' })-$stamp"
    $env:LOAD_TEST_CLOUD_RUN_NAME = $runName

    Push-Location (Join-Path $loadRoot "scenarios")
    try {
        if ($CloudValidateOnly) {
            Write-Host "Archiving script bundle (local archive validation)..."
            Invoke-K6Cli -K6Exe $k6Exe -K6Arguments (@('archive') + $envArgs + @('-O', (Join-Path $loadRoot "reports\cloud-archive-$stamp.tar"), $scenarioPath)) | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "k6 archive failed with exit code $LASTEXITCODE" }

            Write-Host "Uploading to Grafana Cloud without execution (k6 cloud upload)..."
            Invoke-K6Cli -K6Exe $k6Exe -K6Arguments (@('cloud', 'upload') + $envArgs + @($scenarioPath)) | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "k6 cloud upload failed with exit code $LASTEXITCODE" }
        } else {
            Invoke-K6Cli -K6Exe $k6Exe -K6Arguments (@('cloud', 'run') + $envArgs + @($scenarioPath)) | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "k6 cloud run failed with exit code $LASTEXITCODE" }
        }
    } finally {
        Pop-Location
    }

    if ($CloudValidateOnly) {
        Write-Host "Cloud validation complete. Use Grafana Cloud UI to inspect the uploaded test; no load was generated."
    } else {
        Write-Host "Grafana Cloud run submitted. Primary metrics: Grafana Cloud k6 UI (handleSummary file may not land locally)."
    }
    return
}

$k6Args = @("run", $scenarioPath)

function Invoke-K6Native {
    param([string]$K6Exe)
    & $K6Exe @k6Args
}

function Invoke-K6Docker {
    docker run --rm -i `
        -e LOAD_TEST_BASE_URL `
        -e LOAD_TEST_SCENARIO `
        -e LOAD_TEST_PRESET `
        -e LOAD_TEST_STAGE_TARGET `
        -e LOAD_TEST_CONTENT_DATASET `
        -e LOAD_TEST_ENVIRONMENT `
        -e LOAD_TEST_BACKEND_SHA `
        -e LOAD_TEST_TOOL_SHA `
        -e LOAD_TEST_DATA_DIR=/load/data `
        -e LOAD_TEST_THRESHOLDS_FILE=/load/config/thresholds.json `
        -e LOAD_TEST_REPORT_PATH="/load/reports/$(Split-Path $reportPath -Leaf)" `
        -e LOAD_TEST_TOKENS_FILE `
        -e LOAD_TEST_INCLUDE_EXTERNAL_RATINGS `
        -e LOAD_TEST_ALLOW_BULK_WATCH `
        -e LOAD_TEST_BULK_TV_SHOW_ID `
        -e LOAD_TEST_EXECUTION_MODE `
        -e LOAD_TEST_SEARCH_PROFILE `
        -v "${loadRoot}:/load" `
        -w /load/scenarios `
        grafana/k6:latest `
        run "/load/scenarios/$Scenario.js"
}

if ($UseDocker -or -not (Get-Command k6 -ErrorAction SilentlyContinue)) {
    Write-Host "Running k6 via Docker (grafana/k6:latest)..."
    Invoke-K6Docker
} else {
    $k6Exe = Get-LoadTestK6Executable
    Push-Location (Join-Path $loadRoot "scenarios")
    try {
        Invoke-K6Native -K6Exe $k6Exe
    } finally {
        Pop-Location
    }
}

if ($LASTEXITCODE -ne 0) {
    throw "k6 exited with code $LASTEXITCODE"
}

Write-Host "Report written to: $reportPath"
