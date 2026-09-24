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

$preflight = Format-LoadTestCloudPreflight -ExecutionMode Cloud -StageVus 150 -IdentityCount 100 -ReuseRatio 1.5 -VuHours 36 -Duration '00:17:00' -LoadZone 'amazon:de:frankfurt' -SearchProfileHint 'autocomplete-only' -IsProduction $true
Assert-True 'preflight mentions reuse' ($preflight -match '1.5:1')

if ($failures -gt 0) {
    Write-Host ($failures.ToString() + ' assertion(s) failed.')
    exit 1
}

Write-Host 'All LoadTestHarness assertions passed.'
exit 0
