function osRestorePoint {
 <#
    .SYNOPSIS
        Creates a restore point on Windows 10/11
    .DESCRIPTION
        Detects the Windows version and uses the right order of operations
    #>
    [CmdletBinding()]
    param()

    try {
        # Detect the Windows version
        $winVersion = getWindowsVersion

        Write-Host "Creating a restore point for $winVersion..." -ForegroundColor Cyan
        Write-Host ""

        # Number of points BEFORE
        $pointsBefore = @(Get-ComputerRestorePoint -ErrorAction SilentlyContinue).Count
        Write-Host "  Points before: $pointsBefore" -ForegroundColor Gray

        # 1. Registry setting (same for all versions)
        Write-Host "  -> Registry setting..." -ForegroundColor Gray
        New-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore" `
                        -Name "SystemRestorePointCreationFrequency" `
                        -Value 0 `
                        -PropertyType DWord `
                        -Force `
                        -ErrorAction SilentlyContinue | Out-Null

        # 2. Enable System Protection (same for all versions)
        Write-Host "  -> Enabling System Protection..." -ForegroundColor Gray
        Enable-ComputerRestore -Drive "C:\" -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2

        # 3. The ORDER differs by Windows version
        if ($winVersion -eq "Windows 11") {
            # Windows 11: create the point FIRST, THEN resize the storage
            Write-Host "  -> [Win11] Creating the initial point..." -ForegroundColor Gray

            $timestamp = Get-Date -Format "dd-MM-yyyy HH:mm:ss"
            Checkpoint-Computer -Description "AS Shield restore point $timestamp" `
                               -RestorePointType "MODIFY_SETTINGS" `
                               -ErrorAction Stop

            Write-Host "  [OK] System restore point created" -ForegroundColor Green
            Start-Sleep -Seconds 2

            # NOW resize the storage
            Write-Host "  -> [Win11] Setting the storage size (10GB)..." -ForegroundColor Gray
            $vssResult = Start-Process -FilePath "vssadmin.exe" `
                                       -ArgumentList "resize shadowstorage /for=C: /on=C: /maxsize=10GB" `
                                       -NoNewWindow `
                                       -Wait `
                                       -PassThru
        }
        else {
            # Windows 10: resize the storage FIRST, THEN create the point
            Write-Host "  -> [Win10] Setting the storage size (10GB)..." -ForegroundColor Gray
            $vssResult = Start-Process -FilePath "vssadmin.exe" `
                                       -ArgumentList "resize shadowstorage /for=C: /on=C: /maxsize=10GB" `
                                       -NoNewWindow `
                                       -Wait `
                                       -PassThru

            if ($vssResult.ExitCode -eq 0) {
                Write-Host "  [OK] Storage size set" -ForegroundColor Green
            }

            Start-Sleep -Seconds 2

            # NOW create the point
            Write-Host "  -> [Win10] Creating the restore point..." -ForegroundColor Gray
            $timestamp = Get-Date -Format "dd-MM-yyyy HH:mm:ss"
            Checkpoint-Computer -Description "AS Shield restore point $timestamp" `
                               -RestorePointType "MODIFY_SETTINGS" `
                               -ErrorAction Stop
        }

        # 4. Wait for the point to be registered (same for all versions)
        Write-Host "  -> Waiting for the point to be registered..." -ForegroundColor Gray
        Start-Sleep -Seconds 5

        # 5. Check the result (same for all versions)
        $pointsAfter = @(Get-ComputerRestorePoint -ErrorAction SilentlyContinue).Count

        Write-Host "`n  Result:" -ForegroundColor Yellow
        Write-Host "    Before: $pointsBefore points" -ForegroundColor Gray
        Write-Host "    After: $pointsAfter points" -ForegroundColor Gray

        if ($pointsAfter -gt $pointsBefore) {
            Write-Host "`n  [OK] SUCCESS! Restore point created!" -ForegroundColor Green

            # Show the created point
            $lastPoint = Get-ComputerRestorePoint |
                        Sort-Object SequenceNumber -Descending |
                        Select-Object -First 1

            $creationDate = [System.Management.ManagementDateTimeConverter]::ToDateTime($lastPoint.CreationTime)

            Write-Host "`n  Created point details:" -ForegroundColor Cyan
            Write-Host "    Number: $($lastPoint.SequenceNumber)" -ForegroundColor Gray
            Write-Host "    Description: $($lastPoint.Description)" -ForegroundColor Gray
            Write-Host "    Date: $($creationDate.ToString('dd.MM.yyyy HH:mm:ss'))" -ForegroundColor Gray
        }
        elseif ($pointsAfter -eq $pointsBefore -and $pointsAfter -gt 0) {
            Write-Host "`n  [!] The point was not created" -ForegroundColor Yellow
            Write-Host "    Windows may refuse to create a point if the last one is too recent" -ForegroundColor Gray
        }
        else {
            Write-Host "`n  [!] The point was created but is not visible yet" -ForegroundColor Yellow
            Write-Host "    Wait 1-2 minutes" -ForegroundColor Gray
        }

        # 6. Restore the registry setting (same for all versions)
        Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore" `
                        -Name "SystemRestorePointCreationFrequency" `
                        -Value 1440 `
                        -ErrorAction SilentlyContinue

        Write-Host ""

    }
    catch {
        Write-Host "`n[X] ERROR while creating the restore point:" -ForegroundColor Red
        Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
    }
}
































<# function restorePoint_Win_10 {
# Restore point creation frequency (by default 1 point every 24 hours). Setting it to "0" removes the limit
# Add the setting to the registry
New-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore" -Name "SystemRestorePointCreationFrequency" -Value 0 -PropertyType DWord -Force

# Enable restore on drive C:
#---------------------------------------------------------
Enable-ComputerRestore -Drive "C:\"

# Set the restore storage size to 10GB on drive C:
# ---------------------------------------------------------
Start-Process -FilePath "vssadmin.exe" -ArgumentList "resize shadowstorage /for=C: /on=C: /maxsize=10GB" -NoNewWindow -Wait

# Create the restore point
# -----------------------------------------------------------------------------------------------------------------------
$timestamp = Get-Date -Format "dd-MM-yyyy HH:mm:ss"
Checkpoint-Computer -Description "AS Shield restore point $timestamp" -RestorePointType "MODIFY_SETTINGS"

# Set the restore point frequency back to 1 point every 24 hours (value 1440)
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore" -Name "SystemRestorePointCreationFrequency" -Value 1440

Write-Host "┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐"
Write-Host "│######## System protection enabled. Restore storage set to 10GB and a restore point created     ########│"
Write-Host "└──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘"
} #>

<# function restorePoint_Win_11 {
# Add the setting to the registry
New-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore" -Name "SystemRestorePointCreationFrequency" -Value 0 -PropertyType DWord -Force

# Enable restore on drive C:
Enable-ComputerRestore -Drive "C:\"

# An initial restore point MUST be created first
$timestamp = Get-Date -Format "dd-MM-yyyy HH:mm:ss"
Checkpoint-Computer -Description "Initial point $timestamp" -RestorePointType "MODIFY_SETTINGS"

# Resize the storage only after the first point exists
Start-Process -FilePath "vssadmin.exe" -ArgumentList "resize shadowstorage /for=C: /on=C: /maxsize=10GB" -NoNewWindow -Wait

# Restore the restore point frequency setting
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore" -Name "SystemRestorePointCreationFrequency" -Value 1440

Write-Host "┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐"
Write-Host "│######## System protection enabled. Restore storage set to 10GB and a restore point created     ########│"
Write-Host "└──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘"
} #>
