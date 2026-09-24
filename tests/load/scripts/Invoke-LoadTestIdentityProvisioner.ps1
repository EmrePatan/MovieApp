param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("provision", "cleanup", "verify-password", "token-requirements")]
    [string]$Command,

    [int]$SampleSize = 3,

    [string]$ConnectionString = $env:LOAD_TEST_PG_CONNECTION,
    [int]$Count = 50,
    [string]$EmailDomain = "loadtest.invalid",
    [string]$ManifestPath,
    [switch]$ConfirmProduction,
    [switch]$ConfirmDelete
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$project = Join-Path $repoRoot "tests\load\tools\LoadTestIdentityProvisioner\LoadTestIdentityProvisioner.csproj"

$argsList = @("run", "--project", $project, "--", $Command)
if ($ConnectionString) {
    $argsList += @("--connection", $ConnectionString)
}

switch ($Command) {
    "verify-password" {
        $argsList += @("--sample", "$SampleSize")
    }
    "token-requirements" {
        $argsList += @("--count", "$Count")
    }
    default {
        $argsList += @("--count", "$Count", "--email-domain", $EmailDomain)
    }
}

if ($ManifestPath) {
    $argsList += @("--manifest", $ManifestPath)
}

switch ($Command) {
    "provision" {
        if ($ConfirmProduction) {
            $argsList += "--confirm-production"
        }
    }
    "cleanup" {
        if ($ConfirmDelete) {
            $argsList += "--confirm-delete"
        }
    }
}

Push-Location (Join-Path $repoRoot "tests\load")
try {
    & dotnet @argsList
    exit $LASTEXITCODE
} finally {
    Pop-Location
}
