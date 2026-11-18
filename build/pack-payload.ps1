# Packs ..\Payload into build\out\payload.zip (embedded in the exe as the "payload.zip" resource).
param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$src = Join-Path $Root 'Payload'
$out = Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$zip = Join-Path $out 'payload.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($src, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
Write-Host "payload.zip: $((Get-Item $zip).Length) bytes"
