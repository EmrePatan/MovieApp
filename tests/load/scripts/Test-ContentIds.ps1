param(
    [ValidateSet("hot", "varied")]
    [string]$Dataset = "hot",
    [switch]$FailOnError,
    [string]$BaseUrl = $env:LOAD_TEST_BASE_URL,
    [string]$DataDir = $env:LOAD_TEST_DATA_DIR
)

$ErrorActionPreference = "Stop"
$repoLoad = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($DataDir)) {
    $DataDir = Join-Path $repoLoad "data"
}
if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    throw "Set LOAD_TEST_BASE_URL or pass -BaseUrl"
}

$fileName = if ($Dataset -eq "varied") { "varied-content.json" } else { "hot-content.json" }
$prodFile = Join-Path $DataDir ($fileName -replace ".json", ".production.json")
$path = if (Test-Path $prodFile) { $prodFile } else { Join-Path $DataDir $fileName }
if (-not (Test-Path $path)) {
    throw "Content file not found: $path (or production variant)"
}

$base = $BaseUrl.TrimEnd("/")
$data = Get-Content $path -Raw | ConvertFrom-Json
$failures = 0

Write-Host "Validating content IDs from $path against $base"

foreach ($movieId in @($data.movieIds)) {
    try {
        $r = Invoke-WebRequest -Uri "$base/api/movies/$movieId" -Headers @{ Accept = "application/json" } -UseBasicParsing -TimeoutSec 30
        Write-Host "  [OK] movie $movieId — $($r.StatusCode)"
    } catch {
        $status = $_.Exception.Response.StatusCode.value__
        Write-Host "  [FAIL] movie $movieId — HTTP $status"
        $failures++
    }
}

foreach ($tvId in @($data.tvShowIds)) {
    try {
        $r = Invoke-WebRequest -Uri "$base/api/tvshows/$tvId" -Headers @{ Accept = "application/json" } -UseBasicParsing -TimeoutSec 30
        Write-Host "  [OK] tv $tvId — $($r.StatusCode)"
    } catch {
        $status = $_.Exception.Response.StatusCode.value__
        Write-Host "  [FAIL] tv $tvId — HTTP $status"
        $failures++
    }
}

if ($data.externalRatingsWarmMovieIds) {
    foreach ($id in @($data.externalRatingsWarmMovieIds)) {
        try {
            $r = Invoke-WebRequest -Uri "$base/api/movies/$id/external-ratings" -Headers @{ Accept = "application/json" } -UseBasicParsing -TimeoutSec 30
            Write-Host "  [OK] warm external movie $id — $($r.StatusCode)"
        } catch {
            $status = $_.Exception.Response.StatusCode.value__
            Write-Host "  [FAIL] warm external movie $id — HTTP $status"
            $failures++
        }
    }
}

Write-Host ""
if ($failures -gt 0) {
    $msg = "$failures content validation failure(s)."
    if ($FailOnError) { throw $msg }
    Write-Host $msg
} else {
    Write-Host "Content validation complete."
}
