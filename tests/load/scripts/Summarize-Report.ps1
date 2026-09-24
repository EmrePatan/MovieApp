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
if ($dur) {
    Write-Host "Latency p50:     $($dur.med) ms"
    Write-Host "Latency p90:     $($dur.'p(90)') ms"
    Write-Host "Latency p95:     $($dur.'p(95)') ms"
    Write-Host "Latency p99:     $($dur.'p(99)') ms"
    Write-Host "Latency max:     $($dur.max) ms"
}
Write-Host ""
