# Profile.ps1 - loading, inheritance and validation of profiles (schemaVersion 2).
# Compatible with Windows PowerShell 5.1.

$script:ASKnownStepTypes = @(
    'restorePoint', 'userProfilesPath', 'folder', 'eventLogs', 'registry', 'removeApps', 'removeWinXShell',
    'services', 'appLocker', 'lgpo', 'lgpoText', 'shotPolicies', 'userRoles', 'selfExclusion', 'script'
)

function Get-ASProp($Obj, [string]$Name, $Default = $null) {
    if ($null -eq $Obj) { return $Default }
    if ($Obj -is [System.Collections.IDictionary]) { if ($Obj.Contains($Name)) { return $Obj[$Name] } else { return $Default } }
    $p = $Obj.PSObject.Properties[$Name]
    if ($null -eq $p) { return $Default }
    return $p.Value
}

function Get-ASOsVersion {
    $build = [int](Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction Stop).CurrentBuildNumber
    $ver = if ($build -ge 22000) { '11' } else { '10' }
    return [pscustomobject]@{ Version = $ver; Build = $build }
}

# Per-OS value: a string or an object {"10": "...", "11": "..."}.
function Resolve-ASPerOs($Value, [string]$Os) {
    if ($null -eq $Value) { return $null }
    if ($Value -is [string]) { return $Value }
    $v = Get-ASProp $Value $Os
    if ($null -eq $v) { throw "No value for Windows $Os" }
    return [string]$v
}

function Get-ASProfileRoot([string]$PayloadRoot) { Join-Path $PayloadRoot 'Profiles' }

# Administrator-defined local profiles - customization without rebuilding the exe.
function Get-ASLocalProfileRoot {
    if (-not $env:ProgramData) { return $null }
    return [IO.Path]::Combine($env:ProgramData, 'ASShield', 'Profiles')
}

# Path to profile.json: the built-in one wins (a local profile cannot replace a built-in id).
function Find-ASProfileFile([string]$PayloadRoot, [string]$Id) {
    if ($Id -notmatch '^[a-z0-9][a-z0-9-]{1,31}$') { throw "Invalid profile id: $Id" }
    $f = [IO.Path]::Combine((Get-ASProfileRoot $PayloadRoot), $Id, 'profile.json')
    if (Test-Path -LiteralPath $f) { return [pscustomobject]@{ Path = $f; Local = $false } }
    $lr = Get-ASLocalProfileRoot
    if ($lr) {
        $f = [IO.Path]::Combine($lr, $Id, 'profile.json')
        if (Test-Path -LiteralPath $f) { return [pscustomobject]@{ Path = $f; Local = $true } }
    }
    return $null
}

function Get-ASProfileList([string]$PayloadRoot) {
    $list = @()
    $roots = @((Get-ASProfileRoot $PayloadRoot), (Get-ASLocalProfileRoot)) | Where-Object { $_ }
    foreach ($d in (Get-ChildItem -LiteralPath $roots -Directory -ErrorAction SilentlyContinue)) {
        $f = Join-Path $d.FullName 'profile.json'
        if (Test-Path -LiteralPath $f) {
            try { $list += (Read-ASProfileFile $f) } catch { Write-Warning ("Profile {0} skipped: {1}" -f $d.Name, $_.Exception.Message) }
        }
    }
    $seen = @{}
    $list = @($list | Where-Object { if ($seen.ContainsKey($_.id)) { $false } else { $seen[$_.id] = $true; $true } })
    return $list | Sort-Object { [int](Get-ASProp $_ 'order' 100) }, { $_.id }
}

function Read-ASProfileFile([string]$Path) {
    $raw = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)
    $p = $raw | ConvertFrom-Json
    $p | Add-Member -NotePropertyName '_dir' -NotePropertyValue (Split-Path $Path -Parent) -Force
    foreach ($s in @($p.steps)) { $s | Add-Member -NotePropertyName '_dir' -NotePropertyValue $p._dir -Force }
    Test-ASProfile $p
    return $p
}

# Minimal validation without external libraries. The full check against Schema/profile.schema.json runs at build time (build/validate.ps1).
function Test-ASProfile($P) {
    $errors = @()
    if ((Get-ASProp $P 'schemaVersion') -ne 2) { $errors += 'schemaVersion must be 2' }
    if (-not ((Get-ASProp $P 'id') -match '^[a-z0-9][a-z0-9-]{1,31}$')) { $errors += 'invalid id' }
    if (-not (Get-ASProp $P 'title')) { $errors += 'missing title' }
    $os = Get-ASProp $P 'os'
    if (-not $os -or -not (Get-ASProp $os 'supported')) { $errors += 'missing os.supported' }
    $ids = @{}
    foreach ($s in @(Get-ASProp $P 'steps' @())) {
        $sid = Get-ASProp $s 'id'
        if (-not $sid) { $errors += 'step without an id' ; continue }
        if ($ids.ContainsKey($sid)) { $errors += "duplicate step id: $sid" }
        $ids[$sid] = $true
        if ($script:ASKnownStepTypes -notcontains (Get-ASProp $s 'type')) { $errors += "step ${sid}: unknown type '$(Get-ASProp $s 'type')'" }
    }
    if ($errors.Count) { throw ("Profile '{0}' is invalid: {1}" -f (Get-ASProp $P 'id'), ($errors -join '; ')) }
}

# Expands basedOn: base steps + replacement by id + removeSteps + new steps appended at the end.
function Resolve-ASProfile([string]$PayloadRoot, [string]$Id, [int]$Depth = 0) {
    if ($Depth -gt 5) { throw "basedOn chain too long (circular reference?)" }
    $found = Find-ASProfileFile $PayloadRoot $Id
    if (-not $found) { throw "Profile '$Id' not found either in the payload or in $(Get-ASLocalProfileRoot)" }
    if ($found.Local -and (Get-Command Test-ASAdminOnlyWritable -ErrorAction SilentlyContinue)) {
        $bad = Test-ASAdminOnlyWritable (Split-Path $found.Path -Parent)
        if ($bad.Count) { throw ("local profile '$Id' can be modified by non-administrators: " + ($bad -join ', ')) }
    }
    $p = Read-ASProfileFile $found.Path
    $p | Add-Member -NotePropertyName '_local' -NotePropertyValue $found.Local -Force
    $baseId = Get-ASProp $p 'basedOn'
    if (-not $baseId) { return $p }

    $base = Resolve-ASProfile $PayloadRoot $baseId ($Depth + 1)
    $remove = @(Get-ASProp $p 'removeSteps' @())
    $own = @{}
    foreach ($s in @($p.steps)) { $own[$s.id] = $s }
    $merged = New-Object System.Collections.ArrayList
    foreach ($s in @($base.steps)) {
        if ($remove -contains $s.id) { continue }
        if ($own.ContainsKey($s.id)) { [void]$merged.Add($own[$s.id]); $own.Remove($s.id) } else { [void]$merged.Add($s) }
    }
    foreach ($s in @($p.steps)) { if ($own.ContainsKey($s.id)) { [void]$merged.Add($s) } }

    foreach ($k in 'reboot', 'onError') {
        if ($null -eq (Get-ASProp $p $k)) { $p | Add-Member -NotePropertyName $k -NotePropertyValue (Get-ASProp $base $k) -Force }
    }
    $p.steps = @($merged)
    return $p
}

# Path of a step file: relative to the folder of the profile that declares the step; a path starting with Configs/ is relative to the payload.
function Resolve-ASStepFile($Step, [string]$Rel, $Ctx) {
    if ([string]::IsNullOrWhiteSpace($Rel)) { throw 'Empty file path' }
    if ([IO.Path]::IsPathRooted($Rel)) { throw "Absolute paths are not allowed in a profile: $Rel" }
    $sep = [IO.Path]::DirectorySeparatorChar
    $rel = $Rel -replace '[\\/]', $sep
    $baseDir = if ($Rel -match '^Configs[\\/]') { $Ctx.PayloadRoot } else { $Step._dir }
    $full = [IO.Path]::GetFullPath([IO.Path]::Combine($baseDir, $rel))
    $allowed = @([IO.Path]::GetFullPath($Ctx.PayloadRoot), [IO.Path]::GetFullPath($Step._dir)) | ForEach-Object { $_.TrimEnd($sep) + $sep }
    if (-not ($allowed | Where-Object { $full.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) })) { throw "Path points outside the payload/profile: $Rel" }
    if (-not (Test-Path -LiteralPath $full)) { throw "File not found: $Rel" }
    return $full
}
