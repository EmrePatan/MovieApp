param(
    [switch]$DryRun,

    [switch]$PruneStaleManagedSecrets
)

$ErrorActionPreference = 'Stop'
$loadRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Import-Module (Join-Path $PSScriptRoot 'LoadTestHarness.psm1') -Force

$null = Invoke-LoadTestGrafanaSecretsSync `
    -LoadRoot $loadRoot `
    -DryRun:$DryRun `
    -PruneStaleManagedSecrets:$PruneStaleManagedSecrets

if ($DryRun) {
    Write-Host 'Dry run complete. No Grafana secrets were created or updated.'
} else {
    Write-Host 'Grafana secrets sync complete.'
}
