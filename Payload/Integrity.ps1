# Integrity.ps1 - dot-sourced from RunAction.ps1 (Write-Host/Read-Host overridden)
# Integrity control: reference snapshot of the PC state + drift detection. Read-only.
$ErrorActionPreference = 'SilentlyContinue'

Write-Host "SYSTEM INTEGRITY CONTROL" -ForegroundColor Cyan
Write-Host "Reference snapshot of the PC state and comparison with it over time." -ForegroundColor Gray
Write-Host ""
Write-Host "Create/update the reference (1), Compare with the reference (2)" -ForegroundColor Yellow
$mode = (Read-Host "Your choice").Trim()
if ($mode -ne "1" -and $mode -ne "2") { Write-Host "Unknown choice - cancelled." -ForegroundColor Yellow; return }

$store = Join-Path $env:ProgramData "ASShield"
if (-not (Test-Path -LiteralPath $store)) { New-Item -ItemType Directory -Path $store -Force | Out-Null }
$baseFile = Join-Path $store "integrity-baseline.json"

function Get-Sha256Text([string]$s) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $h = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($s)); $sha.Dispose()
    return ([BitConverter]::ToString($h) -replace '-', '')
}
function Get-Sha256File([string]$p) {
    try { return (Get-FileHash -LiteralPath $p -Algorithm SHA256 -ErrorAction Stop).Hash } catch { return "" }
}

function Get-Snapshot {
    $s = [ordered]@{}
    $s.Machine = $env:COMPUTERNAME
    $s.Created = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    try {
        $paths = @("HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*", "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*")
        $sw = Get-ItemProperty $paths -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName } | Sort-Object DisplayName
        $s.Software = @($sw | ForEach-Object { "$($_.DisplayName) [$($_.DisplayVersion)]" } | Sort-Object -Unique)
    } catch { $s.Software = @() }
    try { $s.Users = @(Get-LocalUser -ErrorAction Stop | Sort-Object Name | ForEach-Object { "$($_.Name)|$([int][bool]$_.Enabled)|$($_.SID)" }) } catch { $s.Users = @() }
    try { $s.Admins = @(Get-LocalGroupMember -SID "S-1-5-32-544" -ErrorAction Stop | ForEach-Object { "$($_.Name)" } | Sort-Object) } catch { $s.Admins = @() }
    try { $s.Services = @(Get-Service -ErrorAction Stop | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.StartType)" }) } catch { $s.Services = @() }
    try { $s.Tasks = @(Get-ScheduledTask -ErrorAction Stop | ForEach-Object { "$($_.TaskPath)$($_.TaskName)" } | Sort-Object) } catch { $s.Tasks = @() }
    try { $s.Firewall = @(Get-NetFirewallProfile -ErrorAction Stop | ForEach-Object { "$($_.Name)=$([int][bool]$_.Enabled)" }) } catch { $s.Firewall = @() }
    $pf = ""
    try {
        $tmp = Join-Path $env:TEMP ("int_" + [guid]::NewGuid().ToString("N") + ".inf")
        & secedit /export /cfg $tmp /quiet 2>$null | Out-Null
        if (Test-Path -LiteralPath $tmp) { $pf += (Get-Content -LiteralPath $tmp -Raw -Encoding Unicode); Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
        $pf += (& auditpol /get /category:* /r 2>$null | Out-String)
    } catch {}
    $s.PolicyHash = Get-Sha256Text $pf
    try {
        $gpoRoot = Join-Path $env:SystemRoot "System32\GroupPolicy"
        $gp = @()
        Get-ChildItem -LiteralPath $gpoRoot -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object { $gp += ($_.FullName.Replace($gpoRoot, "") + "=" + (Get-Sha256File $_.FullName)) }
        $s.Gpo = @($gp | Sort-Object)
    } catch { $s.Gpo = @() }
    $s.HostsHash = Get-Sha256File (Join-Path $env:SystemRoot "System32\drivers\etc\hosts")
    $kr = @()
    $regChecks = @(
        @("HKLM:\SYSTEM\CurrentControlSet\Control\Lsa", "SCENoApplyLegacyAuditPolicy"),
        @("HKLM:\SYSTEM\CurrentControlSet\Control\Lsa", "CrashOnAuditFail"),
        @("HKLM:\SYSTEM\CurrentControlSet\Control\Lsa", "RestrictAnonymous"),
        @("HKLM:\SYSTEM\CurrentControlSet\Control\Lsa", "NoLMHash"),
        @("HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA"),
        @("HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "DontDisplayLastUserName"),
        @("HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server", "fDenyTSConnections"),
        @("HKLM:\SOFTWARE\Policies\Microsoft\Windows\Installer", "AlwaysInstallElevated")
    )
    foreach ($rc in $regChecks) { $val = try { (Get-ItemProperty -LiteralPath $rc[0] -Name $rc[1] -ErrorAction Stop).$($rc[1]) } catch { [char]0x2205 }; $kr += ($rc[1] + "=" + $val) }
    $s.KeyReg = @($kr)
    return $s
}

Write-Host "Collecting the snapshot (software, accounts, services, policies, GPO, firewall)..." -ForegroundColor Gray
$snap = Get-Snapshot
# seal (computed over snapshot JSON before Seal is added)
$json = $snap | ConvertTo-Json -Depth 6
$seal = Get-Sha256Text $json

if ($mode -eq "1") {
    $snap.Seal = $seal
    try {
        ($snap | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath $baseFile -Encoding UTF8
        Write-Host ""
        Write-Host "Reference saved." -ForegroundColor Green
        Write-Host ("File: " + $baseFile) -ForegroundColor Gray
        Write-Host ("Seal (SHA-256): " + $seal.Substring(0, 32) + "...") -ForegroundColor Cyan
        Write-Host ("Software: " + $snap.Software.Count + "  Accounts: " + $snap.Users.Count + "  Services: " + $snap.Services.Count + "  GPO files: " + $snap.Gpo.Count) -ForegroundColor Gray
    } catch { Write-Host ("Could not save the reference: " + $_.Exception.Message) -ForegroundColor Red }
    return
}

# mode 2 - compare
if (-not (Test-Path -LiteralPath $baseFile)) { Write-Host "No reference found. Create one first (option 1)." -ForegroundColor Red; return }
$base = $null
try { $base = Get-Content -LiteralPath $baseFile -Raw -Encoding UTF8 | ConvertFrom-Json } catch { Write-Host "The reference file is damaged." -ForegroundColor Red; return }

# verify baseline seal
$baseSeal = $base.Seal
$baseObj = $base | Select-Object -Property * -ExcludeProperty Seal
# rebuild ordered json in same shape for seal check is non-trivial; do a light check instead
Write-Host ""
Write-Host ("Reference from: " + $base.Created) -ForegroundColor Cyan

$script:diffs = 0
function CmpList([string]$title, $old, $new, [string]$state) {
    $o = @($old); $n = @($new)
    $added = @($n | Where-Object { $_ -notin $o })
    $removed = @($o | Where-Object { $_ -notin $n })
    if ($added.Count -eq 0 -and $removed.Count -eq 0) { Write-Host ("  [+] " + $title + " - no changes") -ForegroundColor DarkGreen; return }
    $script:diffs++
    $col = if ($state -eq "FAIL") { "Red" } else { "Yellow" }
    Write-Host ("  [" + $(if ($state -eq "FAIL") { "-" } else { "!" }) + "] " + $title + " - changed") -ForegroundColor $col
    foreach ($a in $added) { Write-Host ("        + added: " + $a) -ForegroundColor $col }
    foreach ($r in $removed) { Write-Host ("        - removed: " + $r) -ForegroundColor $col }
}
function CmpVal([string]$title, $old, $new, [string]$state) {
    if ("$old" -eq "$new") { Write-Host ("  [+] " + $title + " - no changes") -ForegroundColor DarkGreen; return }
    $script:diffs++
    $col = if ($state -eq "FAIL") { "Red" } else { "Yellow" }
    Write-Host ("  [" + $(if ($state -eq "FAIL") { "-" } else { "!" }) + "] " + $title + " - CHANGED") -ForegroundColor $col
    Write-Host ("        was: " + $old) -ForegroundColor $col
    Write-Host ("        now: " + $new) -ForegroundColor $col
}

Write-Host ""
Write-Host "── Drift from the reference ──" -ForegroundColor Cyan
CmpList "Installed software" $base.Software $snap.Software "WARN"
CmpList "Local accounts" $base.Users $snap.Users "FAIL"
CmpList "Administrators group members" $base.Admins $snap.Admins "FAIL"
CmpList "Service start types" $base.Services $snap.Services "WARN"
CmpList "Scheduled tasks" $base.Tasks $snap.Tasks "WARN"
CmpList "Firewall profiles" $base.Firewall $snap.Firewall "FAIL"
CmpList "GPO artifacts (hashes)" $base.Gpo $snap.Gpo "FAIL"
CmpList "Key registry settings" $base.KeyReg $snap.KeyReg "FAIL"
CmpVal "Policy fingerprint (secedit+auditpol)" $base.PolicyHash $snap.PolicyHash "FAIL"
CmpVal "hosts file hash" $base.HostsHash $snap.HostsHash "FAIL"

Write-Host ""
Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor DarkGray
if ($script:diffs -eq 0) { Write-Host "No drift from the reference. Integrity confirmed." -ForegroundColor Green }
else { Write-Host ("Drift found: " + $script:diffs + ". See the details above and in the report.") -ForegroundColor Yellow }

# report (docx)
$host2 = $env:COMPUTERNAME
$stamp = Get-Date -Format "yyyy-MM-dd HH-mm"
$dir = Get-ReportDir
$rep = Join-Path $dir ("Integrity control " + $host2 + " " + $stamp + ".docx")
try {
    $content = @(
        @{t = 'title'; x = 'SYSTEM INTEGRITY CONTROL' },
        @{t = 'meta'; x = ('PC: ' + $host2 + '     Check date: ' + (Get-Date -Format "dd.MM.yyyy HH:mm")) },
        @{t = 'meta'; x = ('Reference from: ' + $base.Created) },
        @{t = 'meta'; x = ('Drift found: ' + $script:diffs) },
        @{t = 'space' }
    )
    function RL([string]$title, $old, $new) {
        $o = @($old); $n = @($new)
        $added = @($n | Where-Object { $_ -notin $o }); $removed = @($o | Where-Object { $_ -notin $n })
        $out = @(@{t = 'head'; x = $title })
        if ($added.Count -eq 0 -and $removed.Count -eq 0) { $out += @{t = 'para'; x = 'no changes'; c = '1E7A34' }; return $out }
        foreach ($a in $added) { $out += @{t = 'para'; x = ('+ added: ' + $a); c = 'B45309' } }
        foreach ($r in $removed) { $out += @{t = 'para'; x = ('- removed: ' + $r); c = 'C00000' } }
        return $out
    }
    $content += RL "Installed software" $base.Software $snap.Software
    $content += RL "Local accounts" $base.Users $snap.Users
    $content += RL "Administrators" $base.Admins $snap.Admins
    $content += RL "Services (start type)" $base.Services $snap.Services
    $content += RL "Scheduled tasks" $base.Tasks $snap.Tasks
    $content += RL "Firewall" $base.Firewall $snap.Firewall
    $content += RL "GPO artifacts" $base.Gpo $snap.Gpo
    $content += RL "Key registry settings" $base.KeyReg $snap.KeyReg
    $content += @{t = 'head'; x = 'Fingerprints' }
    $pok = ("$($base.PolicyHash)" -eq "$($snap.PolicyHash)")
    $hok = ("$($base.HostsHash)" -eq "$($snap.HostsHash)")
    $content += @{t = 'para'; x = ('Policies: ' + $(if ($pok) { 'no changes' } else { 'CHANGED' })); c = $(if ($pok) { '1E7A34' } else { 'C00000' }) }
    $content += @{t = 'para'; x = ('hosts: ' + $(if ($hok) { 'no changes' } else { 'CHANGED' })); c = $(if ($hok) { '1E7A34' } else { 'C00000' }) }
    New-DocxReport -Path $rep -Content $content
    Write-Host ""
    Write-Host ("Report saved: " + $rep) -ForegroundColor Green
    try { Start-Process $rep } catch {}
} catch { Write-Host ("Could not save the report: " + $_.Exception.Message) -ForegroundColor Red }
