# Kabarma sinama kosucusu (Windows PowerShell 5.1 ve PowerShell 7).
# Derler, Sim testlerini kosar, botu ve insa sinamasini basliksiz calistirir (zaman asimiyla), RESULT satirlarini
# okur, GECTI/KALDI tablosu basar. Herhangi bir hata: cikis kodu 1.
#
#   powershell -ExecutionPolicy Bypass -File tools/test.ps1
#   powershell -ExecutionPolicy Bypass -File tools/test.ps1 -Only sim,bot -Log progress/m1/x.log
#
# Adimlar (-Only): build, sim, bot, buildtest. Godot: $env:KABARMA_GODOT, yoksa .tools/ altinda, yoksa ana calisma
# kopyasinin .tools/ klasoru (worktree'ler icin, CLAUDE.md). Kosu gunlukleri .tools/run/test/ altinda.
# Yeni bir Godot sinamasi: game/scripts/Main.cs Tests sozlugune bir satir, buraya bir Invoke-GodotTest cagrisi.
param(
    [string[]]$Only = @('build', 'sim', 'bot', 'buildtest'),
    [int]$BotTimeout = 600,
    [int]$BuildTestTimeout = 300,
    [string]$BotArgs = '--fast=4 --quit_after=400 --mute',
    [string]$Log = ''
)

$ErrorActionPreference = 'Continue'
if (-not $PSBoundParameters.ContainsKey('Only')) { $Only += 'world' } # dunya sinamasi (--test=world), varsayilan listede
$Only = @($Only | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
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

# Sureci baslatir, ciktiyi dosyaya yazar, zaman asiminda agaciyla oldurur. ExitCode PS 5.1'de Handle okunmazsa bos kalir.
function Invoke-Proc([string]$exe, [string]$argLine, [string]$outFile, [int]$timeoutSec) {
    $errFile = "$outFile.err"
    $p = Start-Process -FilePath $exe -ArgumentList $argLine -WorkingDirectory $Root -RedirectStandardOutput $outFile -RedirectStandardError $errFile -PassThru -NoNewWindow
    $null = $p.Handle
    $timedOut = -not $p.WaitForExit($timeoutSec * 1000)
    if ($timedOut) { & taskkill /T /F /PID $p.Id | Out-Null; $p.WaitForExit() }
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
    $r = Invoke-Proc 'dotnet' "test `"$Root\src\Kabarma.Sim.Tests`" -nologo -v q" (Join-Path $RunDir 'sim.log') 600
    $line = ($r.Text -split "`r?`n" | Where-Object { $_ -match '(Passed|Failed)!\s+-' } | Select-Object -Last 1)
    if (-not $line) { $line = 'sonuc satiri yok' }
    $ok = (-not $r.TimedOut) -and $r.Code -eq 0 -and $line -match 'Passed!'
    Add-Result 'sim' $ok ("exit=$($r.Code) " + $line.Trim()) $sw.Elapsed.TotalSeconds
    if (-not $ok) { Show-Tail $r }
}

$G = $null
if (($Only -contains 'bot') -or ($Only -contains 'buildtest')) {
    $G = Find-Godot
    if (-not $G) { Say 'Godot bulunamadi: KABARMA_GODOT ortam degiskenini ver.' }
}
if (($Only -contains 'world') -and -not $G) { $G = Find-Godot }

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

if ($Only -contains 'world') {
    Invoke-GodotTest 'world' '--test=world' 180 {
        param($r)
        $res = ($r.Text -split "`r?`n" | Where-Object { $_ -match '^WORLD RESULT ' } | Select-Object -Last 1)
        if (-not $res) { $res = 'WORLD RESULT satiri yok' }
        [pscustomobject]@{ Ok = ($res -match '^WORLD RESULT ok'); Detail = $res }
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
