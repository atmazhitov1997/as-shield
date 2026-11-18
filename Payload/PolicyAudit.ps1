# PolicyAudit.ps1 - dot-sourced from RunAction.ps1 (Write-Host/Read-Host overridden by GUI console)
# Policy audit: compares the live state of the PC with the GPO backup of the selected profile
# (GptTmpl.inf / registry.pol / audit.csv).

Write-Host "POLICY COMPLIANCE AUDIT" -ForegroundColor Cyan
Write-Host "Compares the live settings of this PC with the GPO backup of a selected profile." -ForegroundColor Gray
Write-Host ""

$bn = 0; try { $bn = [int]([Environment]::OSVersion.Version.Build) } catch {}
$osv = $(if ($bn -ge 22000) { '11' } else { '10' })
$cands = @()
foreach ($p in @(Get-ASProfileList $PSScriptRoot)) {
    $full = $null; try { $full = Resolve-ASProfile $PSScriptRoot $p.id } catch { continue }
    $lg = @($full.steps | Where-Object { $_.type -eq 'lgpo' }) | Select-Object -First 1
    if ($lg -and (@($full.os.supported) -contains $osv)) { $cands += [pscustomobject]@{ Profile = $full; Step = $lg } }
}
if ($cands.Count -eq 0) { Write-Host "No profile with an 'lgpo' step supports Windows $osv - nothing to audit against." -ForegroundColor Yellow; return }
Write-Host "Select the profile to audit against:" -ForegroundColor White
for ($n = 0; $n -lt $cands.Count; $n++) { Write-Host ("  ({0}) {1}" -f ($n + 1), $cands[$n].Profile.title) -ForegroundColor Yellow }
$sel = 0; [void][int]::TryParse((Read-Host "Your choice").Trim(), [ref]$sel)
if ($sel -lt 1 -or $sel -gt $cands.Count) { Write-Host "Unknown choice - audit cancelled." -ForegroundColor Yellow; return }
$pick = $cands[$sel - 1]
$bname = $pick.Profile.title
$ctxA = [pscustomobject]@{ PayloadRoot = $PSScriptRoot; Os = $osv }
try { $cfgRoot = Resolve-ASStepFile $pick.Step (Resolve-ASPerOs (Get-ASProp $pick.Step.params 'gpo') $osv) $ctxA }
catch { Write-Host ("GPO backup of the profile not found: " + $_.Exception.Message) -ForegroundColor Red; return }

Write-Host ""
Write-Host ("Profile: " + $bname + "  (" + (Split-Path $cfgRoot -Leaf) + ")") -ForegroundColor Cyan
Write-Host ("OS: Windows build " + $bn + " (Win" + $osv + ")") -ForegroundColor Gray
Write-Host ""

$gpt = Get-ChildItem -LiteralPath $cfgRoot -Recurse -Filter "GptTmpl.inf" -ErrorAction SilentlyContinue | Select-Object -First 1
$auditBuild = Get-ChildItem -LiteralPath $cfgRoot -Recurse -Filter "audit.csv" -ErrorAction SilentlyContinue | Select-Object -First 1
$machPol = Get-ChildItem -LiteralPath $cfgRoot -Recurse -Filter "registry.pol" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match "\\Machine\\" } | Select-Object -First 1

# ---------- accumulator ----------
$script:res = New-Object System.Collections.Generic.List[object]
$script:nOK = 0; $script:nWarn = 0; $script:nFail = 0
$script:catBad = 0

function AddRes([string]$cat, [string]$name, [string]$exp, [string]$act, [string]$state) {
    $script:res.Add([pscustomobject]@{ Cat = $cat; Name = $name; Exp = $exp; Act = $act; State = $state })
    if ($state -eq "OK") { $script:nOK++; return }
    if ($state -eq "WARN") { $script:nWarn++; $script:catBad++; $sym = "[!]"; $col = "Yellow" }
    else { $script:nFail++; $script:catBad++; $sym = "[-]"; $col = "Red" }
    $line = "  " + $sym + " " + $name + ": " + $act
    if ($exp -ne "") { $line = $line + "  (expected: " + $exp + ")" }
    Write-Host $line -ForegroundColor $col
}
function StartCat([string]$t) { Write-Host ""; Write-Host ("── " + $t + " ──") -ForegroundColor Cyan; $script:catBad = 0 }
function EndCat() { if ($script:catBad -eq 0) { Write-Host "  [+] all settings match" -ForegroundColor Green } }

# ---------- live secedit export ----------
$sx = @{}; $pr = @{}
$expInf = Join-Path $env:TEMP ("audit_" + [guid]::NewGuid().ToString("N") + ".inf")
try {
    & secedit /export /cfg $expInf /quiet 2>$null | Out-Null
    if (Test-Path -LiteralPath $expInf) {
        $sec = ""
        foreach ($l in (Get-Content -LiteralPath $expInf -Encoding Unicode)) {
            if ($l -match '^\s*\[(.+)\]\s*$') { $sec = $matches[1]; continue }
            if ($l -match '^\s*([^=]+?)\s*=\s*(.*)$') {
                $k = $matches[1].Trim(); $vv = $matches[2].Trim()
                if ($sec -eq "System Access") { $sx[$k] = $vv }
                elseif ($sec -eq "Privilege Rights") { $pr[$k] = $vv }
            }
        }
        Remove-Item -LiteralPath $expInf -Force -ErrorAction SilentlyContinue
    }
} catch {}

# ---------- parse build GptTmpl ----------
$bSys = @{}; $bPriv = @{}; $bReg = New-Object System.Collections.Generic.List[object]
if ($gpt) {
    $sec = ""
    foreach ($l in (Get-Content -LiteralPath $gpt.FullName -Encoding Unicode)) {
        if ($l -match '^\s*\[(.+)\]\s*$') { $sec = $matches[1]; continue }
        if ($sec -eq "System Access") { if ($l -match '^\s*([^=]+?)\s*=\s*(.*)$') { $bSys[$matches[1].Trim()] = $matches[2].Trim() } }
        elseif ($sec -eq "Privilege Rights") { if ($l -match '^\s*(Se\w+)\s*=\s*(.*)$') { $bPriv[$matches[1]] = $matches[2].Trim() } }
        elseif ($sec -eq "Registry Values") { if ($l -match '^\s*(.+?)=(\d+),(.*)$') { $bReg.Add([pscustomobject]@{ Path = $matches[1].Trim(); Type = [int]$matches[2]; Val = $matches[3] }) } }
    }
}

# ========== 1. Passwords and lockout ==========
StartCat "Passwords and lockout"
$saMap = [ordered]@{
    "MinimumPasswordAge"   = "Minimum password age (days)"
    "MaximumPasswordAge"   = "Maximum password age (days)"
    "MinimumPasswordLength" = "Minimum password length"
    "PasswordComplexity"   = "Password complexity"
    "PasswordHistorySize"  = "Password history"
    "ClearTextPassword"    = "Store with reversible encryption"
    "LockoutBadCount"      = "Lockout threshold (attempts)"
    "ResetLockoutCount"    = "Reset counter after (min)"
    "LockoutDuration"      = "Lockout duration (min)"
}
foreach ($k in $saMap.Keys) {
    if (-not $bSys.ContainsKey($k)) { continue }
    $e = $bSys[$k].Trim('"'); $a = $(if ($sx.ContainsKey($k)) { $sx[$k].Trim('"') } else { "(missing)" })
    $st = $(if ($a -eq $e) { "OK" } else { "FAIL" })
    AddRes "Passwords and lockout" $saMap[$k] $e $a $st
}
EndCat

# ========== 2. Accounts ==========
StartCat "Accounts"
$accMap = [ordered]@{
    "EnableAdminAccount" = "Administrator enabled"
    "EnableGuestAccount" = "Guest enabled"
    "NewAdministratorName" = "Administrator renamed to"
    "NewGuestName"       = "Guest renamed to"
}
foreach ($k in $accMap.Keys) {
    if (-not $bSys.ContainsKey($k)) { continue }
    $e = $bSys[$k].Trim('"'); $a = $(if ($sx.ContainsKey($k)) { $sx[$k].Trim('"') } else { "(missing)" })
    $st = $(if ($a -eq $e) { "OK" } else { "FAIL" })
    AddRes "Accounts" $accMap[$k] $e $a $st
}
EndCat

# ========== 3. Security options (registry values from GptTmpl) ==========
StartCat "Security options (registry)"
foreach ($r in $bReg) {
    $p = $r.Path
    if ($p -notmatch '^(MACHINE|Machine)\\') { continue }
    $rp = "HKLM:\" + ($p -replace '^(MACHINE|Machine)\\', '')
    $vn = Split-Path $rp -Leaf
    $rk = Split-Path $rp -Parent
    $ty = $r.Type; $raw = $r.Val
    $exp = $raw
    if ($ty -eq 1 -or $ty -eq 2) { $exp = $raw.Trim('"') }
    $have = $false; $act = $null
    try { $pv = Get-ItemProperty -LiteralPath $rk -Name $vn -ErrorAction Stop; $act = $pv.$vn; $have = $true } catch {}
    if (-not $have) { AddRes "Security options" ($vn) $exp "(missing)" "FAIL"; continue }
    $ok = $false
    if ($ty -eq 4) {
        $ei = 0; $ai = 0; [void][int64]::TryParse("$exp", [ref]$ei); [void][int64]::TryParse("$act", [ref]$ai)
        $ok = ($ei -eq $ai)
        if (-not $ok -and $vn -eq "CrashOnAuditFail" -and $ai -eq 2) { $ok = $true }
    } else { $ok = ("$act".Trim() -eq "$exp".Trim()) }
    AddRes "Security options" ($vn) $exp "$act" $(if ($ok) { "OK" } else { "FAIL" })
}
EndCat

# ========== 4. User rights ==========
StartCat "User rights"
$prNames = @{
    "SeNetworkLogonRight" = "Access this computer from the network"
    "SeDenyNetworkLogonRight" = "Deny access from the network"
    "SeDenyInteractiveLogonRight" = "Deny log on locally"
    "SeDenyRemoteInteractiveLogonRight" = "Deny log on through RDP"
    "SeRemoteInteractiveLogonRight" = "Log on through RDP"
    "SeInteractiveLogonRight" = "Log on locally"
    "SeBackupPrivilege" = "Back up files"
    "SeRestorePrivilege" = "Restore files"
    "SeTakeOwnershipPrivilege" = "Take ownership"
    "SeDebugPrivilege" = "Debug programs"
    "SeShutdownPrivilege" = "Shut down the system"
}
function SidSet([string]$s) {
    if ([string]::IsNullOrWhiteSpace($s)) { return "" }
    $a = @(($s -split ',') | ForEach-Object { $_.Trim().TrimStart('*') } | Where-Object { $_ -match '^S-1-' } | Sort-Object -Unique)
    return ($a -join ',')
}
foreach ($k in $bPriv.Keys) {
    $e = SidSet $bPriv[$k]
    $a = SidSet $(if ($pr.ContainsKey($k)) { $pr[$k] } else { "" })
    $nm = $(if ($prNames.ContainsKey($k)) { $prNames[$k] } else { $k })
    if ($e -eq $a) { AddRes "User rights" $nm "" "ok" "OK" }
    else {
        $ad = $(if ($a -eq "") { "(empty)" } else { ($a -split ',').Count.ToString() + " SID" })
        $ed = $(if ($e -eq "") { "(empty)" } else { ($e -split ',').Count.ToString() + " SID" })
        AddRes "User rights" $nm $ed $ad "FAIL"
    }
}
EndCat

# ========== 5. Administrative templates (registry.pol) ==========
function Read-PReg([string]$path) {
    $out = New-Object System.Collections.Generic.List[object]
    if (-not (Test-Path -LiteralPath $path)) { return $out }
    $b = [IO.File]::ReadAllBytes($path)
    if ($b.Length -lt 8 -or $b[0] -ne 80 -or $b[1] -ne 82 -or $b[2] -ne 101 -or $b[3] -ne 103) { return $out }
    $i = 8
    while ($i -lt $b.Length - 1) {
        if ($b[$i] -ne 0x5B) { break }
        $i += 2
        $sb = New-Object System.Text.StringBuilder
        while ($i -lt $b.Length - 1) { $c = [char]($b[$i] -bor ($b[$i + 1] -shl 8)); $i += 2; if ($c -eq [char]0) { break }; [void]$sb.Append($c) }
        $key = $sb.ToString(); $i += 2
        $sb = New-Object System.Text.StringBuilder
        while ($i -lt $b.Length - 1) { $c = [char]($b[$i] -bor ($b[$i + 1] -shl 8)); $i += 2; if ($c -eq [char]0) { break }; [void]$sb.Append($c) }
        $val = $sb.ToString(); $i += 2
        if ($i + 4 -gt $b.Length) { break }
        $type = [BitConverter]::ToUInt32($b, $i); $i += 4; $i += 2
        if ($i + 4 -gt $b.Length) { break }
        $size = [BitConverter]::ToUInt32($b, $i); $i += 4; $i += 2
        $data = New-Object byte[] $size
        if ($size -gt 0 -and ($i + $size) -le $b.Length) { [Array]::Copy($b, $i, $data, 0, $size); $i += $size }
        $i += 2
        $out.Add([pscustomobject]@{ Key = $key; Value = $val; Type = $type; Data = $data })
    }
    return $out
}
StartCat "Administrative templates / registry.pol"
if ($machPol) {
    $mp = Read-PReg $machPol.FullName
    foreach ($e in $mp) {
        if ([string]::IsNullOrEmpty($e.Value) -or $e.Value -like '**') { continue }
        if ($e.Value -like '**del*' -or $e.Value -like '**Soft*' -or $e.Value -like '**') { continue }
        $exp = $null; $kind = ""
        if ($e.Type -eq 4) { if ($e.Data.Length -ge 4) { $exp = [BitConverter]::ToUInt32($e.Data, 0); $kind = "d" } }
        elseif ($e.Type -eq 1 -or $e.Type -eq 2) { $exp = [Text.Encoding]::Unicode.GetString($e.Data).TrimEnd([char]0); $kind = "s" }
        if ($kind -eq "") { continue }
        $rk = "HKLM:\" + $e.Key; $vn = $e.Value
        $have = $false; $act = $null
        try { $pv = Get-ItemProperty -LiteralPath $rk -Name $vn -ErrorAction Stop; $act = $pv.$vn; $have = $true } catch {}
        $label = $vn
        if (-not $have) { AddRes "Administrative templates" $label "$exp" "(missing)" "FAIL"; continue }
        $ok = $false
        if ($kind -eq "d") { $ai = 0; [void][int64]::TryParse("$act", [ref]$ai); $ok = ($ai -eq [int64]$exp) }
        else { $ok = ("$act".Trim() -eq "$exp".Trim()) }
        AddRes "Administrative templates" $label "$exp" "$act" $(if ($ok) { "OK" } else { "FAIL" })
    }
} else { Write-Host "  · no registry.pol in the GPO backup - skipped" -ForegroundColor Gray }
EndCat

# ========== 6. Audit policy of the profile ==========
function Read-AuditCsv([string]$path) {
    $m = @{}
    if (-not (Test-Path -LiteralPath $path)) { return $m }
    try {
        $rows = Import-Csv -LiteralPath $path
        foreach ($r in $rows) {
            $g = ""; $v = ""
            foreach ($p in $r.PSObject.Properties) {
                if ($p.Name -match "GUID") { $g = "$($p.Value)".Trim().ToUpper() }
                if ($p.Name -match "Setting Value") { $v = "$($p.Value)".Trim() }
            }
            if ($g -ne "") { $m[$g] = $v }
        }
    } catch {}
    return $m
}
$liveAuditPath = Join-Path $env:SystemRoot "System32\GroupPolicy\Machine\Microsoft\Windows NT\Audit\audit.csv"
$liveAudit = Read-AuditCsv $liveAuditPath
$buildAudit = @{}
if ($auditBuild) { $buildAudit = Read-AuditCsv $auditBuild.FullName }

StartCat "Audit policy of the profile"
if ($buildAudit.Count -eq 0) { Write-Host "  · no audit.csv in the GPO backup - skipped" -ForegroundColor Gray }
elseif ($liveAudit.Count -eq 0) { AddRes "Profile audit" "GPO audit on the PC" "applied" "not applied (no live audit.csv)" "FAIL" }
else {
    foreach ($g in $buildAudit.Keys) {
        $e = $buildAudit[$g]
        $a = $(if ($liveAudit.ContainsKey($g)) { $liveAudit[$g] } else { "0" })
        if ($a -eq $e) { AddRes "Profile audit" $g $e $a "OK" }
        else { AddRes "Profile audit" ("subcat. " + $g) $e $a "FAIL" }
    }
}
EndCat

# ---------- summary ----------
Write-Host ""
Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor DarkGray
$total = $script:nOK + $script:nWarn + $script:nFail
Write-Host ("SUMMARY:  match " + $script:nOK + "   warnings " + $script:nWarn + "   mismatches " + $script:nFail + "   (total " + $total + ")") -ForegroundColor White
if ($script:nFail -eq 0 -and $script:nWarn -eq 0) { Write-Host "The PC fully matches the selected profile." -ForegroundColor Green }
elseif ($script:nFail -eq 0) { Write-Host "The PC matches, with warnings only (see below)." -ForegroundColor Yellow }
else { Write-Host "Mismatches found - the policies are not fully applied. See the report for details." -ForegroundColor Red }

# ---------- report file (docx) ----------
$host2 = $env:COMPUTERNAME
$stamp = Get-Date -Format "yyyy-MM-dd HH-mm"
$dir = Get-ReportDir
$rep = Join-Path $dir ("Policy audit " + $bname + " " + $host2 + " " + $stamp + ".docx")
$content = @(
    @{t = 'title'; x = 'POLICY COMPLIANCE AUDIT' },
    @{t = 'sub'; x = ('Profile: ' + $bname) },
    @{t = 'meta'; x = ('PC: ' + $host2 + '     Date: ' + (Get-Date -Format "dd.MM.yyyy HH:mm")) },
    @{t = 'meta'; x = ('Match: ' + $script:nOK + '     Warnings: ' + $script:nWarn + '     Mismatches: ' + $script:nFail) },
    @{t = 'space' }
)
$curCat = ""; $rows = $null
foreach ($r in $script:res) {
    if ($r.Cat -ne $curCat) {
        if ($rows -and $rows.Count) { $content += @{t = 'table'; cols = @(@{x = 'Setting'; w = 5200 }, @{x = 'Actual'; w = 2800 }, @{x = 'Expected'; w = 1600 }, @{x = 'Status'; w = 1200 }); rows = $rows } }
        $curCat = $r.Cat; $content += @{t = 'head'; x = $curCat }; $rows = @()
    }
    $stt = switch ($r.State) { "OK" { @{x = 'OK'; c = '1E7A34'; b = $true } } "WARN" { @{x = '!'; c = 'B45309'; b = $true } } default { @{x = 'X'; c = 'C00000'; b = $true } } }
    $ex = if ($r.State -eq 'OK') { '' } else { $r.Exp }
    $rows += , @("$($r.Name)", "$($r.Act)", "$ex", $stt)
}
if ($rows -and $rows.Count) { $content += @{t = 'table'; cols = @(@{x = 'Setting'; w = 5200 }, @{x = 'Actual'; w = 2800 }, @{x = 'Expected'; w = 1600 }, @{x = 'Status'; w = 1200 }); rows = $rows } }
try {
    New-DocxReport -Path $rep -Content $content
    Write-Host ""
    Write-Host ("Report saved: " + $rep) -ForegroundColor Green
    try { Start-Process $rep } catch {}
} catch { Write-Host ("Could not save the report: " + $_.Exception.Message) -ForegroundColor Red }
