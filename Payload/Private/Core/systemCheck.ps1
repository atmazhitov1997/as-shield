function isAdmin {
    <#
    .SYNOPSIS
        Returns $true when the current process runs with a High integrity level (elevated administrator).
    #>
    [CmdletBinding()]
    param()

    $highIntegritySid = "S-1-16-12288"
    $whoami = whoami /all | Select-String $highIntegritySid
    return ($null -ne $whoami)
}

function getWindowsVersion {
    <#
    .SYNOPSIS
        Detects the Windows version.
    .OUTPUTS
        String - "Windows 10", "Windows 11" or "Unknown"
    #>
    [CmdletBinding()]
    param()

    try {
        $build = [int](Get-CimInstance Win32_OperatingSystem).BuildNumber
        if ($build -ge 22000) { return "Windows 11" }
        elseif ($build -ge 10240) { return "Windows 10" }
        else { return "Unknown" }
    }
    catch {
        Write-Error "Failed to detect the Windows version: $_"
        return "Unknown"
    }
}
