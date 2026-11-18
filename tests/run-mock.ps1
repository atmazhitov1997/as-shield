# Engine mock test without Windows (PowerShell 7):
#   pwsh tests/run-mock.ps1 -Id workstation-baseline -Os 11 -Answers "Y"
# Engine mock test on Linux: replaces the helper functions and native tools.
param([string]$Payload = (Join-Path (Split-Path $PSScriptRoot -Parent) 'Payload'), [string]$Id = 'workstation-baseline', [string]$Os = '11', [string[]]$Answers, [string]$FailType)
if ($env:AS_TEST_PD) { $env:ProgramData = $env:AS_TEST_PD } else { $env:ProgramData = Join-Path ([IO.Path]::GetTempPath()) ('asdata-' + [guid]::NewGuid().ToString('N')) }
$env:ASEXE = '/tmp/fake-asshield.exe'
. (Join-Path $Payload 'Engine/Engine.ps1')
$script:q = [System.Collections.Queue]::new(); foreach ($a in ($Answers -join "," -split ",")) { $script:q.Enqueue($a) }
function Read-Host($Prompt) { $a = if ($script:q.Count) { $script:q.Dequeue() } else { 'N' }; Write-Host "  ? $Prompt -> $a"; $a }
$script:rp = 0
function Get-ComputerRestorePoint { if ($script:rp -gt 0) { 1..$script:rp } }
function osRestorePoint { $script:rp++; 'legacy output line' }
function defaultUserPath {} ; function removeAdditionalPrograms {} ; function Remove-Win11TerminalAndPowerShell {}
function Apply-EvidencePolicies {} ; function UsersRole {}
$script:svc = @{}
function Invoke-ASNative([string]$Exe, [string[]]$Arguments) {
  if ($Exe -eq 'sc.exe') {
    $n = $Arguments[1]
    if ($n -like 'Missing*') { return [pscustomobject]@{ Code = 1060; Output = '' } }
    if ($Arguments[0] -eq 'config') { $script:svc[$n] = @{ auto='2'; demand='3'; disabled='4' }[$Arguments[3]]; return [pscustomobject]@{ Code = 0; Output = '' } }
    $c = if ($script:svc.ContainsKey($n)) { $script:svc[$n] } else { '3' }
    return [pscustomobject]@{ Code = 0; Output = "START_TYPE         : $c   X" }
  }
  if ($FailType -and $Exe -like "*$FailType*") { return [pscustomobject]@{ Code = 5; Output = 'mock fail' } }
  return [pscustomobject]@{ Code = 0; Output = '' }
}
# Registry and AppLocker are not available on Linux - their handlers are replaced with simple mocks.
$script:ASSteps['registry'] = { param($S,$P,$C) New-ASResult 'OK' ("mock: values " + @($P.values).Count) }
function Set-AppLockerPolicy { param($XmlPolicy, [switch]$Merge) Write-Host "  mock Set-AppLockerPolicy $XmlPolicy merge=$Merge" }
$script:ASSteps['selfExclusion'] = { param($S,$P,$C) New-ASResult 'OK' 'mock selfExclusion' }
function Test-ASAdminOnlyWritable($Folder) { ,@() }
function shutdown.exe { Write-Host "  mock shutdown $args" }
$code = Invoke-ASProfile -Id $Id -OsOverride $Os -PayloadRoot $Payload
"EXIT=$code"; "ActResult=$script:ActResult"
$r = Get-ChildItem (Join-Path $env:ProgramData 'ASShield/runs') -Filter *.json | Select-Object -First 1
(Get-Content $r.FullName -Raw | ConvertFrom-Json).steps | Format-Table n,id,status,detail -AutoSize | Out-String -Width 220
