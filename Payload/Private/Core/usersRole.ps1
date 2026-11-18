#Requires -RunAsAdministrator

function UsersRole {

# ─────────────────────────────────────────────
#  Assigns the SecAdmin and SysAdmin roles
# ─────────────────────────────────────────────

$rolePrivileges = @{
    SecAdmin = @("SeLoadDriverPrivilege", "SeSecurityPrivilege")
    SysAdmin = @("SeManageVolumePrivilege", "SeRestorePrivilege")
}

# $PSScriptRoot - the correct path to the script folder
$BackupDir = "$PSScriptRoot\Backup\$env:COMPUTERNAME"
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null

# ─── Group members through WMI ────────────────
function Get-GroupMembers {
    param([string]$GroupName)

    $members = Get-CimInstance -Query "
        SELECT * FROM Win32_GroupUser 
        WHERE GroupComponent=`"Win32_Group.Domain='$env:COMPUTERNAME',Name='$GroupName'`"
    "

    return $members | ForEach-Object {
        if ($_.PartComponent -match 'Name = "(.+?)"') {
            $matches[1]
        }
    }
}

# ─── Users with their groups ──────────────────
function Get-LocalUsersWithGroups {
    $users = Get-LocalUser | Where-Object { $_.Enabled -eq $true }
    $result = @()

    $adminGroup = Get-LocalGroup | Where-Object { $_.SID -eq "S-1-5-32-544" }
    $adminMembers = Get-GroupMembers -GroupName $adminGroup.Name

    Write-Host "  Members of the group '$($adminGroup.Name)':" -ForegroundColor Yellow
    $adminMembers | ForEach-Object { Write-Host "    → $_" -ForegroundColor Blue }

    foreach ($user in $users) {
        $isAdmin = $adminMembers | Where-Object { $_.ToLower() -eq $user.Name.ToLower() }
        if (-not $isAdmin) { continue }

        $userGroups = @()
        foreach ($group in Get-LocalGroup) {
            try {
                $members = Get-GroupMembers -GroupName $group.Name
                if ($members | Where-Object { $_.ToLower() -eq $user.Name.ToLower() }) {
                    $userGroups += $group.Name
                }
            } catch { }
        }

        $result += [PSCustomObject]@{
            Name   = $user.Name
            Groups = if ($userGroups) { $userGroups -join ", " } else { "—" }
        }
    }
    return $result
}

# ─── secedit call ─────────────────────────────
function Invoke-Secedit {
    param([string]$Arguments)

    $logFile = "$BackupDir\secedit_log_$(Get-Date -Format 'HHmmss').txt"

    $proc = Start-Process -FilePath "cmd.exe" `
                          -ArgumentList "/c secedit.exe $Arguments > `"$logFile`" 2>&1" `
                          -Wait `
                          -PassThru `
                          -NoNewWindow

    Remove-Item $logFile -ErrorAction SilentlyContinue
    return $proc.ExitCode
}

# ─── User list output ─────────────────────────
function Show-UserList {
    param([array]$Users, [string]$ExcludeUser = "")

    Write-Host ""
    $index = 1
    $displayList = @()

    foreach ($user in $Users) {
        if ($user.Name -eq $ExcludeUser) { continue }
        Write-Host "  [$index] $($user.Name)"
        Write-Host "       Groups: $($user.Groups)"
        $displayList += $user
        $index++
    }
    Write-Host ""
    return $displayList
}

# ─── User selection ───────────────────────────
function Select-User {
    param([array]$DisplayList, [string]$Role)

    while ($true) {
        $inputUser = Read-Host "Enter the user number for the role [$Role]"
        if ($inputUser -match '^\d+$') {
            $num = [int]$inputUser
            if ($num -ge 1 -and $num -le $DisplayList.Count) {
                return $DisplayList[$num - 1]
            }
        }
        Write-Host "Invalid choice. Enter a number from 1 to $($DisplayList.Count)" -ForegroundColor Yellow
    }
}

# ─── SID lookup ───────────────────────────────
function Get-UserSID {
    param([string]$UserName)
    $obj = New-Object System.Security.Principal.NTAccount($UserName)
    return $obj.Translate([System.Security.Principal.SecurityIdentifier]).Value
}

# ─── Apply the privileges ─────────────────────
function applyPrivileges {
    param(
        [string]$UserName,
        [string[]]$Privileges,
        [string]$Role,
        [string]$BackupDir
    )

    $sid       = Get-UserSID -UserName $UserName
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $BackupInf = "$BackupDir\${Role}_backup_${timestamp}.inf"
    $ApplyInf  = "$BackupDir\${Role}_apply_${timestamp}.inf"
    $CheckInf  = "$BackupDir\check_${Role}_${timestamp}.inf"
    $TempDb    = "$BackupDir\${Role}_temp_${timestamp}.sdb"

    Invoke-Secedit "/export /cfg `"$BackupInf`" /areas USER_RIGHTS" | Out-Null

    $privLines = $Privileges | ForEach-Object { "$_ = *${sid}" }
    $infContent = @"
[Unicode]
Unicode=yes
[Version]
signature="`$CHICAGO`$"
Revision=1
[Privilege Rights]
$($privLines -join "`r`n")
"@
    $infContent | Set-Content $ApplyInf -Encoding Unicode

    Invoke-Secedit "/configure /db `"$TempDb`" /cfg `"$ApplyInf`" /areas USER_RIGHTS /overwrite /quiet" | Out-Null
    Remove-Item $TempDb -ErrorAction SilentlyContinue

    Invoke-Secedit "/export /cfg `"$CheckInf`" /areas USER_RIGHTS" | Out-Null
    $check = Get-Content $CheckInf

    foreach ($priv in $Privileges) {
        $line = $check | Where-Object { $_ -match "^${priv}" }
        if ($line -match [regex]::Escape($sid) -or $line -match [regex]::Escape($UserName)) {
            Write-Host "  ✔ $priv" -ForegroundColor Green
        } else {
            Write-Host "  [X] $priv - not applied!" -ForegroundColor Red
        }
    }

    Remove-Item $CheckInf -ErrorAction SilentlyContinue
}

# ─── File security settings ───────────────────
function setFileSecurity {
    param(
        [string]$FilePath,
        [string]$SecAdminName
    )

    Write-Host ""
    Write-Host "  File: $FilePath" -ForegroundColor Gray

    if (-not (Test-Path $FilePath)) {
        Write-Host "  File not found: $FilePath" -ForegroundColor Red
        return
    }

    # 1. Change the owner to Administrators
    $result = & takeown /F "$FilePath" /A 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  [OK] Owner changed to: Administrators" -ForegroundColor Green
    } else {
        Write-Host "  [X] Failed to change the owner: $result" -ForegroundColor Red
        return
    }

    # 2. Give Administrators full control to change the ACL
    & icacls "$FilePath" /grant "Administrators:F" | Out-Null

    # 3. Remove Users and Administrators from the ACL
    $toRemove = @(
        "BUILTIN\Users",
        "BUILTIN\Administrators",
        "NT AUTHORITY\Authenticated Users"
    )

    foreach ($identity in $toRemove) {
        & icacls "$FilePath" /remove:g "$identity" 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  [OK] Removed from the ACL: $identity" -ForegroundColor Green
        }
    }

    # 4. Add SecAdmin with ReadAndExecute and Read
    & icacls "$FilePath" /grant "${SecAdminName}:(RX)" 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  [OK] Added $SecAdminName : ReadAndExecute, Read" -ForegroundColor Green
    } else {
        Write-Host "  [X] Failed to add $SecAdminName" -ForegroundColor Red
    }

    # 5. Check
    Write-Host "  Current permissions:" -ForegroundColor DarkGray
    & icacls "$FilePath" | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkGray }
}

# ─── Start ────────────────────────────────────

Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   Assigning the SecAdmin and SysAdmin roles" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Collecting user information..." -ForegroundColor Gray

$allUsers = Get-LocalUsersWithGroups

if ($allUsers.Count -lt 2) {
    Write-Host "  Not enough users with administrator rights (at least 2)." -ForegroundColor Red

    return  # return instead of exit 1
}

# ─── SecAdmin selection ───────────────────────
Write-Host ""
Write-Host "  Assigning SecAdmin" -ForegroundColor Green
Write-Host "  Privileges: Load and unload device drivers, Manage auditing and security log" -ForegroundColor Gray
$secAdminList = Show-UserList -Users $allUsers
$secAdmin = Select-User -DisplayList $secAdminList -Role "SecAdmin"
Write-Host "  SecAdmin: $($secAdmin.Name)" -ForegroundColor Green

# ─── SysAdmin selection ───────────────────────
Write-Host ""
Write-Host "  Assigning SysAdmin" -ForegroundColor Cyan
Write-Host "  Privileges: Perform volume maintenance tasks, Restore files and directories" -ForegroundColor Gray
Write-Host "  (user '$($secAdmin.Name)' excluded - already SecAdmin)" -ForegroundColor Gray
$sysAdminList = Show-UserList -Users $allUsers -ExcludeUser $secAdmin.Name

if ($sysAdminList.Count -eq 0) {
    Write-Host "  No users available for SysAdmin." -ForegroundColor Red
    return  # return instead of exit 1
}

$sysAdmin = Select-User -DisplayList $sysAdminList -Role "SysAdmin"
Write-Host "  SysAdmin: $($sysAdmin.Name)" -ForegroundColor Cyan

# ─── Apply the privileges ─────────────────────
Write-Host ""
Write-Host "═══════════════════════════════════════════════" -ForegroundColor White
Write-Host "   Applying the security settings..." -ForegroundColor White
Write-Host "═══════════════════════════════════════════════" -ForegroundColor White
Write-Host ""
Write-Host "  SecAdmin: $($secAdmin.Name)" -ForegroundColor Green
applyPrivileges -UserName $secAdmin.Name -Privileges $rolePrivileges.SecAdmin -Role "SecAdmin" -BackupDir $BackupDir

Write-Host ""
Write-Host "  SysAdmin: $($sysAdmin.Name)" -ForegroundColor Cyan
applyPrivileges -UserName $sysAdmin.Name -Privileges $rolePrivileges.SysAdmin -Role "SysAdmin" -BackupDir $BackupDir

# ─── File security settings ───────────────────
Write-Host ""
Write-Host "═══════════════════════════════════════════════" -ForegroundColor White
Write-Host "   File security settings..." -ForegroundColor White
Write-Host "═══════════════════════════════════════════════" -ForegroundColor White

$secureFiles = @(
    "C:\Windows\System32\eventvwr.exe",
    "C:\Windows\System32\eventvwr.msc"
)

foreach ($file in $secureFiles) {
    setFileSecurity -FilePath $file -SecAdminName $secAdmin.Name
}

# ─── Summary ──────────────────────────────────
Write-Host ""
Write-Host "═══════════════════════════════════════════════" -ForegroundColor White
Write-Host "   Summary:" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════" -ForegroundColor White
Write-Host ""
Write-Host "   SecAdmin : $($secAdmin.Name)" -ForegroundColor Green
Write-Host "     • Load and unload device drivers  (SeLoadDriverPrivilege)"
Write-Host "     • Manage auditing and security log (SeSecurityPrivilege)"
Write-Host "     • ReadAndExecute, Read → eventvwr.exe"
Write-Host "     • ReadAndExecute, Read → eventvwr.msc"
Write-Host ""
Write-Host "   SysAdmin : $($sysAdmin.Name)" -ForegroundColor Cyan
Write-Host "     • Perform volume maintenance tasks (SeManageVolumePrivilege)"
Write-Host "     • Restore files and directories   (SeRestorePrivilege)"
Write-Host ""
Write-Host "   Backups: $BackupDir" -ForegroundColor Gray
Write-Host ""

}