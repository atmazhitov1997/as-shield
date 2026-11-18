param([Parameter(Mandatory = $true)][string]$Token)
$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding $false
try { [Console]::OutputEncoding = $utf8 } catch { }

# Shared policy library (the same one the 'shotPolicies' engine step uses).
Set-Location -Path $PSScriptRoot
. (Join-Path $PSScriptRoot 'Private\Policies\shotPolicies.ps1')

Invoke-ShotPolicy $Token

& gpupdate /target:computer /force 2>&1 | Out-Null
Write-Output "OK"
exit 0
