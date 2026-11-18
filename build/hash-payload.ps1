# Produces build\out\payload.manifest.json - the SHA-256 of every payload file.
# The manifest is embedded in the exe as a SEPARATE resource (a file cannot contain its own hash).
param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$src = (Resolve-Path (Join-Path $Root 'Payload')).Path
$out = Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$files = [ordered]@{}
foreach ($f in (Get-ChildItem -LiteralPath $src -Recurse -File | Sort-Object FullName)) {
    $rel = $f.FullName.Substring($src.Length + 1).Replace('\', '/')
    $files[$rel] = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash
}
$m = [ordered]@{ schemaVersion = 1; created = (Get-Date).ToString('s'); files = $files }
$m | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $out 'payload.manifest.json') -Encoding UTF8
Write-Host ("payload.manifest.json: {0} files" -f $files.Count)
