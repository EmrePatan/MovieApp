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

function Assert-LoadTestContentDatasetSupported {
    param([string]$Dataset = 'hot')

    if ([string]::IsNullOrWhiteSpace($Dataset)) {
        $Dataset = 'hot'
    }

    $normalized = $Dataset.Trim().ToLowerInvariant()
    $supported = @('hot', 'varied')
    if ($normalized -notin $supported) {
        throw "Unsupported LOAD_TEST_CONTENT_DATASET: '$Dataset'. Supported values: $($supported -join ', ')."
    }

    return $normalized
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

function Get-LoadTestGrafanaSecretPartName {
    param([int]$PartIndex)

    return ('movie-cave-load-identities-{0}' -f $PartIndex.ToString('000'))
}

function Get-LoadTestGrafanaSecretPartFileName {
    param([int]$PartIndex)

    return ('grafana-cloud-identities.secret-{0}.txt' -f $PartIndex.ToString('000'))
}

function Get-LoadTestGrafanaSecretsSyncStateFileName {
    return 'grafana-cloud-identities.sync-state.json'
}

function Get-LoadTestGrafanaSecretsSyncStatePath {
    param([string]$LoadRoot)

    return Join-Path $LoadRoot ('data\' + (Get-LoadTestGrafanaSecretsSyncStateFileName))
}

function Get-LoadTestUtf8ByteCount {
    param([string]$Text)

    return [System.Text.Encoding]::UTF8.GetByteCount($Text)
}

function Split-LoadTestIdentitiesJsonParts {
    param(
        [Parameter(Mandatory)]
        [array]$Identities,
        [int]$MaxUtf8Bytes = 0,
        [int]$MaxChars = 0
    )

    if ($Identities.Count -lt 1) {
        throw 'At least one identity is required to build identity JSON parts.'
    }

    if ($MaxUtf8Bytes -le 0 -and $MaxChars -le 0) {
        throw 'Specify MaxUtf8Bytes or MaxChars for identity JSON part splitting.'
    }

    function Test-PartFits {
        param([string]$Text)

        if ($MaxUtf8Bytes -gt 0) {
            return (Get-LoadTestUtf8ByteCount -Text $Text) -le $MaxUtf8Bytes
        }

        return $Text.Length -le $MaxChars
    }

    $encoded = @()
    foreach ($id in $Identities) {
        $encoded += (@{ id = [string]$id.id; bearerToken = [string]$id.bearerToken } | ConvertTo-Json -Compress -Depth 3)
    }

    $prefix = '{"identities":['
    $suffix = ']}'
    $parts = [System.Collections.Generic.List[string]]::new()
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

        if (Test-PartFits -Text $withClose) {
            if ($isLast) {
                $parts.Add($withClose)
                $current = $null
            } else {
                $current = $candidate
            }
            $i++
            continue
        }

        if ($null -eq $current -or $current.Length -eq 0) {
            $limitLabel = if ($MaxUtf8Bytes -gt 0) { "$MaxUtf8Bytes UTF-8 bytes" } else { "$MaxChars characters" }
            throw "Identity at index $i exceeds Grafana part limit ($limitLabel)."
        }

        $parts.Add($current)
        $current = $null
    }

    $assembled = -join $parts
    $roundTrip = $assembled | ConvertFrom-Json
    if ($roundTrip.identities.Count -ne $Identities.Count) {
        throw 'Identity JSON part assembly round-trip count mismatch.'
    }

    $partByteSizes = @()
    foreach ($part in $parts) {
        $partByteSizes += (Get-LoadTestUtf8ByteCount -Text $part)
    }

    return @{
        Parts          = $parts
        AssembledJson  = $assembled
        PartByteSizes  = $partByteSizes
        MaxUtf8Bytes   = $MaxUtf8Bytes
        MaxChars       = $MaxChars
    }
}

function Split-LoadTestIdentitiesForGrafanaShards {
    param(
        [Parameter(Mandatory)]
        [array]$Identities,
        [int]$MaxShardChars = 4500
    )

    $split = Split-LoadTestIdentitiesJsonParts -Identities $Identities -MaxChars $MaxShardChars
    $shardEnvNames = @()
    for ($s = 1; $s -le $split.Parts.Count; $s++) {
        $shardEnvNames += Get-LoadTestGrafanaIdentityShardEnvName -ShardIndex $s
    }

    return @{
        Shards         = $split.Parts
        ShardEnvNames  = $shardEnvNames
        AssembledJson  = $split.AssembledJson
        MaxShardChars  = $MaxShardChars
    }
}

function Split-LoadTestIdentitiesForGrafanaSecrets {
    param(
        [Parameter(Mandatory)]
        [array]$Identities,
        [int]$MaxPartUtf8Bytes = 22528
    )

    $split = Split-LoadTestIdentitiesJsonParts -Identities $Identities -MaxUtf8Bytes $MaxPartUtf8Bytes
    $secretNames = @()
    $partFiles = @()
    for ($p = 1; $p -le $split.Parts.Count; $p++) {
        $secretNames += Get-LoadTestGrafanaSecretPartName -PartIndex $p
        $partFiles += Get-LoadTestGrafanaSecretPartFileName -PartIndex $p
    }

    return @{
        Parts          = $split.Parts
        PartByteSizes  = $split.PartByteSizes
        SecretNames    = $secretNames
        PartFiles      = $partFiles
        AssembledJson  = $split.AssembledJson
        MaxPartUtf8Bytes = $MaxPartUtf8Bytes
    }
}

function New-LoadTestGrafanaSecureValueRequestBody {
    param(
        [Parameter(Mandatory)][string]$SecretName,
        [Parameter(Mandatory)][string]$SecretValue,
        [string]$Description
    )

    if ([string]::IsNullOrWhiteSpace($Description)) {
        $Description = ('MC load id {0}' -f ($SecretName -replace '^movie-cave-load-identities-', ''))
    }

    if ($Description.Length -gt 25) {
        $Description = $Description.Substring(0, 25)
    }

    return @{
        metadata = @{ name = $SecretName }
        spec     = @{
            description = $Description
            value       = $SecretValue
            decrypters  = @('k6-cloud')
        }
    }
}

function Resolve-LoadTestGrafanaSecretsNamespace {
    if (-not [string]::IsNullOrWhiteSpace($env:GRAFANA_SECRETS_NAMESPACE)) {
        return $env:GRAFANA_SECRETS_NAMESPACE.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GRAFANA_STACK_ID)) {
        return 'stacks-' + $env:GRAFANA_STACK_ID.Trim()
    }

    throw @'
Grafana Cloud Secrets sync requires GRAFANA_STACK_ID (numeric stack instance ID from Grafana Cloud → your stack → Details)
or GRAFANA_SECRETS_NAMESPACE (e.g. stacks-123456). The API namespace is not "default" on Grafana Cloud.
See tests/load/docs/grafana-cloud-k6.md.
'@.Trim()
}

function Get-LoadTestGrafanaSecureValuesApiUris {
    param(
        [Parameter(Mandatory)][string]$GrafanaBaseUrl,
        [Parameter(Mandatory)][string]$Namespace,
        [string]$SecretName
    )

    $base = $GrafanaBaseUrl.TrimEnd('/')
    $collection = '{0}/apis/secret.grafana.app/v1beta1/namespaces/{1}/securevalues' -f $base, $Namespace
    $resource = if ([string]::IsNullOrWhiteSpace($SecretName)) {
        $null
    } else {
        '{0}/{1}' -f $collection, $SecretName
    }

    return @{
        Collection = $collection
        Resource   = $resource
    }
}

function Format-LoadTestGrafanaSyncHttpError {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$RequestUri,
        [System.Management.Automation.ErrorRecord]$ErrorRecord
    )

    $statusCode = $null
    $statusDescription = $null
    $detail = $null

    if ($null -ne $ErrorRecord -and $null -ne $ErrorRecord.Exception) {
        $response = $ErrorRecord.Exception.Response
        if ($null -ne $response) {
            try {
                $statusCode = [int]$response.StatusCode
            } catch {
                # ignore
            }

            try {
                $statusDescription = [string]$response.StatusDescription
            } catch {
                # ignore
            }
        }
    }

    if ($null -ne $ErrorRecord -and $ErrorRecord.ErrorDetails -and $ErrorRecord.ErrorDetails.Message) {
        $raw = $ErrorRecord.ErrorDetails.Message.Trim()
        if ($raw.Length -gt 280) {
            $raw = $raw.Substring(0, 280) + '...'
        }

        if ($raw -notmatch '(?i)(authorization|bearer\s|eyJ[A-Za-z0-9_-]{8,})') {
            $detail = $raw -replace '\s+', ' '
        }
    }

    $path = $RequestUri
    try {
        $path = ([Uri]$RequestUri).AbsolutePath
    } catch {
        # keep full uri without query if parse fails
    }

    $segments = @()
    if ($Method) { $segments += "method=$Method" }
    if ($null -ne $statusCode) { $segments += "status=$statusCode" }
    if ($statusDescription) { $segments += "statusText=$statusDescription" }
    if ($path) { $segments += "path=$path" }
    if ($detail) { $segments += "message=$detail" }

    if ($segments.Count -lt 1) {
        return 'HTTP request failed (no response metadata).'
    }

    return ($segments -join '; ')
}

function Get-LoadTestStaleManagedGrafanaSecretNames {
    param(
        [string[]]$PreviousSecretNames,
        [string[]]$CurrentSecretNames,
        [string]$ManagedNamePrefix = 'movie-cave-load-identities-'
    )

    $currentSet = @{}
    foreach ($name in $CurrentSecretNames) {
        $currentSet[$name] = $true
    }

    $stale = @()
    foreach ($name in $PreviousSecretNames) {
        if ($name -like ($ManagedNamePrefix + '*') -and -not $currentSet.ContainsKey($name)) {
            $stale += $name
        }
    }

    return ,$stale
}

function Invoke-LoadTestGrafanaSecretsSync {
    param(
        [Parameter(Mandatory)][string]$LoadRoot,
        [string]$GrafanaUrl = $env:GRAFANA_URL,
        [string]$GrafanaToken = $env:GRAFANA_SA_TOKEN,
        [switch]$DryRun,
        [switch]$PruneStaleManagedSecrets,
        [scriptblock]$RestMethodInvoker
    )

    if ([string]::IsNullOrWhiteSpace($GrafanaUrl) -or [string]::IsNullOrWhiteSpace($GrafanaToken)) {
        throw 'Set GRAFANA_URL and GRAFANA_SA_TOKEN in the local environment before syncing Grafana secrets.'
    }

    $manifestPath = Get-LoadTestGrafanaCloudManifestPath -LoadRoot $LoadRoot
    $manifest = Read-LoadTestGrafanaCloudManifest -ManifestPath $manifestPath
    if ($null -eq $manifest) {
        throw "Missing manifest: $manifestPath"
    }

    if ([string]$manifest.transport -ne 'grafana-secrets') {
        throw "Manifest transport must be 'grafana-secrets' (found '$($manifest.transport)'). Re-run Export-LoadTestIdentitiesForGrafanaCloud.ps1."
    }

    if (-not (Test-LoadTestManifestContainsNoSecrets -ManifestPath $manifestPath)) {
        throw 'Manifest must not contain JWT material.'
    }

    $secretNames = @($manifest.secretNames)
    $partFiles = @($manifest.secretPartFiles)
    $maxBytes = [int]$manifest.maxPartUtf8Bytes
    if ($secretNames.Count -lt 1 -or $secretNames.Count -ne $partFiles.Count) {
        throw 'Manifest secretNames and secretPartFiles must be aligned and non-empty.'
    }

    $dataDir = Join-Path $LoadRoot 'data'
    $partsToUpload = @()
    for ($i = 0; $i -lt $secretNames.Count; $i++) {
        $filePath = Join-Path $dataDir $partFiles[$i]
        if (-not (Test-Path $filePath)) {
            throw "Missing secret part file: $filePath"
        }

        $bytes = [System.IO.File]::ReadAllBytes($filePath)
        $byteCount = $bytes.Length
        if ($byteCount -gt $maxBytes) {
            throw "Secret part file exceeds maxPartUtf8Bytes ($maxBytes): $filePath ($byteCount bytes)"
        }

        $partsToUpload += @{
            SecretName = [string]$secretNames[$i]
            FilePath   = $filePath
            ByteCount  = $byteCount
            Value      = [System.Text.Encoding]::UTF8.GetString($bytes)
        }
    }

    $syncStatePath = Get-LoadTestGrafanaSecretsSyncStatePath -LoadRoot $LoadRoot
    $previousNames = @()
    if (Test-Path $syncStatePath) {
        $state = Get-Content $syncStatePath -Raw -Encoding UTF8 | ConvertFrom-Json
        $previousNames = @($state.secretNames)
    }

    $staleNames = Get-LoadTestStaleManagedGrafanaSecretNames -PreviousSecretNames $previousNames -CurrentSecretNames $secretNames
    if ($staleNames.Count -gt 0) {
        Write-Host ("Stale managed Grafana secret name(s) from prior sync: {0}" -f ($staleNames -join ', '))
        if (-not $PruneStaleManagedSecrets) {
            Write-Host 'Pass -PruneStaleManagedSecrets to delete only these stale movie-cave-load-identities-* secrets.'
        }
    }

    $namespace = Resolve-LoadTestGrafanaSecretsNamespace

    Write-Host ("Grafana secrets sync plan: {0} part(s), identityCount={1}, namespace={2}, dryRun={3}" -f $partsToUpload.Count, [int]$manifest.identityCount, $namespace, $DryRun.IsPresent)
    foreach ($part in $partsToUpload) {
        Write-Host ("  - {0} ({1} UTF-8 bytes)" -f $part.SecretName, $part.ByteCount)
    }

    if ($DryRun) {
        return @{
            DryRun      = $true
            SecretNames = $secretNames
            StaleNames  = $staleNames
        }
    }

    $baseUrl = $GrafanaUrl.TrimEnd('/')
    $headers = @{
        Authorization = "Bearer $GrafanaToken"
        Accept        = 'application/json'
    }

    if ($null -eq $RestMethodInvoker) {
        $RestMethodInvoker = {
            param($Method, $Uri, $Headers, $Body)
            $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Compress -Depth 6 } else { $null }
            $params = @{
                Method      = $Method
                Uri         = $Uri
                Headers     = $Headers
                ContentType = 'application/json'
            }
            if ($null -ne $json) {
                $params.Body = $json
            }
            return Invoke-RestMethod @params
        }
    }

    foreach ($part in $partsToUpload) {
        $body = New-LoadTestGrafanaSecureValueRequestBody -SecretName $part.SecretName -SecretValue $part.Value
        $apiUris = Get-LoadTestGrafanaSecureValuesApiUris -GrafanaBaseUrl $baseUrl -Namespace $namespace -SecretName $part.SecretName
        $exists = $false
        try {
            & $RestMethodInvoker -Method 'Get' -Uri $apiUris.Resource -Headers $headers -Body $null | Out-Null
            $exists = $true
        } catch {
            $exists = $false
        }

        $method = if ($exists) { 'Put' } else { 'Post' }
        $writeUri = if ($exists) { $apiUris.Resource } else { $apiUris.Collection }

        try {
            & $RestMethodInvoker -Method $method -Uri $writeUri -Headers $headers -Body $body | Out-Null
        } catch {
            $httpSummary = Format-LoadTestGrafanaSyncHttpError -Method $method -RequestUri $writeUri -ErrorRecord $_
            throw "Grafana secret sync failed for $($part.SecretName). $httpSummary Check GRAFANA_URL, GRAFANA_STACK_ID/GRAFANA_SECRETS_NAMESPACE, and GRAFANA_SA_TOKEN permissions (secret.securevalues:create/write/read)."
        }
    }

    if ($PruneStaleManagedSecrets -and $staleNames.Count -gt 0) {
        foreach ($stale in $staleNames) {
            $deleteUris = Get-LoadTestGrafanaSecureValuesApiUris -GrafanaBaseUrl $baseUrl -Namespace $namespace -SecretName $stale
            try {
                & $RestMethodInvoker -Method 'Delete' -Uri $deleteUris.Resource -Headers $headers -Body $null | Out-Null
                Write-Host "Deleted stale managed secret: $stale"
            } catch {
                $httpSummary = Format-LoadTestGrafanaSyncHttpError -Method 'Delete' -RequestUri $deleteUris.Resource -ErrorRecord $_
                throw "Failed to delete stale managed secret: $stale. $httpSummary"
            }
        }
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $stateJson = (@{
            schemaVersion  = 1
            secretNames    = $secretNames
            secretPartCount = $secretNames.Count
            identityCount  = [int]$manifest.identityCount
            syncedAtUtc    = (Get-Date).ToUniversalTime().ToString('o')
        } | ConvertTo-Json -Compress -Depth 5)
    [System.IO.File]::WriteAllText($syncStatePath, $stateJson, $utf8NoBom)

    return @{
        DryRun      = $false
        SecretNames = $secretNames
        StaleNames  = $staleNames
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
        if ($CloudTransport -eq 'grafana-secrets') {
            $lines += 'Cloud identity transport:     Grafana Secrets'
            if ($CloudManifestShardCount -gt 0) {
                $lines += "Cloud identity secret parts:  $CloudManifestShardCount"
            }
        } elseif ($CloudTransport -eq 'sharded' -and $CloudManifestShardCount -gt 0) {
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
    'Assert-LoadTestContentDatasetSupported',
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
    'Split-LoadTestIdentitiesJsonParts',
    'Split-LoadTestIdentitiesForGrafanaSecrets',
    'Get-LoadTestGrafanaSecretPartName',
    'Get-LoadTestGrafanaSecretPartFileName',
    'Get-LoadTestUtf8ByteCount',
    'New-LoadTestGrafanaSecureValueRequestBody',
    'Resolve-LoadTestGrafanaSecretsNamespace',
    'Get-LoadTestGrafanaSecureValuesApiUris',
    'Format-LoadTestGrafanaSyncHttpError',
    'Get-LoadTestStaleManagedGrafanaSecretNames',
    'Invoke-LoadTestGrafanaSecretsSync',
    'Get-LoadTestGrafanaSecretsSyncStatePath',
    'Read-LoadTestGrafanaCloudManifest',
    'Test-LoadTestManifestContainsNoSecrets',
    'Assert-LoadTestCloudAuth',
    'Get-LoadTestK6PathEnvValues',
    'Build-LoadTestK6EnvArgs',
    'Format-LoadTestCloudPreflight'
)
