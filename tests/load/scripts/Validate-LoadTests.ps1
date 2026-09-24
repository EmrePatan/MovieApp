param(
    [switch]$RunK6ParseSmoke
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$loadRoot = Join-Path $repoRoot "tests\load"

$env:LOAD_TEST_DATA_DIR = Join-Path $loadRoot "data"
$env:LOAD_TEST_THRESHOLDS_FILE = Join-Path $loadRoot "config\thresholds.json"
$env:LOAD_TEST_CONTENT_DATASET = "hot"
$env:LOAD_TEST_BASE_URL = "http://127.0.0.1:59999"
$env:LOAD_TEST_ENVIRONMENT = "validation"

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

function Invoke-K6Docker {
    param([string[]]$K6Args)
    docker run --rm -i `
        -e LOAD_TEST_BASE_URL `
        -e LOAD_TEST_PRESET `
        -e LOAD_TEST_STAGE_TARGET `
        -e LOAD_TEST_DATA_DIR=/load/data `
        -e LOAD_TEST_THRESHOLDS_FILE=/load/config/thresholds.json `
        -e LOAD_TEST_ENVIRONMENT `
        -e LOAD_TEST_REPORT_PATH `
        -v "${loadRoot}:/load" `
        -w /load/scenarios `
        grafana/k6:latest `
        @K6Args
}

Write-Host "Running k6 inspect on scenarios..."
$scenarios = @(
    "user-concurrency.js",
    "request-capacity.js",
    "preflight-health.js",
    "search-rate-limit.js",
    "semantics-selfcheck.js"
)
$k6Native = Get-Command k6 -ErrorAction SilentlyContinue
foreach ($s in $scenarios) {
    $scenarioPath = Join-Path $loadRoot "scenarios\$s"
    if ($k6Native) {
        & k6 inspect $scenarioPath
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  OK inspect $s"
        } else {
            Write-Host "  FAIL inspect $s"
        }
    } else {
        Invoke-K6Docker -K6Args @("inspect", "/load/scenarios/$s") | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  OK inspect $s (docker)"
        } else {
            Write-Host "  SKIP inspect $s (docker/k6 unavailable)"
        }
    }
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

    $reportHost = Join-Path $loadRoot "reports\validation-smoke.json"
    $env:LOAD_TEST_REPORT_PATH = $reportHost

    if ($k6Native) {
        Push-Location (Join-Path $loadRoot "scenarios")
        & k6 run --vus 2 --iterations 4 .\request-capacity.js
        Pop-Location
    } else {
        $env:LOAD_TEST_REPORT_PATH = "/load/reports/validation-smoke.json"
        Invoke-K6Docker -K6Args @("run", "--vus", "2", "--iterations", "4", "/load/scenarios/request-capacity.js")
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Host "k6 exited with code $LASTEXITCODE (connection/ threshold failures expected against dummy host; verify report JSON exists)."
    } else {
        Write-Host "k6 smoke completed."
    }
    if (Test-Path $reportHost) {
        Write-Host "Report JSON written: $reportHost"
        & (Join-Path $loadRoot "scripts\Summarize-Report.ps1") -ReportPath $reportHost
    }
}

Write-Host "Validation finished."
