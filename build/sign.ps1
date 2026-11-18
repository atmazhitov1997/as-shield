# Authenticode signing. Scripts - every .ps1 in Payload (before packing, because the manifest hashes the signed files);
# Exe - the built AS Shield.exe. The certificate must be in Cert:\CurrentUser\My (or LocalMachine\My).
# After signing: a publisher AppLocker rule (selfExclusion scope=publisher) and profiles can use ExecutionPolicy=AllSigned.
param([ValidateSet('Scripts', 'Exe')][string]$Target, [Parameter(Mandatory = $true)][string]$CertThumbprint,
      [string]$TimestampServer = 'http://timestamp.digicert.com')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$cert = Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My | Where-Object Thumbprint -eq $CertThumbprint | Select-Object -First 1
if (-not $cert) { throw "Certificate $CertThumbprint not found" }
$files = if ($Target -eq 'Scripts') { Get-ChildItem (Join-Path $root 'Payload') -Recurse -Filter *.ps1 } else { Get-ChildItem (Join-Path $root 'App\bin\Release') -Recurse -Filter 'AS Shield.exe' }
foreach ($f in $files) {
    $r = Set-AuthenticodeSignature -FilePath $f.FullName -Certificate $cert -TimestampServer $TimestampServer -HashAlgorithm SHA256
    if ($r.Status -ne 'Valid') { throw "$($f.Name): $($r.StatusMessage)" }
}
Write-Host ("Files signed: {0}" -f @($files).Count)
