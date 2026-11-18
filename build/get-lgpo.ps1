# Downloads Microsoft LGPO.exe (part of the Security Compliance Toolkit) into Payload\LGPO.exe.
# LGPO is NOT redistributed with this repository - it is fetched from Microsoft at build time
# and is covered by Microsoft's own license terms.
param(
    [string]$Url = 'https://download.microsoft.com/download/8/5/C/85C25433-A1B0-4FFA-9429-7E023E7DA8D8/LGPO.zip',
    [string]$Sha256 = 'CB7159D134A0A1E7B1ED2ADA9A3CE8CE8F4DE391D14403D55438AF824247CC55'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dest = Join-Path $root 'Payload\LGPO.exe'
if (Test-Path -LiteralPath $dest) { Write-Host "LGPO.exe already present: $dest"; return }
$tmp = Join-Path ([IO.Path]::GetTempPath()) ('lgpo-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    $zip = Join-Path $tmp 'LGPO.zip'
    Invoke-WebRequest -Uri $Url -OutFile $zip -UseBasicParsing
    $h = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    if ($Sha256 -and $h -ne $Sha256) {
        throw "LGPO.zip hash mismatch: $h (expected $Sha256). Microsoft may have published a new version - verify it and pass -Sha256."
    }
    Expand-Archive -LiteralPath $zip -DestinationPath $tmp -Force
    $exe = Get-ChildItem -LiteralPath $tmp -Recurse -Filter 'LGPO.exe' | Select-Object -First 1
    if (-not $exe) { throw 'LGPO.exe not found inside the archive' }
    Copy-Item -LiteralPath $exe.FullName -Destination $dest
    Write-Host "LGPO.exe -> $dest" -ForegroundColor Green
} finally { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }
