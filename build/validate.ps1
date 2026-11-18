# Profile validation before the build: JSON syntax, required fields, known step types,
# files referenced by the steps (for every supported OS), basedOn expansion.
param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$payload = (Resolve-Path (Join-Path $Root 'Payload')).Path
. (Join-Path $payload 'Engine/Lib/Profile.ps1')
$failed = 0
foreach ($d in (Get-ChildItem -LiteralPath (Join-Path $payload 'Profiles') -Directory)) {
    try {
        $p = Resolve-ASProfile $payload $d.Name
        if ($p.id -ne $d.Name) { throw "id '$($p.id)' does not match the folder name" }
        foreach ($os in @($p.os.supported)) {
            $ctx = [pscustomobject]@{ PayloadRoot = $payload; Os = $os }
            foreach ($s in @($p.steps)) {
                if ((Get-ASProp $s 'enabled') -eq $false) { continue }   # disabled steps may point at files you have not added yet
                $prm = Get-ASProp $s 'params'
                foreach ($k in 'file', 'gpo') {
                    $v = Get-ASProp $prm $k
                    if ($null -ne $v) { [void](Resolve-ASStepFile $s (Resolve-ASPerOs $v $os) $ctx) }
                }
            }
        }
        Write-Host ("OK    {0,-22} {1} (steps: {2})" -f $p.id, $p.title, @($p.steps).Count) -ForegroundColor Green
    } catch { $failed++; Write-Host ("FAIL  {0}: {1}" -f $d.Name, $_.Exception.Message) -ForegroundColor Red }
}
foreach ($j in 'Catalog/menu.json', 'Catalog/shots.json') { $null = Get-Content -Raw (Join-Path $payload $j) | ConvertFrom-Json; Write-Host "OK    $j" -ForegroundColor Green }
if ($failed) { exit 1 }
