# shotPolicies.ps1 - single-setting policies that can be applied right before an evidence screenshot.
# Used by ShotApply.ps1 (the "Apply setting" button of the capture bar) and by the engine step type
# 'shotPolicies' (Apply-EvidencePolicies). One implementation, so both paths stay in sync.
#
# The values below are EXAMPLES close to common public baselines. Adjust them to the requirements
# that apply to your systems before use.

$script:EvidencePolicyValues = @{
    PasswordHistorySize   = 24
    MaximumPasswordAge    = 60
    MinimumPasswordAge    = 1
    MinimumPasswordLength = 14
    LockoutBadCount       = 10
    ResetLockoutCount     = 15
    LockoutDuration       = 15
    IdleMinutes           = 15
    LogonBannerCaption    = 'Authorized use only'
    LogonBannerText       = 'This system is for authorized users only. Activity may be monitored and recorded.'
}

function ShotSecedit([string[]]$infLines, [string]$areas) {
    $inf = Join-Path $env:TEMP ("asshot_" + [guid]::NewGuid().ToString('N') + ".inf")
    $head = @('[Unicode]', 'Unicode=yes')
    $tail = @('[Version]', 'signature="$CHICAGO$"', 'Revision=1')
    $all = $head + $infLines + $tail
    [IO.File]::WriteAllText($inf, ($all -join "`r`n"), [System.Text.Encoding]::Unicode)
    $db = Join-Path $env:TEMP ("asshot_" + [guid]::NewGuid().ToString('N') + ".sdb")
    & secedit /configure /db $db /cfg $inf /areas $areas /quiet
    $code = $LASTEXITCODE
    Remove-Item $inf, $db -Force -ErrorAction SilentlyContinue
    if ($code -ne 0) { throw "secedit exited with code $code" }
}

function ShotSetReg([string]$path, [string]$name, [string]$type, $value) {
    if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
    New-ItemProperty -Path $path -Name $name -PropertyType $type -Value $value -Force | Out-Null
}

# Apply one setting by token. The caller runs gpupdate.
function Invoke-ShotPolicy([string]$Token) {
    $v = $script:EvidencePolicyValues
    switch ($Token) {

        'password' {
            Write-Output "Applying the password policy..."
            ShotSecedit @(
                '[System Access]',
                "PasswordHistorySize = $($v.PasswordHistorySize)",
                "MaximumPasswordAge = $($v.MaximumPasswordAge)",
                "MinimumPasswordAge = $($v.MinimumPasswordAge)",
                "MinimumPasswordLength = $($v.MinimumPasswordLength)",
                'PasswordComplexity = 1',
                'ClearTextPassword = 0'
            ) 'SECURITYPOLICY'
            Write-Output ("Done: history {0}, max age {1}, min age {2}, length {3}, complexity on." -f $v.PasswordHistorySize, $v.MaximumPasswordAge, $v.MinimumPasswordAge, $v.MinimumPasswordLength)
        }

        'lockout' {
            Write-Output "Applying the account lockout policy..."
            ShotSecedit @(
                '[System Access]',
                "LockoutBadCount = $($v.LockoutBadCount)",
                "ResetLockoutCount = $($v.ResetLockoutCount)",
                "LockoutDuration = $($v.LockoutDuration)"
            ) 'SECURITYPOLICY'
            Write-Output ("Done: {0} attempts / {1} min lockout / counter reset after {2} min." -f $v.LockoutBadCount, $v.LockoutDuration, $v.ResetLockoutCount)
        }

        'audit' {
            Write-Output "Enabling full auditing (Success + Failure)..."
            ShotSecedit @(
                '[Event Audit]',
                'AuditSystemEvents = 3',
                'AuditLogonEvents = 3',
                'AuditObjectAccess = 3',
                'AuditPrivilegeUse = 3',
                'AuditPolicyChange = 3',
                'AuditAccountManage = 3',
                'AuditProcessTracking = 3',
                'AuditDSAccess = 3',
                'AuditAccountLogon = 3'
            ) 'SECURITYPOLICY'
            & auditpol /set /category:* /success:enable /failure:enable | Out-Null
            Write-Output "Done: all audit categories - Success and Failure."
        }

        'auditsys' {
            Write-Output "Auditing system integrity..."
            & auditpol /set /subcategory:"System Integrity" /success:enable /failure:enable | Out-Null
            Write-Output "Done: System Integrity - Success and Failure."
        }

        'message' {
            Write-Output "Setting the logon warning message..."
            $p = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System'
            ShotSetReg $p 'LegalNoticeCaption' 'String' $v.LogonBannerCaption
            ShotSetReg $p 'LegalNoticeText' 'String' $v.LogonBannerText
            Write-Output "Done: logon message caption and text set."
        }

        'idle' {
            Write-Output ("Idle time before the display turns off and sleep - {0} minutes..." -f $v.IdleMinutes)
            foreach ($s in 'monitor-timeout-ac', 'monitor-timeout-dc', 'standby-timeout-ac', 'standby-timeout-dc') { & powercfg /change $s $v.IdleMinutes | Out-Null }
            Write-Output ("Done: display off and sleep - {0} min." -f $v.IdleMinutes)
        }

        'hideuser' {
            Write-Output "Hiding user information on the lock screen..."
            ShotSetReg 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' 'DontDisplayLockedUserId' 'DWord' 3
            Write-Output "Done: Do not display user information."
        }

        'bitlocker' {
            Write-Output "BitLocker settings (additional authentication at startup)..."
            $p = 'HKLM:\SOFTWARE\Policies\Microsoft\FVE'
            ShotSetReg $p 'UseAdvancedStartup' 'DWord' 1
            ShotSetReg $p 'EnableBDEWithNoTPM' 'DWord' 0      # set to 1 only if PCs without a TPM must be supported
            ShotSetReg $p 'UseTPM' 'DWord' 2
            ShotSetReg $p 'UseTPMPIN' 'DWord' 2
            ShotSetReg $p 'UseTPMKey' 'DWord' 2
            ShotSetReg $p 'UseTPMKeyPIN' 'DWord' 2
            Write-Output "Done: Require additional authentication at startup = Enabled."
        }

        default { throw "Unknown setting: $Token" }
    }
}

# Ask the GUI for an evidence screenshot during a step. Outside the GUI (no Send-Gui) it does nothing.
# Sends 'S|N|title' and waits for the answer.
function Request-Shot([int]$N, [string]$Title) {
    if (-not (Get-Command Send-Gui -ErrorAction SilentlyContinue)) { return }
    $b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$Title))
    Send-Gui ('S|' + $N + '|' + $b64)
    try { $null = [Console]::In.ReadLine() } catch { }
}

# Walk through every item of Catalog/shots.json in order. Items with an "Apply" token get their setting
# applied right before the screenshot prompt.
#   $Shots = $false    - only apply the settings, no screenshot prompts.
#   $Gpupdate = $false - skip the final gpupdate (when the caller runs it anyway).
function Apply-EvidencePolicies([bool]$Shots = $true, [bool]$Gpupdate = $true) {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $catalog = Join-Path $root 'Catalog\shots.json'
    if (-not (Test-Path -LiteralPath $catalog)) { throw "Catalog\shots.json not found" }
    $items = @(([IO.File]::ReadAllText($catalog, [Text.Encoding]::UTF8) | ConvertFrom-Json).items | Sort-Object { [int]$_.N })
    Write-Host ("Evidence items: {0} (settings are applied right before each screenshot)..." -f $items.Count) -ForegroundColor Cyan
    $ok = 0; $err = 0
    foreach ($it in $items) {
        Write-Host ("  - Item " + $it.N + ": " + $it.Title) -ForegroundColor Gray
        if ($it.Apply) {
            try { Invoke-ShotPolicy $it.Apply; $ok++ }
            catch { Write-Host ("    setting failed: " + $_.Exception.Message) -ForegroundColor Red; $err++ }
        }
        if ($Shots) { Request-Shot ([int]$it.N) $it.Title }
    }
    if ($Gpupdate) { & gpupdate /target:computer /force 2>&1 | Out-Null }
    Write-Host ("Evidence settings applied: {0} succeeded, {1} failed. Items walked through: {2}." -f $ok, $err, $items.Count) -ForegroundColor $(if ($err -eq 0) { 'Green' } else { 'Yellow' })
    if ($err) { throw "$err evidence setting(s) failed" }
}
