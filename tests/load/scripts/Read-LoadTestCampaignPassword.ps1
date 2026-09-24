function Get-LoadTestCampaignPassword {
    param(
        [string]$Prompt = "Campaign password"
    )

    $securePassword = Read-Host $Prompt -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
    try {
        $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    } finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        $securePassword.Dispose()
    }

    if ([string]::IsNullOrWhiteSpace($plain)) {
        throw "Password cannot be empty."
    }

    return $plain
}
