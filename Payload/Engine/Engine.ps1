# Engine.ps1 - runs a security profile described by Profiles/<id>/profile.json.
#
# Usage (from RunAction.ps1, which already loads the GUI protocol and the helper modules):
#   . .\Engine\Engine.ps1
#   $code = Invoke-ASProfile -Id workstation-baseline          # apply
#   $code = Invoke-ASProfile -Id workstation-baseline -Plan    # show the plan only, change nothing
#
# Return code: 0 - every step OK/DONE/SKIP; 2 - there were failures (onError=continue);
#              1 - stopped at a step with onError=stop; 3 - invalid profile / OS not supported.

$script:ASEngineRoot = $PSScriptRoot
$script:ASSteps = @{}
. (Join-Path $PSScriptRoot 'Lib\Profile.ps1')
foreach ($f in (Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Steps') -Filter '*.ps1')) { . $f.FullName }

function Get-ASDataDir {
    $d = Join-Path $env:ProgramData 'ASShield'
    if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
    return $d
}

function Import-ASLegacyModules([string]$PayloadRoot) {
    # Helper modules (osRestorePoint, UsersRole, etc.). RunAction.ps1 loads them itself.
    if (Get-Command osRestorePoint -ErrorAction SilentlyContinue) { return }
    foreach ($folder in 'Private\Core', 'Private\UI', 'Private\Services', 'Private\Policies', 'Public') {
        $path = Join-Path $PayloadRoot $folder
        if (Test-Path $path) { foreach ($file in (Get-ChildItem "$path\*.ps1")) { . $file.FullName } }
    }
}

function Test-ASStepApplies($Step, $Ctx) {
    if ((Get-ASProp $Step 'enabled' $true) -eq $false) { return 'disabled in the profile' }
    $w = Get-ASProp $Step 'when'
    if ($w) {
        $os = @(Get-ASProp $w 'os' @())
        if ($os.Count -and ($os -notcontains $Ctx.Os)) { return "Windows $($os -join '/') only" }
        $mb = Get-ASProp $w 'minBuild'
        if ($mb -and $Ctx.Build -lt [int]$mb) { return "requires build >= $mb" }
    }
    return $null
}

function Invoke-ASProfile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Id,
        [switch]$Plan,
        [string]$PayloadRoot = (Split-Path $script:ASEngineRoot -Parent),
        [string]$OsOverride      # tests only
    )
    $ErrorActionPreference = 'Continue'
    try { $prof = Resolve-ASProfile $PayloadRoot $Id }
    catch { Write-Host ("Profile not loaded: " + $_.Exception.Message) -ForegroundColor Red; $script:ActResult = 'ERROR: invalid profile'; return 3 }

    if ($OsOverride) { $os = [pscustomobject]@{ Version = $OsOverride; Build = 99999 } } else { $os = Get-ASOsVersion }
    $sup = @($prof.os.supported)
    if ($sup -notcontains $os.Version) {
        Write-Host ("Profile '{0}' does not support Windows {1} (supported: {2})." -f $prof.title, $os.Version, ($sup -join ', ')) -ForegroundColor Red
        $script:ActResult = 'ERROR: OS not supported'; return 3
    }
    $minBuild = Get-ASProp $prof.os 'minBuild'
    if ($minBuild -and $os.Build -lt [int]$minBuild) {
        Write-Host ("Windows build >= {0} is required, this PC has {1}." -f $minBuild, $os.Build) -ForegroundColor Red
        $script:ActResult = 'ERROR: Windows build'; return 3
    }

    $ctx = [pscustomobject]@{
        PayloadRoot = $PayloadRoot; Os = $os.Version; Build = $os.Build; ProfileId = $prof.id
        DataDir = $(if ($Plan) { [IO.Path]::GetTempPath() } else { Get-ASDataDir }); LastBackup = $null
    }
    $steps = @($prof.steps)
    $profOnError = [string](Get-ASProp $prof 'onError' 'continue')

    Write-Host ("==== {0} · Windows {1} (build {2}) · steps: {3} ====" -f $prof.title, $ctx.Os, $ctx.Build, $steps.Count) -ForegroundColor Yellow
    if ($Plan) {
        $i = 0
        foreach ($s in $steps) {
            $i++
            $why = Test-ASStepApplies $s $ctx
            $mark = if ($why) { "skip: $why" } else { $s.type }
            Write-Host ("{0,2}. {1}  [{2}]" -f $i, $(if ($s.title) { $s.title } else { $s.id }), $mark)
        }
        $script:ActResult = 'Plan (no changes)'
        return 0
    }

    if (-not (Get-Command Send-Progress -ErrorAction SilentlyContinue)) { function script:Send-Progress([int]$Percent, [string]$Label) { } }
    Import-ASLegacyModules $PayloadRoot

    $results = New-Object System.Collections.Generic.List[object]
    $stopped = $false; $i = 0
    foreach ($s in $steps) {
        $i++
        $title = if ($s.title) { $s.title } else { $s.id }
        Send-Progress ([int](100 * ($i - 1) / $steps.Count)) ("Step $i/$($steps.Count): $title")
        Write-Host ""
        Write-Host ("── [{0}/{1}] {2}" -f $i, $steps.Count, $title) -ForegroundColor Cyan
        $started = Get-Date
        $why = Test-ASStepApplies $s $ctx
        if ($why) { $res = New-ASResult 'SKIP' $why }
        elseif ($s.confirm -and ((Read-Host ($(if ($s.confirm -is [string]) { $s.confirm } else { "Run '$title'?" }) + ' (Y/N)')).Trim().ToUpper() -ne 'Y')) {
            $res = New-ASResult 'SKIP' 'declined by the operator'
        }
        else {
            $h = $script:ASSteps[$s.type]
            if (-not $h) { $res = New-ASResult 'FAIL' "no handler for type $($s.type)" }
            else {
                try {
                    # Helper functions may write to the pipeline: show that output, and take the last object with a Status as the result.
                    $res = $null
                    foreach ($o in @(& $h $s (Get-ASProp $s 'params') $ctx)) {
                        if ($null -ne $o -and $o.PSObject.Properties['Status'] -and $o.PSObject.Properties['Detail']) { $res = $o }
                        elseif ($null -ne $o) { Write-Host ("   " + ($o | Out-String).Trim()) -ForegroundColor DarkGray }
                    }
                    if (-not $res) { $res = New-ASResult 'DONE' '' }
                }
                catch { $res = New-ASResult 'FAIL' $_.Exception.Message }
            }
        }
        $color = switch ($res.Status) { 'OK' { 'Green' } 'DONE' { 'Green' } 'SKIP' { 'DarkGray' } default { 'Red' } }
        Write-Host ("   {0}  {1}" -f $res.Status, $res.Detail) -ForegroundColor $color
        $results.Add([pscustomobject]@{ n = $i; id = $s.id; type = $s.type; title = $title; status = $res.Status; detail = $res.Detail
                                        started = $started.ToString('s'); seconds = [math]::Round(((Get-Date) - $started).TotalSeconds, 1) })
        if ($res.Status -eq 'FAIL') {
            $policy = [string](Get-ASProp $s 'onError' $profOnError)
            if ($policy -eq 'stop') { $stopped = $true; Write-Host "Run stopped: the step has onError=stop." -ForegroundColor Red; break }
        }
    }
    Send-Progress 100 'Done'

    $fails = @($results | Where-Object { $_.status -eq 'FAIL' }).Count
    $code = if ($stopped) { 1 } elseif ($fails) { 2 } else { 0 }
    $summary = "OK/DONE: {0}, SKIP: {1}, FAIL: {2}" -f @($results | Where-Object { $_.status -in 'OK', 'DONE' }).Count, @($results | Where-Object { $_.status -eq 'SKIP' }).Count, $fails
    $script:ActResult = $(if ($code -eq 0) { "Done ($summary)" } elseif ($code -eq 1) { "STOPPED ($summary)" } else { "WITH ERRORS ($summary)" })

    # Machine-readable run report, for the work/session reports and audits.
    $runDir = Join-Path $ctx.DataDir 'runs'
    New-Item -ItemType Directory -Path $runDir -Force | Out-Null
    $report = [pscustomobject]@{ profile = $prof.id; title = $prof.title; os = $ctx.Os; build = $ctx.Build; machine = $env:COMPUTERNAME
                                 user = "$env:USERDOMAIN\$env:USERNAME"; finished = (Get-Date).ToString('s'); exitCode = $code
                                 gpoBackup = $ctx.LastBackup; steps = $results }
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runDir ((Get-Date -Format 'yyyyMMdd-HHmmss') + "-$($prof.id).json")) -Encoding UTF8

    Write-Host ""
    Write-Host ("==== Summary: {0} ====" -f $script:ActResult) -ForegroundColor $(if ($code -eq 0) { 'Green' } else { 'Red' })
    if ($ctx.LastBackup) { Write-Host ("Policy backup taken before the changes: " + $ctx.LastBackup) -ForegroundColor DarkGray }

    if (-not $stopped) {
        $reboot = [string](Get-ASProp $prof 'reboot' 'ask')
        if ($reboot -eq 'always' -or ($reboot -eq 'ask' -and (Read-Host 'Restart the PC now? (Y/N)').Trim().ToUpper() -eq 'Y')) {
            shutdown.exe /r /t 15 /c "AS Shield: profile $($prof.title) applied"
        }
    }
    return $code
}
