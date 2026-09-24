param(
    [Parameter(Mandatory = $true)]
    [string]$Scenario,

    [string]$Preset = "smoke",

    [int]$StageTarget = 0,

    [string]$ContentDataset = "hot",

    [string]$BaseUrl = $env:LOAD_TEST_BASE_URL,

    [string]$Environment = $env:LOAD_TEST_ENVIRONMENT,

    [switch]$UseDocker
)

$ErrorActionPreference = "Stop"
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

$backendSha = ""
try {
    Push-Location $repoRoot
    $backendSha = (git rev-parse HEAD).Trim()
} finally {
    Pop-Location
}

$env:LOAD_TEST_BASE_URL = $BaseUrl.TrimEnd("/")
$env:LOAD_TEST_SCENARIO = $Scenario
$env:LOAD_TEST_PRESET = $Preset
$env:LOAD_TEST_CONTENT_DATASET = $ContentDataset
$env:LOAD_TEST_ENVIRONMENT = $Environment
$env:LOAD_TEST_BACKEND_SHA = $backendSha
$env:LOAD_TEST_TOOL_SHA = $backendSha
$env:LOAD_TEST_DATA_DIR = (Join-Path $loadRoot "data").Replace('\', '/')
$env:LOAD_TEST_THRESHOLDS_FILE = (Join-Path $loadRoot "config\thresholds.json").Replace('\', '/')

if ([string]::IsNullOrWhiteSpace($env:LOAD_TEST_TOKENS_FILE)) {
    $defaultTokens = Join-Path $loadRoot "data\tokens.json"
    if (Test-Path $defaultTokens) {
        $env:LOAD_TEST_TOKENS_FILE = $defaultTokens.Replace('\', '/')
    }
} else {
    $env:LOAD_TEST_TOKENS_FILE = $env:LOAD_TEST_TOKENS_FILE.Replace('\', '/')
}

if ($StageTarget -gt 0) {
    $env:LOAD_TEST_STAGE_TARGET = "$StageTarget"
}

$stamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ")
$reportPath = Join-Path $loadRoot "reports\$Scenario-$Preset-$(if ($StageTarget -gt 0) { $StageTarget } else { 'custom' })-$stamp.json"
$env:LOAD_TEST_REPORT_PATH = $reportPath

$k6Args = @(
    "run",
    $scenarioPath
)

function Invoke-K6Native {
    & k6 @k6Args
}

function Invoke-K6Docker {
    $mount = "${loadRoot}:/load"
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
        -v $mount `
        -w /load/scenarios `
        grafana/k6:latest `
        run "/load/scenarios/$Scenario.js"
}

if ($UseDocker -or -not (Get-Command k6 -ErrorAction SilentlyContinue)) {
    Write-Host "Running k6 via Docker (grafana/k6:latest)..."
    Invoke-K6Docker
} else {
    Push-Location (Join-Path $loadRoot "scenarios")
    try {
        Invoke-K6Native
    } finally {
        Pop-Location
    }
}

Write-Host "Report written to: $reportPath"
