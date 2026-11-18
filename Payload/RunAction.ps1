param([Parameter(Mandatory=$true)][string]$Action, [string]$Build)
$utf8 = New-Object System.Text.UTF8Encoding $false
try { [Console]::OutputEncoding = $utf8 } catch { }
try { [Console]::InputEncoding = $utf8 } catch { }
$OutputEncoding = $utf8
Set-Location -Path $PSScriptRoot
try { . (Join-Path $PSScriptRoot 'Report.ps1') } catch {}

# ───────────── Built-in AS Shield console ─────────────
# Output and questions go to the program window instead of a console.
function script:Send-Gui([string]$m) { [Console]::Out.WriteLine([string][char]1 + $m); [Console]::Out.Flush() }
function script:Send-Progress([int]$Percent, [string]$Label) { Send-Gui ('G|' + $Percent + '|' + (ConvertTo-GuiB64 ([string]$Label))) }
function script:Hide-Progress { Send-Gui 'G|-1|' }
function script:ConvertTo-GuiB64($s) { [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes([string]$s)) }

function script:Write-Host {
    param(
        [Parameter(Position = 0, ValueFromPipeline = $true, ValueFromRemainingArguments = $true)] $Object,
        [switch] $NoNewline,
        $Separator = ' ',
        [System.ConsoleColor] $ForegroundColor,
        [System.ConsoleColor] $BackgroundColor
    )
    process {
        if ($null -eq $Object) { $t = '' }
        elseif ($Object -is [System.Collections.IEnumerable] -and $Object -isnot [string]) { $t = (@($Object) | ForEach-Object { "$_" }) -join "$Separator" }
        else { $t = "$Object" }
        $fg = ''
        if ($PSBoundParameters.ContainsKey('ForegroundColor')) { $fg = "$ForegroundColor" }
        $nn = '0'; if ($NoNewline) { $nn = '1' }
        Send-Gui ('H|' + $fg + '|' + $nn + '|' + (ConvertTo-GuiB64 $t))
    }
}

function script:Write-Warning {
    param([Parameter(Position = 0, ValueFromPipeline = $true)] [string] $Message)
    process { Send-Gui ('H|Yellow|0|' + (ConvertTo-GuiB64 ("WARNING: " + $Message))) }
}

function script:Read-Host {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)] $Prompt,
        [switch] $AsSecureString,
        [switch] $MaskInput
    )
    $p = ''
    if ($null -ne $Prompt) { $p = (@($Prompt) | ForEach-Object { "$_" }) -join ' ' }
    $sec = '0'; if ($AsSecureString -or $MaskInput) { $sec = '1' }
    Send-Gui ('A|' + $sec + '|' + (ConvertTo-GuiB64 $p))
    $a = [Console]::In.ReadLine()
    if ($null -eq $a) { exit 2 }
    if ($AsSecureString) { return (ConvertTo-SecureString $a -AsPlainText -Force) }
    return $a
}

function script:pause { Send-Gui 'P'; $null = [Console]::In.ReadLine() }
function script:Clear-Host { Send-Gui 'C' }

foreach ($folder in 'Private\Core', 'Private\Policies') {
    $path = Join-Path $PSScriptRoot $folder
    if (Test-Path $path) {
        foreach ($file in (Get-ChildItem "$path\*.ps1")) {
            try { . $file.FullName } catch { Write-Host "Failed to load $($file.Name): $_" -ForegroundColor Red }
        }
    }
}

# Stubs: the GUI version does not need a console main menu
function Start-Menu { }
function Script-end { exit 0 }
Set-Item -Path 'Function:\End' -Value { }



# ═══════════════ Additional AS Shield tools ═══════════════

# ───────────── Removing the policy "tattoo" left by the registry.pol files of the profiles ─────────────
# Parses binary registry.pol files (PReg) and removes exactly the values/keys the profiles set.
function Get-PolEntries([string]$path) {
    $res = @()
    try { $b = [System.IO.File]::ReadAllBytes($path) } catch { return $res }
    if ($b.Length -lt 8 -or $b[0] -ne 0x50 -or $b[1] -ne 0x52) { return $res }  # 'PR'
    $i = 8
    $ReadStr = {
        param([ref]$pos)
        $sb = New-Object System.Text.StringBuilder
        while ($pos.Value -lt $b.Length - 1) {
            $code = $b[$pos.Value] -bor ($b[$pos.Value + 1] -shl 8); $pos.Value += 2
            if ($code -eq 0x3B) { break }
            if ($code -eq 0) { continue }
            [void]$sb.Append([char]$code)
        }
        return $sb.ToString()
    }
    while ($i -lt $b.Length - 1) {
        if (-not ($b[$i] -eq 0x5B -and $b[$i + 1] -eq 0)) { $i += 2; continue }
        $i += 2
        $ref = [ref]$i
        $key = & $ReadStr $ref
        $val = & $ReadStr $ref
        $i = $ref.Value
        if ($i + 4 -le $b.Length) { $i += 4 }
        if ($i + 1 -lt $b.Length -and $b[$i] -eq 0x3B) { $i += 2 }
        $size = 0
        if ($i + 4 -le $b.Length) { $size = [BitConverter]::ToInt32($b, $i); $i += 4 }
        if ($i + 1 -lt $b.Length -and $b[$i] -eq 0x3B) { $i += 2 }
        if ($size -gt 0 -and $i + $size -le $b.Length) { $i += $size }
        if ($i + 1 -lt $b.Length -and $b[$i] -eq 0x5D) { $i += 2 }
        if ($key) { $res += (New-Object psobject -Property @{ Key = $key.TrimEnd([char]0); Value = $val.TrimEnd([char]0) }) }
    }
    return $res
}

# Apply the policy removal to one registry root ($base: 'HKLM:' or 'Registry::HKEY_USERS\<SID>')
function Clear-TattooAt([string]$base, $fullKeys, $valMap, [ref]$cnt) {
    foreach ($k in $fullKeys) {
        $p = $base + '\' + $k
        try { if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction SilentlyContinue; $cnt.Value++ } } catch { }
    }
    foreach ($k in $valMap.Keys) {
        $p = $base + '\' + $k
        if (-not (Test-Path -LiteralPath $p)) { continue }
        foreach ($v in $valMap[$k]) {
            if ([string]::IsNullOrEmpty($v)) { continue }
            if ($v.StartsWith('**')) { continue }   # PReg directives (**del./**delvals.) are not values
            try { Remove-ItemProperty -LiteralPath $p -Name $v -Force -ErrorAction SilentlyContinue; $cnt.Value++ } catch { }
        }
    }
}

# Remove the policy "tattoo" of every profile: machine + all user profiles (loaded and offline NTUSER.DAT)
function Reset-PolicyTattoo([string]$configRoot) {
    $roots = @((Join-Path $configRoot 'Configs'), (Join-Path $configRoot 'Profiles'), (Get-ASLocalProfileRoot)) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
    if (-not $roots) { Write-Host "  no Configs/Profiles folders found - skipped" -ForegroundColor Yellow; return }

    $mFull = New-Object 'System.Collections.Generic.HashSet[string]'
    $uFull = New-Object 'System.Collections.Generic.HashSet[string]'
    $mVal = @{}; $uVal = @{}
    foreach ($f in (Get-ChildItem -LiteralPath $roots -Recurse -Filter 'registry.pol' -ErrorAction SilentlyContinue)) {
        $isM = $f.FullName -match '[\\/]Machine[\\/]'
        foreach ($e in (Get-PolEntries $f.FullName)) {
            $k = $e.Key.Trim('\'); if (-not $k) { continue }
            if ($k -like 'Software\Policies\*') {
                if ($isM) { [void]$mFull.Add($k) } else { [void]$uFull.Add($k) }
            } else {
                $tbl = $mVal; if (-not $isM) { $tbl = $uVal }
                if (-not $tbl.ContainsKey($k)) { $tbl[$k] = New-Object 'System.Collections.Generic.HashSet[string]' }
                [void]$tbl[$k].Add($e.Value)
            }
        }
    }

    $c = 0; $ref = [ref]$c
    Clear-TattooAt 'HKLM:' $mFull $mVal $ref
    Write-Host ("  machine: policies removed - {0}" -f $c) -ForegroundColor Green

    # User profiles
    $sids = @()
    try { Get-ChildItem 'Registry::HKEY_USERS' -ErrorAction SilentlyContinue | ForEach-Object {
        $n = $_.PSChildName
        if ($n -match '^S-1-5-21-[\d-]+$') { $sids += $n }
    } } catch { }
    $loaded = @{}; foreach ($s in $sids) { $loaded[$s] = $true }

    $uc = 0; $uref = [ref]$uc
    foreach ($s in $sids) { Clear-TattooAt ("Registry::HKEY_USERS\" + $s) $uFull $uVal $uref }

    # Offline profiles (NTUSER.DAT not loaded)
    $pl = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList'
    try {
        foreach ($p in (Get-ChildItem -LiteralPath $pl -ErrorAction SilentlyContinue)) {
            $sid = $p.PSChildName
            if ($sid -notmatch '^S-1-5-21-[\d-]+$' -or $loaded.ContainsKey($sid)) { continue }
            $img = (Get-ItemProperty -LiteralPath $p.PSPath -ErrorAction SilentlyContinue).ProfileImagePath
            if (-not $img) { continue }
            $dat = Join-Path $img 'NTUSER.DAT'
            if (-not (Test-Path -LiteralPath $dat)) { continue }
            $tmp = 'ASRESET_' + $sid
            $null = & reg.exe load "HKU\$tmp" "$dat" 2>&1
            if ($LASTEXITCODE -ne 0) { continue }
            try { Clear-TattooAt ("Registry::HKEY_USERS\" + $tmp) $uFull $uVal $uref }
            finally {
                [gc]::Collect(); [gc]::WaitForPendingFinalizers()
                for ($r = 0; $r -lt 3; $r++) { $null = & reg.exe unload "HKU\$tmp" 2>&1; if ($LASTEXITCODE -eq 0) { break }; Start-Sleep -Milliseconds 300; [gc]::Collect() }
            }
        }
    } catch { }
    Write-Host ("  user profiles: policies removed - {0}" -f $uc) -ForegroundColor Green
}

function Invoke-FactoryRestore {
    Write-Host "Resetting security policies and services to Windows defaults" -ForegroundColor Cyan
    Write-Host "──────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host "WARNING: every applied security setting (policies, AppLocker, services) will be reset." -ForegroundColor Yellow
    if ((Read-Host "Confirm (Y/N)").ToUpper() -ne 'Y') { Write-Host "Cancelled." -ForegroundColor Yellow; return }

    Write-Host "`n[1/6] Removing local group policies..." -ForegroundColor Cyan
    Remove-Item "$env:SystemRoot\System32\GroupPolicy" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item "$env:SystemRoot\System32\GroupPolicyUsers" -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  done" -ForegroundColor Green

    Write-Host "[2/6] Resetting security settings to defaults (secedit)..." -ForegroundColor Cyan
    $inf = Join-Path $PSScriptRoot 'Configs\Defaults\Windows-Default-Security.inf'
    if (Test-Path -LiteralPath $inf) {
        secedit /configure /db "$env:TEMP\asreset.sdb" /cfg "$inf" /overwrite /quiet
        Write-Host "  done (code $LASTEXITCODE)" -ForegroundColor Green
    } else { Write-Host "  default INF not found - skipped" -ForegroundColor Yellow }

    Write-Host "[3/6] Removing the policy tattoo from the registry (based on the profiles' registry.pol)..." -ForegroundColor Cyan
    try { Reset-PolicyTattoo $PSScriptRoot } catch { Write-Host "  error: $($_.Exception.Message)" -ForegroundColor Yellow }

    Write-Host "[4/6] Clearing the AppLocker rules..." -ForegroundColor Cyan
    try {
        $clear = '<AppLockerPolicy Version="1"><RuleCollection Type="Appx" EnforcementMode="NotConfigured"/><RuleCollection Type="Dll" EnforcementMode="NotConfigured"/><RuleCollection Type="Exe" EnforcementMode="NotConfigured"/><RuleCollection Type="Msi" EnforcementMode="NotConfigured"/><RuleCollection Type="Script" EnforcementMode="NotConfigured"/></AppLockerPolicy>'
        $tmp = Join-Path $env:TEMP 'as_applocker_clear.xml'
        Set-Content -LiteralPath $tmp -Value $clear -Encoding UTF8
        Set-AppLockerPolicy -XmlPolicy $tmp -ErrorAction Stop
        try { sc.exe config AppIDSvc start= demand | Out-Null } catch { }
        Write-Host "  AppLocker rules cleared" -ForegroundColor Green
    } catch { Write-Host "  AppLocker: $($_.Exception.Message)" -ForegroundColor Yellow }

    Write-Host "[5/6] Restoring service start types..." -ForegroundColor Cyan
    try {
        $svcStep = [pscustomobject]@{ id = 'default-services'; type = 'services'; _dir = (Join-Path $PSScriptRoot 'Catalog') }
        $c = [pscustomobject]@{ PayloadRoot = $PSScriptRoot; DataDir = (Get-ASDataDir); Os = ''; Build = 0; ProfileId = 'restore'; LastBackup = $null }
        $r = & $script:ASSteps['services'] $svcStep ([pscustomobject]@{ file = 'default-services.csv' }) $c
        Write-Host ("  {0}  {1}" -f $r.Status, $r.Detail) -ForegroundColor $(if ($r.Status -eq 'OK') { 'Green' } else { 'Yellow' })
    } catch { Write-Host "  services: $($_.Exception.Message)" -ForegroundColor Yellow }

    Write-Host "[6/6] Applying the changes (gpupdate)..." -ForegroundColor Cyan
    try { gpupdate /force | Out-Null } catch { }
    Write-Host "`nReset finished." -ForegroundColor Green
    if ((Read-Host "A restart is recommended. Restart now? (Y/N)").ToUpper() -eq 'Y') {
        Write-Host "Restarting in 5 seconds..." -ForegroundColor Yellow
        shutdown /r /t 5 /c "AS Shield: policy reset"
    }
}

function Enable-Priv([string]$name) {
    try {
        if (-not ('ASPriv' -as [type])) {
            Add-Type -Namespace '' -Name 'ASPriv' -MemberDefinition @'
[DllImport("advapi32.dll", SetLastError=true)] static extern bool OpenProcessToken(IntPtr h, int acc, out IntPtr tok);
[DllImport("advapi32.dll", SetLastError=true)] static extern bool LookupPrivilegeValue(string host, string name, out long luid);
[DllImport("advapi32.dll", SetLastError=true)] static extern bool AdjustTokenPrivileges(IntPtr tok, bool dis, ref TOKPRIV1LUID newst, int len, IntPtr prev, IntPtr relen);
[DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
[StructLayout(LayoutKind.Sequential, Pack=1)] public struct TOKPRIV1LUID { public int Count; public long Luid; public int Attr; }
public static void Enable(string priv) { IntPtr tok; OpenProcessToken(GetCurrentProcess(), 0x28, out tok); TOKPRIV1LUID tp; tp.Count = 1; tp.Attr = 2; LookupPrivilegeValue(null, priv, out tp.Luid); AdjustTokenPrivileges(tok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero); }
'@ -ErrorAction Stop
        }
        [ASPriv]::Enable($name)
    } catch { }
}

function Take-OwnRecurse($hive, [string]$sub) {
    try {
        $rights  = [System.Security.AccessControl.RegistryRights]
        $admins  = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-32-544')
        # 1. take ownership of the key
        $k = $hive.OpenSubKey($sub, [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree, $rights::TakeOwnership)
        if ($k) {
            $o = $k.GetAccessControl([System.Security.AccessControl.AccessControlSections]::None)
            $o.SetOwner($admins); $k.SetAccessControl($o); $k.Close()
        }
        # 2. give Administrators full control
        $k2 = $hive.OpenSubKey($sub, [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree, ($rights::ChangePermissions -bor $rights::ReadPermissions))
        if ($k2) {
            $a = $k2.GetAccessControl()
            $a.AddAccessRule((New-Object System.Security.AccessControl.RegistryAccessRule($admins, 'FullControl', 'ContainerInherit', 'None', 'Allow')))
            $k2.SetAccessControl($a); $k2.Close()
        }
        # 3. the same for the subkeys
        $kr = $hive.OpenSubKey($sub)
        if ($kr) { foreach ($c in $kr.GetSubKeyNames()) { Take-OwnRecurse $hive ($sub + '\' + $c) }; $kr.Close() }
    } catch { }
}

function Get-RegHive([string]$name) {
    if ($name -eq 'HKLM') { return [Microsoft.Win32.Registry]::LocalMachine } else { return [Microsoft.Win32.Registry]::CurrentUser }
}

function Remove-RegForce([string]$hiveName, [string]$sub) {
    $psPath = "${hiveName}:\$sub"
    try { Remove-Item -LiteralPath $psPath -Recurse -Force -ErrorAction Stop; return $true } catch { }
    try {
        Enable-Priv 'SeTakeOwnershipPrivilege'; Enable-Priv 'SeRestorePrivilege'
        Take-OwnRecurse (Get-RegHive $hiveName) $sub
        Remove-Item -LiteralPath $psPath -Recurse -Force -ErrorAction Stop; return $true
    } catch { return $false }
}

function Get-CurrentUsbIds {
    $ids = New-Object System.Collections.Generic.List[string]
    function AddId($v) { if ($v) { $t = "$v".Trim(); if ($t.Length -ge 3 -and $ids -notcontains $t) { $ids.Add($t) } } }
    try {
        Get-CimInstance Win32_DiskDrive -Filter "InterfaceType='USB'" -ErrorAction Stop | ForEach-Object {
            AddId $_.SerialNumber
            if ($_.PNPDeviceID) { $leaf = ($_.PNPDeviceID -split '\\')[-1]; AddId $leaf; AddId (($leaf -split '&')[0]) }
        }
    } catch { }
    try {
        Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -match 'USBSTOR|WPDBUSENUM' } | ForEach-Object {
            $leaf = ($_.InstanceId -split '\\')[-1]; AddId $leaf; AddId (($leaf -split '&')[0])
        }
    } catch { }
    try {
        Get-CimInstance Win32_Volume -Filter "DriveType=2" -ErrorAction SilentlyContinue | ForEach-Object {
            if ($_.DeviceID -match '\{[0-9a-fA-F-]+\}') { AddId $matches[0] }
        }
    } catch { }
    return $ids
}

function Test-Protected([string]$name, $ids) {
    foreach ($id in $ids) { if ($name -like "*$id*") { return $true } }
    return $false
}

function Invoke-UsbClean {
    Write-Host "Cleaning the traces of OLD (disconnected) USB drives ONLY" -ForegroundColor Cyan
    Write-Host "──────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host "Devices connected now are NOT touched - keep the drives you use plugged in." -ForegroundColor Green

    $script:usbIds = Get-CurrentUsbIds
    if ($script:usbIds.Count -gt 0) { Write-Host ("Connected devices found (excluded from the cleanup): " + $script:usbIds.Count) -ForegroundColor Green }
    else { Write-Host "No connected USB drives found - the whole history will be cleaned." -ForegroundColor Yellow }
    if ((Read-Host "Continue? (Y/N)").ToUpper() -ne 'Y') { Write-Host "Cancelled." -ForegroundColor Yellow; return }

    $script:removed = 0; $script:kept = 0; $script:skipped = 0

    # ── A. Remove ghost devices in Device Manager (Storage volumes, USBSTOR, WPD) ──
    # Only devices NOT connected now (Present = False). Connected ones are not touched.
    Write-Host "`nGhost devices in Device Manager (not connected):" -ForegroundColor Cyan
    $ghosts = @()
    try {
        $ghosts = Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object {
            (-not $_.Present) -and ($_.InstanceId -match '^(STORAGE\VOLUME|USBSTOR|SWD\WPDBUSENUM)')
        }
    } catch { }
    if (-not $ghosts -or $ghosts.Count -eq 0) {
        Write-Host "  no ghost devices found" -ForegroundColor DarkGray
    } else {
        foreach ($g in $ghosts) {
            $nm = if ($g.FriendlyName) { $g.FriendlyName } else { $g.InstanceId }
            $out = & pnputil /remove-device "$($g.InstanceId)" 2>&1
            if ($LASTEXITCODE -eq 0) { Write-Host "  removed: $nm" -ForegroundColor Gray; $script:removed++ }
            else {
                $sub = 'SYSTEM\CurrentControlSet\Enum' + $g.InstanceId
                if (Remove-RegForce 'HKLM' $sub) { Write-Host "  removed (registry): $nm" -ForegroundColor Gray; $script:removed++ }
                else { Write-Host "  skipped: $nm" -ForegroundColor Yellow; $script:skipped++ }
            }
        }
    }

    function Clean-PerDevice([string]$hiveName, [string]$sub, [string]$label, [int]$depth) {
        $hive = Get-RegHive $hiveName
        $root = $hive.OpenSubKey($sub)
        if (-not $root) { Write-Host "  $label - no records" -ForegroundColor DarkGray; return }
        Write-Host "`n${label}:" -ForegroundColor Cyan
        $targets = @()
        if ($depth -eq 2) {
            foreach ($model in $root.GetSubKeyNames()) {
                $mk = $hive.OpenSubKey("$sub\$model")
                if ($mk) { foreach ($inst in $mk.GetSubKeyNames()) { $targets += @{ Rel = "$sub\$model\$inst"; Leaf = $inst } }; $mk.Close() }
            }
        } else {
            foreach ($n in $root.GetSubKeyNames()) { $targets += @{ Rel = "$sub\$n"; Leaf = $n } }
        }
        $root.Close()
        if ($targets.Count -eq 0) { Write-Host "  (empty)" -ForegroundColor DarkGray; return }
        foreach ($t in $targets) {
            if (Test-Protected $t.Leaf $script:usbIds) { Write-Host "  kept (in use): $($t.Leaf)" -ForegroundColor Green; $script:kept++; continue }
            if (Remove-RegForce $hiveName $t.Rel) { Write-Host "  removed old: $($t.Leaf)" -ForegroundColor Gray; $script:removed++ }
            else { Write-Host "  skipped (access denied): $($t.Leaf)" -ForegroundColor Yellow; $script:skipped++ }
        }
    }

    Write-Host "`nRegistry traces:" -ForegroundColor Cyan
    Clean-PerDevice 'HKLM' 'SYSTEM\CurrentControlSet\Enum\USBSTOR' 'USBSTOR (drives)' 2
    Clean-PerDevice 'HKLM' 'SYSTEM\CurrentControlSet\Enum\SWD\WPDBUSENUM' 'WPDBUSENUM' 1
    Clean-PerDevice 'HKLM' 'SOFTWARE\Microsoft\Windows Portable Devices\Devices' 'Windows Portable Devices' 1
    Clean-PerDevice 'HKCU' 'Software\Microsoft\Windows\CurrentVersion\Explorer\MountPoints2' 'MountPoints2 (current user)' 1
    Clean-PerDevice 'HKLM' 'SOFTWARE\Microsoft\Windows NT\CurrentVersion\EMDMgmt' 'EMDMgmt (ReadyBoost)' 1

    Write-Host ""
    if ((Read-Host "Also clear the setup logs (setupapi)? They list every device, including connected ones. (Y/N)").ToUpper() -eq 'Y') {
        foreach ($log in "setupapi.dev.log", "setupapi.setup.log") {
            $lp = Join-Path $env:windir "inf\$log"
            if (Test-Path -LiteralPath $lp) { try { Remove-Item -LiteralPath $lp -Force -ErrorAction Stop; Write-Host "  $log - removed" -ForegroundColor Gray; $script:removed++ } catch { Write-Host "  $log - in use" -ForegroundColor Yellow; $script:skipped++ } }
        }
    } else { Write-Host "  setupapi logs left untouched" -ForegroundColor DarkGray }

    Write-Host "`n──────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host ("Old records removed: $($script:removed)   ·   connected kept: $($script:kept)   ·   skipped: $($script:skipped)") -ForegroundColor Green
    if ($script:removed -eq 0 -and $script:kept -gt 0 -and $script:skipped -eq 0) {
        Write-Host "No old (disconnected) devices found - the history only contains devices connected now." -ForegroundColor Yellow
    }
    Write-Host "MountedDevices intentionally left untouched - Windows rebuilds it itself." -ForegroundColor DarkGray
}

function Invoke-LicenseScan {
    Write-Host "Windows and Microsoft Office license check" -ForegroundColor Cyan
    Write-Host "──────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
    $map = @{ 0='Unlicensed'; 1='Licensed'; 2='Initial grace period'; 3='Additional grace period'; 4='Non-genuine grace period'; 5='Notification (not activated)'; 6='Extended grace period' }
    $rep = New-Object System.Collections.Generic.List[string]
    function L($t, $c = 'Gray') { Write-Host $t -ForegroundColor $c; $rep.Add($t) }

    L "===== WINDOWS =====" 'Cyan'
    try { $os = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop; L ("OS: " + $os.Caption + " (" + $os.OSArchitecture + "), build " + $os.BuildNumber) } catch { }
    try {
        $win = Get-CimInstance SoftwareLicensingProduct -Filter "ApplicationID='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey IS NOT NULL" -ErrorAction Stop
        if ($win) { foreach ($p in $win) { L (" • " + $p.Name); L ("   Status: " + $map[[int]$p.LicenseStatus] + "  |  Key (partial): " + $p.PartialProductKey + "  |  Channel: " + $p.ProductKeyChannel) 'Green' } }
        else { L "   No active Windows licenses found." 'Yellow' }
    } catch { L ("   Query error: " + $_.Exception.Message) 'Red' }
    try { $sls = Get-CimInstance SoftwareLicensingService -ErrorAction Stop; if ($sls.OA3xOriginalProductKey) { L ("   OEM key (BIOS): " + $sls.OA3xOriginalProductKey) 'Green' } } catch { }

    L "" ; L "===== MICROSOFT OFFICE =====" 'Cyan'
    $found = $false
    try {
        $off = Get-CimInstance SoftwareLicensingProduct -Filter "PartialProductKey IS NOT NULL" -ErrorAction Stop | Where-Object { $_.Name -match 'Office|Microsoft 365|Visio|Project' -and $_.ApplicationID -ne '55c92734-d682-4d71-983e-d6ec3f16059f' }
        foreach ($p in $off) { $found = $true; L (" • " + $p.Name); L ("   Status: " + $map[[int]$p.LicenseStatus] + "  |  Key (partial): " + $p.PartialProductKey) 'Green' }
    } catch { }
    if (-not $found) {
        $dirs = @("$env:ProgramFiles\Microsoft Office", "${env:ProgramFiles(x86)}\Microsoft Office") | Where-Object { Test-Path $_ }
        $ospp = $dirs | ForEach-Object { Get-ChildItem $_ -Filter OSPP.VBS -Recurse -ErrorAction SilentlyContinue } | Select-Object -First 1
        if ($ospp) { L "   Querying via OSPP.VBS..."; foreach ($line in (cscript //Nologo "$($ospp.FullName)" /dstatus 2>&1)) { L ("   " + $line) }; $found = $true }
    }
    if (-not $found) { L "   Microsoft Office not found or no active licenses." 'Yellow' }

    $dir = Get-ReportDir
    $path = Join-Path $dir ("Software licenses " + $env:COMPUTERNAME + " " + (Get-Date -Format 'yyyy-MM-dd') + ".docx")
    try {
        $content = @(@{t = 'title'; x = 'SOFTWARE LICENSE REPORT' }, @{t = 'meta'; x = ('PC: ' + $env:COMPUTERNAME + '     Date: ' + (Get-Date -Format 'dd.MM.yyyy HH:mm')) }, @{t = 'space' })
        foreach ($ln in $rep) {
            if ($ln -match '^=====') { $content += @{t = 'head'; x = ($ln -replace '=', '').Trim() } }
            elseif ($ln.Trim() -eq '') { $content += @{t = 'space' } }
            else { $content += @{t = 'para'; x = $ln } }
        }
        New-DocxReport -Path $path -Content $content
        L ("`nReport saved: " + $path) 'Green'
    } catch { L ("Could not save the report: " + $_.Exception.Message) 'Red' }
}

function Get-DocText($file) {
    try {
        $ext = $file.Extension.ToLower()
        if ($ext -in '.txt', '.rtf', '.csv', '.inf', '.log') {
            if ($file.Length -gt 15MB) { return '' }
            return [IO.File]::ReadAllText($file.FullName)
        }
        if ($ext -in '.docx', '.xlsx', '.pptx', '.odt', '.ods') {
            if ($file.Length -gt 40MB) { return '' }
            Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
            $sb = New-Object System.Text.StringBuilder
            $zip = [IO.Compression.ZipFile]::OpenRead($file.FullName)
            try {
                foreach ($e in $zip.Entries) {
                    if ($e.FullName -match '\.(xml|txt)$' -and $e.Length -lt 8MB) {
                        $sr = New-Object IO.StreamReader($e.Open())
                        [void]$sb.Append($sr.ReadToEnd()); $sr.Close()
                    }
                }
            } finally { $zip.Dispose() }
            return $sb.ToString()
        }
    } catch { }
    return ''
}

function Invoke-DocScan {
    Write-Host "Search for documents containing sensitive phrases" -ForegroundColor Cyan
    Write-Host "Groups and phrases come from search_settings.json (editable)" -ForegroundColor DarkGray
    Write-Host "(the check only reads files and changes nothing)" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "Search scope:"
    Write-Host "  1 - user folders (fast)"
    Write-Host "  2 - all fixed drives (slow)"
    $scope = Read-Host "Your choice (1/2, default 1)"

    $drives = (Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3" -ErrorAction SilentlyContinue).DeviceID
    if (-not $drives) { $drives = @($env:SystemDrive) }
    if ($scope -eq '2') { $roots = $drives | ForEach-Object { "$_\" } }
    else {
        $roots = @("$env:SystemDrive\Users")
        $drives | Where-Object { $_ -ne $env:SystemDrive } | ForEach-Object { $roots += "$_\" }
    }

    $exts = @('.doc', '.docx', '.xls', '.xlsx', '.ppt', '.pptx', '.rtf', '.txt', '.odt', '.ods', '.pdf', '.csv')
    $skip = 'Windows\\|\\Program Files|ProgramData\\|\$Recycle|\\AppData\\|\\WinSxS\\|\\Microsoft\\|\\node_modules\\'
    # ── Phrase groups from search_settings.json (editable by the user) ──
    $groups = @()
    $cfgPath = Join-Path $PSScriptRoot 'search_settings.user.json'
    if (-not (Test-Path -LiteralPath $cfgPath)) { $cfgPath = Join-Path $PSScriptRoot 'search_settings.json' }
    if (Test-Path -LiteralPath $cfgPath) {
        try {
            $j = Get-Content -LiteralPath $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($g in $j.Groups) {
                $ph = @()
                foreach ($pz in $g.Phrases) { $ph += [pscustomobject]@{ Rx = [regex]::Escape($pz.Text); InName = [bool]$pz.SearchInFileName; InBody = [bool]$pz.SearchInContent } }
                $groups += [pscustomobject]@{ Name = $g.Name; Color = ("" + $g.ColorHex).TrimStart('#'); Phrases = $ph }
            }
            Write-Host ("Phrase groups loaded from search_settings.json: " + $groups.Count) -ForegroundColor Gray
        } catch { Write-Host ("Failed to read search_settings.json: " + $_.Exception.Message) -ForegroundColor Yellow }
    }
    if ($groups.Count -eq 0) {
        $groups = @([pscustomobject]@{ Name = 'Confidential'; Color = 'E65050'; Phrases = @(
                    [pscustomobject]@{ Rx = 'confidential'; InName = $true; InBody = $true },
                    [pscustomobject]@{ Rx = [regex]::Escape('internal use only'); InName = $true; InBody = $true },
                    [pscustomobject]@{ Rx = 'secret'; InName = $true; InBody = $true },
                    [pscustomobject]@{ Rx = 'restricted'; InName = $true; InBody = $false }) })
    }
    $needBody = [bool](@($groups | ForEach-Object { $_.Phrases } | Where-Object { $_.InBody }).Count)

    $hits = New-Object System.Collections.Generic.List[object]
    $scanned = 0
    Send-Progress -2 'Preparing the scan…'
    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root)) { continue }
        Write-Host "`nScanning: $root" -ForegroundColor Cyan
        Send-Progress -2 ("Scanning: " + $root)
        Get-ChildItem -LiteralPath $root -Recurse -File -Force -ErrorAction SilentlyContinue | ForEach-Object {
            $f = $_
            if ($f.FullName -match $skip) { return }
            if ($f.Extension.ToLower() -notin $exts) { return }
            $scanned++
            if ($scanned % 400 -eq 0) {
                Write-Host "  files checked: $scanned, found: $($hits.Count)" -ForegroundColor DarkGray
                Send-Progress -2 ("Files checked: {0}    found: {1}    ({2})" -f $scanned, $hits.Count, $root)
            }
            $where = $null; $mark = $null; $gcolor = $null
            foreach ($g in $groups) {
                foreach ($pz in $g.Phrases) { if ($pz.InName -and $f.Name -imatch $pz.Rx) { $where = 'name'; $mark = $g.Name; $gcolor = $g.Color; break } }
                if ($where) { break }
            }
            if (-not $where -and $needBody) {
                $txt = Get-DocText $f
                if ($txt) {
                    foreach ($g in $groups) {
                        foreach ($pz in $g.Phrases) { if ($pz.InBody -and $txt -imatch $pz.Rx) { $where = 'content'; $mark = $g.Name; $gcolor = $g.Color; break } }
                        if ($where) { break }
                    }
                }
            }
            if ($where) {
                $hits.Add([pscustomobject]@{ Label = $mark; Where = $where; File = $f.FullName; SizeKB = [math]::Round($f.Length / 1KB); Modified = $f.LastWriteTime.ToString('yyyy-MM-dd HH:mm'); Color = $gcolor })
                Write-Host ("  [{0}] {1}  ({2})" -f $mark, $f.FullName, $where) -ForegroundColor Yellow
            }
        }
    }
    Send-Progress 100 ("Scan finished: {0} checked, {1} found" -f $scanned, $hits.Count)
    Start-Sleep -Milliseconds 350
    Hide-Progress

    Write-Host "`n──────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host ("Files checked: $scanned   ·   with sensitive phrases: " + $hits.Count) -ForegroundColor $(if ($hits.Count) { 'Yellow' } else { 'Green' })
    $dir = Get-ReportDir
    $path = Join-Path $dir ("Sensitive documents " + $env:COMPUTERNAME + " " + (Get-Date -Format 'yyyy-MM-dd') + ".docx")
    try {
        $content = @(
            @{t = 'title'; x = 'DOCUMENTS WITH SENSITIVE PHRASES' },
            @{t = 'meta'; x = ('PC: ' + $env:COMPUTERNAME + '     Date: ' + (Get-Date -Format 'dd.MM.yyyy HH:mm')) },
            @{t = 'meta'; x = ('Files checked: ' + $scanned + '     Found: ' + $hits.Count) },
            @{t = 'space' }
        )
        if ($hits.Count) {
            $rows = @(); $n = 0
            foreach ($h in $hits) { $n++; $gc = if ($h.Color) { "$($h.Color)" } else { 'C00000' }; $rows += , @("$n", @{x = "$($h.Label)"; b = $true; c = $gc }, "$($h.Where)", "$($h.File)", "$($h.Modified)") }
            $content += @{t = 'table'; cols = @(@{x = 'No.'; w = 500 }, @{x = 'Group'; w = 1500 }, @{x = 'Where'; w = 1100 }, @{x = 'File'; w = 6000 }, @{x = 'Modified'; w = 1800 }); rows = $rows }
        } else { $content += @{t = 'para'; x = 'No documents with sensitive phrases found.'; c = '1E7A34' } }
        New-DocxReport -Path $path -Content $content
        Write-Host ("Report saved: " + $path) -ForegroundColor Green
    } catch { Write-Host ("Could not save the report: " + $_.Exception.Message) -ForegroundColor Red }
}


function Invoke-Integrity {
    $f = Join-Path $PSScriptRoot 'Integrity.ps1'
    if (-not (Test-Path -LiteralPath $f)) { Write-Host "Integrity.ps1 not found in the build." -ForegroundColor Red; return }
    try { . $f } catch { Write-Host ("Integrity control error: " + $_.Exception.Message) -ForegroundColor Red }
}

function Invoke-PolicyAudit {
    $pa = Join-Path $PSScriptRoot 'PolicyAudit.ps1'
    if (-not (Test-Path -LiteralPath $pa)) { Write-Host "PolicyAudit.ps1 not found in the build." -ForegroundColor Red; return }
    try { . $pa } catch { Write-Host ("Audit error: " + $_.Exception.Message) -ForegroundColor Red }
}


function Invoke-PcInfo {
    Write-Host "Collecting PC information (record card)..." -ForegroundColor Cyan
    Write-Host ""
    $gi = Join-Path $PSScriptRoot 'GetInfo.ps1'
    if (-not (Test-Path -LiteralPath $gi)) { Write-Host "GetInfo.ps1 not found in the build." -ForegroundColor Red; return }
    try { . $gi } catch { Write-Host ("Error while collecting information: " + $_.Exception.Message) -ForegroundColor Red }
}


function script:Write-ActLog([string]$Action, [string]$Result) {
    try {
        $dir = Join-Path $env:ProgramData 'ASShield'
        if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        $log = Join-Path $dir 'action-log.csv'
        $names = @{
            'profile' = 'Profile applied'; 'plan' = 'Profile plan (no changes)';
            'phrases' = 'Phrase editor';
            'terminal' = 'Removed Terminal/PowerShell from the menu';
            'users' = 'Created users and working folders';
            'restore' = 'Policies reset to Windows defaults'; 'usbclean' = 'USB history cleanup';
            'scanlic' = 'Software license scan'; 'scandocs' = 'Sensitive document scan';
            'pcinfo' = 'PC information collected'; 'policyaudit' = 'Policy compliance audit';
            'integrity' = 'System integrity control';
            'allowself' = 'Program allowed to run (SRP/AppLocker) for auditing'
        }
        $desc = if ($names.ContainsKey($Action)) { $names[$Action] } else { $Action }
        if ($Action -like 'profile:*') {
            $pid2 = $Action.Substring(8); $t = $pid2
            try { $t = (Resolve-ASProfile $PSScriptRoot $pid2).title } catch { }
            $desc = "Profile applied: '$t'"
        }
        $ts = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
        $usr = $env:USERNAME; $mach = $env:COMPUTERNAME
        $rows = @(); if (Test-Path -LiteralPath $log) { $rows = @(Import-Csv -LiteralPath $log) }
        $prev = 'GENESIS'; $seq = 1
        if ($rows.Count) { $prev = $rows[$rows.Count - 1].Hash; $seq = [int]$rows[$rows.Count - 1].Seq + 1 }
        $pl = $prev + '|' + $ts + '|' + $usr + '|' + $Action + '|' + $desc + '|' + $Result
        $sha = [System.Security.Cryptography.SHA256]::Create()
        $hash = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($pl))) -replace '-', ''); $sha.Dispose()
        $row = [pscustomobject]@{ Seq = $seq; Timestamp = $ts; User = $usr; Machine = $mach; Action = $Action; Details = $desc; Result = $Result; PrevHash = $prev; Hash = $hash }
        $all = @($rows) + $row
        $all | Export-Csv -LiteralPath $log -NoTypeInformation -Encoding UTF8
    } catch {}
}

$script:ActResult = 'Done'
$script:ActExit = 0

# Profile engine (v2)
. (Join-Path $PSScriptRoot 'Engine\Engine.ps1')
function Invoke-ProfileAction([string]$Id) {
    if ([string]::IsNullOrWhiteSpace($Id)) { Write-Host "No profile specified (-Build <id>)." -ForegroundColor Red; $script:ActExit = 3; return }
    $script:ActExit = Invoke-ASProfile -Id $Id
}

try {
switch ($Action) {
    # ── v2: profiles from Profiles\<id>\profile.json ──
    'profile'  { Invoke-ProfileAction $Build }
    'plan'     { $script:ActExit = Invoke-ASProfile -Id $Build -Plan }
    'terminal' { Remove-Win11TerminalAndPowerShell }
    'users'    { createNewUser }
    'restore'  { Invoke-FactoryRestore }
    'usbclean' { Invoke-UsbClean }
    'scanlic'  { Invoke-LicenseScan }
    'scandocs' { Invoke-DocScan }
    'pcinfo'   { Invoke-PcInfo }
    'policyaudit' { Invoke-PolicyAudit }
    'integrity' { Invoke-Integrity }
    'allowself' {
        # v2: safe exception (exe only, for Administrators, folder ACL check) - Engine\Steps\SelfExclusion.ps1
        $c = [pscustomobject]@{ PayloadRoot = $PSScriptRoot; DataDir = (Get-ASDataDir); Os = ''; Build = 0; ProfileId = 'allowself'; LastBackup = $null }
        $r = & $script:ASSteps['selfExclusion'] $null ([pscustomobject]@{ scope = 'publisher'; requireAdminOnlyFolder = $true }) $c
        Write-Host ("{0}  {1}" -f $r.Status, $r.Detail) -ForegroundColor $(if ($r.Status -eq 'OK') { 'Green' } else { 'Red' })
        $script:ActResult = "$($r.Status): $($r.Detail)"; if ($r.Status -eq 'FAIL') { $script:ActExit = 2 }
    }
    default    { Write-Host "Unknown action: $Action" -ForegroundColor Red }
}
} catch {
    $script:ActResult = 'ERROR: ' + $_.Exception.Message
    $script:ActExit = 1
    Write-Host ("Action failed: " + $_.Exception.Message) -ForegroundColor Red
}
$logAction = if ($Action -in 'profile', 'plan') { "$Action`:$Build" } else { $Action }
if ($Action -ne 'plan') { Write-ActLog $logAction $script:ActResult }

exit ([int]$script:ActExit)
