function removeAdditionalPrograms {
    <#
    .SYNOPSIS
        Removes OneDrive and Skype
    #>
    [CmdletBinding()]
    param()

    # ─── Remove OneDrive ──────────────────────────
function removeOneDrive {
    Write-Host ""
    Write-Host "  Removing OneDrive..." -ForegroundColor Gray

    # 1. Stop the process
    Stop-Process -Name "OneDrive" -Force -ErrorAction SilentlyContinue

    # 2. Uninstall with the setup program
    $oneDrivePaths = @(
        "$env:SystemRoot\System32\OneDriveSetup.exe",
        "$env:SystemRoot\SysWOW64\OneDriveSetup.exe",
        "$env:LOCALAPPDATA\Microsoft\OneDrive\OneDriveSetup.exe"
    )

    $uninstalled = $false
    foreach ($path in $oneDrivePaths) {
        if (Test-Path $path) {
            Write-Host "  Setup program found: $path" -ForegroundColor DarkGray
            & $path /uninstall 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  [OK] [OneDrive] Removed with: $path" -ForegroundColor Green
                $uninstalled = $true
            }
        }
    }

    if (-not $uninstalled) {
        Write-Host "  [!] [OneDrive] Not found or already removed" -ForegroundColor Yellow
    }

    # 4. Remove leftover folders
    $oneDriveFolders = @(
        "$env:USERPROFILE\OneDrive",
        "$env:LOCALAPPDATA\Microsoft\OneDrive",
        "$env:PROGRAMDATA\Microsoft OneDrive",
        "$env:SystemRoot\ServiceProfiles\LocalService\AppData\Roaming\Microsoft\Windows\Recent\AutomaticDestinations"
    )

    foreach ($folder in $oneDriveFolders) {
        if (Test-Path $folder) {
            Remove-Item $folder -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "  [OK] [OneDrive] Folder removed: $folder" -ForegroundColor Green
        }
    }

    # 5. Check and remove from the system folders
    $systemOneDrive = @(
        "$env:SystemRoot\System32\OneDriveSetup.exe",
        "$env:SystemRoot\SysWOW64\OneDriveSetup.exe"
    )

    foreach ($sysFile in $systemOneDrive) {
        if (Test-Path $sysFile) {
            & takeown /F "$sysFile" /A 2>&1 | Out-Null
            & icacls "$sysFile" /grant "Administrators:F" 2>&1 | Out-Null
            Remove-Item $sysFile -Force -ErrorAction SilentlyContinue
            Write-Host "  [OK] [OneDrive] System file removed: $sysFile" -ForegroundColor Green
        }
    }

    # 6. Block reinstallation through the registry
    $regPaths = @(
        "HKLM:\SOFTWARE\Policies\Microsoft\Windows\OneDrive",
        "HKCU:\SOFTWARE\Policies\Microsoft\Windows\OneDrive"
    )

    foreach ($reg in $regPaths) {
        if (-not (Test-Path $reg)) {
            New-Item -Path $reg -Force | Out-Null
        }
        Set-ItemProperty -Path $reg -Name "DisableFileSyncNGSC" -Value 1 -Force
    }
    Write-Host "  [OK] [OneDrive] Reinstallation blocked through the registry" -ForegroundColor Green
}

# ─── Remove Skype ─────────────────────────────
function removeSkype {
    Write-Host ""
    Write-Host "  Removing Skype..." -ForegroundColor Gray

    $removed = $false

    # 1. Stop the process
    Stop-Process -Name "Skype" -Force -ErrorAction SilentlyContinue

    # 2. Remove through AppX (filtering by name is faster than enumerating every package)
    $skypeApps = @(Get-AppxPackage -AllUsers -Name "*Skype*" -ErrorAction SilentlyContinue)

    foreach ($app in $skypeApps) {
        Remove-AppxPackage -AllUsers -Package $app.PackageFullName -ErrorAction SilentlyContinue
        Write-Host "  [OK] [Skype] AppX package removed: $($app.Name)" -ForegroundColor Green
        $removed = $true
    }

    # 4. Check provisioned packages only if an AppX package was found
    if ($skypeApps) {
        $provisionedSkype = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | 
            Where-Object { $_.DisplayName -match "Skype" }

        foreach ($pkg in $provisionedSkype) {
            Remove-AppxProvisionedPackage -Online -PackageName $pkg.PackageName -ErrorAction SilentlyContinue
            Write-Host "  [OK] [Skype] Provisioned package removed: $($pkg.DisplayName)" -ForegroundColor Green
            $removed = $true
        }
    }

    if (-not $removed) {
        Write-Host "  [!] [Skype] Not found or already removed" -ForegroundColor Yellow
        return
    }

    # 5. Remove leftover folders
    $skypeFolders = @(
        "$env:APPDATA\Skype",
        "$env:LOCALAPPDATA\Packages\Microsoft.SkypeApp_kzf8qxf38zg5c",
        "$env:PROGRAMFILES\Skype",
        "${env:ProgramFiles(x86)}\Skype"
    )

    foreach ($folder in $skypeFolders) {
        if (Test-Path $folder) {
            Remove-Item $folder -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "  [OK] [Skype] Folder removed: $folder" -ForegroundColor Green
        }
    }

    # 6. Remove registry keys
    $skypeRegPaths = @(
        "HKCU:\SOFTWARE\Skype",
        "HKLM:\SOFTWARE\Skype",
        "HKLM:\SOFTWARE\WOW6432Node\Skype"
    )

    foreach ($reg in $skypeRegPaths) {
        if (Test-Path $reg) {
            Remove-Item $reg -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "  [OK] [Skype] Registry key removed: $reg" -ForegroundColor Green
        }
    }
}

# ─── Main removal block ───────────────────────
Write-Host ""
Write-Host "═══════════════════════════════════════════════" -ForegroundColor White
Write-Host "   Removing unwanted software..." -ForegroundColor White
Write-Host "═══════════════════════════════════════════════" -ForegroundColor White

if (Get-Command Send-Progress -ErrorAction SilentlyContinue) { Send-Progress -2 "Removing unwanted software..." }
# Is OneDrive present?
$oneDriveExists = (
    (Get-Process "OneDrive" -ErrorAction SilentlyContinue) -or
    (Test-Path "$env:LOCALAPPDATA\Microsoft\OneDrive\OneDrive.exe") -or
    (Test-Path "$env:SystemRoot\System32\OneDriveSetup.exe")
)

if ($oneDriveExists) {
    removeOneDrive
} else {
    Write-Host "  [!] [OneDrive] Not present on the system" -ForegroundColor Yellow
}

# Is Skype present?
$skypeExists = (
    (Get-Process "Skype" -ErrorAction SilentlyContinue) -or
    (@(Get-AppxPackage -AllUsers -Name "*Skype*" -ErrorAction SilentlyContinue).Count -gt 0) -or
    (Test-Path "$env:PROGRAMFILES\Skype") -or
    (Test-Path "${env:ProgramFiles(x86)}\Skype")
)

if ($skypeExists) {
    removeSkype
} else {
    Write-Host "  [!] [Skype] Not present on the system" -ForegroundColor Yellow
}

if (Get-Command Hide-Progress -ErrorAction SilentlyContinue) { Hide-Progress }



}
