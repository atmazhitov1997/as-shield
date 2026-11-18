# Full build of AS Shield:
#   1) profile validation;  2) payload.zip;  3) SHA-256 manifest;  4) exe;  5) (optional) Authenticode signing.
param([string]$CertThumbprint)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'get-lgpo.ps1')
& (Join-Path $PSScriptRoot 'validate.ps1'); if ($LASTEXITCODE) { throw 'Profile validation failed' }
if ($CertThumbprint) { & (Join-Path $PSScriptRoot 'sign.ps1') -Target Scripts -CertThumbprint $CertThumbprint }
& (Join-Path $PSScriptRoot 'pack-payload.ps1')
& (Join-Path $PSScriptRoot 'hash-payload.ps1')
dotnet build (Join-Path $root 'App\ASShield.csproj') -c Release
if ($CertThumbprint) { & (Join-Path $PSScriptRoot 'sign.ps1') -Target Exe -CertThumbprint $CertThumbprint }
