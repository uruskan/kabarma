# dunya akisi: M1'in 9 sus modelini (6 bitki, 3 kaya; URETIM §5.1, Tripo API ile uretildi) clean_asset.py'den gecirir.
# Kaynak git'te degil: ana calisma kopyasinin .tools/tripo/<ad>/tripo-out/*/model.glb (CLAUDE.md, yalniz okunur).
# Cikti: game/assets/models/<ad>/<ad>.glb + <ad>.report.json (SANAT-SES §9 olcut 1-5) ve
#        progress/m1/dunya/models/<ad>/ (4 aci + 64 px siluet, olcut 6-8 icin).
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File pipeline/import_decor.ps1 [-Only ot,bugday]
#
# Boy (m) 1,6 m'lik karaktere gore; ucgen butceleri SANAT-SES §4 (agac 5-8 bin, esya ve kaya 2-6 bin; kucuk bitki
# icin en dusuk esya butcesi, cunku cok sayida orneklenir). Kredi harcamaz.
param([string[]]$Only = @())

$ErrorActionPreference = 'Continue'
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$Blender = 'C:\Program Files\Blender Foundation\Blender 5.3\blender.exe'
$Src = 'C:\Users\uruskan\Documents\GitHub\kabarma\.tools\tripo'
if ($env:KABARMA_TRIPO_SRC) { $Src = $env:KABARMA_TRIPO_SRC }

# ad, boy (m), ucgen butcesi, ek argumanlar
$assets = @(
    @('cali',         1.1, 2000, ''),
    @('bugday',       1.0, 2000, ''),
    @('cicek',        0.6, 2000, ''),
    @('ot',           0.9, 2000, ''),
    @('egrelti',      0.7, 2000, ''),
    @('turuncu_agac', 4.8, 7000, ''),
    @('kaya_duz',     0.7, 4000, ''),
    @('kaya_dik',     2.4, 4000, ''),
    @('bakir',        1.3, 4000, '')
)
$Only = @($Only | ForEach-Object { $_ -split ',' } | Where-Object { $_ })

foreach ($a in $assets) {
    $name = $a[0]
    if ($Only.Count -gt 0 -and $Only -notcontains $name) { continue }
    $in = Get-ChildItem -Path (Join-Path $Src "$name\tripo-out") -Recurse -Filter model.glb | Select-Object -First 1
    if (-not $in) { Write-Host "YOK $name"; continue }
    $out = "game/assets/models/$name/$name.glb"
    $renders = "progress/m1/dunya/models/$name"
    $argList = @('-b', '--factory-startup', '--python-exit-code', '1', '-P', 'pipeline/clean_asset.py', '--',
        '--in', $in.FullName, '--out', $out, '--name', $name, '--height', $a[1], '--tris', $a[2],
        '--keep_texture', '--tex_size', '1024', '--renders', $renders)
    if ($a[3]) { $argList += ($a[3] -split ' ') }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $log = & $Blender @argList 2>&1 | Out-String
    $lines = $log -split "`r?`n" | Where-Object { $_ -match '^(PIPELINE_REPORT|CHECK|Error|Traceback)' }
    Write-Host ("== {0} ({1:N0} sn)" -f $name, $sw.Elapsed.TotalSeconds)
    $lines | ForEach-Object { Write-Host "   $_" }
}
