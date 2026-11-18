function Remove-Win11TerminalAndPowerShell {
    <#
    .SYNOPSIS
        Removes Windows Terminal and PowerShell from the Win+X menu
    #>
    [CmdletBinding()]
    param()

      function removeTerminalMenu {
        Write-Host "Removing Windows Terminal + PowerShell from the context menu (Win+X)" -ForegroundColor Cyan
        Write-Host "Choose an option:" -ForegroundColor Cyan
        Write-Host "1. Keep Windows Terminal + PowerShell for the current user only, $($env:USERNAME)"
        Write-Host "2. Remove for everyone (including the current and future users)"
        Write-Host "3. Skip the removal"
    }

    function Remove-WinXShortcuts {
        param (
            [string]$ExcludeUser = ""
        )

        Write-Host "Removing the PowerShell and Windows Terminal shortcuts from the Win+X menu..." -ForegroundColor Yellow
        $patterns = @("*PowerShell*.lnk", "*Windows Terminal*.lnk")

        $usersFolders = @()
        foreach ($user in Get-ChildItem 'C:\Users' -Directory) {
            $winXPath = "$($user.FullName)\AppData\Local\Microsoft\Windows\WinX"
            if ($user.Name -ne "Default" -and $user.Name -ne "Public" -and $user.Name -ne $ExcludeUser -and (Test-Path $winXPath)) {
                $usersFolders += $winXPath
            }
        }

        foreach ($path in $usersFolders) {
            foreach ($pattern in $patterns) {
                Get-ChildItem -Path $path -Recurse -Include $pattern -ErrorAction SilentlyContinue |
                    Remove-Item -Force -ErrorAction SilentlyContinue
            }
        }

        # Also from the Default profile
        $defaultPath = "C:\Users\Default\AppData\Local\Microsoft\Windows\WinX"
        if (Test-Path $defaultPath) {
            foreach ($pattern in $patterns) {
                Get-ChildItem -Path $defaultPath -Recurse -Include $pattern -ErrorAction SilentlyContinue |
                    Remove-Item -Force -ErrorAction SilentlyContinue
            }
        }

        Write-Host "PowerShell/Terminal shortcuts removed from Win+X" -ForegroundColor Green
    }

    removeTerminalMenu
    $choice = Read-Host "Enter the option number (1-3)"
    $currentUser = $env:USERNAME

    switch ($choice) {
      '1' {
            $currentUser = $env:USERNAME
            Write-Host "Removing Windows Terminal and PowerShell for everyone except the current user: $currentUser" -ForegroundColor Yellow

            # Remove the provisioned package (for future users)
            Get-AppxProvisionedPackage -Online | Where-Object DisplayName -EQ "Microsoft.WindowsTerminal" |
                Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue

            # Other users (except the current one)
            $users = @()
            foreach ($user in Get-ChildItem "C:\Users" -Directory) {
                $packagePath = "$($user.FullName)\AppData\Local\Packages"
                if ($user.Name -ne "Default" -and $user.Name -ne "Public" -and $user.Name -ne $currentUser -and (Test-Path $packagePath)) {
                    $users += $user
                }
            }

            foreach ($user in $users) {
                $terminalPath = "$($user.FullName)\AppData\Local\Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe"
                if (Test-Path $terminalPath) {
                    try {
                        Remove-Item -Path $terminalPath -Recurse -Force -ErrorAction SilentlyContinue
                        Write-Host "Terminal removed for: $($user.Name)" -ForegroundColor Green
                    } catch {
                        Write-Warning "Could not remove for $($user.Name): $_"
                    }
                }
            }

            # Remove the Win+X shortcuts (except for the current user)
            Remove-WinXShortcuts -ExcludeUser $currentUser
         }

        '2' {
            Write-Host "Full removal of Windows Terminal and PowerShell for all current and future users" -ForegroundColor Yellow

            # Current user
            Get-AppxPackage -Name "Microsoft.WindowsTerminal" | Remove-AppxPackage -ErrorAction SilentlyContinue

            # Provisioned Terminal
            Get-AppxProvisionedPackage -Online | Where-Object DisplayName -EQ "Microsoft.WindowsTerminal" |
                Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue

            # Other users
            $users = @()
            foreach ($user in Get-ChildItem "C:\Users" -Directory) {
                $packagePath = "$($user.FullName)\AppData\Local\Packages"
                if ($user.Name -ne "Default" -and $user.Name -ne "Public" -and (Test-Path $packagePath)) {
                    $users += $user
                }
            }

            foreach ($user in $users) {
                $terminalPath = "$($user.FullName)\AppData\Local\Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe"
                if (Test-Path $terminalPath) {
                    try {
                        Remove-Item -Path $terminalPath -Recurse -Force -ErrorAction SilentlyContinue
                        Write-Host "Terminal removed for: $($user.Name)" -ForegroundColor Green
                    } catch {
                        Write-Warning "Could not remove for $($user.Name): $_"
                    }
                }
            }

            Remove-WinXShortcuts
        }

        '3' {
            Write-Host "Removal skipped by the user." -ForegroundColor Gray
        }

        default {
            Write-Warning "Invalid choice. Choose one of the options"
            Write-Host ""
            Remove-Win11TerminalAndPowerShell
            }

    }
}

