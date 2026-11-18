if (-not $script:ASSteps) { $script:ASSteps = @{} }
# SelfExclusion.ps1 - safe replacement for Add-SelfToExclusions (v1.5.9).
# Differences from 1.5.9:
#   - allows ONLY the exe itself (no "<folder>\*" rule);
#   - the AppLocker rule targets BUILTIN\Administrators (S-1-5-32-544), not Everyone;
#   - scope = publisher: a rule by signature (when the exe is signed), otherwise by hash;
#   - requireAdminOnlyFolder: refuses when non-administrators can write to the exe folder
#     (otherwise replacing the exe would bypass SRP/AppLocker).

$script:ASAdminSids = @('S-1-5-32-544', 'S-1-5-18', 'S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464') # Administrators, SYSTEM, TrustedInstaller

function Test-ASAdminOnlyWritable([string]$Folder) {
    $acl = Get-Acl -LiteralPath $Folder
    $writeMask = [Security.AccessControl.FileSystemRights]'Write, Modify, FullControl, CreateFiles, AppendData, WriteData, ChangePermissions, TakeOwnership'
    $bad = @()
    foreach ($ace in $acl.Access) {
        if ($ace.AccessControlType -ne 'Allow') { continue }
        if (($ace.FileSystemRights -band $writeMask) -eq 0) { continue }
        try { $sid = $ace.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value } catch { $sid = "$($ace.IdentityReference)" }
        if ($script:ASAdminSids -notcontains $sid -and $sid -ne 'S-1-3-0') { $bad += "$($ace.IdentityReference)" }   # S-1-3-0 = CREATOR OWNER
    }
    return ,$bad
}

$script:ASSteps['selfExclusion'] = {
    param($Step, $P, $Ctx)
    $exe = $env:ASEXE
    if ([string]::IsNullOrWhiteSpace($exe) -or -not (Test-Path -LiteralPath $exe)) { return New-ASResult 'FAIL' 'program path (ASEXE) is not set' }
    $exe = (Resolve-Path -LiteralPath $exe).Path
    $folder = Split-Path $exe -Parent

    if (Get-ASProp $P 'requireAdminOnlyFolder' $true) {
        $bad = Test-ASAdminOnlyWritable $folder
        if ($bad.Count) {
            return New-ASResult 'FAIL' ("non-administrators can write to '$folder': " + ($bad -join ', ') + ". Install the program to C:\Program Files and try again.")
        }
    }

    $scope = [string](Get-ASProp $P 'scope' 'file')
    $sig = Get-AuthenticodeSignature -LiteralPath $exe
    $info = Get-AppLockerFileInformation -Path $exe
    $ruleType = if ($scope -eq 'publisher' -and $sig.Status -eq 'Valid') { 'Publisher' } else { 'Hash' }
    $policy = $info | New-AppLockerPolicy -RuleType $ruleType -User 'S-1-5-32-544' -RuleNamePrefix 'AS Shield (audit)' -Optimize
    $tmp = Join-Path $Ctx.DataDir ('applocker-self-' + [guid]::NewGuid().ToString('N') + '.xml')
    $policy.ToXml() | Set-Content -LiteralPath $tmp -Encoding UTF8
    try { Set-AppLockerPolicy -XmlPolicy $tmp -Merge -ErrorAction Stop } finally { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }

    # SRP: an "Unrestricted" rule for the exe file only (not for the folder).
    $base = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Safer\CodeIdentifiers\262144\Paths'
    if (-not (Test-Path $base)) { New-Item -Path $base -Force | Out-Null }
    $existing = Get-ChildItem $base -ErrorAction SilentlyContinue | Where-Object { (Get-ItemProperty $_.PSPath).ItemData -eq $exe }
    if (-not $existing) {
        $key = Join-Path $base ('{' + [guid]::NewGuid().ToString() + '}')
        New-Item -Path $key -Force | Out-Null
        New-ItemProperty -Path $key -Name 'ItemData' -PropertyType ExpandString -Value $exe -Force | Out-Null
        New-ItemProperty -Path $key -Name 'SaferFlags' -PropertyType DWord -Value 0 -Force | Out-Null
        New-ItemProperty -Path $key -Name 'Description' -PropertyType String -Value 'AS Shield (audit) - executable file only' -Force | Out-Null
    }
    & gpupdate /target:computer /force 2>&1 | Out-Null
    return New-ASResult 'OK' ("AppLocker: $ruleType rule for Administrators; SRP: path to $exe only")
}
