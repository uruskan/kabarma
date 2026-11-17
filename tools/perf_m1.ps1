# M1 performans olcumu (TEKNIK §10): "yuksek" on ayar, 1920 x 1080, dikey esitleme kapali.
#  1. ogle: dort yer iminin her birinde 60 sn (--test=perf --perf=noon)
#  2. ilk gecenin son dalgasi: her yer imi icin ayri kosu (bot ilk gunu hizli oynar, son dalgada zaman 1x)
# Pencere acar (basliksiz cizim yok). Sonuclar progress/m1/dunya/perf/*.json ve ozet perf_m1.log.
# Ayni anda baska Godot surecleri calisiyorsa sonuc dosyasinda "other_godot_processes" sayisi yazar.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/perf_m1.ps1 [-Only noon,night] [-Bookmarks us,gol]
param([string[]]$Only = @('noon', 'night'), [string[]]$Bookmarks = @('kapsul', 'us', 'gol', 'tepe'), [int]$Fast = 4)

$ErrorActionPreference = 'Continue'
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$Only = @($Only | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$Bookmarks = @($Bookmarks | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$G = $env:KABARMA_GODOT
if (-not $G) {
    $rel = '.tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
    $G = Join-Path $Root $rel
    if (-not (Test-Path $G)) { $G = Join-Path (Split-Path -Parent (& git rev-parse --git-common-dir)) $rel }
}
$out = Join-Path $Root 'progress\m1\dunya\perf'
New-Item -ItemType Directory -Force $out | Out-Null
$summary = New-Object System.Collections.ArrayList

function GpuBusy {
    # Olcumden once GPU baskasi tarafindan kullaniliyor mu? (yalniz NVIDIA; yoksa bos)
    try { $u = & nvidia-smi --query-gpu=utilization.gpu --format=csv,noheader,nounits 2>$null | Select-Object -First 1; if ($u) { return [int]$u.Trim() } } catch { }
    return $null
}

function Run([string]$name, [string]$a, [int]$timeout) {
    $log = Join-Path $out "$name.log"
    # Baska oturumlarin Godot'u GPU'yu dolduruyorsa sonuc gecersizdir: once yaz, karar sahibin.
    $busy = GpuBusy
    $others = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like 'Godot*' }).Count
    $pre = "$name  ONCESI gpu_kullanim=$busy% godot_sureci=$others"
    if ($busy -ne $null -and $busy -gt 20) { $pre += '  UYARI: GPU bos degil, sonuc gecersiz sayilmali' }
    [void]$summary.Add($pre); Write-Host $pre
    $p = Start-Process -FilePath $G -ArgumentList "--path `"$Root\game`" -- $a" -RedirectStandardOutput $log -RedirectStandardError "$log.err" -PassThru -NoNewWindow
    $null = $p.Handle
    if (-not $p.WaitForExit($timeout * 1000)) { & taskkill /T /F /PID $p.Id | Out-Null; [void]$summary.Add("$name ZAMAN ASIMI") }
    Get-Content $log | Where-Object { $_ -match '^PERF ' } | ForEach-Object { [void]$summary.Add("$name  $_"); Write-Host "$name  $_" }
}

if ($Only -contains 'noon') { Run 'noon' '--test=perf --perf=noon --mute' 600 }
if ($Only -contains 'night') {
    foreach ($b in $Bookmarks) { Run "night_$b" "--test=perf --perf=night --perf_bookmark=$b --fast=$Fast --mute" 900 }
}
[IO.File]::WriteAllLines((Join-Path $out 'perf_m1.log'), [string[]]$summary, (New-Object Text.UTF8Encoding($false)))
