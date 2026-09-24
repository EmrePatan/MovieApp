param(
    [switch]$RunK6ParseSmoke
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$loadRoot = Join-Path $repoRoot "tests\load"

Write-Host "Validating JSON configuration..."
$jsonFiles = @(
    "config\thresholds.json",
    "config\presets.json",
    "data\hot-content.example.json",
    "data\varied-content.example.json",
    "data\search-terms.example.json",
    "data\tokens.example.json"
)
foreach ($rel in $jsonFiles) {
    $path = Join-Path $loadRoot $rel
    Get-Content $path -Raw | ConvertFrom-Json | Out-Null
    Write-Host "  OK $rel"
}

if (-not (Test-Path (Join-Path $loadRoot "data\hot-content.json"))) {
    Write-Host "Note: data/hot-content.json not present; k6 will fall back to hot-content.example.json for local parse runs."
}

if ($RunK6ParseSmoke) {
    Write-Host "Running k6 parse smoke (expect connection errors if API is down)..."
    $env:LOAD_TEST_BASE_URL = "http://127.0.0.1:59999"
    $env:LOAD_TEST_PRESET = "smoke"
    $env:LOAD_TEST_VUS = "1"
    $env:LOAD_TEST_STAGE_TARGET = "1"
    $env:LOAD_TEST_DATA_DIR = Join-Path $loadRoot "data"
    $env:LOAD_TEST_THRESHOLDS_FILE = Join-Path $loadRoot "config\thresholds.json"
    $env:LOAD_TEST_TOKENS_FILE = ""
    $env:LOAD_TEST_ENVIRONMENT = "validation"

    $scenario = Join-Path $loadRoot "scenarios\request-capacity.js"
    docker run --rm -i `
        -e LOAD_TEST_BASE_URL `
        -e LOAD_TEST_PRESET `
        -e LOAD_TEST_STAGE_TARGET `
        -e LOAD_TEST_DATA_DIR=/load/data `
        -e LOAD_TEST_THRESHOLDS_FILE=/load/config/thresholds.json `
        -e LOAD_TEST_ENVIRONMENT `
        -v "${loadRoot}:/load" `
        -w /load/scenarios `
        grafana/k6:latest `
        run --vus 1 --iterations 1 /load/scenarios/request-capacity.js

    if ($LASTEXITCODE -ne 0) {
        Write-Host "k6 exited with code $LASTEXITCODE (connection failure to dummy host is acceptable if script executed)."
    } else {
        Write-Host "k6 parse smoke completed."
    }
}

Write-Host "Validation finished."
