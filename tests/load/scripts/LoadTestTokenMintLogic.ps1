# Testable helpers for Mint-LoadTestTokens.ps1 (no HTTP).

function Get-LoadTestJwtExpiryUtc {
    param([string]$Jwt)

    if ([string]::IsNullOrWhiteSpace($Jwt)) {
        return $null
    }

    $parts = $Jwt.Split('.')
    if ($parts.Count -lt 2) {
        return $null
    }

    $body = $parts[1]
    $pad = '=' * ((4 - ($body.Length % 4)) % 4)
    try {
        $bytes = [Convert]::FromBase64String(($body + $pad).Replace('-', '+').Replace('_', '/'))
    } catch {
        return $null
    }

    try {
        $json = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
    } catch {
        return $null
    }

    if ($null -eq $json.exp) {
        return $null
    }

    return [DateTimeOffset]::FromUnixTimeSeconds([int64]$json.exp).UtcDateTime
}

function Test-LoadTestTokenReusable {
    param(
        [string]$ExpectedHarnessId,
        [object]$ExistingIdentity,
        [datetime]$RequiredValidityDeadlineUtc
    )

    if ($null -eq $ExistingIdentity) {
        return $false
    }

    if ([string]::IsNullOrWhiteSpace($ExistingIdentity.id) -or $ExistingIdentity.id -ne $ExpectedHarnessId) {
        return $false
    }

    if ([string]::IsNullOrWhiteSpace($ExistingIdentity.bearerToken)) {
        return $false
    }

    $exp = Get-LoadTestJwtExpiryUtc -Jwt $ExistingIdentity.bearerToken
    if (-not $exp) {
        return $false
    }

    $expSeconds = [DateTimeOffset]$exp | ForEach-Object { $_.ToUnixTimeSeconds() }
    $deadlineSeconds = [DateTimeOffset]$RequiredValidityDeadlineUtc | ForEach-Object { $_.ToUnixTimeSeconds() }
    return $expSeconds -ge $deadlineSeconds
}

function Get-LoadTestTokensById {
    param([object[]]$Identities)

    $map = @{}
    if ($null -eq $Identities) {
        return $map
    }

    foreach ($row in $Identities) {
        if ($null -eq $row -or [string]::IsNullOrWhiteSpace($row.id)) {
            continue
        }

        $map[$row.id] = $row
    }

    return $map
}

function Get-LoadTestMintRefreshPlan {
    param(
        [object[]]$ManifestEntries,
        [hashtable]$ExistingById,
        [datetime]$RequiredValidityDeadlineUtc,
        [bool]$ForceFull
    )

    $toRefresh = New-Object System.Collections.Generic.List[object]
    $preserved = @{}
    $reusableCount = 0

    foreach ($entry in $ManifestEntries) {
        $harnessId = $entry.harnessId
        $existing = $null
        if ($ExistingById.ContainsKey($harnessId)) {
            $existing = $ExistingById[$harnessId]
        }

        if ($ForceFull -or -not (Test-LoadTestTokenReusable -ExpectedHarnessId $harnessId -ExistingIdentity $existing -RequiredValidityDeadlineUtc $RequiredValidityDeadlineUtc)) {
            $toRefresh.Add($entry)
        } else {
            $preserved[$harnessId] = $existing
            $reusableCount++
        }
    }

    $refreshArray = @()
    foreach ($item in $toRefresh) {
        $refreshArray += $item
    }

    return [pscustomobject]@{
        ToRefresh     = $refreshArray
        PreservedById = $preserved
        ReusableCount = [int]$reusableCount
        RefreshCount  = [int]$refreshArray.Length
        IdentityCount = [int]$ManifestEntries.Count
    }
}

function Build-LoadTestTokensIdentityList {
    param(
        [object[]]$ManifestEntries,
        [hashtable]$TokenByHarnessId
    )

    $list = New-Object System.Collections.Generic.List[object]
    foreach ($entry in $ManifestEntries) {
        $id = $entry.harnessId
        if (-not $TokenByHarnessId.ContainsKey($id)) {
            throw "Mint incomplete: missing token for $id."
        }

        $row = $TokenByHarnessId[$id]
        $list.Add([ordered]@{
                id           = $row.id
                bearerToken  = $row.bearerToken
                expiresAtUtc = $row.expiresAtUtc
            })
    }

    return $list
}

function New-LoadTestTokenRowFromLogin {
    param(
        [string]$HarnessId,
        [object]$LoginResponse
    )

    $expiresAtUtc = $null
    if ($LoginResponse.expiresAt) {
        $expiresAtUtc = ([DateTime]$LoginResponse.expiresAt).ToUniversalTime().ToString('o')
    }

    return [ordered]@{
        id           = $HarnessId
        bearerToken  = $LoginResponse.accessToken
        expiresAtUtc = $expiresAtUtc
    }
}

function Get-LoadTestMintEstimatedRefreshDuration {
    param(
        [int]$RefreshCount,
        [int]$ThrottleSeconds
    )

    if ($RefreshCount -le 0) {
        return [TimeSpan]::Zero
    }

    $sleepSeconds = 0
    if ($ThrottleSeconds -gt 0 -and $RefreshCount -gt 1) {
        $sleepSeconds = ($RefreshCount - 1) * $ThrottleSeconds
    }

    return [TimeSpan]::FromSeconds($sleepSeconds)
}

function Write-LoadTestTokensJsonAtomic {
    param(
        [string]$OutputPath,
        [object[]]$Identities,
        [int]$ExpectedCount
    )

    if ($Identities.Count -ne $ExpectedCount) {
        throw "Mint incomplete: $($Identities.Count)/$ExpectedCount. tokens.json was not updated."
    }

    $payload = @{ identities = $Identities }
    $json = $payload | ConvertTo-Json -Depth 5
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false

    $dir = Split-Path $OutputPath -Parent
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $tempPath = Join-Path $dir ('tokens.json.tmp.' + [Guid]::NewGuid().ToString('N'))
    [System.IO.File]::WriteAllText($tempPath, $json, $utf8NoBom)
    Move-Item -Path $tempPath -Destination $OutputPath -Force
}
