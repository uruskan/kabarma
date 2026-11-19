# Kabarma sinama kosucusu (Windows PowerShell 5.1 ve PowerShell 7).
# Derler, Sim testlerini kosar, botu ve insa sinamasini basliksiz calistirir (zaman asimiyla), RESULT satirlarini
# okur, GECTI/KALDI tablosu basar. Herhangi bir hata: cikis kodu 1.
#
#   powershell -ExecutionPolicy Bypass -File tools/test.ps1
#   powershell -ExecutionPolicy Bypass -File tools/test.ps1 -Only sim,bot -Log progress/m1/x.log
#
# Adimlar (-Only): build, sim, bench, bot, buildtest, latency, input. Godot: $env:KABARMA_GODOT, yoksa .tools/ altinda,
# yoksa ana calisma kopyasinin .tools/ klasoru (worktree'ler icin, CLAUDE.md). Kosu gunlukleri .tools/run/test/ altinda.
#   input: gercek girdi surucusu (--test=input), botun kararlari sentetik olayla; iki kez: klavye-fare ve oyun kolu.
#          Zincir bitince kendisi cikar; --quit_after yalnizca ust sinir (kol nisani daha yavas, ~520 oyun sn).
#   bench: 1.000 oyun gunu Sim kiyaslamasi (TEKNIK 1.2 adim 2), Release; sonuc .tools/run/test/sim_bench.txt.
# Yeni bir Godot sinamasi: game/scripts/Main.cs Tests sozlugune bir satir, buraya bir Invoke-GodotTest cagrisi.
# creatures (yaratik akisi): --test=creatures; her yaratigin modeli, iskeleti, 8 klibi, ucgen butcesi ve sisme
# shader'i. -Only verilmezse varsayilan adimlara eklenir.
param(
    [string[]]$Only = @('build', 'sim', 'bench', 'bot', 'buildtest', 'latency', 'input'),
    [int]$BotTimeout = 600,
    [int]$BuildTestTimeout = 300,
    [int]$LatencyTimeout = 300,
    [string]$BotArgs = '--fast=4 --quit_after=900 --mute', # entegrasyon: 16 adimli ilk gun ~460 oyun sn surer (400 yetmez)
    [int]$InputTimeout = 900,
    [string]$InputArgs = '--fast=4 --quit_after=900 --mute',
    [int]$BenchTimeout = 600,
    [string]$Log = ''
)

if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'gece' } # gecehud: --test=gece varsayilan adimlara eklenir
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'ilerleme' } # ilerleme: --test=ilerleme varsayilan adimlara eklenir
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'savunma' } # savunma M3: --test=savunma
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'vehicles' } # arac M4: --test=vehicles
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'cip' } # cip: --test=cip varsayilan adimlara eklenir
$ErrorActionPreference = 'Continue'
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'world' } # dunya sinamasi (--test=world), varsayilan listede
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'explore' } # dunya2: kesif sinamasi (--test=explore), varsayilan listede
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'oyuk' } # oyuk (M3): Kostebek Bey ve Yarasa Yumagi (--test=oyuk)
$Only = @($Only | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'audio' } # ses akisi: --test=audio varsayilan adimlarda
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'creatures' }
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'camera' } # oyuncu akisi: --test=camera varsayilan adimlarda
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'grip' } # oyuncu akisi: --test=grip varsayilan adimlarda
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'vfx' } # efekt akisi: --test=vfx varsayilan adimlarda
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'dolap' } # dolap akisi: --test=dolap varsayilan adimlarda
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'zincir' } # zincir M2: --test=zincir (bot T2 urununu arastirir ve uretir)
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'kartlar' } # kartlar M3: --test=kartlar (yenileme, surgun, kartlarin oyun karsiligi)
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'ekonomi' }  # ekonomi sinamasi (--test=ekonomi) varsayilan kosuda
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'save' } # kayit akisi: --test=save (bot bir gun, kaydet, bastan yukle, fark 0)
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'menu' } # kayit akisi: --test=menu (ana menu, Esc, Devam, kapatma kaydi)
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'hayvan' }   # hayvan akisi: zincir + Pofuduk evcillestir, sutunu al (--test=hayvan)
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'savashayvan' } # M3 savas-hayvan: Kabarma'da hayvanlar ve binek (--test=savashayvan)
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'kabarma' } # M3 kabarma3: tam/kucuk/tatbikat Kabarma akisi (--test=kabarma, KabarmaTest.cs)
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'borsa' }    # ekonomi2: Borsa, Katalog, Cark (--test=borsa)
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'kale' }   # kale (M4): yeni kuleler, tuzaklar, Cam seti (--test=kale)
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'tasima' }   # tasima (M4): Kol, Bant, Drone (--test=tasima)
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'holding' }  # holding (M3): Bereket Holding kampi, Perde, kervan, baskinlar (--test=holding)
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'coop' }  # ortak: host + 3 istemci (ENet), senkron < 1 cm; steam yalniz -Only steam
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$GamePath = Join-Path $Root 'game'
$RunDir = Join-Path $Root '.tools\run\test'
New-Item -ItemType Directory -Force $RunDir | Out-Null

$results = New-Object System.Collections.ArrayList
$report = New-Object System.Collections.ArrayList

function Say([string]$s) { Write-Host $s; [void]$report.Add($s) }

function Add-Result([string]$name, [bool]$ok, [string]$detail, [double]$secs) {
    $status = 'FAIL'
    if ($ok) { $status = 'PASS' }
    [void]$results.Add([pscustomobject]@{ Name = $name; Status = $status; Seconds = [math]::Round($secs, 1); Detail = $detail })
    Say ("[{0}] {1}  {2}" -f $status, $name, $detail)
}

function Find-Godot {
    if ($env:KABARMA_GODOT -and (Test-Path $env:KABARMA_GODOT)) { return $env:KABARMA_GODOT }
    $rel = '.tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
    $local = Join-Path $Root $rel
    if (Test-Path $local) { return $local }
    $common = & git -C $Root rev-parse --git-common-dir
    if ($common) {
        if (-not [IO.Path]::IsPathRooted($common)) { $common = Join-Path $Root $common }
        $main = Join-Path (Split-Path -Parent $common) $rel
        if (Test-Path $main) { return $main }
    }
    return $null
}

# Sahibin istegi (18 Kasim 2026, makine donuyordu): butun oturumlar birlikte ayni anda en cok KABARMA_GODOT_MAX
# (varsayilan 2) Godot kosturur; fazlasi sirada bekler. Yuva adli bir mutex'tir: surec olurse Windows birakir (sizinti yok).
function Enter-GodotSlot {
    $max = 2
    if ($env:KABARMA_GODOT_MAX -match '^\d+$') { $max = [Math]::Max(1, [int]$env:KABARMA_GODOT_MAX) }
    $sw = [Diagnostics.Stopwatch]::StartNew(); $next = 30
    while ($true) {
        for ($i = 0; $i -lt $max; $i++) {
            $m = New-Object System.Threading.Mutex($false, "KabarmaGodotSlot$i")
            $got = $false
            try { $got = $m.WaitOne(0) }
            catch {
                $e = $_.Exception
                if ($e -is [Threading.AbandonedMutexException] -or $e.InnerException -is [Threading.AbandonedMutexException]) { $got = $true } else { throw }
            }
            if ($got) { return $m }
            $m.Dispose()
        }
        if ($sw.Elapsed.TotalSeconds -ge $next) { Write-Host ("Godot sirasi bekleniyor ({0:N0} sn)" -f $sw.Elapsed.TotalSeconds); $next += 60 }
        Start-Sleep -Seconds 3
    }
}

# Sureci baslatir, ciktiyi dosyaya yazar, zaman asiminda agaciyla oldurur. ExitCode PS 5.1'de Handle okunmazsa bos kalir.
# Godot surecleri once Enter-GodotSlot ile yer alir; sure siniri yer alindiktan sonra baslar.
function Invoke-Proc([string]$exe, [string]$argLine, [string]$outFile, [int]$timeoutSec) {
    $errFile = "$outFile.err"
    $slot = $null
    if ($exe -match 'Godot') { $slot = Enter-GodotSlot }
    try {
        $p = Start-Process -FilePath $exe -ArgumentList $argLine -WorkingDirectory $Root -RedirectStandardOutput $outFile -RedirectStandardError $errFile -PassThru -NoNewWindow
        $null = $p.Handle
        $timedOut = -not $p.WaitForExit($timeoutSec * 1000)
        if ($timedOut) { & taskkill /T /F /PID $p.Id | Out-Null; $p.WaitForExit() }
    } finally {
        if ($slot) { $slot.ReleaseMutex(); $slot.Dispose() }
    }
    $text = ''
    if (Test-Path $outFile) { $text = [IO.File]::ReadAllText($outFile) }
    return [pscustomobject]@{ Code = $p.ExitCode; TimedOut = $timedOut; Text = $text; Out = $outFile }
}

function Show-Tail($r) {
    Say "      --- son satirlar: $($r.Out)"
    ($r.Text -split "`r?`n" | Where-Object { $_ -ne '' } | Select-Object -Last 12) | ForEach-Object { Say "      $_" }
}

$buildFailed = $false
$sw = [Diagnostics.Stopwatch]::new()

if ($Only -contains 'build') {
    $sw.Restart()
    $r = Invoke-Proc 'dotnet' "build `"$GamePath\Kabarma.Game.csproj`" -nologo -v q" (Join-Path $RunDir 'build.log') 600
    $warn = '?'; $err = '?'
    if ($r.Text -match '(\d+) Warning\(s\)') { $warn = $Matches[1] }
    if ($r.Text -match '(\d+) Error\(s\)') { $err = $Matches[1] }
    $ok = (-not $r.TimedOut) -and $r.Code -eq 0
    $buildFailed = -not $ok
    Add-Result 'build' $ok "exit=$($r.Code) warnings=$warn errors=$err" $sw.Elapsed.TotalSeconds
    if (-not $ok) { Show-Tail $r }
}

if ($Only -contains 'sim') {
    $sw.Restart()
    $r = Invoke-Proc 'dotnet' "test `"$Root\src\Kabarma.Sim.Tests`" -nologo -v q --filter `"Category!=Bench`"" (Join-Path $RunDir 'sim.log') 600
    $line = ($r.Text -split "`r?`n" | Where-Object { $_ -match '(Passed|Failed)!\s+-' } | Select-Object -Last 1)
    if (-not $line) { $line = 'sonuc satiri yok' }
    $ok = (-not $r.TimedOut) -and $r.Code -eq 0 -and $line -match 'Passed!'
    Add-Result 'sim' $ok ("exit=$($r.Code) " + $line.Trim()) $sw.Elapsed.TotalSeconds
    if (-not $ok) { Show-Tail $r }
}

if ($Only -contains 'bench') {
    $sw.Restart()
    $benchOut = Join-Path $RunDir 'sim_bench.txt'
    if (Test-Path $benchOut) { Remove-Item $benchOut }
    $env:KABARMA_BENCH_OUT = $benchOut
    $r = Invoke-Proc 'dotnet' "test `"$Root\src\Kabarma.Sim.Tests`" -c Release -nologo -v q --filter `"Category=Bench`"" (Join-Path $RunDir 'bench.log') $BenchTimeout
    Remove-Item Env:\KABARMA_BENCH_OUT
    $detail = 'sonuc dosyasi yok'
    if (Test-Path $benchOut) {
        $m1 = (Get-Content $benchOut -Encoding UTF8 | Where-Object { $_ -match '^M1 ' } | Select-Object -First 1)
        if ($m1 -match 'seconds=([0-9.]+).*allocated_mb=([0-9.]+).*peak_ws_mb=([0-9.]+)') { $detail = "1000 gun $($Matches[1]) sn, ayrilan $($Matches[2]) MB, tepe $($Matches[3]) MB (Release, 3x3)" }
    }
    $ok = (-not $r.TimedOut) -and $r.Code -eq 0
    Add-Result 'bench' $ok "exit=$($r.Code) $detail" $sw.Elapsed.TotalSeconds
    if (-not $ok) { Show-Tail $r }
}

$G = $null
if (($Only -contains 'bot') -or ($Only -contains 'buildtest') -or ($Only -contains 'latency') -or ($Only -contains 'input')) {
    $G = Find-Godot
    if (-not $G) { Say 'Godot bulunamadi: KABARMA_GODOT ortam degiskenini ver.' }
}
if (($Only -contains 'world') -and -not $G) { $G = Find-Godot }
if (($Only -contains 'creatures') -and -not $G) { $G = Find-Godot }
if ((-not $G) -and ($Only -contains 'ekonomi')) { $G = Find-Godot }

function Invoke-GodotTest([string]$name, [string]$userArgs, [int]$timeoutSec, [scriptblock]$judge) {
    $sw2 = [Diagnostics.Stopwatch]::StartNew()
    if (-not $G) { Add-Result $name $false 'Godot yok' 0; return }
    if ($buildFailed) { Add-Result $name $false 'atlandi: derleme kaldi' 0; return }
    $r = Invoke-Proc $G "--headless --path `"$GamePath`" -- $userArgs" (Join-Path $RunDir "$name.log") $timeoutSec
    $verdict = & $judge $r
    $detail = "exit=$($r.Code) " + $verdict.Detail
    if ($r.TimedOut) { $detail = "ZAMAN ASIMI ($timeoutSec sn) " + $detail }
    $ok = (-not $r.TimedOut) -and $r.Code -eq 0 -and $verdict.Ok
    Add-Result $name $ok $detail $sw2.Elapsed.TotalSeconds
    if (-not $ok) { Show-Tail $r }
}

if ($Only -contains 'bot') {
    Invoke-GodotTest 'bot' "--test=bot $BotArgs" $BotTimeout {
        param($r)
        $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^RESULT ' } | Select-Object -Last 1)
        if (-not $res) { $res = 'RESULT satiri yok' }
        [pscustomobject]@{ Ok = ($res -match '^RESULT ok'); Detail = ($res -replace ' t=[0-9.]+$', '') }
    }
}

if ($Only -contains 'hayvan') {
    if (-not $G) { $G = Find-Godot }
    Invoke-GodotTest 'hayvan' '--test=hayvan --fast=4 --quit_after=1300 --mute' ([Math]::Max($BotTimeout, 900)) {
        param($r)
        $lines = $r.Text -split "`r?`n"
        $res = ($lines | Where-Object { $_ -match '^RESULT ' } | Select-Object -Last 1)
        $hv = ($lines | Where-Object { $_ -match '^HAYVAN ok' } | Select-Object -Last 1)
        if (-not $res) { $res = 'RESULT satiri yok' }
        if (-not $hv) { $hv = 'HAYVAN satiri yok' }
        [pscustomobject]@{ Ok = ($res -match '^RESULT ok') -and ($hv -match '^HAYVAN ok'); Detail = (($hv -replace ' t=[0-9.]+$', '') + ' | ' + ($res -replace ' t=[0-9.]+$', '')) }
    }
}

if ($Only -contains 'savashayvan') {
    # M3 savas-hayvan: hayvan botu + Kabarma'da hayvanlar ve binek (husbandry/Husbandry.Combat.Test.cs)
    if (-not $G) { $G = Find-Godot }
    Invoke-GodotTest 'savashayvan' '--test=savashayvan --fast=4 --quit_after=2200 --mute' ([Math]::Max($BotTimeout, 1200)) {
        param($r)
        $lines = $r.Text -split "`r?`n"
        $sv = ($lines | Where-Object { $_ -match '^SAVASHAYVAN ok checks' } | Select-Object -Last 1)
        $fl = ($lines | Where-Object { $_ -match '^RESULT fail' } | Select-Object -Last 1)
        $det = if ($sv) { $sv } elseif ($fl) { $fl } else { 'SAVASHAYVAN satiri yok' }
        [pscustomobject]@{ Ok = [bool]$sv -and -not $fl; Detail = ($det -replace ' t=[0-9.]+$', '') }
    }
}

if ($Only -contains 'buildtest') {
    $out = Join-Path $RunDir 'buildtest'
    New-Item -ItemType Directory -Force $out | Out-Null
    Invoke-GodotTest 'buildtest' "--test=build --test_out=`"$out`"" $BuildTestTimeout {
        param($r)
        $ok = $false; $detail = 'BUILD RESULT satiri yok'
        if ($r.Text -match 'BUILD RESULT placed=(\d+) failed=(\d+) pieces=(\d+)') {
            $ok = [int]$Matches[2] -eq 0
            $detail = "placed=$($Matches[1]) failed=$($Matches[2]) pieces=$($Matches[3])"
        }
        [pscustomobject]@{ Ok = $ok; Detail = $detail }
    }
}
if ($Only -contains 'botstrict') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'botstrict' "--test=bot --strict-scenario $BotArgs" $BotTimeout { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^RESULT ok'); Detail = ($res -replace ' t=[0-9.]+$', '') } } } # siki kip (-Only botstrict): atlanan senaryo adimi basarisizlik; varsayilan listede yok (birlestirme sonrasi dogrulama)

# Ses sinamasi (ses akisi): kanallar, 60 ses, muzik durum makinesi, Suna kismasi. "AUDIO RESULT ok" bekler.
if ($Only -contains 'audio') {
    if (-not $G) { $G = Find-Godot }
    Invoke-GodotTest 'audio' "--test=audio" 120 {
        param($r)
        $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^AUDIO RESULT ' } | Select-Object -Last 1)
        if (-not $res) { $res = 'AUDIO RESULT satiri yok' }
        [pscustomobject]@{ Ok = ($res -match '^AUDIO RESULT ok'); Detail = ($res -replace '^AUDIO RESULT ', '') }
    }
}

if ($Only -contains 'world') {
    if (-not $G) { $G = Find-Godot }
    Invoke-GodotTest 'world' '--test=world' 180 {
        param($r)
        $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^WORLD RESULT ' } | Select-Object -Last 1)
        if (-not $res) { $res = 'WORLD RESULT satiri yok' }
        [pscustomobject]@{ Ok = ($res -match '^WORLD RESULT ok'); Detail = $res }
    }
}

if ($Only -contains 'explore') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'explore' '--test=explore --mute' $BotTimeout { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^EXPLORE RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'EXPLORE RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^EXPLORE RESULT ok'); Detail = $res } } } # dunya2: kesif (--test=explore)
if ($Only -contains 'oyuk') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'oyuk' '--test=oyuk --mute' $BotTimeout { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^OYUK RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'OYUK RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^OYUK RESULT ok'); Detail = $res } } } # oyuk (M3): Oyuk koruyuculari (--test=oyuk)
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'ata4' } # ata4 (M4): yeni Atalar, Kabarik, Azmanlar, Oyuk koruyucular (--test=ata4)
if ($Only -contains 'ata4') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'ata4' '--test=ata4' ([Math]::Max($BotTimeout, 600)) { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^ATA4 RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'ATA4 RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^ATA4 RESULT ok'); Detail = ($res -replace ' t=[0-9.]+.*$', '') } } } # ata4: 4 yeni Ata, Kabarik, 4 Azman, 3 Oyuk koruyucusu
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'boss' } # boss akisi: --test=boss varsayilan adimlarda
if ($Only -contains 'boss') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'boss' '--test=boss' ([Math]::Max($BotTimeout, 900)) { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^RESULT ok'); Detail = ($res -replace ' t=[0-9.]+.*$', '') } } } # boss akisi: bot iki Ata'yi bulur ve yener (--test=boss)
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'savas' } # savas akisi: --test=savas varsayilan adimlarda
if ($Only -contains 'savas') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'savas' '--test=savas' ([Math]::Max($BotTimeout, 900)) { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^RESULT ok'); Detail = ($res -replace ' t=[0-9.]+.*$', '') } } } # savas akisi: gunduz savasi ve iki Azman (--test=savas)
if ($Only -contains 'camera') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'camera' '--test=camera --mute' 300 { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^RESULT ok'); Detail = $res } } } # oyuncu akisi: kamera (--test=camera)
if ($Only -contains 'grip') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'grip' '--test=grip --mute' 300 { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^RESULT ok'); Detail = $res } } } # oyuncu akisi: Capa elde, namlu nisanda, araba iki elle (--test=grip)
if ($Only -contains 'vfx') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'vfx' '--test=vfx --mute' 300 { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^RESULT ok'); Detail = $res } } } # efekt akisi: kitaplik, durum aurasi (--test=vfx)
if ($Only -contains 'dolap') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'dolap' '--test=dolap --mute' 300 { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^RESULT ok'); Detail = $res } } } # dolap akisi: uye pasifleri, kozmetik, jest, menu (--test=dolap)
if ($Only -contains 'save') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'save' '--test=save --fast=4' $BotTimeout { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^RESULT ok'); Detail = $res } } } # kayit akisi: kaydet, sahneyi bastan yukle, bolumler bayt bayt ayni (--test=save; yalniz user://test/)
if ($Only -contains 'menu') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'menu' '--test=menu' 300 { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^RESULT ok'); Detail = $res } } } # kayit akisi: menu yolu dugmelerin kendi geri cagrilariyla (--test=menu)
if (-not $PSBoundParameters.ContainsKey('Only') -and -not ($Only -contains 'tarim')) { $Only += 'tarim' } # tarim (M2): --test=tarim varsayilan adimlarda
if ($Only -contains 'tarim') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'tarim' "--test=tarim --fast=4 --mute" $BotTimeout { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^RESULT ok'); Detail = ($res -replace ' t=[0-9.]+$', '') } } } # tarim (M2): bot ikinci ekini (yabani tohumdan) eker, korur, hasat eder
if ($Only -contains 'creatures') {
    Invoke-GodotTest 'creatures' '--test=creatures' 300 {
        param($r)
        $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^RESULT ' } | Select-Object -Last 1)
        if (-not $res) { $res = 'RESULT satiri yok' }
        [pscustomobject]@{ Ok = ($res -match '^RESULT ok'); Detail = $res }
    }
}

# His olcutleri (GDD 4): her baslik ve silah icin girdi->tepki karesi, vurus duraklamasinin gercek suresi. Ayrinti: latency.log.
if ($Only -contains 'latency') {
    $out = Join-Path $RunDir 'latency'
    New-Item -ItemType Directory -Force $out | Out-Null
    Invoke-GodotTest 'latency' "--test=latency --mute --test_out=`"$out`"" $LatencyTimeout {
        param($r)
        $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^RESULT ' } | Select-Object -Last 1)
        if (-not $res) { $res = 'RESULT satiri yok' }
        [pscustomobject]@{ Ok = ($res -match '^RESULT ok'); Detail = $res }
    }
}

if ($Only -contains 'power') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'power' "--test=power $BotArgs --quit_after=1500" $BotTimeout { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^POWER RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'POWER RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^POWER RESULT ok'); Detail = ($res -replace ' t=[0-9.]+$', '') } } } # guc akisi (-Only power): ilk gun zinciri + uretec, lamba, dugme; varsayilan listede yok (uzun)
if ($Only -contains 'gucler') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'gucler' "--test=gucler --quit_after=700" $BotTimeout { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^GUCLER RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'GUCLER RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^GUCLER RESULT ok'); Detail = ($res -replace ' t=[0-9.]+$', '') } } } # gucler akisi (-Only gucler): Kadim Tas parkuru, Atilma, Tatbikat, Fener

if ($Only -contains 'ekonomi') {
    $out = Join-Path $RunDir 'ekonomi'
    New-Item -ItemType Directory -Force $out | Out-Null
    Invoke-GodotTest 'ekonomi' "--test=ekonomi --test_out=`"$out`"" 300 {
        param($r)
        $ok = $false; $detail = 'ECONOMY RESULT satiri yok'
        if ($r.Text -match 'ECONOMY RESULT ok=(\d+) fail=(\d+)') {
            $ok = [int]$Matches[2] -eq 0
            $detail = "ok=$($Matches[1]) fail=$($Matches[2])"
        }
        [pscustomobject]@{ Ok = $ok; Detail = $detail }
    }
}

if ($Only -contains 'borsa') { if (-not $G) { $G = Find-Godot }; $bo = Join-Path $RunDir 'borsa'; New-Item -ItemType Directory -Force $bo | Out-Null; Invoke-GodotTest 'borsa' "--test=borsa --fast=4 --test_out=`"$bo`"" 300 { param($r) $ok = $false; $detail = 'BORSA RESULT satiri yok'; if ($r.Text -match 'BORSA RESULT ok=(\d+) fail=(\d+)') { $ok = [int]$Matches[2] -eq 0; $detail = "ok=$($Matches[1]) fail=$($Matches[2])" }; [pscustomobject]@{ Ok = $ok; Detail = $detail } } } # ekonomi2: Borsa, Katalog, Cark (--test=borsa)
if ($Only -contains 'holding') { if (-not $G) { $G = Find-Godot }; $ho = Join-Path $RunDir 'holding'; New-Item -ItemType Directory -Force $ho | Out-Null; Invoke-GodotTest 'holding' "--test=holding --fast=2 --test_out=`"$ho`"" 420 { param($r) $ok = $false; $detail = 'HOLDING RESULT satiri yok'; if ($r.Text -match 'HOLDING RESULT ok=(\d+) fail=(\d+)') { $ok = [int]$Matches[2] -eq 0; $detail = "ok=$($Matches[1]) fail=$($Matches[2])" }; [pscustomobject]@{ Ok = $ok; Detail = $detail } } } # holding M3: kamp, Perde, kervan, baskinlar (holding/HoldingTest.cs)
if ($Only -contains 'kale') { if (-not $G) { $G = Find-Godot }; $ka = Join-Path $RunDir 'kale'; New-Item -ItemType Directory -Force $ka | Out-Null; Invoke-GodotTest 'kale' "--test=kale --fast=2 --test_out=`"$ka`"" 480 { param($r) $ok = $false; $detail = 'KALE RESULT satiri yok'; if ($r.Text -match 'KALE RESULT ok=(\d+) fail=(\d+)') { $ok = [int]$Matches[2] -eq 0; $detail = "ok=$($Matches[1]) fail=$($Matches[2])" }; [pscustomobject]@{ Ok = $ok; Detail = $detail } } } # kale M4: yeni kuleler, tuzaklar, Cam seti (defense/KaleTest.cs)
if ($Only -contains 'tasima') { if (-not $G) { $G = Find-Godot }; $ta = Join-Path $RunDir 'tasima'; New-Item -ItemType Directory -Force $ta | Out-Null; Invoke-GodotTest 'tasima' "--test=tasima --fast=4 --test_out=`"$ta`"" 420 { param($r) $ok = $false; $detail = 'TASIMA RESULT satiri yok'; if ($r.Text -match 'TASIMA RESULT ok=(\d+) fail=(\d+)') { $ok = [int]$Matches[2] -eq 0; $detail = "ok=$($Matches[1]) fail=$($Matches[2])" }; [pscustomobject]@{ Ok = $ok; Detail = $detail } } } # tasima M4: Kol, Bant, Drone (power/TasimaTest.cs)
if ($Only -contains 'zincir') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'zincir' "--test=zincir --fast=4 --mute" $BotTimeout { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^ZINCIR RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'ZINCIR RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match 'fail=0'); Detail = ($res -replace '^ZINCIR RESULT ', '') } } } # zincir M2: arastirma ve T2 uretimi (ZincirTest.cs)
if ($Only -contains 'kabarma') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'kabarma' "--test=kabarma --mute" ([Math]::Max($BotTimeout, 900)) { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^KABARMA RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'KABARMA RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^KABARMA RESULT ok'); Detail = ($res -replace '^KABARMA RESULT ', '') } } } # kabarma3 M3 (KabarmaTest.cs)
if ($Only -contains 'kartlar') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'kartlar' "--test=kartlar --mute" $BotTimeout { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^KARTLAR RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'KARTLAR RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match 'fail=0'); Detail = ($res -replace '^KARTLAR RESULT ', '') } } } # kartlar M3 (KartlarTest.cs)
if (($Only -contains 'gece') -and -not $G) { $G = Find-Godot }
if ($Only -contains 'gece') {
    # gecehud: ilk gece kartlari, Hasat Raporu, Defter, envanter, takvim ve uyku, F1'den Kucuk Kabarma (game/scripts/GeceHudTest.cs)
    Invoke-GodotTest 'gece' "--test=gece --mute" $BotTimeout {
        param($r)
        $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^GECE RESULT ' } | Select-Object -Last 1)
        if (-not $res) { $res = 'GECE RESULT satiri yok' }
        [pscustomobject]@{ Ok = ($res -match '^GECE RESULT ok'); Detail = ($res -replace ' t=[0-9.]+$', '') }
    }
}

if ($Only -contains 'ilerleme') {
    # ilerleme: is emri odemesi, Defter alt sekmeleri, Ustalik secimi, basarim (game/scripts/IlerlemeTest.cs)
    if (-not $G) { $G = Find-Godot }
    Invoke-GodotTest 'ilerleme' "--test=ilerleme --mute" $BotTimeout {
        param($r)
        $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^ILERLEME RESULT ' } | Select-Object -Last 1)
        if (-not $res) { $res = 'ILERLEME RESULT satiri yok' }
        [pscustomobject]@{ Ok = ($res -match '^ILERLEME RESULT ok'); Detail = ($res -replace ' t=[0-9.]+$', '') }
    }
}
if ($Only -contains 'savunma') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'savunma' '--test=savunma --mute' $BotTimeout { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^DEFENSE RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'DEFENSE RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^DEFENSE RESULT ok=\d+ fail=0'); Detail = ($res -replace '^DEFENSE RESULT ', '') } } } # savunma M3: kuleler ve tuzaklar (defense/DefenseTest.cs)
if ($Only -contains 'vehicles') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'vehicles' '--test=vehicles --mute' $BotTimeout { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^VEHICLE RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'VEHICLE RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^VEHICLE RESULT ok=\d+ fail=0'); Detail = ($res -replace '^VEHICLE RESULT ', '') } } } # arac M4: surus, eklenti, hava yastikli, zeplin (tests/VehicleTest.cs)
if ($Only -contains 'coop') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'coop' '--test=coop' ([math]::Max($BotTimeout, 600)) { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^COOP RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'COOP RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^COOP RESULT ok'); Detail = ($res -replace '^COOP RESULT ', '' -replace ' t=[0-9.]+', '') } } } # ortak: host 3 istemciyi kendisi baslatir (net/CoopTest.cs); istemci gunlukleri .tools/run/test/coop_client_N.log
if ($Only -contains 'stress') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'stress' '--test=stress' ([math]::Max($BotTimeout, 700)) { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^STRESS RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'STRESS RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^STRESS RESULT ok'); Detail = ($res -replace '^STRESS RESULT ', '') } } } # yolbulma: host 7 istemciyi baslatir, 220 dusman (ai/StressTest.cs); agir, varsayilan listede yok
if ($Only -contains 'steam') { if (-not $G) { $G = Find-Godot }; Invoke-GodotTest 'steam' '--test=steam' 120 { param($r) $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^STEAM RESULT ' } | Select-Object -Last 1); if (-not $res) { $res = 'STEAM RESULT satiri yok' }; [pscustomobject]@{ Ok = ($res -match '^STEAM RESULT ok'); Detail = ($res -replace '^STEAM RESULT ', '') } } } # ortak: tek surec Steam baslatma ve lobi (Steam acik olmali); varsayilan listede yok

if ($Only -contains 'cip') {
    # cip: Defter Cipler sekmesi, takma, yuva siniri, Kabarma kilidi, Ata dususu (game/scripts/CipTest.cs)
    if (-not $G) { $G = Find-Godot }
    Invoke-GodotTest 'cip' "--test=cip --mute" $BotTimeout {
        param($r)
        $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^CIP RESULT ' } | Select-Object -Last 1)
        if (-not $res) { $res = 'CIP RESULT satiri yok' }
        [pscustomobject]@{ Ok = ($res -match '^CIP RESULT ok'); Detail = ($res -replace ' t=[0-9.]+$', '') }
    }
}

if ($Only -contains 'input') {
    foreach ($dev in @('kbm', 'pad')) {
        # Yeniden atama denetimi sahibin user://input.json dosyasina degil, buraya yazar.
        $userFile = Join-Path $RunDir "input_user_$dev.json"
        if (Test-Path $userFile) { Remove-Item $userFile }
        Invoke-GodotTest "input_$dev" "--test=input --device=$dev --input_user=`"$userFile`" $InputArgs" $InputTimeout {
            param($r)
            $lines = $r.Text -split "`r?`n"
            $res = ($lines | Where-Object { $_ -match '^RESULT ' } | Select-Object -Last 1)
            if (-not $res) { $res = 'RESULT satiri yok' }
            $probe = [bool]($lines | Where-Object { $_ -match '^INPUT probe ok' }) -and [bool]($lines | Where-Object { $_ -match '^INPUT heads ok' })
            $sum = ($lines | Where-Object { $_ -match '^INPUT summary' } | Select-Object -Last 1)
            $ev = ''
            if ($sum -match 'events=(\d+) taps=(\d+) wheels=(\d+) menuNavs=(\d+) mouseMoves=(\d+) sidesteps=(\d+)') { $ev = " olay=$($Matches[1]) basis=$($Matches[2]) cark=$($Matches[3]) menu=$($Matches[4]) fare=$($Matches[5]) yanadim=$($Matches[6])" }
            $ok = ($res -match '^RESULT ok') -and $probe -and [bool]$sum
            $d = ($res -replace ' t=[0-9.]+$', '') + $ev
            if (-not $probe) { $d += ' (INPUT probe ok / INPUT heads ok yok)' }
            [pscustomobject]@{ Ok = $ok; Detail = $d }
        }
    }
}

Say ''
Say ('{0,-10} {1,-6} {2,8}  {3}' -f 'sinama', 'sonuc', 'sure(sn)', 'ayrinti')
Say ('{0,-10} {1,-6} {2,8}  {3}' -f '------', '-----', '--------', '-------')
foreach ($x in $results) { Say ('{0,-10} {1,-6} {2,8}  {3}' -f $x.Name, $x.Status, $x.Seconds, $x.Detail) }
$failed = @($results | Where-Object { $_.Status -ne 'PASS' }).Count
if ($results.Count -eq 0) { $failed = 1; Say 'Hicbir adim kosmadi (-Only?).' }
if ($failed -eq 0) { Say 'TEST PASS' } else { Say "TEST FAIL ($failed)" }

if ($Log) {
    $logPath = $Log
    if (-not [IO.Path]::IsPathRooted($logPath)) { $logPath = Join-Path $Root $logPath }
    New-Item -ItemType Directory -Force (Split-Path -Parent $logPath) | Out-Null
    $stamp = (Get-Date).ToString('yyyy-MM-dd HH:mm')
    $head = "tools/test.ps1  $stamp  git $(& git -C $Root rev-parse --short HEAD)"
    [IO.File]::WriteAllLines($logPath, [string[]](@($head, '') + $report), (New-Object Text.UTF8Encoding($false)))
}

if ($failed -eq 0) { exit 0 } else { exit 1 }
