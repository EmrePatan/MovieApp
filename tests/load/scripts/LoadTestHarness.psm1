# Pure helpers for Invoke-K6.ps1 (importable / unit-testable).

function ConvertFrom-K6Duration {
    param([string]$Value)

    $v = $Value.Trim()
    if ($v -match '^(\d+(?:\.\d+)?)(ms|s|m|h)$') {
        $amount = [double]$Matches[1]
        switch ($Matches[2]) {
            'ms' { return [TimeSpan]::FromMilliseconds($amount) }
            's' { return [TimeSpan]::FromSeconds($amount) }
            'm' { return [TimeSpan]::FromMinutes($amount) }
            'h' { return [TimeSpan]::FromHours($amount) }
        }
    }
    throw "Unsupported k6 duration format: $Value"
}

function Get-LoadTestPresetTiming {
    param(
        [string]$PresetName,
        [int]$StageTarget = 0
    )

    $presetsPath = Join-Path $PSScriptRoot '..\config\presets.json'
    $presets = Get-Content $presetsPath -Raw | ConvertFrom-Json

    $preset = if ($PresetName -eq 'capacity') { $presets.capacity } else { $presets.smoke }
    if ($StageTarget -eq 1000 -and $presets.capacityStage1000) {
        $preset = $presets.capacityStage1000
    }

    return @{
        RampUp   = ConvertFrom-K6Duration $preset.rampUp
        Hold     = ConvertFrom-K6Duration $preset.hold
        RampDown = ConvertFrom-K6Duration $preset.rampDown
    }
}

function Get-LoadTestStageDuration {
    param(
        [string]$PresetName,
        [int]$StageTarget = 0
    )

    $t = Get-LoadTestPresetTiming -PresetName $PresetName -StageTarget $StageTarget
    return $t.RampUp + $t.Hold + $t.RampDown
}

function Get-LoadTestIdentityReuseRatio {
    param(
        [int]$StageVus,
        [int]$IdentityCount
    )

    if ($IdentityCount -lt 1) {
        return [double]::PositiveInfinity
    }
    return [double]$StageVus / [double]$IdentityCount
}

function Get-LoadTestVuHourEstimate {
    param(
        [int]$StageVus,
        [string]$PresetName,
        [int]$StageTarget = 0
    )

    if ($StageVus -lt 1) {
        return 0.0
    }

    $t = Get-LoadTestPresetTiming -PresetName $PresetName -StageTarget $StageTarget
    $rampHours = $t.RampUp.TotalHours
    $holdHours = $t.Hold.TotalHours
    $downHours = $t.RampDown.TotalHours

    # ramping-vus: linear 0→target and target→0; average VUs = target/2 per ramp stage.
    $vuHours = ($StageVus / 2.0) * $rampHours + ($StageVus * $holdHours) + ($StageVus / 2.0) * $downHours
    return [Math]::Round($vuHours, 3)
}

function Test-LoadTestIsProductionTarget {
    param(
        [string]$BaseUrl,
        [string]$EnvironmentLabel
    )

    if ($EnvironmentLabel -eq 'production') {
        return $true
    }
    if ($BaseUrl -match 'movieapp-fpkg\.onrender\.com') {
        return $true
    }
    return $false
}

function Get-LoadTestK6Executable {
    $fromPath = $null
    $cmd = Get-Command k6 -ErrorAction SilentlyContinue
    if ($cmd) {
        $fromPath = $cmd.Source
    }
    $candidates = @( @($fromPath, 'C:\Program Files\k6\k6.exe') | Where-Object { $_ -and (Test-Path $_) } )

    if ($candidates.Count -lt 1) {
        throw 'k6 not found. Install native k6 or use -ExecutionMode Local with k6 on PATH. Cloud mode does not use Docker.'
    }
    return [string]$candidates[0]
}

function Read-LoadTestIdentityCount {
    param([string]$TokensFilePath)

    if (-not $TokensFilePath -or -not (Test-Path $TokensFilePath)) {
        return 0
    }

    $parsed = Get-Content $TokensFilePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $count = 0
    foreach ($id in @($parsed.identities)) {
        if ($id.bearerToken -and $id.bearerToken -ne 'REPLACE_WITH_JWT') {
            $count++
        }
    }
    return $count
}

function Get-LoadTestGrafanaCloudManifestFileName {
    return 'grafana-cloud-identities.manifest.json'
}

function Get-LoadTestGrafanaCloudManifestPath {
    param([string]$LoadRoot)

    return Join-Path $LoadRoot ('data\' + (Get-LoadTestGrafanaCloudManifestFileName))
}

function Read-LoadTestMinimalIdentitiesFromTokensFile {
    param([string]$TokensFilePath)

    if (-not (Test-Path $TokensFilePath)) {
        throw "Tokens file not found: $TokensFilePath"
    }

    $parsed = Get-Content $TokensFilePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $minimal = @()
    foreach ($id in @($parsed.identities)) {
        if (-not $id.bearerToken -or $id.bearerToken -eq 'REPLACE_WITH_JWT') {
            continue
        }
        $minimal += @{
            id          = [string]$id.id
            bearerToken = [string]$id.bearerToken
        }
    }

    if ($minimal.Count -lt 1) {
        throw 'No valid bearer tokens in tokens file.'
    }

    return ,$minimal
}

function Export-LoadTestIdentitiesPayload {
    param([string]$TokensFilePath)

    $minimal = Read-LoadTestMinimalIdentitiesFromTokensFile -TokensFilePath $TokensFilePath
    return (@{ identities = $minimal } | ConvertTo-Json -Compress -Depth 5)
}

function Get-LoadTestGrafanaIdentityShardEnvName {
    param([int]$ShardIndex)

    return ('LOAD_TEST_IDENTITIES_JSON_{0}' -f $ShardIndex.ToString('000'))
}

function Split-LoadTestIdentitiesForGrafanaShards {
    param(
        [Parameter(Mandatory)]
        [array]$Identities,
        [int]$MaxShardChars = 4500
    )

    if ($Identities.Count -lt 1) {
        throw 'At least one identity is required to build Grafana shards.'
    }

    $encoded = @()
    foreach ($id in $Identities) {
        $encoded += (@{ id = [string]$id.id; bearerToken = [string]$id.bearerToken } | ConvertTo-Json -Compress -Depth 3)
    }

    $prefix = '{"identities":['
    $suffix = ']}'
    $shards = [System.Collections.Generic.List[string]]::new()
    $current = $null
    $i = 0
    $total = $encoded.Count

    while ($i -lt $total) {
        $enc = $encoded[$i]
        if ($null -eq $current) {
            $candidate = if ($i -eq 0) { $prefix + $enc } else { ',' + $enc }
        } else {
            $candidate = $current + ',' + $enc
        }

        $isLast = ($i -eq ($total - 1))
        $withClose = if ($isLast) { $candidate + $suffix } else { $candidate }

        if ($withClose.Length -le $MaxShardChars) {
            if ($isLast) {
                $shards.Add($withClose)
                $current = $null
            } else {
                $current = $candidate
            }
            $i++
            continue
        }

        if ($null -eq $current -or $current.Length -eq 0) {
            throw "Identity at index $i exceeds Grafana shard limit ($MaxShardChars characters)."
        }

        $shards.Add($current)
        $current = $null
    }

    $shardEnvNames = @()
    for ($s = 1; $s -le $shards.Count; $s++) {
        $shardEnvNames += Get-LoadTestGrafanaIdentityShardEnvName -ShardIndex $s
    }

    $assembled = -join $shards
    $roundTrip = $assembled | ConvertFrom-Json
    if ($roundTrip.identities.Count -ne $Identities.Count) {
        throw 'Shard assembly round-trip identity count mismatch.'
    }

    return @{
        Shards         = $shards
        ShardEnvNames  = $shardEnvNames
        AssembledJson  = $assembled
        MaxShardChars  = $MaxShardChars
    }
}

function Read-LoadTestGrafanaCloudManifest {
    param([string]$ManifestPath)

    if (-not $ManifestPath -or -not (Test-Path $ManifestPath)) {
        return $null
    }

    $parsed = Get-Content $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($null -eq $parsed.schemaVersion) {
        throw "Invalid Grafana Cloud identity manifest (missing schemaVersion): $ManifestPath"
    }

    return $parsed
}

function Test-LoadTestManifestContainsNoSecrets {
    param([string]$ManifestPath)

    $raw = Get-Content $ManifestPath -Raw -Encoding UTF8
    return (-not ($raw -match 'bearerToken|eyJ[A-Za-z0-9_-]{10,}'))
}

function Invoke-K6Cli {
    param(
        [string]$K6Exe,
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$K6Arguments
    )

    & $K6Exe @K6Arguments
}

function Assert-LoadTestCloudAuth {
    param([string]$K6Exe)

    $out = (Invoke-K6Cli -K6Exe $K6Exe -K6Arguments @('cloud', 'load-zone', 'list') 2>&1 | Out-String)
    if ($out -match 'access token not configured|requires auth') {
        throw @"
Grafana Cloud k6 is not authenticated on this machine.
Run once: k6 cloud login
Or set K6_CLOUD_TOKEN for your stack (see tests/load/docs/grafana-cloud-k6.md).
"@
    }
}

function Get-LoadTestK6PathEnvValues {
    param(
        [ValidateSet('Local', 'Cloud')]
        [string]$ExecutionMode,
        [string]$LoadRoot
    )

    if ($ExecutionMode -eq 'Cloud') {
        return @{
            LOAD_TEST_DATA_DIR        = '../data'
            LOAD_TEST_THRESHOLDS_FILE = '../config/thresholds.json'
        }
    }

    return @{
        LOAD_TEST_DATA_DIR        = (Join-Path $LoadRoot 'data').Replace('\', '/')
        LOAD_TEST_THRESHOLDS_FILE = (Join-Path $LoadRoot 'config\thresholds.json').Replace('\', '/')
    }
}

function Build-LoadTestK6EnvArgs {
    param([hashtable]$EnvVars)

    $args = @()
    foreach ($key in ($EnvVars.Keys | Sort-Object)) {
        $value = $EnvVars[$key]
        if ($null -eq $value) {
            continue
        }
        $args += '-e'
        $args += "${key}=$value"
    }
    return $args
}

function Format-LoadTestCloudPreflight {
    param(
        [string]$ExecutionMode,
        [int]$StageVus,
        [int]$IdentityCount,
        [double]$ReuseRatio,
        [double]$VuHours,
        [string]$Duration,
        [string]$LoadZone,
        [string]$SearchProfileHint,
        [bool]$IsProduction,
        [int]$LocalTokenCount = 0,
        [int]$CloudManifestIdentityCount = 0,
        [int]$CloudManifestShardCount = 0,
        [string]$CloudTransport = '',
        [string]$ManifestNote = ''
    )

    $reuseText = if ([double]::IsPositiveInfinity($ReuseRatio)) {
        'n/a (no identities)'
    } else {
        ([Math]::Round($ReuseRatio, 2)).ToString([System.Globalization.CultureInfo]::InvariantCulture) + ':1'
    }
    $lines = @(
        "Execution mode:     $ExecutionMode",
        "Stage target VUs:   $StageVus"
    )

    if ($ExecutionMode -eq 'Cloud' -and $CloudManifestIdentityCount -gt 0) {
        $lines += "Cloud identity pool (manifest): $CloudManifestIdentityCount"
        if ($CloudTransport -eq 'sharded' -and $CloudManifestShardCount -gt 0) {
            $lines += "Cloud identity shards:          $CloudManifestShardCount"
        } elseif ($CloudTransport -eq 'single') {
            $lines += 'Cloud identity transport:     single LOAD_TEST_IDENTITIES_JSON'
        }
        if ($LocalTokenCount -gt 0) {
            $lines += "Local tokens (preflight only):  $LocalTokenCount"
        }
        if ($ManifestNote) {
            $lines += "Manifest note:                $ManifestNote"
        }
        $lines += "VU/identity reuse (manifest): $reuseText"
    } else {
        $lines += "Identity pool:      $IdentityCount"
        $lines += "VU/identity reuse:  $reuseText"
    }

    $lines += @(
        "Preset duration:    $Duration",
        "VU-hour estimate:   $VuHours (approx.; not billing-accurate)",
        "Cloud load zone:    $LoadZone",
        "Search profile:     $SearchProfileHint",
        "Production target:  $IsProduction"
    )
    return ($lines -join [Environment]::NewLine)
}

Export-ModuleMember -Function @(
    'Invoke-K6Cli',
    'Get-LoadTestPresetTiming',
    'Get-LoadTestStageDuration',
    'Get-LoadTestIdentityReuseRatio',
    'Get-LoadTestVuHourEstimate',
    'Test-LoadTestIsProductionTarget',
    'Get-LoadTestK6Executable',
    'Read-LoadTestIdentityCount',
    'Read-LoadTestMinimalIdentitiesFromTokensFile',
    'Export-LoadTestIdentitiesPayload',
    'Get-LoadTestGrafanaCloudManifestFileName',
    'Get-LoadTestGrafanaCloudManifestPath',
    'Get-LoadTestGrafanaIdentityShardEnvName',
    'Split-LoadTestIdentitiesForGrafanaShards',
    'Read-LoadTestGrafanaCloudManifest',
    'Test-LoadTestManifestContainsNoSecrets',
    'Assert-LoadTestCloudAuth',
    'Get-LoadTestK6PathEnvValues',
    'Build-LoadTestK6EnvArgs',
    'Format-LoadTestCloudPreflight'
)
