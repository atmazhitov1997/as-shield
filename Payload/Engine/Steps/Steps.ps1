# Steps.ps1 - handlers for the profile step types.
# Each handler: scriptblock param($Step, $P, $Ctx) -> New-ASResult.
#   $Step - the step from the profile, $P - its params (may be $null), $Ctx - the run context.
# Statuses: OK (done and verified), DONE (done by a helper function without verifying the result),
#           SKIP (skipped), FAIL (error).
# A new step type = a new entry in $script:ASSteps + a line in Schema/profile.schema.json (type enum).

if (-not $script:ASSteps) { $script:ASSteps = @{} }

function New-ASResult([string]$Status, [string]$Detail = '') {
    [pscustomobject]@{ Status = $Status; Detail = $Detail }
}

function Invoke-ASNative([string]$Exe, [string[]]$Arguments) {
    $out = & $Exe @Arguments 2>&1 | Out-String
    return [pscustomobject]@{ Code = $LASTEXITCODE; Output = $out.Trim() }
}

function Get-ASLgpoExe($Ctx) {
    $p = Join-Path $Ctx.PayloadRoot 'LGPO.exe'
    if (-not (Test-Path -LiteralPath $p)) { throw "LGPO.exe not found: $p (run build\get-lgpo.ps1)" }
    return $p
}

# ── restorePoint ─────────────────────────────────────────────────────────────
$script:ASSteps['restorePoint'] = {
    param($Step, $P, $Ctx)
    $before = @(Get-ComputerRestorePoint -ErrorAction SilentlyContinue).Count
    osRestorePoint
    $after = @(Get-ComputerRestorePoint -ErrorAction SilentlyContinue).Count
    if ($after -gt $before) { return New-ASResult 'OK' "restore points: $before -> $after" }
    return New-ASResult 'FAIL' 'no new restore point found (System Protection is off or the frequency limit applies)'
}

# ── userProfilesPath ─────────────────────────────────────────────────────────
$script:ASSteps['userProfilesPath'] = {
    param($Step, $P, $Ctx)
    defaultUserPath
    $v = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList' -ErrorAction SilentlyContinue).ProfilesDirectory
    return New-ASResult 'OK' "ProfilesDirectory = $v"
}

# ── folder ───────────────────────────────────────────────────────────────────
$script:ASSteps['folder'] = {
    param($Step, $P, $Ctx)
    $path = [string](Get-ASProp $P 'path')
    if (Get-ASProp $P 'askPath' $false) {
        $a = Read-Host "Folder path (Enter - $path)"
        if (-not [string]::IsNullOrWhiteSpace($a)) { $path = $a.Trim() }
    }
    if (-not (Test-Path -LiteralPath $path)) { New-Item -ItemType Directory -Path $path -Force -ErrorAction Stop | Out-Null; return New-ASResult 'OK' "created $path" }
    if (-not (Test-Path -LiteralPath $path -PathType Container)) { return New-ASResult 'FAIL' "$path exists but is not a folder" }
    return New-ASResult 'OK' "already exists: $path"
}

# ── eventLogs ────────────────────────────────────────────────────────────────
$script:ASSteps['eventLogs'] = {
    param($Step, $P, $Ctx)
    $fail = @(); $n = 0
    foreach ($l in @(Get-ASProp $P 'logs' @())) {
        $bytes = [int64]$l.maxSizeKB * 1024
        $rt = if (Get-ASProp $l 'retention' $false) { 'true' } else { 'false' }
        $r = Invoke-ASNative 'wevtutil.exe' @('sl', $l.name, "/ms:$bytes", "/rt:$rt")
        if ($r.Code -eq 0) { $n++; Write-Host ("  {0}: {1} KB" -f $l.name, $l.maxSizeKB) -ForegroundColor Gray }
        else { $fail += "$($l.name) (code $($r.Code))" }
    }
    if ($fail.Count) { return New-ASResult 'FAIL' ("configured $n, errors: " + ($fail -join ', ')) }
    return New-ASResult 'OK' "logs configured: $n"
}

# ── registry ─────────────────────────────────────────────────────────────────
$script:ASSteps['registry'] = {
    param($Step, $P, $Ctx)
    $ok = 0; $skip = 0; $fail = @()
    foreach ($v in @(Get-ASProp $P 'values' @())) {
        if (-not (Test-Path -LiteralPath $v.path)) {
            if (Get-ASProp $v 'createKey' $false) { New-Item -Path $v.path -Force | Out-Null }
            else { $skip++; continue }
        }
        try {
            New-ItemProperty -LiteralPath $v.path -Name $v.name -PropertyType $v.type -Value $v.value -Force -ErrorAction Stop | Out-Null
            $back = (Get-ItemProperty -LiteralPath $v.path -Name $v.name -ErrorAction Stop).($v.name)
            if ("$back" -eq "$($v.value)") { $ok++ } else { $fail += "$($v.name): read back '$back'" }
        } catch { $fail += "$($v.name): $($_.Exception.Message)" }
    }
    $d = "applied $ok, skipped (key missing) $skip"
    if ($fail.Count) { return New-ASResult 'FAIL' ($d + '; errors: ' + ($fail -join '; ')) }
    return New-ASResult 'OK' $d
}

# ── removeApps (OneDrive + Skype, helper function) ──────────────────────────
$script:ASSteps['removeApps'] = {
    param($Step, $P, $Ctx)
    removeAdditionalPrograms
    return New-ASResult 'DONE' 'OneDrive/Skype - done by removeAdditionalPrograms'
}

# ── removeWinXShell ──────────────────────────────────────────────────────────
$script:ASSteps['removeWinXShell'] = {
    param($Step, $P, $Ctx)
    Remove-Win11TerminalAndPowerShell
    return New-ASResult 'DONE' 'Terminal/PowerShell in the Win+X menu'
}

# ── services ─────────────────────────────────────────────────────────────────
# CSV: Name,StartType,AskOptions,AskPrompt.  AskOptions = "AUTO|DISABLED" - the operator chooses.
$script:ASStartTypeByCode = @{ '2' = 'AUTO'; '3' = 'DEMAND'; '4' = 'DISABLED' }

function Get-ASServiceStartType([string]$Name) {
    $r = Invoke-ASNative 'sc.exe' @('qc', $Name)
    if ($r.Code -eq 1060) { return $null }             # ERROR_SERVICE_DOES_NOT_EXIST - independent of the OS language
    if ($r.Code -ne 0) { throw "sc qc $Name -> code $($r.Code)" }
    if ($r.Output -match 'START_TYPE\s*:\s*(\d)') { return $script:ASStartTypeByCode[$matches[1]] }
    return 'UNKNOWN'
}

$script:ASSteps['services'] = {
    param($Step, $P, $Ctx)
    $file = Resolve-ASStepFile $Step (Resolve-ASPerOs (Get-ASProp $P 'file') $Ctx.Os) $Ctx
    $changed = 0; $same = 0; $missing = 0; $kept = 0; $fail = @()
    foreach ($s in (Import-Csv -LiteralPath $file -Encoding UTF8)) {
        if ([string]::IsNullOrWhiteSpace($s.Name)) { continue }
        $cur = Get-ASServiceStartType $s.Name
        if ($null -eq $cur) { $missing++; continue }
        $want = $s.StartType.ToUpper()
        if ($s.AskOptions) {
            $opts = @($s.AskOptions.Split('|'))
            Write-Host ""
            Write-Host ("Service {0} (currently {1}). {2}" -f $s.Name, $cur, $s.AskPrompt) -ForegroundColor Cyan
            for ($i = 0; $i -lt $opts.Count; $i++) { Write-Host ("[{0}] {1}" -f ($i + 1), $opts[$i]) }
            Write-Host ("[{0}] Keep as is" -f ($opts.Count + 1))
            $c = 0; [void][int]::TryParse((Read-Host 'Your choice'), [ref]$c)
            if ($c -ge 1 -and $c -le $opts.Count) { $want = $opts[$c - 1].ToUpper() } else { $kept++; continue }
        }
        if ($cur -eq $want) { $same++; continue }
        $r = Invoke-ASNative 'sc.exe' @('config', $s.Name, 'start=', $want.ToLower())
        $now = Get-ASServiceStartType $s.Name
        if ($r.Code -eq 0 -and $now -eq $want) { $changed++; Write-Host ("  {0}: {1} → {2}" -f $s.Name, $cur, $want) -ForegroundColor Gray }
        else { $fail += "$($s.Name) (code $($r.Code), now $now)" }
    }
    $d = "changed $changed, unchanged $same, not present $missing, kept by the operator $kept"
    if ($fail.Count) { return New-ASResult 'FAIL' ($d + '; errors: ' + ($fail -join ', ')) }
    return New-ASResult 'OK' $d
}

# ── appLocker ────────────────────────────────────────────────────────────────
$script:ASSteps['appLocker'] = {
    param($Step, $P, $Ctx)
    $file = Resolve-ASStepFile $Step (Get-ASProp $P 'file') $Ctx
    $mode = [string](Get-ASProp $P 'mode' 'ask')
    if ($mode -eq 'ask') {
        Write-Host "AppLocker rules: [1] replace existing  [2] merge with existing  [3] do not apply" -ForegroundColor Cyan
        switch (Read-Host 'Your choice') { '1' { $mode = 'replace' } '2' { $mode = 'merge' } default { $mode = 'skip' } }
    }
    if ($mode -eq 'skip') { return New-ASResult 'SKIP' 'AppLocker not applied by the operator' }
    if ($mode -eq 'merge') { Set-AppLockerPolicy -XmlPolicy $file -Merge -ErrorAction Stop } else { Set-AppLockerPolicy -XmlPolicy $file -ErrorAction Stop }
    $svc = Get-ASServiceStartType 'AppIDSvc'
    $d = "mode $mode; AppIDSvc = $svc"
    if ($svc -ne 'AUTO') { return New-ASResult 'FAIL' ($d + ' - the rules have no effect without the Application Identity service') }
    return New-ASResult 'OK' $d
}

# ── lgpo ─────────────────────────────────────────────────────────────────────
$script:ASSteps['lgpo'] = {
    param($Step, $P, $Ctx)
    $gpo = Resolve-ASStepFile $Step (Resolve-ASPerOs (Get-ASProp $P 'gpo') $Ctx.Os) $Ctx
    $lgpo = Get-ASLgpoExe $Ctx
    $d = ''
    if (Get-ASProp $P 'backupBefore' $false) {
        $bk = Join-Path $Ctx.DataDir ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $Ctx.ProfileId)
        New-Item -ItemType Directory -Path $bk -Force | Out-Null
        $b = Invoke-ASNative $lgpo @('/b', $bk, '/n', "AS Shield backup before $($Ctx.ProfileId)")
        if ($b.Code -ne 0) { return New-ASResult 'FAIL' "GPO backup not created (code $($b.Code)) - nothing was applied" }
        $Ctx.LastBackup = $bk
        $d = "backup: $bk; "
    }
    $r = Invoke-ASNative $lgpo @('/g', $gpo)
    if ($r.Code -ne 0) { return New-ASResult 'FAIL' ($d + "LGPO /g exited with code $($r.Code)") }
    return New-ASResult 'OK' ($d + "applied $(Split-Path $gpo -Leaf)")
}

# ── lgpoText (individual settings, e.g. DisableCMD) ─────────────────────────
$script:ASSteps['lgpoText'] = {
    param($Step, $P, $Ctx)
    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($e in @(Get-ASProp $P 'entries' @())) {
        $lines.Add($e.scope); $lines.Add($e.key); $lines.Add($e.name); $lines.Add($e.value); $lines.Add('')
    }
    $tmp = Join-Path $Ctx.DataDir ('lgpo-' + [guid]::NewGuid().ToString('N') + '.txt')
    [IO.File]::WriteAllLines($tmp, $lines, (New-Object System.Text.UTF8Encoding $false))   # LGPO /t does not accept a BOM
    try {
        $r = Invoke-ASNative (Get-ASLgpoExe $Ctx) @('/t', $tmp)
        if ($r.Code -ne 0) { return New-ASResult 'FAIL' "LGPO /t exited with code $($r.Code)" }
        return New-ASResult 'OK' ("settings: " + @(Get-ASProp $P 'entries' @()).Count)
    } finally { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
}

# ── shotPolicies / userRoles (helper functions) ─────────────────────────────
$script:ASSteps['shotPolicies'] = {
    param($Step, $P, $Ctx)
    Apply-EvidencePolicies $true $false
    return New-ASResult 'DONE' 'evidence settings with screenshots'
}
$script:ASSteps['userRoles'] = {
    param($Step, $P, $Ctx)
    UsersRole
    return New-ASResult 'DONE' 'SecAdmin/SysAdmin roles (see the backup INF files in the log)'
}

# ── script (extension without changing the engine) ──────────────────────────
$script:ASSteps['script'] = {
    param($Step, $P, $Ctx)
    $file = Resolve-ASStepFile $Step (Get-ASProp $P 'file') $Ctx
    . $file
    $fn = [string](Get-ASProp $P 'function')
    if (-not (Get-Command $fn -ErrorAction SilentlyContinue)) { return New-ASResult 'FAIL' "function $fn not found in $file" }
    $args2 = @{}
    $a = Get-ASProp $P 'arguments'
    if ($a) { foreach ($pp in $a.PSObject.Properties) { $args2[$pp.Name] = $pp.Value } }
    $ret = & $fn @args2
    if ($ret -and $ret.PSObject.Properties['Status']) { return $ret }   # the function may return New-ASResult itself
    return New-ASResult 'DONE' "$fn done"
}
