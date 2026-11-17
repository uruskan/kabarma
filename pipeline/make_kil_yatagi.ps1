# dunya akisi: kil yatagi (URETIM §6 M1 kararlari: Blender betiginden, kredisiz) -> clean_asset.py (SANAT-SES §9 olcut 1-5).
#   powershell -NoProfile -ExecutionPolicy Bypass -File pipeline/make_kil_yatagi.ps1 [-Seed 7]
# Ham GLB gecici klasore yazilir (tohumdan her seferinde ayni cikar, git'e girmez).
# Cikti: game/assets/models/kil_yatagi/kil_yatagi.glb + .report.json; progress/m1/dunya/models/kil_yatagi/ (4 aci + siluet).
param([int]$Seed = 7, [double]$Height = 0.55, [int]$Tris = 3000)

$ErrorActionPreference = 'Continue'
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$Blender = 'C:\Program Files\Blender Foundation\Blender 5.3\blender.exe'
$raw = Join-Path ([IO.Path]::GetTempPath()) "kabarma_kil_yatagi_raw_$Seed.glb"

if (Test-Path $raw) { Remove-Item $raw }  # eski ham cikti yanlislikla kullanilmasin
$log = & $Blender -b --factory-startup --python-exit-code 1 -P pipeline/gen_kil_yatagi.py -- --out $raw --seed $Seed --tris ($Tris - 200) 2>&1 | Out-String
$log -split "`r?`n" | Where-Object { $_ -match '^(KIL|Error|Traceback)|Error:' } | ForEach-Object { Write-Host "  $_" }
if (-not (Test-Path $raw)) { Write-Host 'HAM GLB YOK'; exit 1 }

$argList = @('-b', '--factory-startup', '--python-exit-code', '1', '-P', 'pipeline/clean_asset.py', '--',
    '--in', $raw, '--out', 'game/assets/models/kil_yatagi/kil_yatagi.glb', '--name', 'kil_yatagi', '--height', $Height, '--tris', $Tris,
    '--keep_texture', '--tex_size', '1024', '--renders', 'progress/m1/dunya/models/kil_yatagi')
$log = & $Blender @argList 2>&1 | Out-String
$log -split "`r?`n" | Where-Object { $_ -match '^(PIPELINE_REPORT|CHECK|Error|Traceback)' } | ForEach-Object { Write-Host "  $_" }
