param(
    [Parameter(Mandatory = $true)]
    [string]$ReportPath
)

$ErrorActionPreference = "Stop"
$json = Get-Content $ReportPath -Raw | ConvertFrom-Json

$meta = $json.metadata
$dur = $json.metrics.http_req_duration
$failed = $json.metrics.http_req_failed
$reqs = $json.metrics.http_reqs

Write-Host ""
Write-Host "=== Movie Cave load test summary ==="
Write-Host "Timestamp (UTC): $($meta.timestampUtc)"
Write-Host "Environment:     $($meta.environment)"
Write-Host "Base URL:        $($meta.baseUrl)"
Write-Host "Scenario:        $($meta.scenario)"
Write-Host "Preset:          $($meta.preset)"
Write-Host "Stage VUs:       $($meta.stageTargetVus)"
Write-Host "Content dataset: $($meta.contentDataset)"
Write-Host "Backend SHA:     $($meta.backendCommitSha)"
Write-Host "Tool SHA:        $($meta.loadTestCommitSha)"
Write-Host ""
Write-Host "Requests:        $($reqs.count) total, $($reqs.rate)/s"
Write-Host "HTTP failures:   $([math]::Round($failed.rate * 100, 2))%"
if ($json.metrics.semantic_success) {
    $sem = $json.metrics.semantic_success
    Write-Host "Semantic success: $([math]::Round($sem.rate * 100, 2))%"
}
if ($json.metrics.unexpected_status) {
    $unex = $json.metrics.unexpected_status
    Write-Host "Unexpected status: $([math]::Round($unex.rate * 100, 2))%"
}
if ($json.metrics.rate_limited) {
    $rl = $json.metrics.rate_limited
    $rlCount = $json.metrics.rate_limited_count.count
    if (-not $rlCount) { $rlCount = 0 }
    Write-Host "Rate limited (429): $([math]::Round($rl.rate * 100, 2))% (count: $rlCount)"
}
if ($dur) {
    Write-Host "Latency p50:     $($dur.med) ms"
    Write-Host "Latency p90:     $($dur.'p(90)') ms"
    Write-Host "Latency p95:     $($dur.'p(95)') ms"
    Write-Host "Latency p99:     $($dur.'p(99)') ms"
    Write-Host "Latency max:     $($dur.max) ms"
}
Write-Host ""
