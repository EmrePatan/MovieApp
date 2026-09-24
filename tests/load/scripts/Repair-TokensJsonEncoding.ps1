param(
    [string]$TokensPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path "data\tokens.json")
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $TokensPath)) {
    throw "tokens.json not found: $TokensPath"
}

$bytes = [System.IO.File]::ReadAllBytes($TokensPath)
if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
    $text = [System.Text.Encoding]::UTF8.GetString($bytes, 3, $bytes.Length - 3)
} else {
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
}

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($TokensPath, $text, $utf8NoBom)
Write-Host "Rewrote tokens.json as UTF-8 without BOM (content not displayed)."
